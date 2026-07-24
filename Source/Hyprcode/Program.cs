using System.Runtime.CompilerServices;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends.SDL3;
using SDL3;

namespace Hyprcode;

internal static class Program
{
    private const float BaseFontSize = 16.0f;
    private static float appliedUiScale = 1.0f;

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
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;

        ApplyDpiScale(window, renderer, updateBackend: false);

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
                {
                    loop = false;
                }

                if (eventType == SDL.EventType.WindowDisplayScaleChanged)
                {
                    ApplyDpiScale(window, renderer, updateBackend: true);
                }

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

            ImGui.ShowDemoWindow();

            ImGui.Begin("Test");
            ImGui.Text("Yes");
            ImGui.End();

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

    private static unsafe void ApplyDpiScale(nint window, nint renderer, bool updateBackend = false)
    {
        var displayScale = SDL.GetWindowDisplayScale(window);
        if (displayScale <= 0.0f)
        {
            displayScale = 1.0f;
        }

        var io = ImGui.GetIO();

        io.Fonts.Clear();

        var fontConfig = ImGui.ImFontConfig();
        fontConfig.PixelSnapH = true;
        fontConfig.OversampleH = 2;
        fontConfig.OversampleV = 1;

        var targetFontSize = MathF.Round(BaseFontSize * displayScale);

        var fontPath = GetSystemFontPath();
        if (fontPath != null)
        {
            io.Fonts.AddFontFromFileTTF(fontPath, targetFontSize, fontConfig);
        }
        else
        {
            io.Fonts.AddFontDefault(fontConfig);
        }

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
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
