using Lizard.Gui;
using Lizard.Watch;
using Exception = System.Exception;

namespace Lizard;

static class Program
{
    public static int Main()
    {
        try
        {
            var iceManager = new IceSessionManager();

            var history = new LogHistory();
            var memoryCache = new MemoryCache();
            var debugger = new Debugger(iceManager, history, memoryCache);

            using var uiManager = new UiManager();
            var watcher = new WatcherCore(memoryCache, uiManager.TextureStore);
            var ui = new Ui(uiManager, debugger, history, watcher);
            ui.Run();
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}