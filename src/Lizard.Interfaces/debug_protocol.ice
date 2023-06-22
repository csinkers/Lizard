module Lizard
{
	struct Address { short segment; int offset; }
	sequence<byte> ByteSequence;

	enum Register { Flags,
		EAX, EBX, ECX, EDX,
		ESI, EDI,
		EBP, ESP, EIP,
		ES, CS, SS, DS, FS, GS 
	}

	struct Registers {
		bool stopped;
		int flags;

		int eax; int ebx; int ecx; int edx;
		int esi; int edi;
		int ebp; int esp; int eip;

		short es; short cs; short ss;
		short ds; short fs; short gs;
	}

	enum BreakpointType 
	{
		Unknown,
		Normal,
		Read,
		Write,
		Interrupt,
		InterruptWithAH,
		InterruptWithAX
	}

	struct Breakpoint { Address address; BreakpointType type; byte ah; byte al; }
	sequence<Breakpoint> BreakpointSequence;

	struct AssemblyLine { Address address; string line; ByteSequence bytes; }
	sequence<AssemblyLine> AssemblySequence;

    interface DebugClient
    {
		void Stopped(Registers state);
    }

	enum SegmentType
	{
		SysInvalid     = 0x00,
		Sys286TssA     = 0x01,
		SysLdt         = 0x02,
		Sys286TssB     = 0x03,

		Sys286CallGate = 0x04, // Gate
		SysTaskGate    = 0x05, // Gate
		Sys286IntGate  = 0x06, // Gate
		Sys286TrapGate = 0x07, // Gate

		// No 0x8
		Sys386TssA     = 0x09,
		// No 0xa
		Sys386TssB     = 0x0b,

		Sys386CallGate = 0x0c, // Gate
		// No 0xd
		Sys386IntGate  = 0x0e, // Gate
		Sys386TrapGate = 0x0f, // Gate

		// Expand UP/DOWN, Read-Only/Read-Write, Accessed
		DataUpRead      = 0x10,
		DataUpReadAcc   = 0x11,
		DataUpWrite     = 0x12,
		DataUpWriteAcc  = 0x13,
		DataDnRead      = 0x14,
		DataDnReadAcc   = 0x15,
		DataDnWrite     = 0x16,
		DataDnWriteAcc  = 0x17,

		// Readable, Confirming, Accessed
		CodeAcc          = 0x18,
		Code             = 0x19,
		CodeReadAcc      = 0x1a,
		CodeRead         = 0x1b,
		CodeConfAcc      = 0x1c,
		CodeConf         = 0x1d,
		CodeReadConfAcc  = 0x1e,
		CodeReadConf     = 0x1f
	}

	/*
	Segment Selector:
		bits 0..1: requestor's privilege level
		bit 2: Table indicator (0 = GDT, 1 = LDT)
		bits 3..15: index into the specified descriptor table

	Segment Descriptor:
		0..15: limit 0..15
		16..31: base 0..15

		Second 4 bytes:
		0..7: base 16..23

		8..12: Type
		13..14: DPL = privilege level
		15: Segment present

		16..19: Limit 16..19
		20: AVL = Available for use by systems programmers
		21: R
		22: Big (1 = max offset is 2^32-1 rather than 2^16-1, basically same as O)
		23: G = Granularity (0 = limit in bytes, 1 = limit in units of 4 kB)

		24..31: Base 24..31

	Gate Descriptor:
		0..15: offset 0..15
		16..31: selector
		
		0..4: param count
		5..7: reserved

		8..12: type
		13..14: DPL
		15: segment present

		16..31: offset 16..31
	*/

	class Descriptor 
	{
		SegmentType type;
	}

	class SegmentDescriptor extends Descriptor
	{
		int base;
		int limit;
		byte dpl; // ring 0-3
		bool big;
	}

	class GateDescriptor extends Descriptor
	{
		int offset;
		short selector;
		byte dpl; // ring 0-3
		bool big;
	}

	sequence<Descriptor> Descriptors;
	sequence<Address> Addresses;

    interface DebugHost
    {
		void Connect(DebugClient* proxy);
		void Continue();
		Registers Break();
		Registers StepIn();
		Registers StepMultiple(int cycles);
		void RunToAddress(Address address);
		Registers GetState();
		int GetMaxNonEmptyAddress(short seg);
		Addresses SearchMemory(Address start, int length, ByteSequence pattern, int advance);

		AssemblySequence Disassemble(Address address, int length);
		ByteSequence GetMemory(Address address, int length);
		void SetMemory(Address address, ByteSequence bytes);

		BreakpointSequence ListBreakpoints();
		void SetBreakpoint(Breakpoint breakpoint);
		void DelBreakpoint(Address address);

		void SetReg(Register reg, int value);

		Descriptors GetGdt();
		Descriptors GetLdt();
    }
}

