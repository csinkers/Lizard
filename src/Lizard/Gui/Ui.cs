using System.Numerics;
using ImGuiNET;
using Lizard.Gui.Windows;
using Lizard.Watch;
using SharpFileDialog;

namespace Lizard.Gui;

class Ui
{
    readonly Debugger _debugger;
    readonly WatcherCore _watcherCore;
    readonly UiManager _uiManager;
    readonly ToolbarIcons _icons;
    bool _done;

    public BreakpointsWindow BreakpointsWindow { get; }
    public CallStackWindow CallStackWindow { get; }
    public CodeWindow CodeWindow { get; }
    public ConnectWindow ConnectWindow { get; }
    public CommandWindow CommandWindow { get; }
    public DisassemblyWindow DisassemblyWindow { get; }
    public LocalsWindow LocalsWindow { get; }
    public RegistersWindow RegistersWindow { get; }
    public WatchWindow WatchWindow { get; }
    // public ErrorsWindow ErrorsWindow { get; }

    public Ui(UiManager uiManager, Debugger debugger, LogHistory logs, WatcherCore watcherCore)
    {
        _uiManager   = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        _debugger    = debugger ?? throw new ArgumentNullException(nameof(debugger));
        _watcherCore = watcherCore ?? throw new ArgumentNullException(nameof(watcherCore));
        _icons = new ToolbarIcons(uiManager, true);

        debugger.ExitRequested += () => _done = true;

        BreakpointsWindow = new BreakpointsWindow();
        CallStackWindow   = new CallStackWindow();
        CodeWindow        = new CodeWindow();
        CommandWindow     = new CommandWindow(debugger, logs);
        ConnectWindow     = new ConnectWindow(debugger.SessionManager);
        DisassemblyWindow = new DisassemblyWindow();
        LocalsWindow      = new LocalsWindow();
        RegistersWindow   = new RegistersWindow(debugger);
        WatchWindow       = new WatchWindow(watcherCore);

        uiManager.AddMenu(DrawFileMenu);
        uiManager.AddMenu(DrawWindowsMenu);
        uiManager.AddMenu(DrawToolbar);
        uiManager.AddWindow(BreakpointsWindow);
        uiManager.AddWindow(CallStackWindow);
        uiManager.AddWindow(CodeWindow);
        uiManager.AddWindow(CommandWindow);
        uiManager.AddWindow(ConnectWindow);
        uiManager.AddWindow(DisassemblyWindow);
        uiManager.AddWindow(LocalsWindow);
        uiManager.AddWindow(RegistersWindow);
        uiManager.AddWindow(WatchWindow);
    }

    public void Run()
    {
        while (!_done)
            if (!_uiManager.RenderFrame())
                _done = true;
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
        _uiManager.SaveProject(path);
        /* try
        {
            _uiManager.SaveProject(path);
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

        if (ImGui.MenuItem("Open Project"))
        { 
            using var openFile = new OpenFileDialog();
            openFile.Open(x =>
            {
                if (x.Success)
                    _uiManager.LoadProject(x.FileName);
            }, "Lizard Project (*.lizard)|*.lizard");
        }

        if (ImGui.MenuItem("Save Project"))
        {
            if (!string.IsNullOrEmpty(_uiManager.Project.Path))
                Save(_uiManager.Project.Path);
            else
                SaveAs();
        }

        if (ImGui.MenuItem("Save Project As"))
            SaveAs();

        if (_debugger.Host == null && ImGui.MenuItem("Connect"))
            ConnectWindow.Open();

        if (_debugger.Host != null && ImGui.MenuItem("Disconnect"))
            _debugger.SessionManager.Disconnect();

        if (ImGui.MenuItem("Load Program Data"))
        {
            using var openFile = new OpenFileDialog();
            openFile.Open(x =>
            {
                if (x.Success)
                    _watcherCore.LoadProgramData(x.FileName);
            }, "Ghidra Program Data XML (*.xml)|*.xml");
        }

        ImGui.EndMenu();
    }

    void DrawWindowsMenu()
    {
        if (!ImGui.BeginMenu("Windows"))
            return;

        if (ImGui.MenuItem("Breakpoints")) BreakpointsWindow.Open();
        if (ImGui.MenuItem("Call Stack")) CallStackWindow.Open();
        if (ImGui.MenuItem("Code")) CodeWindow.Open();
        if (ImGui.MenuItem("Command")) CommandWindow.Open();
        if (ImGui.MenuItem("Disassembly")) DisassemblyWindow.Open();
        if (ImGui.MenuItem("Locals")) LocalsWindow.Open();
        if (ImGui.MenuItem("Registers")) RegistersWindow.Open();
        if (ImGui.MenuItem("Watch")) WatchWindow.Open();
        ImGui.EndMenu();
    }

    void DrawToolbar()
    {
        if (_debugger.Host == null)
        {
            if (Toolbar("connect##", _icons.Debug, "Connect _debugger"))
                ConnectWindow.Open();
        }
        else
        {
            if (Toolbar("disconnect##", _icons.Disconnect, "Disconnect"))
                _debugger.SessionManager.Disconnect();
        }

        if (_debugger.Host != null)
        {
            if (_debugger.IsPaused)
            {
                if (Toolbar("start##",_icons.Start, "Resume (F5)"))
                    _debugger.Host.Continue();

                if (Toolbar("stepin##",_icons.StepInto, "Step In (F11)"))
                    _debugger.Host.StepIn();

                if (Toolbar("stepover##",_icons.StepOver, "Step Over (F10)"))
                    _debugger.Log.Warn("TODO");

                if (Toolbar("stepout##",_icons.StepOut, "Step Out (Shift+F11)"))
                    _debugger.Log.Warn("TODO");
            }
            else
            {
                if (Toolbar("pause##",_icons.Pause, "Pause (Shift+F5)"))
                    _debugger.Host.Break();
            }
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
