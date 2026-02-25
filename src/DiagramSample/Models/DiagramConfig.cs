namespace DiagramSample.Models;

public record DiagramConfig
{
	public List<Node> Platforms { get; init; } = new();

	// Grouping of business processes only (top-level BusinessProcesses removed)
	public List<BusinessProcessGroup> BusinessProcessGroups { get; init; } = new();
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
    public string? Icon { get; init; } // optional icon/kind for UI (e.g. queue, database, web, desktop, worker)
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

public record BusinessProcessGroup
{
    public string? Id { get; init; }
    public string? DisplayName { get; init; }
    // Processes are defined under the group in YAML
    public List<BusinessProcess> Processes { get; init; } = new();
    // If true, group is collapsed by default in the UI
    public bool HiddenByDefault { get; init; }
}

public record ProcessStep
{
    public string? Node { get; init; }
    public string? Link { get; init; }
}
