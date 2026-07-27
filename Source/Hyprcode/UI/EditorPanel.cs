using Hexa.NET.ImGui;
using Hyprcode.Editor;

namespace Hyprcode.UI;

public static class EditorPanel
{
    private static EditorBuffer? pendingClose;
    private static string? pendingFocusPath;
    private static string? lastNotifiedPath;
    private static uint? sharedDockId;
    public static event Action<string>? ActiveFileChanged;

    public static void RequestFocus(string path) => pendingFocusPath = path;

    public static void Draw(List<EditorBuffer> buffers, ref string? activePath)
    {
        if (buffers.Count == 0)
        {
            DrawPlaceholder();
            activePath = null;
            NotifyIfActiveChanged(activePath);
            return;
        }

        bool requestUnsavedPopup = false;
        EditorBuffer? toCloseImmediately = null;
        string? focusedThisFrame = null;

        foreach (EditorBuffer buffer in buffers)
        {
            string fileName = Path.GetFileName(buffer.Path);
            string title = $"{fileName}###{buffer.Path}";

            if (sharedDockId is uint dockId)
                ImGui.SetNextWindowDockID(dockId, ImGuiCond.FirstUseEver);

            if (pendingFocusPath == buffer.Path)
                ImGui.SetNextWindowFocus();

            ImGuiWindowFlags flags = buffer.IsDirty
                ? ImGuiWindowFlags.UnsavedDocument
                : ImGuiWindowFlags.None;
            bool open = true;

            if (ImGui.Begin(title, ref open, flags))
            {
                if (ImGui.IsWindowFocused())
                    focusedThisFrame = buffer.Path;

                if (ImGui.IsWindowDocked())
                    sharedDockId = ImGui.GetWindowDockID();

                DrawBufferContent(buffer);
            }
            ImGui.End();

            if (!open)
            {
                if (buffer.IsDirty)
                {
                    pendingClose = buffer;
                    requestUnsavedPopup = true;
                }
                else
                    toCloseImmediately = buffer;
            }
        }

        pendingFocusPath = null;

        if (focusedThisFrame != null)
            activePath = focusedThisFrame;

        if (toCloseImmediately != null)
            CloseBuffer(buffers, ref activePath, toCloseImmediately);

        if (requestUnsavedPopup)
            ImGui.OpenPopup("UnsavedChangesPopup");

        DrawUnsavedChangesPopup(buffers, ref activePath);

        NotifyIfActiveChanged(activePath);
    }

    private static void DrawPlaceholder()
    {
        ImGui.Begin("Editor");

        if (ImGui.IsWindowDocked())
            sharedDockId = ImGui.GetWindowDockID();

        ImGui.TextDisabled("No file open. Select a file in the Explorer panel.");
        ImGui.End();
    }

    private static void NotifyIfActiveChanged(string? activePath)
    {
        if (activePath == lastNotifiedPath)
            return;

        lastNotifiedPath = activePath;
        if (activePath != null)
            ActiveFileChanged?.Invoke(activePath);
    }

    private static void DrawBufferContent(EditorBuffer buffer)
    {
        if (buffer.LoadError != null)
        {
            ImGui.TextColored(
                new System.Numerics.Vector4(0.95f, 0.35f, 0.35f, 1f),
                buffer.LoadError
            );
            return;
        }

        if (ImGui.GetIO().KeyCtrl && ImGui.IsKeyPressed(ImGuiKey.S))
            buffer.Save();

        string text = buffer.Text;

        bool changed = ImGui.InputTextMultiline(
            "##editor",
            ref text,
            1024 * 1024,
            new(-1.0f, -1.0f),
            ImGuiInputTextFlags.AllowTabInput
        );

        if (changed)
        {
            buffer.Text = text;
            buffer.IsDirty = true;
        }
    }

    private static void DrawUnsavedChangesPopup(List<EditorBuffer> buffers, ref string? activePath)
    {
        if (ImGui.BeginPopupModal("UnsavedChangesPopup", ImGuiWindowFlags.AlwaysAutoResize))
        {
            EditorBuffer? buffer = pendingClose;
            string fileName = buffer != null ? Path.GetFileName(buffer.Path) : "this file";
            ImGui.TextUnformatted($"'{fileName}' has unsaved changes.");

            if (ImGui.Button("Save"))
            {
                buffer?.Save();
                if (buffer != null)
                    CloseBuffer(buffers, ref activePath, buffer);
                pendingClose = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Discard"))
            {
                if (buffer != null)
                    CloseBuffer(buffers, ref activePath, buffer);
                pendingClose = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
            {
                pendingClose = null;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndPopup();
        }
    }

    private static void CloseBuffer(
        List<EditorBuffer> buffers,
        ref string? activePath,
        EditorBuffer buffer
    )
    {
        buffers.Remove(buffer);
        if (activePath == buffer.Path)
            activePath = buffers.Count > 0 ? buffers[^1].Path : null;
    }
}
