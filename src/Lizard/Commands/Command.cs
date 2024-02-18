using Lizard.Gui;

namespace Lizard.Commands;

public delegate string GetArg();
public delegate void DebugCommand(GetArg getArg, CommandContext c);

public class Command
{
    public Command(string[] names, string description, DebugCommand func)
    {
        Names = names ?? throw new ArgumentNullException(nameof(names));
        Description = description;
        Func = func ?? throw new ArgumentNullException(nameof(func));
    }

    public string[] Names { get; }
    public string Description { get; }
    public DebugCommand Func { get; }
}
