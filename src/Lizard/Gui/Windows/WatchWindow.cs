using Lizard.Gui.Windows.Watch;

namespace Lizard.Gui.Windows;

public class WatchWindow : SingletonWindow
{
    readonly WatcherCore _watcherCore;

    public WatchWindow(WatcherCore watcherCore)
        : base("Watch") => _watcherCore = watcherCore ?? throw new ArgumentNullException(nameof(watcherCore));

    protected override void DrawContents()
    {
        _watcherCore.Draw();
    }
}
