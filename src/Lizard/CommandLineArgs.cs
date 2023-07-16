namespace Lizard;

public class CommandLineArgs
{
    public string? ProjectPath { get; }
    public static CommandLineArgs Parse(string[] args) => new(args);
    CommandLineArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToUpperInvariant();
            if (arg is "-P" or "--PROJECT")
            {
                if (i + 1 >= args.Length)
                    throw new FormatException("\"--project\" must be followed by the path of the project file to load");

                ProjectPath = args[++i];
            }
        }
    }
}