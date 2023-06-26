using System.Numerics;
using ImGuiNET;
using Lizard.Interfaces;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Lizard.Gui;

class UiManager : IDisposable
{
    public delegate void WindowFunc();
    readonly List<IImGuiWindow> _windows = new();
    readonly List<WindowFunc> _menus = new();
    readonly Sdl2Window _window;
    readonly GraphicsDevice _gd;
    readonly ImGuiRenderer _imguiRenderer;
    readonly CommandList _cl;
    public ITextureStore TextureStore { get; }
    public GraphicsDevice GraphicsDevice => _gd;

    public void AddWindow(IImGuiWindow window) { _windows.Add(window); }
    public void RemoveWindow(IImGuiWindow window) { _windows.Remove(window); }
    public void AddMenu(WindowFunc window) { _menus.Add(window); }
    public void RemoveMenu(WindowFunc window) { _menus.Remove(window); }
    public IntPtr GetOrCreateImGuiBinding(Texture texture) => _imguiRenderer.GetOrCreateImGuiBinding(_gd.ResourceFactory, texture);

    public UiManager()
    {
#if RENDERDOC
        RenderDoc.Load(out var renderDoc);
        bool capturePending = false;
#endif

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(100, 100, 800, 1024, WindowState.Normal, "Lizard"),
            new GraphicsDeviceOptions(true) { SyncToVerticalBlank = true },
            GraphicsBackend.Direct3D11,
            out _window,
            out _gd);

        _imguiRenderer = new ImGuiRenderer(
            _gd,
            _gd.MainSwapchain.Framebuffer.OutputDescription,
            (int)_gd.MainSwapchain.Framebuffer.Width,
            (int)_gd.MainSwapchain.Framebuffer.Height);

        TextureStore = new TextureStore(_gd, _imguiRenderer);

        _window.Resized += () =>
        {
            _gd.ResizeMainWindow((uint)_window.Width, (uint)_window.Height);
            _imguiRenderer.WindowResized(_window.Width, _window.Height);
        };

        _cl = _gd.ResourceFactory.CreateCommandList();
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 1));
    }

    public bool RenderFrame()
    {
        if (!_window.Exists)
            return false;

#if RENDERDOC
        if (capturePending)
        {
            renderDoc.TriggerCapture();
            capturePending = false;
        }
#endif

        var input = _window.PumpEvents();
        if (!_window.Exists)
            return false;

        _imguiRenderer.Update(1 / 60.0f, input);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4));

        if (ImGui.BeginMainMenuBar())
        {
            foreach (var menu in _menus)
                menu();

            ImGui.EndMainMenuBar();
        }

        ImGui.PopStyleVar();

        ImGui.DockSpaceOverViewport();

        foreach (var imGuiWindow in _windows)
            imGuiWindow.Draw();


        _cl.Begin();
        _cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
        _cl.ClearColorTarget(0, RgbaFloat.Black);
        _imguiRenderer.Render(_gd, _cl);
        _cl.End();
        _gd.SubmitCommands(_cl);
        _gd.SwapBuffers(_gd.MainSwapchain);
        return true;
    }

    public void Dispose()
    {
        _cl.Dispose();
        _imguiRenderer.Dispose();
        _gd.Dispose();
    }
}