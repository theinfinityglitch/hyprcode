using Hyprcode.ProjectModel;

namespace Hyprcode.Explorer;

public static class ExplorerNodeBuilder
{
    public static List<ExplorerNode> Build(DotnetSolution solution)
    {
        var children = new List<ExplorerNode>();
        children.AddRange(solution.Folders.Select(BuildFolderNode));
        children.AddRange(solution.Projects.Select(BuildProjectNode));

        return
        [
            new ExplorerNode
            {
                Id = $"solution:{solution.Path}",
                Name = solution.Name,
                Kind = ExplorerNodeKind.Solution,
                Path = Path.GetDirectoryName(solution.Path),
            }.WithChildren(children),
        ];
    }

    private static ExplorerNode BuildFolderNode(DotnetFolder folder)
    {
        var children = new List<ExplorerNode>();
        children.AddRange(folder.Children.Select(BuildFolderNode));
        children.AddRange(folder.Projects.Select(BuildProjectNode));

        return new ExplorerNode
        {
            Id = $"folder:{folder.Path}",
            Name = folder.Name,
            Kind = ExplorerNodeKind.SolutionFolder,
        }.WithChildren(children);
    }

    private static ExplorerNode BuildProjectNode(DotnetProject project)
    {
        var children = new List<ExplorerNode>();

        ExplorerNode? deps = BuildDependenciesNode(project);
        if (deps != null)
            children.Add(deps);

        children.AddRange(ProjectFileScanner.BuildFileNodes(project));

        return new ExplorerNode
        {
            Id = $"project:{project.Dir}",
            Name = project.Name,
            Kind = ExplorerNodeKind.Project,
            Path = project.Dir,
        }.WithChildren(children);
    }

    private static ExplorerNode? BuildDependenciesNode(DotnetProject project)
    {
        var groups = new List<ExplorerNode>();

        if (project.Properties.TargetFrameworks.Count > 0)
            groups.Add(
                GroupNode(
                    "Frameworks",
                    project.Properties.TargetFrameworks.Select(
                        (fw, i) => LeafNode($"fw:{project.Dir}:{i}", fw, ExplorerNodeKind.Framework)
                    )
                )
            );

        if (project.Packages.Count > 0)
            groups.Add(
                GroupNode(
                    "Packages",
                    project.Packages.Select(
                        (pkg, i) =>
                        {
                            string label =
                                pkg.Version == null ? pkg.Name : $"{pkg.Name} ({pkg.Version})";
                            return LeafNode(
                                $"pkg:{project.Dir}:{i}",
                                label,
                                ExplorerNodeKind.Package
                            );
                        }
                    )
                )
            );

        if (project.References.Count > 0)
            groups.Add(
                GroupNode(
                    "Projects",
                    project.References.Select(
                        (r, i) =>
                            new ExplorerNode
                            {
                                Id = $"ref:{project.Dir}:{i}",
                                Name = r.Name,
                                Kind = ExplorerNodeKind.ProjectReference,
                                Path = r.Path,
                            }
                    )
                )
            );

        if (project.Imports.Count > 0)
            groups.Add(
                GroupNode(
                    "Imports",
                    project.Imports.Select(
                        (imp, i) =>
                            LeafNode(
                                $"import:{project.Dir}:{i}",
                                imp.Project,
                                ExplorerNodeKind.Import
                            )
                    )
                )
            );

        if (groups.Count == 0)
            return null;

        return new ExplorerNode
        {
            Id = $"deps:{project.Dir}",
            Name = "Dependencies",
            Kind = ExplorerNodeKind.DependenciesRoot,
        }.WithChildren(groups);
    }

    private static ExplorerNode GroupNode(string name, IEnumerable<ExplorerNode> children) =>
        new ExplorerNode
        {
            Id = $"group:{name}:{Guid.NewGuid()}",
            Name = name,
            Kind = ExplorerNodeKind.DependencyGroup,
        }.WithChildren([.. children]);

    private static ExplorerNode LeafNode(string id, string name, ExplorerNodeKind kind) =>
        new()
        {
            Id = id,
            Name = name,
            Kind = kind,
        };

    private static ExplorerNode WithChildren(this ExplorerNode node, List<ExplorerNode> children)
    {
        node.Children.AddRange(children);
        return node;
    }
}
