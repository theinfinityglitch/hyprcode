namespace Hyprcode.ProjectModel;

public sealed class DotnetSolution
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public List<DotnetProject> Projects { get; } = [];
    public List<DotnetFolder> Folders { get; } = [];
}

public sealed class DotnetFolder
{
    public required string Name { get; init; }
    public required string Path { get; set; }
    public string? ParentPath { get; set; }
    public string? Guid { get; init; }
    public string? ParentGuid { get; set; }
    public List<DotnetFolder> Children { get; } = [];
    public List<DotnetProject> Projects { get; } = [];
}

public sealed class DotnetProject
{
    public string? Type { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public required string Dir { get; init; }
    public string? Guid { get; init; }
    public List<ProjectReference> References { get; } = [];
    public List<PackageReference> Packages { get; } = [];
    public List<ImportRef> Imports { get; } = [];
    public ProjectProperties Properties { get; set; } = new();
}

public sealed class ProjectProperties
{
    public string? Sdk { get; set; }
    public List<string> TargetFrameworks { get; } = [];
}

public sealed class PackageReference
{
    public required string Name { get; init; }
    public string? Version { get; init; }
    public string? PrivateAssets { get; init; }
    public string? IncludeAssets { get; init; }
    public string? ExcludeAssets { get; init; }
}

public sealed class ProjectReference
{
    public required string Name { get; init; }
    public required string Include { get; init; }
    public string? Path { get; init; }
}

public sealed class ImportRef
{
    public required string Project { get; init; }
    public string? Condition { get; init; }
}
