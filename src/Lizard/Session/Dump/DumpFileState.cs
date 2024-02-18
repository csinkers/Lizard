namespace Lizard.Session.Dump;

public class DumpFileState
{
    public DumpFileState(DumpRegisters registers)
    {
        Version = 1;
        Registers = registers;
    }

    public int Version { get; }
    public DumpRegisters Registers { get; }
    public List<string>? Mapping { get; init; }
    public string? DataPath { get; init; }
    public string? CodePath { get; init; }
}
