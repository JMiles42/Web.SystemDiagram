using System.Text;
using DiagramSample.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DiagramSample.Services;

public class DiagramConfigLoader
{
    private readonly IWebHostEnvironment _env;
    private readonly string _configPathRelative = "Config/diagram.yaml";

    public DiagramConfigLoader(IWebHostEnvironment env)
    {
        _env = env;
    }

    public DiagramConfig LoadConfig()
    {
        var full = Path.Combine(_env.ContentRootPath, _configPathRelative.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full))
            throw new FileNotFoundException($"Config file not found: {full}");

        var text = File.ReadAllText(full, Encoding.UTF8);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var config = deserializer.Deserialize<DiagramConfig>(text) ?? new DiagramConfig();

        ValidateConfig(config);
        return config;
    }

    private void ValidateConfig(DiagramConfig config)
    {
        var ids = new HashSet<string>();

        void addList(IEnumerable<Node>? nodes)
        {
            if (nodes == null) return;
            foreach (var n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n.id)) throw new Exception("Node missing id");
                if (!ids.Add(n.id)) throw new Exception($"Duplicate node id: {n.id}");
            }
        }

        addList(config.platforms);
        addList(config.applications);
        addList(config.modules);

        // Validate containment
        if (config.platforms != null)
        {
            foreach (var p in config.platforms)
            {
                if (p.applications == null) continue;
                foreach (var aid in p.applications)
                    if (!ids.Contains(aid)) throw new Exception($"Platform '{p.id}' references unknown application '{aid}'");
            }
        }

        if (config.applications != null)
        {
            foreach (var a in config.applications)
            {
                if (a.modules == null) continue;
                foreach (var mid in a.modules)
                    if (!ids.Contains(mid)) throw new Exception($"Application '{a.id}' references unknown module '{mid}'");
            }
        }

        // Validate links
        if (config.links != null)
        {
            foreach (var l in config.links)
            {
                if (string.IsNullOrWhiteSpace(l.id)) throw new Exception("Link missing id");
                if (!ids.Contains(l.from ?? "")) throw new Exception($"Link '{l.id}' references unknown from '{l.from}'");
                if (!ids.Contains(l.to ?? "")) throw new Exception($"Link '{l.id}' references unknown to '{l.to}'");
                if (l.via != null)
                {
                    foreach (var v in l.via)
                    {
                        if (!ids.Contains(v)) throw new Exception($"Link '{l.id}' references unknown via '{v}'");
                    }
                }
            }
        }

        // Validate business process steps
        if (config.businessProcesses != null)
        {
            var linkIds = new HashSet<string>(config.links?.Select(x => x.id ?? string.Empty) ?? Enumerable.Empty<string>());
            foreach (var bp in config.businessProcesses)
            {
                if (bp.steps == null) continue;
                foreach (var s in bp.steps)
                {
                    if (!string.IsNullOrEmpty(s.node))
                    {
                        if (!ids.Contains(s.node)) throw new Exception($"BusinessProcess '{bp.id}' references unknown node '{s.node}'");
                    }
                    if (!string.IsNullOrEmpty(s.link))
                    {
                        if (!linkIds.Contains(s.link)) throw new Exception($"BusinessProcess '{bp.id}' references unknown link '{s.link}'");
                    }
                }
            }
        }
    }

    public object BuildGraph()
    {
        var config = LoadConfig();

        var nodes = new List<object>();
        var nodeMap = new Dictionary<string, Node?>();

        void addNodes(IEnumerable<Node>? list)
        {
            if (list == null) return;
            foreach (var n in list)
            {
                nodeMap[n.id!] = n;
                nodes.Add(new
                {
                    data = new Dictionary<string, object?> {
                        ["id"] = n.id,
                        ["type"] = n.type,
                        ["displayName"] = n.displayName
                    }
                });
            }
        }

        addNodes(config.platforms);
        addNodes(config.applications);
        addNodes(config.modules);

        // Attach parents for compound nodes
        var nodesWithParent = new List<object>();
        // rebuild nodes with parent where applicable
        foreach (var n in nodeMap.Values)
        {
            var dict = new Dictionary<string, object?> {
                ["id"] = n!.id,
                ["type"] = n.type,
                ["displayName"] = n.displayName
            };
            // application -> parent platform
            if (n.type == "application")
            {
                // find platform that lists this application
                var parent = config.platforms?.FirstOrDefault(p => p.applications != null && p.applications.Contains(n.id));
                if (parent != null) dict["parent"] = parent.id;
            }
            // module -> parent application
            if (n.type == "module")
            {
                var parent = config.applications?.FirstOrDefault(a => a.modules != null && a.modules.Contains(n.id));
                if (parent != null) dict["parent"] = parent.id;
            }

            nodesWithParent.Add(new { data = dict });
        }

        var edges = new List<object>();
        if (config.links != null)
        {
            foreach (var l in config.links)
            {
                var path = new List<string>();
                path.Add(l.from!);
                if (l.via != null && l.via.Any()) path.AddRange(l.via);
                path.Add(l.to!);

                for (int i = 0; i < path.Count - 1; i++)
                {
                    var segId = $"{l.id}__seg__{i}";
                    var showLabel = (i == 0) ? (l.displayName ?? "") : ""; // only first segment shows label
                    edges.Add(new
                    {
                        data = new Dictionary<string, object?> {
                            ["id"] = segId,
                            ["source"] = path[i],
                            ["target"] = path[i+1],
                            ["displayName"] = showLabel,
                            ["kind"] = l.kind,
                            ["originalLinkId"] = l.id,
                            ["segmentIndex"] = i,
                            ["segmentCount"] = path.Count - 1
                        }
                    });
                }
            }
        }

        return new { nodes = nodesWithParent, edges = edges };
    }

    public object GetProcesses()
    {
        var config = LoadConfig();
        var list = new List<object>();
        if (config.businessProcesses != null)
        {
            foreach (var bp in config.businessProcesses)
            {
                if (string.IsNullOrWhiteSpace(bp.displayName)) continue; // do not show unnamed
                list.Add(new
                {
                    id = bp.id,
                    displayName = bp.displayName,
                    steps = bp.steps,
                    usesLinks = bp.usesLinks,
                    color = bp.color,
                    hiddenByDefault = bp.hiddenByDefault ?? false
                });
            }
        }

        return list;
    }
}
