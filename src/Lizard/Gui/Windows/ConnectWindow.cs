using System.Numerics;
using ImGuiNET;
using Lizard.Config.Properties;

namespace Lizard.Gui.Windows;

internal class ConnectWindow : SingletonWindow
{
    IntProperty _portProperty = new("Port", 7243);
    readonly IceSessionManager _sessionManager;
    readonly ImText _hostname = new(256, "localhost");
    string _error = "";
    int _port;

    public ConnectWindow(IceSessionManager sessionManager) : base("Connect", false)
    {
        _sessionManager = sessionManager;
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
                _sessionManager.Connect(_hostname.Text, _port);
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