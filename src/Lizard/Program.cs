using ImGuiNET;
using Lizard.Gui;
using Exception = System.Exception;

namespace Lizard;

static class Program
{
    public static int Main()
    {
        try
        {
            using var ice = new IceSession();
            using var ui = new Ui();

            var history = new LogHistory();
            var debugger = new Debugger(ice, history);

            var breakpointsWindow = new BreakpointsWindow();
            var callStackWindow = new CallStackWindow();
            var codeWindow = new CodeWindow();
            var commandWindow = new CommandWindow(debugger, history);
            var disassemblyWindow = new DisassemblyWindow();
            var localsWindow = new LocalsWindow();
            var registersWindow = new RegistersWindow(debugger);
            var watchWindow = new WatchWindow();

            ui.AddWindow(breakpointsWindow.Draw);
            ui.AddWindow(callStackWindow.Draw);
            ui.AddWindow(codeWindow.Draw);
            ui.AddWindow(commandWindow.Draw);
            ui.AddWindow(disassemblyWindow.Draw);
            ui.AddWindow(localsWindow.Draw);
            ui.AddWindow(registersWindow.Draw);
            ui.AddWindow(watchWindow.Draw);

            ui.AddMenu(() =>
            {
                if (ImGui.BeginMenu("Windows"))
                {
                    if (ImGui.MenuItem("Breakpoints")) breakpointsWindow.Open();
                    if (ImGui.MenuItem("Call Stack")) callStackWindow.Open();
                    if (ImGui.MenuItem("Code")) codeWindow.Open();
                    if (ImGui.MenuItem("Command")) commandWindow.Open();
                    if (ImGui.MenuItem("Disassembly")) disassemblyWindow.Open();
                    if (ImGui.MenuItem("Locals")) localsWindow.Open();
                    if (ImGui.MenuItem("Registers")) registersWindow.Open();
                    if (ImGui.MenuItem("Watch")) watchWindow.Open();
                    ImGui.EndMenu();
                }

            });
            ui.Run();
/*
            string? line;
            while ((line = Console.ReadLine())?.ToUpperInvariant() != "EXIT")
                if (!string.IsNullOrEmpty(line))
                    CommandParser.RunCommand(line, debugger);
*/
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }
}
