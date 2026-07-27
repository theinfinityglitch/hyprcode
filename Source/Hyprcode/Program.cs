using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;
using Hyprcode.Editor;
using Hyprcode.Explorer;
using Hyprcode.ProjectModel;
using Hyprcode.ProjectModel.Parsing;
using Hyprcode.UI;
using SDL3;

namespace Hyprcode;

internal static class Program
{
    private const float BaseFontSize = 16.0f;
    private static float appliedUiScale = 1.0f;

    private static DotnetSolution? currentSolution;
    private static List<ExplorerNode> explorerRoots = [];
    private static string? selectedFilePath;
    private static readonly List<EditorBuffer> openBuffers = [];
    private static string? activeEditorPath;
    private static string openPathBuffer = "";
    private static string? openError;

    private static nint iniFilenamePtr;
    private static nint lucideFontDataPtr;
    private static nint lucideGlyphRangesPtr;
    private static nint lucideExcludeRangesPtr;

    [STAThread]
    private static unsafe void Main()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            SDL.LogError(SDL.LogCategory.System, $"SDL could not initialize: {SDL.GetError()}");
            return;
        }

        SDL.SetHint(SDL.Hints.AppID, "hyprcode");

        if (
            !SDL.CreateWindowAndRenderer(
                "Hypr Code",
                1280,
                720,
                SDL.WindowFlags.Resizable | SDL.WindowFlags.HighPixelDensity,
                out var window,
                out var renderer
            )
        )
        {
            SDL.LogError(
                SDL.LogCategory.Application,
                $"Error creating window and rendering: {SDL.GetError()}"
            );
            return;
        }

        SDL.SetRenderDrawColor(renderer, 100, 149, 237, 255);
        SDL.SetRenderLogicalPresentation(renderer, 0, 0, SDL.RendererLogicalPresentation.Disabled);

        ImGui.CreateContext();
        ImGui.StyleColorsDark();
        var style = ImGui.GetStyle();
        style.FramePadding = new(
            ImGui.GetStyle().FramePadding.X,
            4.0f * SDL.GetWindowDisplayScale(window)
        );
        style.TabRounding = 0.0f;
        style.WindowMenuButtonPosition = ImGuiDir.None;
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

        ConfigureIniPath(io);
        ApplyDpiScale(window, updateBackend: false);

        ProjectExplorerPanel.FileSelected += OpenFile;
        EditorPanel.ActiveFileChanged += path => selectedFilePath = path;

        ImGuiImplSDL3.SetCurrentContext(ImGui.GetCurrentContext());

        var imguiWindow = Unsafe.BitCast<nint, SDLWindowPtr>(window);
        var imguiRenderer = Unsafe.BitCast<nint, SDLRendererPtr>(renderer);

        if (!ImGuiImplSDL3.InitForSDLRenderer(imguiWindow, imguiRenderer))
        {
            SDL.LogError(SDL.LogCategory.Application, "Error initializing the ImGui SDL3 backend.");
            ImGui.DestroyContext();
            SDL.DestroyRenderer(renderer);
            SDL.DestroyWindow(window);
            SDL.Quit();
            return;
        }

        if (!ImGuiImplSDL3.SDLRenderer3Init(imguiRenderer))
        {
            SDL.LogError(
                SDL.LogCategory.Application,
                "Error initializing the ImGui SDL renderer backend."
            );
            ImGuiImplSDL3.Shutdown();
            ImGui.DestroyContext();
            SDL.DestroyRenderer(renderer);
            SDL.DestroyWindow(window);
            SDL.Quit();
            return;
        }

        var loop = true;

        while (loop)
        {
            while (SDL.PollEvent(out var e))
            {
                var imguiEvent = Unsafe.BitCast<nint, SDLEventPtr>((nint)Unsafe.AsPointer(ref e));
                ImGuiImplSDL3.ProcessEvent(imguiEvent);

                var eventType = (SDL.EventType)e.Type;
                if (eventType == SDL.EventType.Quit)
                    loop = false;

                if (eventType == SDL.EventType.WindowDisplayScaleChanged)
                    ApplyDpiScale(window, updateBackend: true);

                if (eventType == SDL.EventType.MouseMotion)
                {
                    SDL.GetWindowSize(window, out int evtWinW, out int evtWinH);
                    SDL.GetWindowSizeInPixels(window, out int evtPxW, out int evtPxH);

                    if (evtWinW > 0 && evtWinH > 0)
                    {
                        float evtScaleX = (float)evtPxW / evtWinW;
                        float evtScaleY = (float)evtPxH / evtWinH;
                        io.AddMousePosEvent(e.Motion.X * evtScaleX, e.Motion.Y * evtScaleY);
                    }
                }
            }

            ImGuiImplSDL3.NewFrame();
            ImGuiImplSDL3.SDLRenderer3NewFrame();

            SDL.GetWindowSizeInPixels(window, out int pxW, out int pxH);
            if (pxW > 0 && pxH > 0)
            {
                io.DisplaySize = new System.Numerics.Vector2(pxW, pxH);
                io.DisplayFramebufferScale = System.Numerics.Vector2.One;
            }

            ImGui.NewFrame();

            ImGui.DockSpaceOverViewport(ImGuiDockNodeFlags.PassthruCentralNode);

            bool requestOpenSolutionDialog = false;

            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Open Solution/Project..."))
                    {
                        requestOpenSolutionDialog = true;
                        openError = null;
                    }

                    if (ImGui.MenuItem("Save", "Ctrl+S"))
                    {
                        openBuffers.FirstOrDefault(b => b.Path == activeEditorPath)?.Save();
                    }

                    ImGui.EndMenu();
                }
                ImGui.EndMainMenuBar();
            }

            if (requestOpenSolutionDialog)
                ImGui.OpenPopup("OpenPathPopup");

            DrawOpenPathPopup();

            ProjectExplorerPanel.Draw(explorerRoots, selectedFilePath);
            EditorPanel.Draw(openBuffers, ref activeEditorPath);
            DrawDetailsPanel();

            ImGui.Render();

            SDL.RenderClear(renderer);
            ImGuiImplSDL3.SDLRenderer3RenderDrawData(ImGui.GetDrawData(), imguiRenderer);
            SDL.RenderPresent(renderer);
        }

        ImGuiImplSDL3.SDLRenderer3Shutdown();
        ImGuiImplSDL3.Shutdown();
        ImGui.DestroyContext();
        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);

        SDL.Quit();
    }

    private static void OpenFile(string path)
    {
        if (openBuffers.All(b => b.Path != path))
        {
            openBuffers.Add(EditorBuffer.Load(path));
        }

        EditorPanel.RequestFocus(path);
    }

    private static void DrawOpenPathPopup()
    {
        if (ImGui.BeginPopupModal("OpenPathPopup", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("Path to a .sln, .slnx, .csproj, or a folder containing one:");
            ImGui.SetNextItemWidth(420);

            ImGui.InputText("##openPath", ref openPathBuffer, 512);

            if (openError != null)
                ImGui.TextColored(new System.Numerics.Vector4(0.95f, 0.35f, 0.35f, 1f), openError);

            if (ImGui.Button("Open"))
            {
                DotnetSolution? solution = SolutionLoader.Load(openPathBuffer);
                if (solution == null)
                    openError =
                        $"Couldn't find a .sln/.slnx/.csproj at or under '{openPathBuffer}'.";
                else
                {
                    currentSolution = solution;
                    explorerRoots = ExplorerNodeBuilder.Build(solution);
                    openError = null;
                    ImGui.CloseCurrentPopup();
                }
            }

            ImGui.SameLine();
            if (ImGui.Button("Cancel"))
                ImGui.CloseCurrentPopup();

            ImGui.EndPopup();
        }
    }

    private static void DrawDetailsPanel()
    {
        ImGui.Begin("Details");

        if (currentSolution != null)
        {
            ImGui.TextUnformatted($"Solution: {currentSolution.Name}");
            ImGui.TextUnformatted($"Projects: {CountProjects(currentSolution)}");
            ImGui.Separator();
        }

        if (selectedFilePath != null)
            ImGui.TextUnformatted($"Selected: {selectedFilePath}");
        else
            ImGui.TextDisabled("No file selected.");

        ImGui.End();
    }

    private static int CountProjects(DotnetSolution solution)
    {
        int count = solution.Projects.Count;
        foreach (DotnetFolder folder in solution.Folders)
            count += CountProjectsInFolder(folder);
        return count;
    }

    private static int CountProjectsInFolder(DotnetFolder folder)
    {
        int count = folder.Projects.Count;
        foreach (DotnetFolder child in folder.Children)
            count += CountProjectsInFolder(child);
        return count;
    }

    private static unsafe void ApplyDpiScale(nint window, bool updateBackend = false)
    {
        var displayScale = SDL.GetWindowDisplayScale(window);
        if (displayScale <= 0.0f)
            displayScale = 1.0f;

        var io = ImGui.GetIO();

        io.Fonts.Clear();

        var fontConfig = ImGui.ImFontConfig();
        fontConfig.PixelSnapH = true;
        fontConfig.OversampleH = 2;
        fontConfig.OversampleV = 1;

        uint[] excludeRanges = [IconFonts.Lucide.IconMin, IconFonts.Lucide.IconMax, 0];
        if (lucideExcludeRangesPtr != 0)
            Marshal.FreeHGlobal(lucideExcludeRangesPtr);

        lucideExcludeRangesPtr = Marshal.AllocHGlobal(excludeRanges.Length * sizeof(uint));
        for (int i = 0; i < excludeRanges.Length; i++)
            Marshal.WriteInt32(
                lucideExcludeRangesPtr,
                i * sizeof(uint),
                unchecked((int)excludeRanges[i])
            );

        fontConfig.GlyphExcludeRanges = (uint*)lucideExcludeRangesPtr;

        var targetFontSize = MathF.Round(BaseFontSize * displayScale);

        var fontPath = GetSystemFontPath();
        ImFontPtr baseFont;
        if (fontPath != null)
            baseFont = io.Fonts.AddFontFromFileTTF(fontPath, targetFontSize, fontConfig);
        else
            baseFont = io.Fonts.AddFontDefault(fontConfig);

        LoadLucideIconFont(io, targetFontSize, baseFont);

        if (updateBackend)
        {
            ImGuiImplSDL3.SDLRenderer3DestroyDeviceObjects();
            ImGuiImplSDL3.SDLRenderer3CreateDeviceObjects();
        }

        var style = ImGui.GetStyle();
        var scaleDelta = displayScale / appliedUiScale;
        style.ScaleAllSizes(scaleDelta);
        style.FontSizeBase = targetFontSize;
        appliedUiScale = displayScale;
    }

    private static string? GetSystemFontPath()
    {
        string[] candidates =
        [
            "/usr/share/fonts/Adwaita/AdwaitaSans-Regular.ttf",
            "/usr/share/fonts/TTF/OpenSans-Regular.ttf",
            "/usr/share/fonts/TTF/Vera.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/noto/NotoSans-Regular.ttf",
            "/usr/share/fonts/ubuntu/Ubuntu-R.ttf",
        ];

        foreach (var path in candidates)
            if (File.Exists(path))
                return path;

        return null;
    }

    private static unsafe void ConfigureIniPath(ImGuiIOPtr io)
    {
        string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg
            ? xdg
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "share"
            );

        string appDataDir = Path.Combine(dataHome, "hyprcode");
        Directory.CreateDirectory(appDataDir);
        string iniPath = Path.Combine(appDataDir, "imgui.ini");

        byte[] utf8 = Encoding.UTF8.GetBytes(iniPath + "\0");
        iniFilenamePtr = Marshal.AllocHGlobal(utf8.Length);
        Marshal.Copy(utf8, 0, iniFilenamePtr, utf8.Length);

        io.IniFilename = (byte*)iniFilenamePtr;
    }

    private static unsafe void LoadLucideIconFont(
        ImGuiIOPtr io,
        float targetFontSize,
        ImFontPtr baseFont
    )
    {
        byte[]? fontBytes = ReadEmbeddedResource("lucide.ttf");
        if (fontBytes == null)
            return;

        if (lucideFontDataPtr != 0)
        {
            Marshal.FreeHGlobal(lucideFontDataPtr);
            lucideFontDataPtr = 0;
        }
        if (lucideGlyphRangesPtr != 0)
        {
            Marshal.FreeHGlobal(lucideGlyphRangesPtr);
            lucideGlyphRangesPtr = 0;
        }

        lucideFontDataPtr = Marshal.AllocHGlobal(fontBytes.Length);
        Marshal.Copy(fontBytes, 0, lucideFontDataPtr, fontBytes.Length);

        uint[] ranges = [IconFonts.Lucide.IconMin, IconFonts.Lucide.IconMax, 0];
        lucideGlyphRangesPtr = Marshal.AllocHGlobal(ranges.Length * sizeof(uint));
        for (int i = 0; i < ranges.Length; i++)
            Marshal.WriteInt32(lucideGlyphRangesPtr, i * sizeof(uint), unchecked((int)ranges[i]));

        var iconFontConfig = ImGui.ImFontConfig();
        iconFontConfig.MergeMode = true;
        iconFontConfig.PixelSnapH = true;
        iconFontConfig.GlyphMinAdvanceX = targetFontSize;
        iconFontConfig.FontDataOwnedByAtlas = false;

        iconFontConfig.DstFont = baseFont;

        io.Fonts.AddFontFromMemoryTTF(
            (void*)lucideFontDataPtr,
            fontBytes.Length,
            targetFontSize,
            iconFontConfig,
            (uint*)lucideGlyphRangesPtr
        );
    }

    private static byte[]? ReadEmbeddedResource(string logicalName)
    {
        using Stream? stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream(logicalName);
        if (stream == null)
            return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
