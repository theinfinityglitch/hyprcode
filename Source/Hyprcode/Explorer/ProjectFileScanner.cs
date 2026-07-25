using Hyprcode.ProjectModel;

namespace Hyprcode.Explorer;

public static class ProjectFileScanner
{
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        "node_modules",
        "bin",
        "obj",
        ".vs",
        "packages",
        ".idea",
        ".vscode",
    };

    public static List<ExplorerNode> BuildFileNodes(DotnetProject project)
    {
        string ignoreFileName = Path.GetFileName(project.Path);
        return ScanDirectory(project.Dir, ignoreFileName);
    }

    private static List<ExplorerNode> ScanDirectory(string dir, string ignoreFileName)
    {
        var directories = new List<(string Name, string Path)>();
        var files = new List<(string Name, string Path)>();

        IEnumerable<string> entries;
        try
        {
            entries = Directory.EnumerateFileSystemEntries(dir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException)
        {
            return [];
        }

        foreach (string entry in entries)
        {
            string name = Path.GetFileName(entry);
            if (Directory.Exists(entry))
            {
                if (IsIgnoredDir(name))
                    continue;
                directories.Add((name, entry));
            }
            else
            {
                if (name == ignoreFileName)
                    continue;
                files.Add((name, entry));
            }
        }

        directories.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        files.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

        var nodes = new List<ExplorerNode>(directories.Count + files.Count);

        foreach ((string name, string path) in directories)
        {
            var dirNode = new ExplorerNode
            {
                Id = $"dir:{path}",
                Name = name,
                Kind = ExplorerNodeKind.Directory,
                Path = path,
            };
            dirNode.Children.AddRange(ScanDirectory(path, ignoreFileName));
            nodes.Add(dirNode);
        }

        foreach ((string name, string path) in files)
        {
            nodes.Add(
                new ExplorerNode
                {
                    Id = $"file:{path}",
                    Name = name,
                    Kind = ExplorerNodeKind.File,
                    Path = path,
                }
            );
        }

        return nodes;
    }

    private static bool IsIgnoredDir(string name) =>
        IgnoredDirs.Contains(name) || name.StartsWith('.');
}
