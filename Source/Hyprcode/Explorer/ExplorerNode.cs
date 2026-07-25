namespace Hyprcode.Explorer;

public enum ExplorerNodeKind
{
    Solution,
    SolutionFolder,
    Project,
    DependenciesRoot,
    DependencyGroup,
    Framework,
    Package,
    ProjectReference,
    Import,
    Directory,
    File,
}

public sealed class ExplorerNode
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ExplorerNodeKind Kind { get; init; }

    public string? Path { get; init; }

    public List<ExplorerNode> Children { get; } = [];

    public bool IsLeaf => Children.Count == 0;
}
