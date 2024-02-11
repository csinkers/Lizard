using System.Numerics;
using ImGuiNET;
using Lizard.Config;
using Lizard.Gui.Windows;
using Lizard.Gui.Windows.Watch;
using SharpFileDialog;
using Veldrid;

namespace Lizard.Gui;

class Ui
{
    readonly ProjectManager _projectManager;
    readonly UiManager _uiManager;
    readonly CommandContext _context;
    readonly ToolbarIcons _icons;
    readonly BreakpointsWindow _breakpointsWindow;
    readonly CallStackWindow _callStackWindow;
    readonly CodeWindow _codeWindow;
    readonly ConnectWindow _connectWindow;
    readonly CommandWindow _commandWindow;
    readonly DisassemblyWindow _disassemblyWindow;
    readonly LocalsWindow _localsWindow;
    readonly RegistersWindow _registersWindow;
    readonly WatchWindow _watchWindow;
    readonly ProgramDataWindow _programDataWindow;
    // ErrorsWindow _errorsWindow;
    bool _done;

    IDebugSession Session => _context.Session;

    public Ui(
        LogHistory logHistory,
        ProjectManager projectManager,
        UiManager uiManager,
        CommandContext context,
        WatcherCore watcherCore)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _uiManager   = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _icons = new ToolbarIcons(uiManager, true);

        _context.ExitRequested += () => _done = true;

        _breakpointsWindow = new BreakpointsWindow(context);
        _callStackWindow   = new CallStackWindow(context);
        _codeWindow        = new CodeWindow(context);
        _commandWindow     = new CommandWindow(context, logHistory);
        _connectWindow     = new ConnectWindow(context);
        _disassemblyWindow = new DisassemblyWindow(context);
        _localsWindow      = new LocalsWindow();
        _registersWindow   = new RegistersWindow(context);
        _watchWindow       = new WatchWindow(watcherCore);
        _programDataWindow = new ProgramDataWindow(context);

        uiManager.AddMenu(DrawFileMenu);
        uiManager.AddMenu(DrawWindowsMenu);
        uiManager.AddMenu(DrawToolbar);
        uiManager.AddWindow(_breakpointsWindow);
        uiManager.AddWindow(_callStackWindow);
        uiManager.AddWindow(_codeWindow);
        uiManager.AddWindow(_commandWindow);
        uiManager.AddWindow(_connectWindow);
        uiManager.AddWindow(_disassemblyWindow);
        uiManager.AddWindow(_localsWindow);
        uiManager.AddWindow(_registersWindow);
        uiManager.AddWindow(_watchWindow);
        uiManager.AddWindow(_programDataWindow);

        uiManager.AddHotkey(new KeyBinding(Key.S, ModifierKeys.Control), () =>
        {
            if (!string.IsNullOrEmpty(_projectManager.Project.Path))
                Save(_projectManager.Project.Path);
        }, true);

        uiManager.AddHotkey(new KeyBinding(Key.Grave, ModifierKeys.None), () => _commandWindow.Open(), true);
        uiManager.AddHotkey(new KeyBinding(Key.F5, ModifierKeys.None), () => Session.Continue(), true);
        uiManager.AddHotkey(new KeyBinding(Key.Pause, ModifierKeys.Control), () => Session.Break(), true);
        uiManager.AddHotkey(new KeyBinding(Key.ScrollLock, ModifierKeys.Control), () => Session.Break(), true); // Control+Pause is coming through as scroll lock for some weird reason
        uiManager.AddHotkey(new KeyBinding(Key.F10, ModifierKeys.None), () => Session.StepOver(), true);
        uiManager.AddHotkey(new KeyBinding(Key.F11, ModifierKeys.None), () => Session.StepIn(), true);
        uiManager.AddHotkey(new KeyBinding(Key.F11, ModifierKeys.Shift), () => Session.StepOut(), true);
    }

    public void Run()
    {
        while (!_done)
        {
            if (!_uiManager.RenderFrame())
                _done = true;

            Session.Refresh();
            Session.FlushDeferredResults();
        }
    }

    void SaveAs()
    {
        using var saveFile = new SaveFileDialog();
        saveFile.Save(x =>
        {
            if (!x.Success)
                return;

            var path = x.FileName;
            if (string.IsNullOrEmpty(Path.GetExtension(path)))
                path += ".lizard";

            Save(path);
        }, "Lizard Project (*.lizard)|*.lizard");
    }

    void Save(string path)
    {
        _projectManager.Save(path);
        /* try
        {
            _projectManager.SaveProject(path);
        }
        catch (Exception ex)
        {
            ErrorsWindow.Add(ex.ToString());
        }*/
    }

    void DrawFileMenu()
    {
        if (!ImGui.BeginMenu("File")) 
            return;

        if (!Session.IsActive && ImGui.MenuItem("Connect"))
            _connectWindow.Open();

        if (ImGui.MenuItem("Open Project"))
        { 
            using var openFile = new OpenFileDialog();
            openFile.Open(x =>
            {
                if (x.Success)
                    _projectManager.Load(x.FileName);
            }, "Lizard Project (*.lizard)|*.lizard");
        }

        if (ImGui.MenuItem("Save Project"))
        {
            if (!string.IsNullOrEmpty(_projectManager.Project.Path))
                Save(_projectManager.Project.Path);
            else
                SaveAs();
        }

        if (ImGui.MenuItem("Save Project As"))
            SaveAs();

        if (Session.IsActive && ImGui.MenuItem("Disconnect"))
            _context.SessionProvider.Disconnect();

        if (ImGui.MenuItem("Load Program Data"))
            _programDataWindow.Open();

        if (ImGui.MenuItem("Exit"))
            _done = true;

        ImGui.EndMenu();
    }

    void DrawWindowsMenu()
    {
        if (IsAltKeyPressed(ImGuiKey._1)) _commandWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._2)) _watchWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._3)) _localsWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._4)) _registersWindow.Open();
        // if ( IsAltKeyPressed(ImGuiKey._5)) _memoryWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._6)) _callStackWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._7)) _disassemblyWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._8)) _breakpointsWindow.Open();
        if (IsAltKeyPressed(ImGuiKey._9)) _codeWindow.Open();

        if (!ImGui.BeginMenu("Windows"))
            return;

        if (ImGui.MenuItem("Command (Alt+1)"))     _commandWindow.Open();
        if (ImGui.MenuItem("Watch (Alt+2)"))       _watchWindow.Open();
        if (ImGui.MenuItem("Locals (Alt+3)"))      _localsWindow.Open();
        if (ImGui.MenuItem("Registers (Alt+4)"))   _registersWindow.Open();
        // if (ImGui.MenuItem("Memory (Alt+5)")) _memoryWindow.Open();
        if (ImGui.MenuItem("Call Stack (Alt+6)"))  _callStackWindow.Open();
        if (ImGui.MenuItem("Disassembly (Alt+7)")) _disassemblyWindow.Open();
        if (ImGui.MenuItem("Breakpoints (Alt+8)")) _breakpointsWindow.Open();
        if (ImGui.MenuItem("Code (Alt+9)"))        _codeWindow.Open();

        ImGui.EndMenu();
    }

    static bool IsAltKeyPressed(ImGuiKey key)
    {
        var io = ImGui.GetIO();
        return io is { KeyCtrl: false, KeyAlt: true } 
            && ImGui.IsKeyPressed(key);
    }

    void DrawToolbar()
    {
        if (!Session.IsActive)
        {
            if (Toolbar("connect##", _icons.Debug, "Connect"))
                _connectWindow.Open();
            return;
        }

        if (Toolbar("disconnect##", _icons.Disconnect, "Disconnect"))
            _context.SessionProvider.Disconnect();

        if (Session.IsPaused)
        {
            if (Toolbar("start##", _icons.Start, "Resume (F5)"))
                Session.Continue();

            if (Toolbar("stepin##", _icons.StepInto, "Step In (F11)"))
                Session.StepIn();

            if (Toolbar("stepover##", _icons.StepOver, "Step Over (F10)"))
                Session.StepOver();

            if (Toolbar("stepout##", _icons.StepOut, "Step Out (Shift+F11)"))
                Session.StepOut();
        }
        else
        {
            if (Toolbar("pause##", _icons.Pause, "Pause (Shift+F5)"))
                Session.Break();
        }
    }

    static bool Toolbar(string name, IntPtr icon, string tooltip)
    {
        var result = ImGui.ImageButton(name, icon, new Vector2(16, 16), Vector2.Zero, Vector2.One);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return result;
    }
}
