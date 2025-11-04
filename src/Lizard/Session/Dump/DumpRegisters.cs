namespace Lizard.Session.Dump;

public class DumpRegisters
{
    // ReSharper disable InconsistentNaming
    public ushort Cs { get; set; }
    public ushort Ds { get; set; }
    public ushort Es { get; set; }
    public ushort Fs { get; set; }
    public ushort Gs { get; set; }
    public ushort Ss { get; set; }
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
