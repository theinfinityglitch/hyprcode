using System.Text.RegularExpressions;

namespace Hyprcode.ProjectModel.Parsing;

public static partial class SlnParser
{
    private const string SolutionFolderGuid = "{2150E333-8FDC-42A3-9474-1A3956D46DE8}";

    [GeneratedRegex(
        @"Project\(""(\{[A-Fa-f0-9\-]+\})""\)\s*=\s*""([^""]+)"",\s*""([^""]+)"",\s*""(\{[A-Fa-f0-9\-]+\})""",
        RegexOptions.None
    )]
    private static partial Regex ProjectLineRegex();

    [GeneratedRegex(@"GlobalSection\(NestedProjects\).*?EndGlobalSection", RegexOptions.Singleline)]
    private static partial Regex NestedProjectsSectionRegex();

    [GeneratedRegex(@"(\{[\w\-]+\})\s*=\s*(\{[\w\-]+\})")]
    private static partial Regex NestedPairRegex();

    public static DotnetSolution? Parse(string rootDir, string targetFile)
    {
        if (!File.Exists(targetFile))
            return null;
        string content = File.ReadAllText(targetFile);

        var solution = new DotnetSolution
        {
            Name = Path.GetFileNameWithoutExtension(targetFile),
            Path = targetFile,
        };

        var projectsByGuid = new Dictionary<string, DotnetProject>();
        var foldersByGuid = new Dictionary<string, DotnetFolder>();

        foreach (Match m in ProjectLineRegex().Matches(content))
        {
            string projectType = m.Groups[1].Value;
            string projectName = m.Groups[2].Value;
            string projectPath = m.Groups[3].Value.Replace('\\', '/');
            string projectGuid = m.Groups[4].Value;

            if (projectType == SolutionFolderGuid)
            {
                string folderPath = projectPath.Trim('/');
                foldersByGuid[projectGuid] = new DotnetFolder
                {
                    Name = Path.GetFileNameWithoutExtension(folderPath),
                    Path = folderPath,
                    Guid = projectGuid,
                };
            }
            else
            {
                string fullPath = Path.GetFullPath(Path.Combine(rootDir, projectPath));
                string dir = Path.GetDirectoryName(fullPath) ?? rootDir;
                projectsByGuid[projectGuid] = new DotnetProject
                {
                    Type = projectType,
                    Name = projectName,
                    Path = fullPath,
                    Dir = dir,
                    Guid = projectGuid,
                };
            }
        }

        var nestedFolders = new HashSet<string>();
        var nestedProjects = new HashSet<string>();

        Match nestedSectionMatch = NestedProjectsSectionRegex().Match(content);
        if (nestedSectionMatch.Success)
        {
            foreach (Match pair in NestedPairRegex().Matches(nestedSectionMatch.Value))
            {
                string childGuid = pair.Groups[1].Value;
                string parentGuid = pair.Groups[2].Value;

                if (!foldersByGuid.TryGetValue(parentGuid, out DotnetFolder? parentFolder))
                    continue;

                if (projectsByGuid.TryGetValue(childGuid, out DotnetProject? childProject))
                {
                    parentFolder.Projects.Add(childProject);
                    nestedProjects.Add(childGuid);
                }
                else if (foldersByGuid.TryGetValue(childGuid, out DotnetFolder? childFolder))
                {
                    childFolder.ParentGuid = parentGuid;
                    parentFolder.Children.Add(childFolder);
                    nestedFolders.Add(childGuid);
                }
            }
        }

        foreach ((string guid, DotnetProject project) in projectsByGuid)
            if (!nestedProjects.Contains(guid))
                solution.Projects.Add(project);

        foreach ((string guid, DotnetFolder folder) in foldersByGuid)
            if (!nestedFolders.Contains(guid))
                solution.Folders.Add(folder);

        foreach (DotnetFolder folder in solution.Folders)
            AssignFolderPaths(folder, null);

        return solution;
    }

    private static void AssignFolderPaths(DotnetFolder folder, string? parentPath)
    {
        folder.ParentPath = parentPath;
        folder.Path = parentPath == null ? folder.Name : $"{parentPath}/{folder.Name}";
        foreach (DotnetFolder child in folder.Children)
            AssignFolderPaths(child, folder.Path);
    }
}
