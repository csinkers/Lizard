namespace Lizard.Session.Dump;

public class DumpRegisters
{
    // ReSharper disable InconsistentNaming
    public uint Cs { get; set; }
    public uint Ds { get; set; }
    public uint Es { get; set; }
    public uint Fs { get; set; }
    public uint Gs { get; set; }
    public uint Ss { get; set; }
    public uint Eax { get; set; }
    public uint Ebx { get; set; }
    public uint Ecx { get; set; }
    public uint Edx { get; set; }
    public uint Esi { get; set; }
    public uint Edi { get; set; }
    public uint Ebp { get; set; }
    public uint Esp { get; set; }
    public uint Eip { get; set; }
    public uint Flags { get; set; }
    // ReSharper restore InconsistentNaming
}
