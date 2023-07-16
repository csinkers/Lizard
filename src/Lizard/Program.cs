using Lizard.Config;
using Lizard.Gui;
using Lizard.Watch;
using Exception = System.Exception;

namespace Lizard;

static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var cmdLine = CommandLineArgs.Parse(args);
            var iceManager = new IceSessionManager();

            var history = new LogHistory();
            var memoryCache = new MemoryCache();
            var debugger = new Debugger(iceManager, history, memoryCache);

            var project = cmdLine.ProjectPath == null 
                ? new ProjectConfig() 
                : ProjectConfig.Load(cmdLine.ProjectPath);

            using var uiManager = new UiManager(project);
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