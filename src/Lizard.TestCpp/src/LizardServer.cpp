#include "CsConsole.h"
#include "LizardFormat.h"
#include "main.h"
#include <algorithm>
#include <future>

using namespace LizardComms;
using namespace CsConsole;

static uint16_t Hex16(ArgumentSource& args, const std::string& name)
{
	const std::string raw = args.Arg(name);
	try
	{
		return static_cast<uint16_t>(std::stoul(raw, nullptr, 16));
	}
	catch (...)
	{
		throw ConsoleCommandException("Could not parse \"" + raw + "\" as a 16-bit hexadecimal number");
	}
}

static uint32_t Hex32(ArgumentSource& args, const std::string& name)
{
	const std::string raw = args.Arg(name);
	try
	{
		return static_cast<uint32_t>(std::stoul(raw, nullptr, 16));
	}
	catch (...)
	{
		throw ConsoleCommandException("Could not parse \"" + raw + "\" as a 32-bit hexadecimal number");
	}
}

class PendingRequests {
	using CallbackEntry = std::pair<uint8_t, std::function<void()> >;
	std::mutex mutex_;
	std::shared_ptr<ILogger> log_;
	std::list<CallbackEntry> callbacks_;

	static int get_id()
	{
		static std::atomic<uint8_t> current_id = 0;
		return current_id++;
	}

public:
	explicit PendingRequests(const std::shared_ptr<ILogger>& log) : log_(log) {}
	std::vector<uint8_t> get_pending_ids()
	{
		std::unique_lock lock(mutex_);
		std::vector<uint8_t> ids;
		for (const auto& key : callbacks_ | std::views::keys) {
			ids.push_back(key);
		}
		return ids;
	}

	void process(uint8_t id)
	{
		std::unique_lock lock(mutex_);

		auto it = std::ranges::find_if(
			callbacks_,
			[id](const CallbackEntry& entry) { return entry.first == id; });

		if (it == callbacks_.end())
		{
			log_->Error(std::format("No pending operation with id {} found", id));
			return;
		}

		while (!callbacks_.empty()) {
			CallbackEntry entry = callbacks_.front();
			callbacks_.pop_front();
			entry.second();
		}
	}

	void add(std::function<void()> response)
	{
		const std::unique_lock lock(mutex_);
		int id = get_id();
		callbacks_.emplace_back(id, std::move(response));
	}
};

class ClientList
{
	std::mutex m_;
	std::vector<std::shared_ptr<IDuplex>> clients_;

public:
	void Add(const std::shared_ptr<IDuplex>& duplex)
	{
		std::unique_lock ul(m_);
		clients_.push_back(duplex);
	}

	void Remove(const std::shared_ptr<IDuplex>& duplex)
	{
		std::unique_lock ul(m_);
		const auto& it =
			std::ranges::remove_if(
				clients_,
				[&duplex](const auto& x) { return x.get() == duplex.get(); }
			).begin();

		if (it != clients_.end()) {
			clients_.erase(it);
		}
	}

	void ForEach(const std::function<void(IDuplex&)>& func)
	{
		std::unique_lock ul(m_);
		for (const auto& it : clients_) {
			func(*it);
		}
	}
};

class ServerBehavior : public ILizard1
{
	std::shared_ptr<ILogger> m_log;
	std::shared_ptr<PendingRequests> m_queue;

	std::unordered_map<std::string, LRegister1> m_regNameMap =
	{
		{"eax", LRegister1::EAX},
		{"ebx", LRegister1::EBX},
		{"ecx", LRegister1::ECX},
		{"edx", LRegister1::EDX},
		{"esi", LRegister1::ESI},
		{"edi", LRegister1::EDI},
		{"ebp", LRegister1::EBP},
		{"esp", LRegister1::ESP},
		{"eip", LRegister1::EIP},
		{"es", LRegister1::ES},
		{"cs", LRegister1::CS},
		{"ss", LRegister1::SS},
		{"ds", LRegister1::DS},
		{"fs", LRegister1::FS},
		{"gs", LRegister1::GS},
		{"flags", LRegister1::Flags}
	};

	void Do(std::function<void()> func)
	{
		ManualResetEvent mre;
		m_queue->add([&mre, &func] {
			func();
			mre.set();
		});

		mre.wait();
	}

	class RegState : ICommandState
	{
		bool m_done = false;

	public:
		LRegisters1 reg;
		[[nodiscard]] bool IsDone() const override { return m_done; }
		void SetDone() { m_done = true; }
	};

	uint32_t InputHex32() const
	{
		while (true)
		{
			std::string line;
			std::getline(std::cin, line);
			try
			{
				return static_cast<uint32_t>(std::stoul(line, nullptr, 16));
			}
			catch (...)
			{
				m_log->Error("Invalid input. Please enter a valid hexadecimal number.");
			}
		}
	}

	LRegisters1 GetRegisterResponse(bool isStopped)
	{
		auto loop = ConsoleLoop(std::make_unique<RegState>());
		loop.GetState().reg.is_stopped = isStopped ? 1 : 0;
		loop.AddCommand(HelpCommand(loop.GetParser()));
		loop.AddCommand(ClearCommand());
		loop.AddCommand(SyncCommandT<RegState>(
			"p", "Print registers",
			[](ArgumentSource&, IConsoleOutput& out, RegState& s)
			{
				out.WriteLine("Registers:");
				out.WriteLine(std::format("EAX: {:08x} EBX: {:08x} ECX: {:08x} EDX: {:08x}",
					s.reg.eax, s.reg.ebx, s.reg.ecx, s.reg.edx));
				out.WriteLine(std::format("ESI: {:08x} EDI: {:08x} EBP: {:08x} ESP: {:08x}",
					s.reg.esi, s.reg.edi, s.reg.ebp, s.reg.esp));

				out.WriteLine(std::format("EIP: {:08x}", s.reg.eip));

				out.WriteLine(std::format("ES: {:04x} CS: {:04x} SS: {:04x} DS: {:04x} FS: {:04x} GS: {:04x}",
					s.reg.es, s.reg.cs, s.reg.ss, s.reg.ds, s.reg.fs, s.reg.gs));

				out.WriteLine(std::format("Flags: {:08x}", s.reg.flags));
			}
		));

		loop.AddCommand(SyncCommandT<RegState>(
			"r", "Set register", "<reg> <value>",
			[this](ArgumentSource& args, IConsoleOutput& out, RegState& s)
			{
				const auto regName = args.Optional();
				if (!regName.has_value())
				{
					PrintRegs(out, s.reg);
					return;
				}

				const auto it = m_regNameMap.find(regName.value());
				if (it == m_regNameMap.end())
					throw ConsoleCommandException("Unknown register name: " + regName.value());

				switch (it->second)
				{
				case LRegister1::EAX: s.reg.eax = Hex32(args, "value"); break;
				case LRegister1::EBX: s.reg.ebx = Hex32(args, "value"); break;
				case LRegister1::ECX: s.reg.ecx = Hex32(args, "value"); break;
				case LRegister1::EDX: s.reg.edx = Hex32(args, "value"); break;
				case LRegister1::ESI: s.reg.esi = Hex32(args, "value"); break;
				case LRegister1::EDI: s.reg.edi = Hex32(args, "value"); break;
				case LRegister1::EBP: s.reg.ebp = Hex32(args, "value"); break;
				case LRegister1::ESP: s.reg.esp = Hex32(args, "value"); break;
				case LRegister1::EIP: s.reg.eip = Hex32(args, "value"); break;
				case LRegister1::ES: s.reg.es = Hex16(args, "value"); break;
				case LRegister1::CS: s.reg.cs = Hex16(args, "value"); break;
				case LRegister1::SS: s.reg.ss = Hex16(args, "value"); break;
				case LRegister1::DS: s.reg.ds = Hex16(args, "value"); break;
				case LRegister1::FS: s.reg.fs = Hex16(args, "value"); break;
				case LRegister1::GS: s.reg.gs = Hex16(args, "value"); break;
				case LRegister1::Flags: s.reg.flags = Hex32(args, "value"); break;
				default: throw ConsoleCommandException("Unexpected register name: " + regName.value());
				}
			}));

		loop.AddCommand(SyncCommandT<RegState>(
			"ok", "Returns the current register set to the caller",
			[](ArgumentSource&, IConsoleOutput&, RegState& s) {  s.SetDone(); }));

		loop.RunMain();

		return loop.GetState().reg;
	}

public:
	explicit ServerBehavior(const std::shared_ptr<ILogger>& log) : m_log(log) {}

	void Continue() override
	{
		m_log->Info("Continue received");
	}

	LRegisters1 Break() override
	{
		m_log->Info("Break received");
		LRegisters1 result;
		Do([&] { result = GetRegisterResponse(true); });
		return result;
	}

	LRegisters1 StepIn() override
	{
		m_log->Info("StepIn received");
		LRegisters1 result;
		Do([&] { result = GetRegisterResponse(true); });
		return result;
	}

	LRegisters1 StepOver() override
	{
		m_log->Info("StepOver received");
		LRegisters1 result;
		Do([&] { result = GetRegisterResponse(true); });
		return result;
	}

	LRegisters1 StepOut() override
	{
		m_log->Info("StepOut received");
		LRegisters1 result;
		Do([&] { result = GetRegisterResponse(true); });
		return result;
	}

	LRegisters1 StepMultiple(uint32_t cycles) override
	{
		m_log->Info(std::format("StepMultiple({}) received", cycles));
		LRegisters1 result;
		Do([&] { result = GetRegisterResponse(true); });
		return result;
	}

	void RunToAddress(LAddress1& address) override
	{
		m_log->Info(std::format("RunToAddress({}) received", address));
	}

	LRegisters1 GetState() override
	{
		m_log->Info("Break received");
		LRegisters1 result;
		Do([&] { result = GetRegisterResponse(true); });
		return result;
	}

	uint32_t GetMaxNonEmptyAddress(uint16_t seg) override
	{
		m_log->Info(std::format("GetMaxNonEmptyAddress(0x{:04x}) received", seg));
		m_log->Info(std::format("Enter the maximum non-empty address for segment 0x{:x}: ", seg));
		uint32_t result;
		Do([&] { result = InputHex32(); });
		return result;
	}

	std::vector<LAddress1> SearchMemory(
		LAddress1& start,
		uint32_t length,
		std::vector<uint8_t>& pattern,
		uint32_t advance) override
	{
		m_log->Info(std::format(
			"SearchMemory(start: {}, length: {}, pattern: {} bytes, advance: {}) received",
			start, length, pattern.size(), advance));

		m_log->Info("Returning empty result set.");
		return {};
	}


	std::vector<LAssemblyLine1> Disassemble(LAddress1& address, uint32_t length) override
	{
		m_log->Info(std::format("Disassemble({}, {}) received", address, length));
		m_log->Info("Returning empty result set.");
		return {};
	}

	std::vector<uint8_t> GetMemory(LAddress1& address, uint32_t length) override
	{
		m_log->Info(std::format("GetMemory({}, {}) received", address, length));
		m_log->Info("Returning empty result set.");
		return {};
	}

	void SetMemory(LAddress1& address, std::vector<uint8_t>& bytes) override
	{
		m_log->Info(std::format("SetMemory({}, {} bytes) received", address, bytes.size()));
	}

	std::vector<LBreakpoint1> ListBreakpoints() override
	{
		m_log->Info("ListBreakpoints() received");
		m_log->Info("Returning empty result set.");
		return {};
	}

	void SetBreakpoint(LBreakpoint1& breakpoint) override
	{
		m_log->Info("SetBreakpoint() received");
	}

	void EnableBreakpoint(uint32_t id, bool is_enabled) override
	{
		m_log->Info(std::format("EnableBreakpoint({}, {}) received", id, is_enabled));
	}

	void DeleteBreakpoint(uint32_t id) override
	{
		m_log->Info(std::format("DeleteBreakpoint({}) received", id));
	}

	void SetRegister(LRegister1 reg, uint32_t value) override
	{
		m_log->Info(std::format("SetRegister({}, {}) received", reg, value));
	}

	std::vector<LDescriptor1> GetGdt() override
	{
		m_log->Info("GetGdt() received");
		m_log->Info("Returning empty result set.");
		return {};
	}

	std::vector<LDescriptor1> GetLdt() override
	{
		m_log->Info("GetLdt() received");
		m_log->Info("Returning empty result set.");
		return {};
	}
};

class LizardServer : ICommandState
{
	std::shared_ptr<ILogger> log_;
	PendingRequests pendingRequests_;
	ClientList clients_;
	ManualResetEvent done_;

public:
	explicit LizardServer(const std::shared_ptr<ILogger>& log)
		: log_(log), pendingRequests_(log)
	{
	}

	bool IsDone() const override { return done_.get(); }

	void Run(uint16_t port)
	{
		log_->Info("Starting server...");
		std::unique_ptr<ISocket> serverSocket(CreateSdlNetServerSocket(port));
		auto future = std::async(
			std::launch::async,
			&LizardServer::AcceptorLoop,
			this,
			std::move(serverSocket));

		ConsoleLoop loop(this);
		loop.SetInvoker([](CommandParser<LizardServer>& parser, const std::vector<std::string>& parts, IConsoleOutput& out, LizardServer& state)
			{
				try
				{
					parser.Handle(parts, out, state);
				}
				catch (const ConsoleCommandException&) { throw; }
				catch (const std::exception& ex)
				{
					throw ConsoleCommandException(std::format("Error executing command: {}", ex.what()));
				}
			});

		loop.AddCommand(ClearCommand());
		loop.AddCommand(HelpCommand(loop.GetParser()));
		loop.AddCommand(SyncCommandT<LizardServer>(
			{ "exit", "quit", "q" },
			"Stops the server",
			[this](ArgumentSource&, IConsoleOutput&, LizardServer&)
			{
				done_.set();
			}));

		loop.AddCommand(SyncCommandT<LizardServer>(
			"clients", "List connected clients",
			[this](ArgumentSource&, IConsoleOutput& out, LizardServer&)
			{
				int count = 0;
				// TODO: Add client address info to ISocket + IDuplex
				clients_.ForEach([&count](IDuplex&) { count++; });
				out.WriteLine(std::format("Connected clients: {}", count));
			}
		));

		loop.AddCommand(SyncCommandT<LizardServer>(
			"pending", "List pending requests",
			[](ArgumentSource&, IConsoleOutput& out, LizardServer& state)
			{
				auto ids = state.pendingRequests_.get_pending_ids();
				out.WriteLine("Pending ids:");
				for (const auto& id : ids) {
					out.WriteLine(std::format("  {}", id));
				}
			}
		));

		loop.AddCommand(SyncCommandT<LizardServer>(
			"handle", "Handle a pending request", "<id>",
			[](ArgumentSource& args, IConsoleOutput& out, LizardServer& state)
			{
				uint8_t id = static_cast<uint8_t>(std::stoul(args.Arg("id")));
				state.pendingRequests_.process(id);
			}
		));

		loop.RunMain();
		done_.set();

		clients_.ForEach([](IDuplex& duplex)
		{
			duplex.Disconnect();
		});
		future.get();
	}

	void AcceptorLoop(std::unique_ptr<ISocket> serverSocket)
	{
		while (!IsDone())
		{
			std::unique_ptr<ISocket> socket(serverSocket->Accept());
			if (!socket)
			{
				std::this_thread::sleep_for(std::chrono::milliseconds(500));
				continue;
			}

			log_->Info(std::format("Accepted a connection from {}", socket->GetPeerAddress()));

			constexpr uint8_t maxServerVersion = 1;
			uint8_t version = ServerHandshake(*socket, maxServerVersion);
			switch (version)
			{
			case 1:
				RunServerLoopV1(std::move(socket), log_); // Server is responsible for closing the client socket
				break;

			default:
				throw CommsError(std::format("Unsupported minimum comms version {}", version));
			}
		}

		log_->Info("Server stopped");
	}

	void RunServerLoopV1(std::unique_ptr<ISocket> socket, const std::shared_ptr<ILogger>& log)
	{
		ServerBehavior behavior(log);
		std::unique_ptr<IDeserializer> deserializer(MakeLizard1Deserializer(behavior));

		std::shared_ptr<IDuplex> duplex(
			CreateDuplex(
				std::move(socket),
				log,
				[&deserializer](std::vector<uint8_t>& buffer)
				{
					deserializer->HandleMessage(buffer);
				}));

		clients_.Add(duplex);
		duplex->WaitForExit();
		clients_.Remove(duplex);
	}
};

int RunServer(uint16_t port, const std::shared_ptr<ILogger>& log)
{
	LizardServer server(log);
	try
	{
		server.Run(port);
		return 0;
	}
	catch (const CommsError& ex)
	{
		log->Error(std::format("Server error: {}", ex.what()));
		return 1;
	}
}

