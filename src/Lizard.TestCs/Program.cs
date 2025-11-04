using CsConsole;

namespace Lizard.TestCs;

internal static class Program
{
    public static async Task Main()
    {
        var parser = new CommandParser<TopLevelState>();

        parser.Add(new ClearCommand());
        parser.Add(new HelpCommand(parser));
        parser.Add(new LizardClientCommand());
        parser.Add(new LizardServerCommand());

        parser.Add(
            new SyncCommand<TopLevelState>(["quit", "exit", "q"], (_, _, state) => state.Done = true)
            {
                Description = "Exits the program",
            }
        );

        var loop = new ConsoleLoop<TopLevelState>(parser, new TopLevelState());
        var cin = new ConsoleInput();
        var cout = new ConsoleOutput();
        await loop.RunMain(cin, cout, true);
    }

    class TopLevelState : ICommandState
    {
        public bool Done { get; set; }
    }
}
