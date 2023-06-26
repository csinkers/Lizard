using System.Reflection;

namespace Lizard.Gui;

class ToolbarIcons
{
    readonly bool _useDarkTheme;
    readonly Assembly _assembly;
    string Prefix => _useDarkTheme ? "Lizard.IconsDark." : "Lizard.IconsLight.";

    public IntPtr Continue { get; }
    public IntPtr Disconnect { get; }
    public IntPtr Pause { get; }
    public IntPtr Start { get; }
    public IntPtr StepInto { get; }
    public IntPtr StepOut { get; }
    public IntPtr StepOver { get; }
    public IntPtr Stop { get; }
    public IntPtr Debug { get; }
    public IntPtr Gear { get; }

    public ToolbarIcons(UiManager uiManager, bool useDarkTheme)
    {
        var gd = uiManager.GraphicsDevice;
        _useDarkTheme = useDarkTheme;
        _assembly = Assembly.GetExecutingAssembly();

        IntPtr Load(string name)
        {
            var resourceName = Prefix + name;
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            var imageSharpTexture = new Veldrid.ImageSharp.ImageSharpTexture(stream);
            var texture = imageSharpTexture.CreateDeviceTexture(gd, gd.ResourceFactory);
            return uiManager.GetOrCreateImGuiBinding(texture);
        }

        Continue = Load("debug-continue.png");
        Disconnect = Load("debug-disconnect.png");
        Pause = Load("debug-pause.png");
        Start = Load("debug-start.png");
        StepInto = Load("debug-step-into.png");
        StepOut = Load("debug-step-out.png");
        StepOver = Load("debug-step-over.png");
        Stop = Load("debug-stop.png");
        Debug = Load("debug.png");
        Gear = Load("gear.png");
    }
}