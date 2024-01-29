namespace Lizard;

public class CommandLineArgs
{
    public static CommandLineArgs Parse(string[] args) => new(args);
    public string? ProjectPath { get; }
    public string? DumpPath { get; set; }
    public bool AutoConnect { get; }

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

            if (arg is "-D" or "--DUMP")
            {
                if (i + 1 >= args.Length)
                    throw new FormatException("\"--dump\" must be followed by the path of the dump file to load");

                DumpPath = args[++i];
            }

            if (arg is "-C" or "--CONNECT")
                AutoConnect = true;
        }
    }
}