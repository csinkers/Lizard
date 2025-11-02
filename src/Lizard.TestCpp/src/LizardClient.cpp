#include "CsConsole.h"
#include "LizardComms.g.h"
#include "LizardFormat.h"
#include "main.h"
#include <cctype>
#include <format>

using namespace LizardComms;
using namespace CsConsole;

static int ParseHexDigit(char c)
{
	if (std::isdigit(static_cast<unsigned char>(c)))
		return c - '0';

	if (c >= 'a' && c <= 'f')
		return std::tolower(static_cast<unsigned char>(c)) - 'a' + 10;

	if (c >= 'A' && c <= 'F')
		return std::tolower(static_cast<unsigned char>(c)) - 'A' + 10;

	throw std::runtime_error("Invalid hex digit: " + std::string(1, c));
}

// Converts a hex string (with or without spaces) to a vector of bytes
static std::vector<uint8_t> ParseHexBytes(const std::string& hex)
{
	std::vector<uint8_t> bytes;
	size_t i = 0;
	while (i < hex.size())
	{
		// Skip spaces
		while (i < hex.size() && std::isspace(static_cast<unsigned char>(hex[i])))
			++i;

		if (i >= hex.size())
			break;

		int hi = ParseHexDigit(hex[i++]);
		if (i >= hex.size())
			throw std::runtime_error("Expected second hex character in byte");

		int lo = ParseHexDigit(hex[i++]);
		bytes.push_back(static_cast<uint8_t>((hi << 4) | lo));
	}

	return bytes;
}

static void HexDump(IConsoleOutput& out, const std::vector<uint8_t>& data, size_t bytesPerLine = 16)
{
	size_t len = data.size();
	std::string line;
	line.reserve(8 + 2 + 3 * bytesPerLine + 1 + bytesPerLine); // Preallocate for typical line

	for (size_t i = 0; i < len; i += bytesPerLine)
	{
		line.clear();

		std::format_to(std::back_inserter(line), "{:08x}  ", i);

		for (size_t j = 0; j < bytesPerLine; ++j)
		{
			if (i + j < len)
				std::format_to(std::back_inserter(line), "{:02x} ", data[i + j]);
			else
				line.append("   ");
		}

		line.push_back(' ');

		for (size_t j = 0; j < bytesPerLine; ++j)
		{
			if (i + j < len)
			{
				uint8_t c = data[i + j];
				line.push_back(std::isprint(c) ? static_cast<char>(c) : '.');
			}
			else
			{
				line.push_back(' ');
			}
		}

		out.WriteLine(line);
	}
}

class ClientState final : ICommandState, ILizardClient1
{
	bool m_done = false;

	std::shared_ptr<ILogger> m_log;
	std::unique_ptr<IDuplex> m_duplex;
	std::unique_ptr<ILizard1> m_serializer;
	std::unique_ptr<IDeserializer> m_deserializer;

public:
	explicit ClientState(std::unique_ptr<ISocket> socket, const std::shared_ptr<ILogger>& log)
		: m_log(log),
		m_duplex(CreateDuplex(
			std::move(socket),
			log,
			[this](std::vector<uint8_t>& buffer) { m_deserializer->HandleMessage(buffer); })
		),
		m_serializer(MakeLizard1Serializer([this](std::vector<uint8_t>& buffer) { m_duplex->SendAndReplaceBufferWithResponse(buffer); })),
		m_deserializer(MakeLizardClient1Deserializer(*this))
	{
	}

	~ClientState() override = default;

	[[nodiscard]] bool IsDone() const override { return m_done; }
	void SetDone()
	{
		m_duplex->Disconnect();
		m_done = true;
	}

	ILizard1& GetSerializer() { return *m_serializer; }
	void Stopped(LRegisters1& state) override { m_log->Info("Received stopped notification from server."); }
};

int RunClient(const std::string& target, uint16_t port, const std::shared_ptr<ILogger>& log)
{
	log->Info("Starting client...");

	constexpr uint8_t maxClientVersion = 1;
	std::unique_ptr<ISocket> socket(CreateSdlNetClientSocket(target, port));
	uint8_t version = ClientHandshake(*socket, maxClientVersion);
	log->Debug(std::format("Connected to server with protocol version {}", version));

	auto loop = ConsoleLoop(std::make_unique<ClientState>(std::move(socket), log));
	loop.AddCommand(HelpCommand(loop.GetParser()));
	loop.AddCommand(ClearCommand());

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"quit"}, {"q"}, {"exit"} },
		"Exits the client",
		[](ArgumentSource&, IConsoleOutput&, ClientState& s) { s.SetDone(); }));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"continue"}, {"g"} },
		"Continue execution",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			s.GetSerializer().Continue();
			out.WriteLine("OK");
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"break"}, {"b"} },
		"Pauses execution",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s) { PrintRegs(out, s.GetSerializer().Break()); }));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"step_in"}, {"p"} },
		"Single-steps execution",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s) { PrintRegs(out, s.GetSerializer().StepIn()); }));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"step_over"}, {"n"} },
		"Single-steps execution without following calls",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s) { PrintRegs(out, s.GetSerializer().StepOver()); }));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"step_multiple"}, {"gn"} },
		"Single-steps the specified number of times",
		"<n>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			int n = args.Int("n");
			PrintRegs(out, s.GetSerializer().StepMultiple(n));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"run_to_address"}, {"run"} },
		"Runs until the specified address",
		"<address>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			LAddress1 address = ParseLAddress1(args.Arg("address"));
			s.GetSerializer().RunToAddress(address);
			out.WriteLine("OK");
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"get_state"}, {"state"} },
		"Gets the current state of the debugger",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s)
		{
			PrintRegs(out, s.GetSerializer().GetState());
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"get_max_non_empty_address"}, {"max_addr"} },
		"Gets the maximum non-empty address in the specified segment",
		"<segment>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			uint16_t seg = static_cast<uint16_t>(args.Int("segment"));
			uint32_t maxAddr = s.GetSerializer().GetMaxNonEmptyAddress(seg);
			out.WriteLine(std::format("Max non-empty address in segment {:04x}: {:08x}", seg, maxAddr));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"search_memory"}, {"search"} },
		"Searches memory for a pattern",
		"<start> <length> <pattern> [advance]",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			LAddress1 start = ParseLAddress1(args.Arg("start"));
			uint32_t length = static_cast<uint32_t>(args.Int("length"));
			std::string patternStr = args.Arg("pattern");
			uint32_t advance = args.OptionalInt().value_or(1);

			std::vector<uint8_t> pattern = ParseHexBytes(patternStr);
			if (pattern.empty())
			{
				out.WriteLine("Pattern must not be empty.");
				return;
			}

			auto results = s.GetSerializer().SearchMemory(start, length, pattern, advance);
			if (results.empty())
			{
				out.WriteLine("No matches found.");
			}
			else
			{
				for (const auto& addr : results)
					out.WriteLine(std::format("Found match at {}", addr));
			}
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"disassemble"}, {"d"} },
		"Disassembles the specified address",
		"<address> <length>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			LAddress1 address = ParseLAddress1(args.Arg("address"));
			uint32_t length = args.UInt32("length");
			auto disassembly = s.GetSerializer().Disassemble(address, length);

			for (const auto& line : disassembly)
				out.WriteLine(std::format("{} {}", line.address, line.line));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"get_memory"}, {"gm"} },
		"Gets memory from the specified address",
		"<address> <length>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			LAddress1 address = ParseLAddress1(args.Arg("address"));
			uint32_t length = static_cast<uint32_t>(args.Int("length"));
			auto memory = s.GetSerializer().GetMemory(address, length);
			out.WriteLine(std::format("Memory at {}:", address));
			HexDump(out, memory);
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"set_memory"}, {"sm"} },
		"Sets memory at the specified address",
		"<address> <data>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			LAddress1 address = ParseLAddress1(args.Arg("address"));
			std::string dataString = args.Arg("data");
			std::vector<uint8_t> data = ParseHexBytes(dataString);
			s.GetSerializer().SetMemory(address, data);
			out.WriteLine(std::format("Memory set at {}", address));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"list_breakpoints"}, {"lb"} },
		"Lists all breakpoints",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s)
		{
			auto breakpoints = s.GetSerializer().ListBreakpoints();
			if (breakpoints.empty())
				out.WriteLine("No breakpoints set.");
			else
			{
				for (const auto& bp : breakpoints)
				{
					out.WriteLine(std::format("Breakpoint {}: {} at {} ({}, AH: {}, AL: {})",
						bp.id,
						bp.type,
						bp.address,
						bp.is_enabled ? "enabled" : "disabled",
						bp.ah, bp.al));
				}
			}
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"set_breakpoint"}, {"sb"} },
		"Sets a breakpoint at the specified address",
		"<address> <type> [enabled] [ah] [al]",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			LAddress1 address = ParseLAddress1(args.Arg("address"));
			LBreakpointType1 type = ParseLBreakpointType1(args.Arg("type"));
			uint8_t isEnabled = args.OptionalBool().value_or(false) ? 1 : 0;
			uint8_t ah = static_cast<uint8_t>(args.OptionalInt().value_or(0));
			uint8_t al = static_cast<uint8_t>(args.OptionalInt().value_or(0));
			LBreakpoint1 breakpoint{ 0, address, type, isEnabled, ah, al };
			s.GetSerializer().SetBreakpoint(breakpoint);
			out.WriteLine(std::format("Breakpoint set at {}", address));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"enable_breakpoint"}, {"eb"} },
		"Enables or disables a breakpoint",
		"<id> <enabled>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			uint32_t id = static_cast<uint32_t>(args.Int("id"));
			uint8_t isEnabled = args.Bool("enabled");
			s.GetSerializer().EnableBreakpoint(id, isEnabled);
			out.WriteLine(std::format("Breakpoint {} {}", id, isEnabled ? "enabled" : "disabled"));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"delete_breakpoint"}, {"db"} },
		"Deletes a breakpoint",
		"<id>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			uint32_t id = static_cast<uint32_t>(args.Int("id"));
			s.GetSerializer().DeleteBreakpoint(id);
			out.WriteLine(std::format("Breakpoint {} deleted", id));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"set_register"}, {"sr"} },
		"Sets a register to the specified value",
		"<register> <value>",
		[](ArgumentSource& args, IConsoleOutput& out, ClientState& s)
		{
			std::string regName = args.Arg("register");
			uint32_t value = static_cast<uint32_t>(args.Int("value"));
			LRegister1 reg = ParseLRegister1(regName);
			s.GetSerializer().SetRegister(reg, value);
			out.WriteLine(std::format("Set register {} to {:08x}", regName, value));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"get_gdt"}, {"gdt"} },
		"Gets the Global Descriptor Table",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s)
		{
			auto gdt = s.GetSerializer().GetGdt();
			if (gdt.empty())
			{
				out.WriteLine("GDT is empty.");
				return;
			}

			for (const auto& desc : gdt)
				out.WriteLine(std::format("GDT entry of type {}", (int)desc.type));
		}));

	loop.AddCommand(SyncCommandT<ClientState>(
		{ {"get_ldt"}, {"ldt"} },
		"Gets the Local Descriptor Table",
		[](ArgumentSource&, IConsoleOutput& out, ClientState& s)
		{
			auto ldt = s.GetSerializer().GetLdt();
			if (ldt.empty())
			{
				out.WriteLine("LDT is empty.");
				return;
			}

			for (const auto& desc : ldt)
				out.WriteLine(std::format("LDT entry of type {}", (int)desc.type));
		}));

	loop.RunMain();
	return 0;
}
