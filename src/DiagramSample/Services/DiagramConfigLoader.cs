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

    private IEnumerable<Node> EnumerateAllNodes(DiagramConfig config)
    {
        foreach (var p in config.Platforms)
        {
            yield return p;
            foreach (var a in p.Applications)
            {
                yield return a;
                foreach (var m in a.Modules)
                {
                    yield return m;
                }
            }
        }
    }

    private IEnumerable<Link> EnumerateAllLinks(DiagramConfig config)
    {
        // collect links from all nodes (platforms, applications, modules)
        foreach (var p in config.Platforms)
        {
            foreach (var l in p.Links)
            {
                yield return l;
            }

            foreach (var a in p.Applications)
            {
                foreach (var l in a.Links)
                {
                    yield return l;
                }

                foreach (var m in a.Modules)
                {
                    foreach (var l in m.Links)
                    {
                        yield return l;
                    }
                }
            }
        }
    }

    private void ValidateConfig(DiagramConfig config)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var n in EnumerateAllNodes(config))
        {
            if (string.IsNullOrWhiteSpace(n.Id)) throw new Exception("Node missing id");
            if (!ids.Add(n.Id!)) throw new Exception($"Duplicate node id: {n.Id}");
        }

        // Validate types for containment (optional but helpful)
        foreach (var p in config.Platforms)
        {
            foreach (var a in p.Applications)
            {
                if (a.Type != null && a.Type != "application")
                    throw new Exception($"Platform '{p.Id}' contains a child that is not an application: '{a.Id}'");
            }

            foreach (var a in p.Applications)
            {
                foreach (var m in a.Modules)
                {
                    if (m.Type != null && m.Type != "module")
                        throw new Exception($"Application '{a.Id}' contains a child that is not a module: '{m.Id}'");
                }
            }
        }

        // Validate links collected from nodes
        var allLinks = EnumerateAllLinks(config).ToList();
        foreach (var l in allLinks)
        {
            if (string.IsNullOrWhiteSpace(l.Id)) throw new Exception("Link missing id");
            if (string.IsNullOrWhiteSpace(l.From) || !ids.Contains(l.From)) throw new Exception($"Link '{l.Id}' references unknown from '{l.From}'");
            if (string.IsNullOrWhiteSpace(l.To) || !ids.Contains(l.To)) throw new Exception($"Link '{l.Id}' references unknown to '{l.To}'");
            foreach (var v in l.Via)
            {
                if (string.IsNullOrWhiteSpace(v) || !ids.Contains(v)) throw new Exception($"Link '{l.Id}' references unknown via '{v}'");
            }
        }

        // Validate business process steps
        var linkIds = new HashSet<string>(allLinks.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
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

        var nodeMap = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);

        // collect all nodes by flattening nested structure
        foreach (var n in EnumerateAllNodes(config))
        {
            nodeMap[n.Id!] = n;
        }

        // build node elements with parent when applicable
        var nodesWithParent = new List<object>();
        foreach (var kv in nodeMap)
        {
            var n = kv.Value;
            var dict = new Dictionary<string, object?> {
                ["id"] = n.Id,
                ["type"] = n.Type,
                ["displayName"] = string.IsNullOrWhiteSpace(n.DisplayName) ? n.Id : n.DisplayName
            };

            if (n.Type == "application")
            {
                // find platform parent
                var parent = config.Platforms.FirstOrDefault(p => p.Applications.Any(a => string.Equals(a.Id, n.Id, StringComparison.OrdinalIgnoreCase)));
                if (parent != null) dict["parent"] = parent.Id;
            }

            if (n.Type == "module")
            {
                // find application parent
                var parent = config.Platforms.SelectMany(p => p.Applications).FirstOrDefault(a => a.Modules.Any(m => string.Equals(m.Id, n.Id, StringComparison.OrdinalIgnoreCase)));
                if (parent != null) dict["parent"] = parent.Id;
            }

            nodesWithParent.Add(new { data = dict });
        }

        var edges = new List<object>();
        var allLinks = EnumerateAllLinks(config).ToList();
        foreach (var l in allLinks)
        {
            var path = new List<string> { l.From };
            if (l.Via != null && l.Via.Any()) path.AddRange(l.Via);
            path.Add(l.To);

            // normalize path ids to canonical casing from nodeMap when available
            var normalizedPath = path.Select(p => nodeMap.TryGetValue(p, out var node) ? node.Id! : p).ToList();

            for (int i = 0; i < normalizedPath.Count - 1; i++)
            {
                var segId = $"{l.Id}__seg__{i}";
                var showLabel = (i == 0) ? (l.DisplayName ?? string.Empty) : string.Empty; // only first segment shows label
                edges.Add(new
                {
                    data = new Dictionary<string, object?> {
                        ["id"] = segId,
                        ["source"] = normalizedPath[i],
                        ["target"] = normalizedPath[i+1],
                        ["displayName"] = showLabel,
                        ["kind"] = l.Kind,
                        ["originalLinkId"] = l.Id,
                        ["segmentIndex"] = i,
                        ["segmentCount"] = normalizedPath.Count - 1
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
