using Hexa.NET.ImGui;
using Hyprcode.Explorer;
using IconFonts;

namespace Hyprcode.UI;

public static class ProjectExplorerPanel
{
    public static event Action<string>? FileSelected;

    public static void Draw(IReadOnlyList<ExplorerNode> roots, string? selectedPath)
    {
        ImGui.Begin("Explorer");

        if (roots.Count == 0)
        {
            ImGui.TextDisabled("No solution or project open.");
            ImGui.TextDisabled("File > Open Solution/Project...");
        }
        else
            foreach (ExplorerNode root in roots)
                DrawNode(root, selectedPath);

        ImGui.End();
    }

    private static void DrawNode(ExplorerNode node, string? selectedPath)
    {
        ImGui.PushID(node.Id);

        string label = $"{Icon(node.Kind)} {node.Name}";
        bool isSelected = node.Path != null && node.Path == selectedPath;

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow
            | ImGuiTreeNodeFlags.SpanFullWidth
            | ImGuiTreeNodeFlags.FramePadding;

        if (isSelected)
            flags |= ImGuiTreeNodeFlags.Selected;

        if (node.IsLeaf)
        {
            ImGuiTreeNodeFlags leafFlags =
                flags | ImGuiTreeNodeFlags.Leaf | ImGuiTreeNodeFlags.NoTreePushOnOpen;

            ImGui.TreeNodeEx(label, leafFlags);
            bool clicked = ImGui.IsItemClicked();

            if (clicked && node.Kind == ExplorerNodeKind.File && node.Path != null)
                FileSelected?.Invoke(node.Path);

            if (node.Path != null && ImGui.IsItemHovered())
                ImGui.SetTooltip(node.Path);

            ImGui.PopID();
            return;
        }

        if (node.Kind is ExplorerNodeKind.Solution or ExplorerNodeKind.Project)
            flags |= ImGuiTreeNodeFlags.DefaultOpen;

        bool open = ImGui.TreeNodeEx(label, flags);

        if (node.Path != null && ImGui.IsItemHovered())
            ImGui.SetTooltip(node.Path);

        if (open)
        {
            foreach (ExplorerNode child in node.Children)
                DrawNode(child, selectedPath);

            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private static string Icon(ExplorerNodeKind kind) =>
        kind switch
        {
            ExplorerNodeKind.Solution => Lucide.Layout,
            ExplorerNodeKind.SolutionFolder => Lucide.Folder,
            ExplorerNodeKind.Project => Lucide.Component,
            ExplorerNodeKind.DependenciesRoot => Lucide.Boxes,
            ExplorerNodeKind.DependencyGroup => Lucide.FolderTree,
            ExplorerNodeKind.Framework => Lucide.Cpu,
            ExplorerNodeKind.Package => Lucide.Package,
            ExplorerNodeKind.ProjectReference => Lucide.Link,
            ExplorerNodeKind.Import => Lucide.Import,
            ExplorerNodeKind.Directory => Lucide.Folder,
            ExplorerNodeKind.File => Lucide.File,
            _ => "",
        };
}
