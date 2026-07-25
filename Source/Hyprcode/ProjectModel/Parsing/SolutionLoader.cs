namespace Hyprcode.ProjectModel.Parsing;

public static class SolutionLoader
{
    public static DotnetSolution? Load(string inputPath)
    {
        string resolved = ResolveTargetFile(NormalizeUserPath(inputPath));
        if (resolved.Length == 0)
            return null;

        string rootDir = Path.GetDirectoryName(resolved) ?? ".";
        string ext = Path.GetExtension(resolved).ToLowerInvariant();

        DotnetSolution? solution = ext switch
        {
            ".sln" => SlnParser.Parse(rootDir, resolved),
            ".slnx" => SlnxParser.Parse(rootDir, resolved),
            ".csproj" => WrapSingleProject(resolved),
            _ => null,
        };

        if (solution == null)
            return null;

        foreach (DotnetProject project in AllProjects(solution))
            CsprojParser.Enrich(project);

        return solution;
    }

    private static IEnumerable<DotnetProject> AllProjects(DotnetSolution solution)
    {
        foreach (DotnetProject p in solution.Projects)
            yield return p;
        foreach (DotnetFolder folder in solution.Folders)

        foreach (DotnetProject p in AllProjectsInFolder(folder))
            yield return p;
    }

    private static IEnumerable<DotnetProject> AllProjectsInFolder(DotnetFolder folder)
    {
        foreach (DotnetProject p in folder.Projects)
            yield return p;
        foreach (DotnetFolder child in folder.Children)

        foreach (DotnetProject p in AllProjectsInFolder(child))
            yield return p;
    }

    private static DotnetSolution WrapSingleProject(string csprojPath)
    {
        string dir = Path.GetDirectoryName(csprojPath) ?? ".";
        var project = new DotnetProject
        {
            Name = Path.GetFileNameWithoutExtension(csprojPath),
            Path = csprojPath,
            Dir = dir,
        };

        var solution = new DotnetSolution { Name = project.Name, Path = csprojPath };
        solution.Projects.Add(project);
        return solution;
    }

    private static string NormalizeUserPath(string inputPath)
    {
        string path = inputPath.Trim();
        if (path.Length >= 2 && path[0] == '"' && path[^1] == '"')
            path = path[1..^1];

        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return path;
    }

    private static string ResolveTargetFile(string inputPath)
    {
        if (File.Exists(inputPath))
            return Path.GetFullPath(inputPath);
        if (!Directory.Exists(inputPath))
            return "";

        string? sln = Directory
            .EnumerateFiles(inputPath, "*.sln", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (sln != null)
            return sln;

        string? slnx = Directory
            .EnumerateFiles(inputPath, "*.slnx", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (slnx != null)
            return slnx;

        string? csproj = Directory
            .EnumerateFiles(inputPath, "*.csproj", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();
        if (csproj != null)
            return csproj;

        foreach (string pattern in new[] { "*.sln", "*.slnx", "*.csproj" })
        {
            string? found = SearchBounded(inputPath, pattern, maxDepth: 3);
            if (found != null)
                return found;
        }

        return "";
    }

    private static string? SearchBounded(string root, string pattern, int maxDepth)
    {
        return SearchBoundedRec(root, pattern, 0, maxDepth);
    }

    private static string? SearchBoundedRec(string dir, string pattern, int depth, int maxDepth)
    {
        if (depth > maxDepth)
            return null;

        string? hit;
        try
        {
            hit = Directory
                .EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        if (hit != null)
            return hit;

        IEnumerable<string> subDirs;
        try
        {
            subDirs = Directory.EnumerateDirectories(dir);
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        foreach (string sub in subDirs)
        {
            string name = Path.GetFileName(sub);
            if (name.StartsWith('.') || name is "bin" or "obj" or "node_modules")
                continue;

            string? found = SearchBoundedRec(sub, pattern, depth + 1, maxDepth);
            if (found != null)
                return found;
        }

        return null;
    }
}
