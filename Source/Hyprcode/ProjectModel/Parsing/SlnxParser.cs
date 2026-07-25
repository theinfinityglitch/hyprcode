using System.Xml.Linq;

namespace Hyprcode.ProjectModel.Parsing;

public static class SlnxParser
{
    public static DotnetSolution? Parse(string rootDir, string targetFile)
    {
        if (!File.Exists(targetFile))
            return null;

        XDocument doc;
        try
        {
            doc = XDocument.Load(targetFile);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var solution = new DotnetSolution
        {
            Name = Path.GetFileNameWithoutExtension(targetFile),
            Path = targetFile,
        };

        XElement? root = doc.Root;
        if (root == null)
            return solution;

        DotnetProject CreateProject(string rawPath)
        {
            string projectPath = rawPath.Replace('\\', '/');
            string fullPath = Path.GetFullPath(Path.Combine(rootDir, projectPath));
            string dir = Path.GetDirectoryName(fullPath) ?? rootDir;
            return new DotnetProject
            {
                Name = Path.GetFileNameWithoutExtension(projectPath),
                Path = fullPath,
                Dir = dir,
            };
        }

        var parsedFolders = new List<DotnetFolder>();

        foreach (XElement folderEl in root.Elements("Folder"))
        {
            string? nameAttr = folderEl.Attribute("Name")?.Value;
            if (string.IsNullOrEmpty(nameAttr))
                continue;

            string folderPath = nameAttr.Replace('\\', '/').Trim('/');
            string? parentPath = null;
            int lastSlash = folderPath.LastIndexOf('/');
            if (lastSlash >= 0)
                parentPath = folderPath[..lastSlash];

            var folder = new DotnetFolder
            {
                Name = Path.GetFileNameWithoutExtension(folderPath),
                Path = folderPath,
                ParentPath = parentPath,
            };

            foreach (XElement projectEl in folderEl.Elements("Project"))
            {
                string? pathAttr = projectEl.Attribute("Path")?.Value;
                if (!string.IsNullOrEmpty(pathAttr))
                    folder.Projects.Add(CreateProject(pathAttr));
            }

            parsedFolders.Add(folder);
        }

        Dictionary<string, DotnetFolder> folderIndex = parsedFolders.ToDictionary(f => f.Path);

        foreach (DotnetFolder folder in parsedFolders)
        {
            if (
                folder.ParentPath != null
                && folderIndex.TryGetValue(folder.ParentPath, out DotnetFolder? parent)
            )
                parent.Children.Add(folder);
            else
                solution.Folders.Add(folder);
        }

        foreach (XElement projectEl in root.Elements("Project"))
        {
            string? pathAttr = projectEl.Attribute("Path")?.Value;
            if (!string.IsNullOrEmpty(pathAttr))
                solution.Projects.Add(CreateProject(pathAttr));
        }

        return solution;
    }
}
