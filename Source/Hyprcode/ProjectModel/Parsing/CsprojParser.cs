using System.Xml.Linq;

namespace Hyprcode.ProjectModel.Parsing;

public static class CsprojParser
{
    private sealed record ParsedProject(
        ProjectProperties Properties,
        List<PackageReference> Packages,
        List<ProjectReference> References,
        List<ImportRef> Imports
    );

    public static void Enrich(DotnetProject project)
    {
        ParsedProject? data = ParseFile(project.Path);
        if (data == null)
            return;

        project.Properties = data.Properties;
        project.Packages.Clear();
        project.Packages.AddRange(data.Packages);
        project.References.Clear();
        project.References.AddRange(data.References);
        project.Imports.Clear();
        project.Imports.AddRange(data.Imports);
    }

    private static ParsedProject? ParseFile(string targetFile)
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

        StripNamespaces(doc.Root);

        XElement? projectEl = doc.Root;
        if (projectEl == null || projectEl.Name.LocalName != "Project")
            return null;

        string projectDir = Path.GetDirectoryName(targetFile) ?? ".";

        var properties = new ProjectProperties { Sdk = projectEl.Attribute("Sdk")?.Value };

        foreach (XElement propertyGroup in projectEl.Elements("PropertyGroup"))
        {
            string? tf = propertyGroup.Element("TargetFramework")?.Value;
            string? tfs = propertyGroup.Element("TargetFrameworks")?.Value;
            properties.TargetFrameworks.AddRange(SplitFrameworks(tf));
            properties.TargetFrameworks.AddRange(SplitFrameworks(tfs));
        }

        var packages = new List<PackageReference>();
        var references = new List<ProjectReference>();

        foreach (XElement itemGroup in projectEl.Elements("ItemGroup"))
        {
            foreach (XElement pkg in itemGroup.Elements("PackageReference"))
            {
                string? name = pkg.Attribute("Include")?.Value ?? pkg.Attribute("Update")?.Value;
                if (string.IsNullOrEmpty(name))
                    continue;

                packages.Add(
                    new PackageReference
                    {
                        Name = name,
                        Version = pkg.Attribute("Version")?.Value ?? pkg.Element("Version")?.Value,
                        PrivateAssets =
                            pkg.Attribute("PrivateAssets")?.Value
                            ?? pkg.Element("PrivateAssets")?.Value,
                        IncludeAssets =
                            pkg.Attribute("IncludeAssets")?.Value
                            ?? pkg.Element("IncludeAssets")?.Value,
                        ExcludeAssets =
                            pkg.Attribute("ExcludeAssets")?.Value
                            ?? pkg.Element("ExcludeAssets")?.Value,
                    }
                );
            }

            foreach (XElement projRef in itemGroup.Elements("ProjectReference"))
            {
                string? include = projRef.Attribute("Include")?.Value;
                if (string.IsNullOrEmpty(include))
                    continue;

                string normalized = include.Replace('\\', '/');
                string resolvedPath = Path.GetFullPath(Path.Combine(projectDir, normalized));

                references.Add(
                    new ProjectReference
                    {
                        Name = Path.GetFileNameWithoutExtension(normalized),
                        Include = include,
                        Path = resolvedPath,
                    }
                );
            }
        }

        var imports = new List<ImportRef>();
        foreach (XElement import in projectEl.Elements("Import"))
        {
            string? importProject = import.Attribute("Project")?.Value;
            if (string.IsNullOrEmpty(importProject))
                continue;

            imports.Add(
                new ImportRef
                {
                    Project = importProject,
                    Condition = import.Attribute("Condition")?.Value,
                }
            );
        }

        return new ParsedProject(properties, packages, references, imports);
    }

    private static List<string> SplitFrameworks(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];
        return
        [
            .. value.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            ),
        ];
    }

    private static void StripNamespaces(XElement? element)
    {
        if (element == null)
            return;
        element.Name = element.Name.LocalName;
        foreach (XElement child in element.Elements())
            StripNamespaces(child);
    }
}
