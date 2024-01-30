using Lizard.Config;
using Lizard.Gui;
using Lizard.Gui.Windows;
using Lizard.Gui.Windows.Watch;
using Exception = System.Exception;

namespace Lizard;

internal static class Program
{
    static readonly string DefaultProjectPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LizardDebugger",
            "default.lizard");

    public static int Main(string[] args)
    {
        try
        {
            var cmdLine = CommandLineArgs.Parse(args);

            var projectManager = new ProjectManager(new ProjectConfig());

            var mapping = new MemoryMapping();
            projectManager.ProjectLoading += mapping.LoadProject;
            projectManager.ProjectSaving += mapping.SaveProject;

            var symbols = new SymbolStore();
            projectManager.ProjectLoading += symbols.LoadProject;
            projectManager.ProjectSaving += symbols.SaveProject;

            using var sessionProvider = new DebugSessionProvider();
            using var uiManager = new UiManager(projectManager);
            var context = new CommandContext(sessionProvider, mapping, symbols, projectManager);
            var watcher = new WatcherCore(context, uiManager.TextureStore);
            var ui = new Ui(LogHistory.Instance, projectManager, uiManager, context, watcher);

            if (!string.IsNullOrEmpty(cmdLine.DumpPath))
            {
                sessionProvider.StartDumpSession(cmdLine.DumpPath, context);
            }
            else if (cmdLine.AutoConnect)
            {
                void Connect()
                {
                    var p = projectManager.Project;
                    var hostname = p.GetProperty(ConnectWindow.HostProperty)!;
                    var port = p.GetProperty(ConnectWindow.PortProperty);
                    sessionProvider.StartIceSession(hostname, port);
                    projectManager.ProjectLoaded -= Connect;
                }

                projectManager.ProjectLoaded += Connect;
            }

            if (!string.IsNullOrEmpty(cmdLine.ProjectPath))
                projectManager.Load(cmdLine.ProjectPath);
            else if (File.Exists(DefaultProjectPath))
                projectManager.Load(DefaultProjectPath);

            ui.Run();

            var defaultProjDir = Path.GetDirectoryName(DefaultProjectPath)!;
            if (!Directory.Exists(defaultProjDir))
                Directory.CreateDirectory(defaultProjDir);

            projectManager.Save(DefaultProjectPath);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

}

