namespace DiagramSample.Models;

public record DiagramConfig
{
    public List<Node> Platforms { get; init; } = new();
    public List<BusinessProcess> BusinessProcesses { get; init; } = new();
}

public record Node
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public string? DisplayName { get; init; }
    public List<Node> Applications { get; init; } = new();
    public List<Node> Modules { get; init; } = new();
    public List<Link> Links { get; init; } = new();
    public List<string> Tags { get; init; } = new();
    public string? Owner { get; init; }
}

public record Link
{
    public string? Id { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public string? DisplayName { get; init; }
    public string? Kind { get; init; }
    public List<string> Via { get; init; } = new();
}

public record BusinessProcess
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    public List<ProcessStep> Steps { get; init; } = new();
    public List<string> UsesLinks { get; init; } = new();
    public string? Color { get; init; }
    public bool HiddenByDefault { get; init; }
}

public record ProcessStep
{
    public string? Node { get; init; }
    public string? Link { get; init; }
}
