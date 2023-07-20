using System.Numerics;
using ImGuiNET;
using Lizard.Gui.Windows;
using Lizard.Gui.Windows.Watch;
using SharpFileDialog;
using Veldrid;

namespace Lizard.Gui;

class Ui
{
    readonly Debugger _debugger;
    readonly ProjectManager _projectManager;
    readonly UiManager _uiManager;
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

    public Ui(ProjectManager projectManager, ProgramDataManager programDataManager, UiManager uiManager, Debugger debugger, LogHistory logs, WatcherCore watcherCore)
    {
        _projectManager = projectManager ?? throw new ArgumentNullException(nameof(projectManager));
        _uiManager   = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        _debugger    = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _icons = new ToolbarIcons(uiManager, true);

        debugger.ExitRequested += () => _done = true;

        _breakpointsWindow = new BreakpointsWindow();
        _callStackWindow   = new CallStackWindow();
        _codeWindow        = new CodeWindow();
        _commandWindow     = new CommandWindow(debugger, logs);
        _connectWindow     = new ConnectWindow(debugger.SessionManager);
        _disassemblyWindow = new DisassemblyWindow(debugger);
        _localsWindow      = new LocalsWindow();
        _registersWindow   = new RegistersWindow(debugger);
        _watchWindow       = new WatchWindow(watcherCore);
        _programDataWindow = new ProgramDataWindow(programDataManager);

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

        uiManager.AddHotkey(new KeyBinding(Key.Grave, ModifierKeys.None), () => _commandWindow.Focus(), false);
        uiManager.AddHotkey(new KeyBinding(Key.F5, ModifierKeys.None), () => _debugger.Continue(), true);
        uiManager.AddHotkey(new KeyBinding(Key.Pause, ModifierKeys.Control), () => _debugger.Break(), true);
        uiManager.AddHotkey(new KeyBinding(Key.ScrollLock, ModifierKeys.Control), () => _debugger.Break(), true); // Control+Pause is coming through as scroll lock for some weird reason
        uiManager.AddHotkey(new KeyBinding(Key.F10, ModifierKeys.None), () => _debugger.StepOver(), true);
        uiManager.AddHotkey(new KeyBinding(Key.F11, ModifierKeys.None), () => _debugger.StepIn(), true);
        uiManager.AddHotkey(new KeyBinding(Key.F11, ModifierKeys.Shift), () => _debugger.StepOut(), true);
    }

    public void Run()
    {
        while (!_done)
        {
            if (!_uiManager.RenderFrame())
                _done = true;

            _debugger.FlushDeferredResults();
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

        if (!_debugger.IsConnected && ImGui.MenuItem("Connect"))
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

        if (_debugger.IsConnected && ImGui.MenuItem("Disconnect"))
            _debugger.SessionManager.Disconnect();

        if (ImGui.MenuItem("Load Program Data"))
            _programDataWindow.Open();

        if (ImGui.MenuItem("Exit"))
            _done = true;

        ImGui.EndMenu();
    }

    void DrawWindowsMenu()
    {
        if (!ImGui.BeginMenu("Windows"))
            return;

        if (ImGui.MenuItem("Breakpoints")) _breakpointsWindow.Open();
        if (ImGui.MenuItem("Call Stack")) _callStackWindow.Open();
        if (ImGui.MenuItem("Code")) _codeWindow.Open();
        if (ImGui.MenuItem("Command")) _commandWindow.Open();
        if (ImGui.MenuItem("Disassembly")) _disassemblyWindow.Open();
        if (ImGui.MenuItem("Locals")) _localsWindow.Open();
        if (ImGui.MenuItem("Registers")) _registersWindow.Open();
        if (ImGui.MenuItem("Watch")) _watchWindow.Open();
        ImGui.EndMenu();
    }

    void DrawToolbar()
    {
        if (!_debugger.IsConnected)
        {
            if (Toolbar("connect##", _icons.Debug, "Connect _debugger"))
                _connectWindow.Open();
            return;
        }

        if (Toolbar("disconnect##", _icons.Disconnect, "Disconnect"))
            _debugger.SessionManager.Disconnect();

        if (_debugger.IsPaused)
        {
            if (Toolbar("start##", _icons.Start, "Resume (F5)"))
                _debugger.Continue();

            if (Toolbar("stepin##", _icons.StepInto, "Step In (F11)"))
                _debugger.StepIn();

            if (Toolbar("stepover##", _icons.StepOver, "Step Over (F10)"))
                _debugger.StepOver();

            if (Toolbar("stepout##", _icons.StepOut, "Step Out (Shift+F11)"))
                _debugger.StepOut();
        }
        else
        {
            if (Toolbar("pause##", _icons.Pause, "Pause (Shift+F5)"))
                _debugger.Break();
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
