using System.Numerics;
using ImGuiNET;
using Lizard.Config;

namespace Lizard.Gui;

public abstract class SingletonWindow : IImGuiWindow
{
    readonly string _name;
    bool _open;

    protected SingletonWindow(string name, bool open = false)
    {
        _name = name;
        _open = open;
    }

    protected bool JustOpened { get; private set; }
    public string Prefix => _name;
    public void Open()
    {
        _open = true;
        JustOpened = true;
    }

    public void Close() => _open = false;
    public void Draw()
    {
        if (!_open)
            return;

        ImGui.Begin(_name, ref _open);
        DrawContents();
        ImGui.End();
        JustOpened = false;
    }

    public void ClearState() { }

    public void Load(WindowId id, WindowConfig config)
    {
        if (!string.Equals(id.Prefix, _name, StringComparison.Ordinal))
            throw new InvalidOperationException($"A window with prefix \"{id.Prefix}\" was passed to {GetType().Name} which expects a prefix of \"{Prefix}\"");

        Load(config);
    }

    public void Save(Dictionary<string, WindowConfig> configs)
    {
        if (!configs.TryGetValue(_name, out var config))
        {
            config = new WindowConfig(_name);
            configs[_name] = config;
        }

        Save(config);
    }

    protected abstract void DrawContents();
    protected virtual void Load(WindowConfig config) => _open = config.Open;
    protected virtual void Save(WindowConfig config) => config.Open = _open;
}