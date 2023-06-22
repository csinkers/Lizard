using ImGuiNET;

namespace Lizard.Gui;

public class SingletonWindow : IImGuiWindow
{
    readonly string _name;
    bool _open = true;

    protected SingletonWindow(string name) { _name = name; }
    public void Open() => _open = true;
    public void Close() => _open = false;

    protected virtual void DrawContents()
    {
    }

    public void Draw()
    {
        if (!_open)
            return;

        ImGui.Begin(_name, ref _open);
        DrawContents();
        ImGui.End();
    }
}