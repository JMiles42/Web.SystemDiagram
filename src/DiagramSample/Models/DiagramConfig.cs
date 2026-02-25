using System.Collections.Generic;

namespace DiagramSample.Models;

public class DiagramConfig
{
    public List<Node>? platforms { get; set; }
    public List<Node>? applications { get; set; }
    public List<Node>? modules { get; set; }

    public List<Link>? links { get; set; }

    public List<BusinessProcess>? businessProcesses { get; set; }
}

public class Node
{
    public string? id { get; set; }
    public string? type { get; set; }
    public string? displayName { get; set; }
    public List<string>? applications { get; set; } // for platform
    public List<string>? modules { get; set; } // for application
    public List<string>? tags { get; set; }
    public string? owner { get; set; }
}

public class Link
{
    public string? id { get; set; }
    public string? from { get; set; }
    public string? to { get; set; }
    public string? displayName { get; set; }
    public string? kind { get; set; }
    public List<string>? via { get; set; }
}

public class BusinessProcess
{
    public string? id { get; set; }
    public string? displayName { get; set; }
    public List<ProcessStep>? steps { get; set; }
    public List<string>? usesLinks { get; set; }
    public string? color { get; set; }
    public bool? hiddenByDefault { get; set; }
}

public class ProcessStep
{
    public string? node { get; set; }
    public string? link { get; set; }
}
