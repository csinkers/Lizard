namespace Lizard;

public delegate string GetArg();
public delegate void DebugCommand(GetArg getArg, Debugger d);

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