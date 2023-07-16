using ImGuiNET;
using Lizard.Config;

namespace Lizard.Gui;

public abstract class MultiWindowManager<T> : IImGuiWindow where T : MultiWindowInstance
{
    public string Prefix { get; }
    readonly List<T> _windows = new();

    protected MultiWindowManager(string prefix) => Prefix = prefix;
    protected abstract T ConstructChild(WindowId id);

    public void OpenNew()
    {
        // Find unused child id
        int max = 0;
        foreach (var window in _windows)
            if (window.Id.Id > max)
                max = window.Id.Id;

        var id = new WindowId(Prefix, max + 1);
        _windows.Add(ConstructChild(id));
    }

    public void Draw()
    {
        List<T>? closedWindows = null;
        foreach (var window in _windows)
        {
            bool open = true;
            ImGui.Begin(window.Id.ImGuiName, ref open);
            if (!open)
            {
                closedWindows ??= new List<T>();
                closedWindows.Add(window);
                ImGui.End();
                continue;
            }

            window.DrawContents();
            ImGui.End();
        }

        if (closedWindows != null)
            foreach (var window in closedWindows)
                _windows.Remove(window);
    }

    public void ClearState() => _windows.Clear();
    public void Load(WindowId id, WindowConfig config)
    {
        if (!string.Equals(id.Prefix, Prefix, StringComparison.Ordinal))
            throw new InvalidOperationException($"A window with prefix \"{id.Prefix}\" was passed to {GetType().Name} which expects a prefix of \"{Prefix}\"");

        var window = _windows.FirstOrDefault(x => x.Id == id) ?? ConstructChild(id);
        window.Load(config);
    }

    public void Save(Dictionary<string, WindowConfig> configs)
    {
        foreach (var window in _windows)
        {
            var key = window.Id.LogicalName;
            if (!configs.TryGetValue(key, out var config))
            {
                config = new WindowConfig(key);
                configs[key] = config;
            }

            window.Save(config);
        }
    }
}
