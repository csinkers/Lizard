using Lizard.Config;

namespace Lizard.Gui;

public abstract class MultiWindowInstance
{
    public WindowId Id { get; }
    bool _open = true;
    public MultiWindowInstance(WindowId id) => Id = id;

    public abstract void DrawContents();
    public virtual void Load(WindowConfig config) => _open = config.Open;
    public virtual void Save(WindowConfig config) => config.Open = _open;
}