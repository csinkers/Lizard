using System.Numerics;
using ImGuiNET;
using Lizard.Config;
using Lizard.Config.Properties;

namespace Lizard.Gui.Windows;

internal class ConnectWindow : SingletonWindow
{
    public static readonly StringProperty HostProperty = new(nameof(ConnectWindow), "Hostname", "localhost");
    public static readonly IntProperty PortProperty = new(nameof(ConnectWindow), "Port", 7243);

    readonly Debugger _debugger;
    readonly ImText _hostname = new(256, "localhost");
    string _error = "";
    int _port;

    public ConnectWindow(Debugger debugger) : base("Connect")
        => _debugger = debugger ?? throw new ArgumentNullException(nameof(debugger));

    protected override void Load(WindowConfig config)
    {
        _hostname.Text = config.GetProperty(HostProperty) ?? "";
        _port = config.GetProperty(PortProperty);
        base.Load(config);
    }

    protected override void Save(WindowConfig config)
    {
        base.Save(config);
        config.SetProperty(HostProperty, _hostname.Text);
        config.SetProperty(PortProperty, _port);
    }

    protected override void DrawContents()
    {
        _hostname.Draw("Hostname");
        ImGui.InputInt("Port", ref _port);
        if (ImGui.Button("Cancel"))
            Close();

        ImGui.SameLine();
        if (ImGui.Button("Connect"))
        {
            try
            {
                _debugger.Connect(_hostname.Text, _port);
                _error = "";
                Close();
            }
            catch (Exception ex)
            {
                _error = $"Could not connect: {ex.GetType().Name} {ex.InnerException?.Message}";
            }
        }

        if (_error.Length > 0)
            ImGui.TextColored(new Vector4(1.0f, 0.4f, 0.4f, 1.0f), _error);
    }
}
