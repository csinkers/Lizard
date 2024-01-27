using Lizard.Config;
using Lizard.Gui;
using Lizard.Gui.Windows;
using Lizard.Gui.Windows.Watch;
using Exception = System.Exception;

namespace Lizard;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var cmdLine = CommandLineArgs.Parse(args);
            ProjectConfig project;
            if (cmdLine.ProjectPath == null)
                project = new ProjectConfig();
            else
            {
                project = ProjectConfig.Load(cmdLine.ProjectPath);
                project.Path = cmdLine.ProjectPath;
            }

            var projectManager = new ProjectManager(project);
            var programDataManager = new ProgramDataManager(projectManager);
            var iceManager = new IceSessionManager(projectManager, cmdLine.AutoConnect);
            var hostProvider = new IceDebugTargetProvider(iceManager, programDataManager);

            var memoryCache = new MemoryCache();
            using var debugger = new Debugger(hostProvider, LogHistory.Instance, memoryCache);

            using var uiManager = new UiManager(projectManager);
            var watcher = new WatcherCore(programDataManager, memoryCache, uiManager.TextureStore);
            var ui = new Ui(projectManager, programDataManager, uiManager, debugger, LogHistory.Instance, watcher);

            if (cmdLine.AutoConnect)
            {
                var hostname = project.GetProperty(ConnectWindow.HostProperty)!;
                var port = project.GetProperty(ConnectWindow.PortProperty);
                iceManager.Connect(hostname, port);
            }

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
