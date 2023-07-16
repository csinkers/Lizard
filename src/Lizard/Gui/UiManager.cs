using System.Numerics;
using ImGuiNET;
using Lizard.Config;
using Lizard.Config.Properties;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace Lizard.Gui;

class UiManager : IDisposable
{
    static readonly StringProperty ImGuiLayout = new(nameof(UiManager), "ImGuiLayout");
    static readonly IntProperty Width = new(nameof(UiManager), "Width", 800);
    static readonly IntProperty Height = new(nameof(UiManager), "Height", 1024);
    static readonly IntProperty PositionX = new(nameof(UiManager), "PositionX", 100);
    static readonly IntProperty PositionY = new(nameof(UiManager), "PositionY", 100);

    public delegate void WindowFunc();
    readonly ProjectManager _projectManager;
    readonly Dictionary<string, IImGuiWindow> _windows = new();
    readonly List<WindowFunc> _menus = new();
    readonly Sdl2Window _window;
    readonly GraphicsDevice _gd;
    readonly ImGuiRenderer _imguiRenderer;
    readonly CommandList _cl;
    readonly HotkeyManager _hotkeys = new();
    bool _projectDirty = true;

    public ITextureStore TextureStore { get; }
    public GraphicsDevice GraphicsDevice => _gd;

    public void AddWindow(IImGuiWindow window)
    {
        if (!_windows.TryAdd(window.Prefix, window))
            throw new InvalidOperationException($"Tried to add a window ({window.GetType().Name}) with prefix \"\", but that prefix is already in use by {_windows[window.Prefix].GetType().Name}");
    }

    public void RemoveWindow(IImGuiWindow window) { _windows.Remove(window.Prefix); }
    public void AddMenu(WindowFunc window) { _menus.Add(window); }
    public void RemoveMenu(WindowFunc window) { _menus.Remove(window); }
    public void AddHotkey(KeyBinding binding, Action action, bool isGlobal) => _hotkeys.Add(binding, action, isGlobal);
    public void RemoveHotkey(KeyBinding binding, Action action) => _hotkeys.Remove(binding);
    public IntPtr GetOrCreateImGuiBinding(Texture texture) => _imguiRenderer.GetOrCreateImGuiBinding(_gd.ResourceFactory, texture);

    public UiManager(ProjectManager projectManager)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _projectManager.ProjectLoaded += _ => _projectDirty = true;
        _projectManager.ProjectSaving += SaveProject;

        var project = _projectManager.Project;
        var x = project.GetProperty(PositionX);
        var y = project.GetProperty(PositionY);
        var width = project.GetProperty(Width);
        var height = project.GetProperty(Height);

#if RENDERDOC
        RenderDoc.Load(out var renderDoc);
        bool capturePending = false;
#endif

        VeldridStartup.CreateWindowAndGraphicsDevice(
            new WindowCreateInfo(x, y, width, height, WindowState.Normal, "Lizard"),
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

    void PostLoad(ProjectConfig project)
    {
        foreach (var kvp in _windows)
            kvp.Value.ClearState();

        foreach (var kvp in project.Windows)
        {
            var id = WindowId.TryParse(kvp.Key);
            if (id == null)
                continue;

            if (!_windows.TryGetValue(id.Prefix, out var window))
                continue;

            window.Load(id, kvp.Value);
        }

        var layout = project.GetProperty(ImGuiLayout);
        if (layout != null)
            ImGui.LoadIniSettingsFromMemory(layout);
    }

    void SaveProject(ProjectConfig project)
    {
        project.SetProperty(ImGuiLayout, ImGui.SaveIniSettingsToMemory());
        project.SetProperty(PositionX, _window.X);
        project.SetProperty(PositionY, _window.Y);
        project.SetProperty(Width, _window.Width);
        project.SetProperty(Height, _window.Height);

        foreach (var kvp in _windows)
            kvp.Value.Save(project.Windows);
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

        if (_projectDirty)
        {
            PostLoad(_projectManager.Project);
            _projectDirty = false;
        }

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

        foreach (var kvp in _windows)
            kvp.Value.Draw();

        var io = ImGui.GetIO();
        _hotkeys.HandleInput(input, io.WantCaptureKeyboard);

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