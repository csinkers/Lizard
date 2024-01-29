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

            var projectManager = new ProjectManager(new ProjectConfig());

            var mapping = new MemoryMapping();
            projectManager.ProjectLoaded += mapping.LoadProject;
            projectManager.ProjectSaving += mapping.SaveProject;

            var symbols = new SymbolStore();
            projectManager.ProjectLoaded += symbols.LoadProject;
            projectManager.ProjectSaving += symbols.SaveProject;

            bool connected = false;
            using var sessionProvider = new DebugSessionProvider();
            projectManager.ProjectLoaded += p =>
            {
                if (cmdLine.AutoConnect && !connected)
                {
                    connected = true;
                    var hostname = p.GetProperty(ConnectWindow.HostProperty)!;
                    var port = p.GetProperty(ConnectWindow.PortProperty);
                    sessionProvider.StartIceSession(hostname, port);
                }
            };

            using var uiManager = new UiManager(projectManager);
            var context = new CommandContext(sessionProvider, mapping, symbols);
            var watcher = new WatcherCore(context, uiManager.TextureStore);
            var ui = new Ui(LogHistory.Instance, projectManager, uiManager, context, watcher);

            if (!string.IsNullOrEmpty(cmdLine.ProjectPath))
                projectManager.Load(cmdLine.ProjectPath);

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

