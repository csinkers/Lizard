#include "main.h"
#include "CsConsole.h"
#include <SDL_net.h>

constexpr int default_port = 7243;
constexpr auto default_ip = "127.0.0.1";

using namespace LizardComms;
using namespace CsConsole;

class SdlNetSocket : public ISocket
{
	TCPsocket m_socket;

public:
	explicit SdlNetSocket(TCPsocket socket) : m_socket(socket) {}
	ISocket* Accept() override
	{
		if (m_socket == nullptr)
			throw CommsError("Socket is closed");

		TCPsocket socket = SDLNet_TCP_Accept(m_socket);
		if (socket == nullptr)
			return nullptr;

		return new SdlNetSocket(socket);
	}

	void Send(std::span<const uint8_t> buffer) override
	{
		if (m_socket == nullptr)
			throw CommsError("Socket is closed");

		int size = static_cast<int>(buffer.size());
		int len = SDLNet_TCP_Send(m_socket, buffer.data(), size);
		if (len < size)
			throw CommsError(std::format("SDLNet_TCP_Send: {}", SDLNet_GetError()));
	}

	void Receive(std::span<uint8_t> buffer) override
	{
		if (m_socket == nullptr)
			throw CommsError("Socket is closed");

		uint32_t size = static_cast<uint32_t>(buffer.size());
		uint32_t totalReceived = 0;
		while (totalReceived < size)
		{
			int remaining = static_cast<int>(size - totalReceived);
			int bytesReceived = SDLNet_TCP_Recv(m_socket, buffer.data(), remaining);
			if (bytesReceived <= 0)
				throw CommsError(std::format("SDLNet_TCP_Recv: {}", SDLNet_GetError()));

			totalReceived += bytesReceived;
		}
	}

	void GetPeerAddress(uint32_t &ip, uint16_t& port) const override
	{
		if (m_socket == nullptr)
		{
			ip = 0;
			port = 0;
			return;
		}
			
		IPaddress* remoteip = SDLNet_TCP_GetPeerAddress(m_socket);
		if (!remoteip)
			throw CommsError(std::format("SDLNet_TCP_GetPeerAddress: {}", SDLNet_GetError()));

		ip = SDL_SwapBE32(remoteip->host);
		port = remoteip->port;
	}

	std::string GetPeerAddress() const override
	{
		uint32_t ip;
		uint16_t port;
		GetPeerAddress(ip, port);
		return std::format("{}.{}.{}.{}:{}", 
			ip >> 24,
			(ip >> 16) & 0xff,
			(ip >> 8) & 0xff,
			ip & 0xff,
			port);
	}

	void Close() override
	{
		if (m_socket)
		{
			SDLNet_TCP_Close(m_socket);
			m_socket = nullptr;
		}
	}

	~SdlNetSocket() override { SdlNetSocket::Close(); }
};

ISocket* CreateSdlNetClientSocket(const std::string& target, uint16_t port)
{
	IPaddress ip;
	if (SDLNet_ResolveHost(&ip, target.c_str(), port) == -1)
		throw CommsError(std::format("SDLNet_ResolveHost: {}", SDLNet_GetError()));

	TCPsocket tcpsock = SDLNet_TCP_Open(&ip);
	if (!tcpsock)
		throw CommsError(std::format("SDLNet_TCP_Open: {}", SDLNet_GetError()));

	return new SdlNetSocket(tcpsock);
}

ISocket* CreateSdlNetServerSocket(uint16_t port)
{
	IPaddress ip;
	if (SDLNet_ResolveHost(&ip, nullptr, port) == -1)
		throw CommsError(std::format("SDLNet_ResolveHost: {}", SDLNet_GetError()));

	TCPsocket serverSocket = SDLNet_TCP_Open(&ip);
	if (!serverSocket)
		throw CommsError(std::format("SDLNet_TCP_Open: {}", SDLNet_GetError()));

	return new SdlNetSocket(serverSocket);
}

static int WithSdl(const std::function<int()>& func)
{
	if (SDLNet_Init() == -1)
	{
		printf("SDLNet_Init: %s\n", SDLNet_GetError());
		return 2;
	}

	int result = func();

	SDLNet_Quit();
	return result;
}

class ConsoleLogger final : public ILogger
{
	LogLevel m_level;

	static void Log(std::string_view level, std::string_view message)
	{
		printf("%.*s %.*s\n",
			(int)level.size(),
			level.data(),
			static_cast<int>(message.size()),
			message.data());
	}

public:
	explicit ConsoleLogger(LogLevel level) : m_level(level) {}
	~ConsoleLogger() override = default;
	void Debug(std::string_view message) override { if (m_level <= LizardComms::Debug) Log("[DEBUG]", message); }
	void Info(std::string_view message)  override { if (m_level <= LizardComms::Info)  Log("[INFO] ", message); }
	void Warn(std::string_view message)  override { if (m_level <= LizardComms::Warn)  Log("[WARN] ", message); }
	void Error(std::string_view message) override { if (m_level <= LizardComms::Error) Log("[ERROR]", message); }
	void Fatal(std::string_view message) override { if (m_level <= LizardComms::Fatal) Log("[FATAL]", message); }
};

class SimpleState final : public ICommandState
{
	bool m_done = false;

public:
	[[nodiscard]] bool IsDone() const override { return m_done; }
	void SetDone() { m_done = true; }
};

class ClientCommand final : public ISyncCommandT<SimpleState>
{
	std::shared_ptr<ILogger> m_log;

public:
	explicit ClientCommand(const std::shared_ptr<ILogger>& log) : m_log(log) {}

	[[nodiscard]] std::vector<std::string> Names() const override { return { "client", "c" }; }
	[[nodiscard]] std::string Description() const override { return "Connect to the given server (defaults to 127.0.0.1:9189)"; }
	[[nodiscard]] std::string Usage() const override { return "[ip] [port]"; }

	void Invoke(ArgumentSource& args, IConsoleOutput& output, SimpleState& state) override
	{
		WithSdl(
			[&] {
				auto ip = args.Optional();
				auto port = args.OptionalInt().value_or(default_port);
				if (port > std::numeric_limits<uint16_t>::max() || port < 1)
					throw ConsoleCommandException("Port must be between 1 and " + std::to_string(std::numeric_limits<uint16_t>::max()));

				return RunClient(
					ip.value_or(default_ip),
					static_cast<uint16_t>(port),
					m_log);
			});
	}
};

class ServerCommand final : public ISyncCommandT<SimpleState>
{
	std::shared_ptr<ILogger> m_log;

public:
	explicit ServerCommand(const std::shared_ptr<ILogger>& log) : m_log(log) {}

	[[nodiscard]] std::vector<std::string> Names() const override { return { "server", "s" }; }
	[[nodiscard]] std::string Description() const override { return "Hosts a server on the given port (defaults to 9189)"; }
	[[nodiscard]] std::string Usage() const override { return "[port]"; }

	void Invoke(ArgumentSource& args, IConsoleOutput& output, SimpleState& state) override
	{
		WithSdl(
			[&] {
				auto port = args.OptionalInt().value_or(default_port);
				if (port > std::numeric_limits<uint16_t>::max() || port < 1)
					throw ConsoleCommandException("Port must be between 1 and " + std::to_string(std::numeric_limits<uint16_t>::max()));

				return RunServer(static_cast<uint16_t>(port), m_log);
			});
	}
};

int main(int argc, char* argv[])
{
	std::shared_ptr<ILogger> log = std::make_shared<ConsoleLogger>(Debug);

	ConsoleLoop loop(std::make_unique<SimpleState>());
	loop.AddCommand(SyncCommandT<SimpleState>(
		{ {"quit"}, {"q"}, {"exit"} },
		"Exits the application",
		[](ArgumentSource&, IConsoleOutput&, SimpleState& s) { s.SetDone(); }));

	loop.AddCommand(HelpCommand(loop.GetParser()));
	loop.AddCommand(ClearCommand());
	loop.AddCommand(ClientCommand(log));
	loop.AddCommand(ServerCommand(log));

	const auto testsCommand = std::make_shared<SyncCommandT<SimpleState>>(
		"test",
		"Runs the test suite",
		[](ArgumentSource&, IConsoleOutput&, SimpleState&) { RunTests(); });

	loop.AddCommand(testsCommand);
	loop.RunMain();
	return 0;
}

