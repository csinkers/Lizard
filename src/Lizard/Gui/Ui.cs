using System.Numerics;
using ImGuiNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Lizard.Gui;

class Ui : IDisposable
{
    public delegate void WindowFunc();
    readonly List<WindowFunc> _windows = new();
    readonly List<WindowFunc> _menus = new();
    readonly Sdl2Window _window;
    readonly GraphicsDevice _gd;
    readonly ImGuiRenderer _imguiRenderer;

    public void AddWindow(WindowFunc window) { _windows.Add(window); }
    public void RemoveWindow(WindowFunc window) { _windows.Remove(window); }
    public void AddMenu(WindowFunc window) { _menus.Add(window); }
    public void RemoveMenu(WindowFunc window) { _menus.Remove(window); }

    public Ui()
    {
#if RENDERDOC
        RenderDoc.Load(out var renderDoc);
        bool capturePending = false;
#endif

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(100, 100, 800, 1024, WindowState.Normal, "MemWatcher"),
            new GraphicsDeviceOptions(true) { SyncToVerticalBlank = true },
            GraphicsBackend.Direct3D11,
            out _window,
            out _gd);

        _imguiRenderer = new ImGuiRenderer(
            _gd,
            _gd.MainSwapchain.Framebuffer.OutputDescription,
            (int)_gd.MainSwapchain.Framebuffer.Width,
            (int)_gd.MainSwapchain.Framebuffer.Height);

        _window.Resized += () =>
        {
            _gd.ResizeMainWindow((uint)_window.Width, (uint)_window.Height);
            _imguiRenderer.WindowResized(_window.Width, _window.Height);
        };

    }

    public void Run()
    {
        var cl = _gd.ResourceFactory.CreateCommandList();
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
        while (_window.Exists)
        {
#if RENDERDOC
            if (capturePending)
            {
                renderDoc.TriggerCapture();
                capturePending = false;
            }
#endif

            var input = _window.PumpEvents();
            if (!_window.Exists)
                break;

            _imguiRenderer.Update(1f / 60f, input);

            if (!ImGui.BeginMainMenuBar())
                return;

            foreach (var menu in _menus)
                menu();

            ImGui.EndMainMenuBar();

            ImGui.DockSpaceOverViewport();

            foreach (var imGuiWindow in _windows)
                imGuiWindow();

            cl.Begin();
            cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
            cl.ClearColorTarget(0, RgbaFloat.Black);
            _imguiRenderer.Render(_gd, cl);
            cl.End();
            _gd.SubmitCommands(cl);
            _gd.SwapBuffers(_gd.MainSwapchain);
        }
    }

    public void Dispose()
    {
        _imguiRenderer.Dispose();
        _gd.Dispose();
    }
}