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

        void AddNodes(IEnumerable<Node> nodes)
        {
            foreach (var n in nodes)
            {
                if (string.IsNullOrWhiteSpace(n.Id)) throw new Exception("Node missing id");
                if (!ids.Add(n.Id)) throw new Exception($"Duplicate node id: {n.Id}");
            }
        }

        AddNodes(config.Platforms);
        AddNodes(config.Applications);
        AddNodes(config.Modules);

        // Validate containment
        foreach (var p in config.Platforms)
        {
            foreach (var aid in p.Applications)
            {
                if (!ids.Contains(aid)) throw new Exception($"Platform '{p.Id}' references unknown application '{aid}'");
            }
        }

        foreach (var a in config.Applications)
        {
            foreach (var mid in a.Modules)
            {
                if (!ids.Contains(mid)) throw new Exception($"Application '{a.Id}' references unknown module '{mid}'");
            }
        }

        // Validate links
        foreach (var l in config.Links)
        {
            if (string.IsNullOrWhiteSpace(l.Id)) throw new Exception("Link missing id");
            if (!ids.Contains(l.From)) throw new Exception($"Link '{l.Id}' references unknown from '{l.From}'");
            if (!ids.Contains(l.To)) throw new Exception($"Link '{l.Id}' references unknown to '{l.To}'");
            foreach (var v in l.Via)
            {
                if (!ids.Contains(v)) throw new Exception($"Link '{l.Id}' references unknown via '{v}'");
            }
        }

        // Validate business process steps
        var linkIds = new HashSet<string>(config.Links.Select(x => x.Id));
        foreach (var bp in config.BusinessProcesses)
        {
            foreach (var s in bp.Steps)
            {
                if (!string.IsNullOrEmpty(s.Node))
                {
                    if (!ids.Contains(s.Node)) throw new Exception($"BusinessProcess '{bp.Id}' references unknown node '{s.Node}'");
                }
                if (!string.IsNullOrEmpty(s.Link))
                {
                    if (!linkIds.Contains(s.Link)) throw new Exception($"BusinessProcess '{bp.Id}' references unknown link '{s.Link}'");
                }
            }
        }
    }

    public object BuildGraph()
    {
        var config = LoadConfig();

        var nodeMap = new Dictionary<string, Node>();

        // collect all nodes
        foreach (var n in config.Platforms) nodeMap[n.Id] = n;
        foreach (var n in config.Applications) nodeMap[n.Id] = n;
        foreach (var n in config.Modules) nodeMap[n.Id] = n;

        // build node elements with parent when applicable
        var nodesWithParent = new List<object>();
        foreach (var n in nodeMap.Values)
        {
            var dict = new Dictionary<string, object?> {
                ["id"] = n.Id,
                ["type"] = n.Type,
                ["displayName"] = n.DisplayName
            };

            if (n.Type == "application")
            {
                var parent = config.Platforms.FirstOrDefault(p => p.Applications.Contains(n.Id));
                if (parent != null) dict["parent"] = parent.Id;
            }

            if (n.Type == "module")
            {
                var parent = config.Applications.FirstOrDefault(a => a.Modules.Contains(n.Id));
                if (parent != null) dict["parent"] = parent.Id;
            }

            nodesWithParent.Add(new { data = dict });
        }

        var edges = new List<object>();
        foreach (var l in config.Links)
        {
            var path = new List<string> { l.From };
            if (l.Via != null && l.Via.Any()) path.AddRange(l.Via);
            path.Add(l.To);

            for (int i = 0; i < path.Count - 1; i++)
            {
                var segId = $"{l.Id}__seg__{i}";
                var showLabel = (i == 0) ? (l.DisplayName ?? string.Empty) : string.Empty; // only first segment shows label
                edges.Add(new
                {
                    data = new Dictionary<string, object?> {
                        ["id"] = segId,
                        ["source"] = path[i],
                        ["target"] = path[i+1],
                        ["displayName"] = showLabel,
                        ["kind"] = l.Kind,
                        ["originalLinkId"] = l.Id,
                        ["segmentIndex"] = i,
                        ["segmentCount"] = path.Count - 1
                    }
                });
            }
        }

        return new { nodes = nodesWithParent, edges = edges };
    }

    public object GetProcesses()
    {
        var config = LoadConfig();
        var list = new List<object>();
        foreach (var bp in config.BusinessProcesses)
        {
            if (string.IsNullOrWhiteSpace(bp.DisplayName)) continue; // do not show unnamed
            list.Add(new
            {
                id = bp.Id,
                displayName = bp.DisplayName,
                steps = bp.Steps,
                usesLinks = bp.UsesLinks,
                color = bp.Color,
                hiddenByDefault = bp.HiddenByDefault
            });
        }

        return list;
    }
}
