﻿using Lizard.Config;
using Lizard.Gui;
using Lizard.Gui.Windows;
using Lizard.Gui.Windows.Watch;
using Exception = System.Exception;

namespace Lizard;

static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var cmdLine = CommandLineArgs.Parse(args);
            var project = cmdLine.ProjectPath == null 
                ? new ProjectConfig() 
                : ProjectConfig.Load(cmdLine.ProjectPath);

            var projectManager = new ProjectManager(project);
            var programDataManager = new ProgramDataManager(projectManager);
            var iceManager = new IceSessionManager(projectManager, cmdLine.AutoConnect);

            var memoryCache = new MemoryCache();
            var debugger = new Debugger(iceManager, LogHistory.Instance, memoryCache);

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