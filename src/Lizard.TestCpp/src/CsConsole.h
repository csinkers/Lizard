#pragma once // CsConsole.h - A simple console command library for C++20 applications
#include <algorithm>
#include <format>
#include <functional>
#include <iostream>
#include <map>
#include <memory>
#include <mutex>
#include <optional>
#include <ranges>
#include <set>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace CsConsole
{
	/* Example usage:
	#include "CsConsole.h"

	class MyState final : public ICommandState
	{
		bool m_done = false;
		// TODO: Add any extra state that the commands will interact with

	public:
		[[nodiscard]] bool IsDone() const override { return m_done; }
		void SetDone() { m_done = true; }
		// TODO: Add public interface methods for any extra state
	};

	class CustomCommand final : public ISyncCommandT<MyState>
	{
	public:
		[[nodiscard]] std::vector<std::string> Names() const override { return { "custom" }; }

		// Note: Only need to override Names and Invoke, the others will default to returning the empty string.
		[[nodiscard]] std::string Description() const override { return "A custom command"; }
		[[nodiscard]] std::string ShortDescription() const override { return "Custom"; }
		[[nodiscard]] std::string Usage() const override { return "<arg>"; }

		void Invoke(ArgumentSource& args, IConsoleOutput& output, MyState& state) override
		{
			output.WriteLine("Custom command executed with argument: " + args.Arg("arg"));
		}
	};

	int main(int argc, char* argv[])
	{
		run_tests();

		// Ad-hoc defined command to handle quitting
		const auto quitCommand = std::make_shared<SyncCommandT<MyState>>(
			"quit",
			"Exits the application",
			[](ArgumentSource&, IConsoleOutput&, MyState& s) { s.SetDone(); });

		quitCommand->AddAlias("q");
		quitCommand->AddAlias("exit");

		ConsoleLoop<MyState> loop;
		loop.AddCommand(quitCommand); // Note: Passing an l-value requires it to be a shared_ptr
		loop.AddCommand(HelpCommand(loop.GetParser())); // Passing r-values is simpler
		loop.AddCommand(ClearCommand());
		loop.AddCommand(std::make_shared<CustomCommand>());

		loop.RunMain();
		return 0;
	}
	*/

	enum class ConsoleColor : uint8_t
	{
		Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow, Gray,
		DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow, White
	};

	enum StringId
	{
		S_CommandAlreadyRegistered,
		S_CouldNotParseAsBool,
		S_CouldNotParseAsUInt32,
		S_CouldNotParseAsWholeNumber,
		S_ExpectedParameter,
		S_HelpDescription,
		S_InvocationUnsupported,
		S_UnknownCommand,
	};

#ifdef CSCONSOLE_NO_DEFAULT_STRINGS
	const std::string_view GetString(StringId id);
#else
#define UNK_STRING(id) ("###"#id"###")
	inline const std::string_view GetString(StringId id)
	{
		switch (id)
		{
		case S_CommandAlreadyRegistered:   return "Could not register alias \"{}\" for command as it is already registered";
		case S_CouldNotParseAsBool:        return "Could not parse \"{}\" as a bool (e.g. true, false, 1, 0)";
		case S_CouldNotParseAsUInt32:      return "Could not parse \"{}\" as a 32-bit unsigned integer";
		case S_CouldNotParseAsWholeNumber: return "Could not parse \"{}\" as a whole number";
		case S_ExpectedParameter:          return "Expected parameter \"{}\"";
		case S_HelpDescription:            return "When given a command line, shows detailed info on the command. When run without an argument, lists all available commands.";
		case S_InvocationUnsupported:      return "Command does not support invocation";
		case S_UnknownCommand:             return "Unknown command \"{}\"";
		default: return UNK_STRING(id);
		}
	}
#endif

	template<typename... Args>
	std::string Format(StringId id, Args&&... args)
	{
		const std::string_view fmt = GetString(id);
		return std::vformat(fmt, std::make_format_args(args...));
	}

	class IConsoleInput
	{
	protected:
		IConsoleInput() = default;

	public:
		IConsoleInput(const IConsoleInput&) = delete;
		IConsoleInput& operator=(const IConsoleInput&) = delete;
		IConsoleInput(IConsoleInput&&) = default;
		IConsoleInput& operator=(IConsoleInput&&) = default;
		virtual ~IConsoleInput() = default;

		virtual std::string ReadLine() = 0;
		std::function<void()> Interrupt;
	};

	class IConsoleOutput
	{
	protected:
		IConsoleOutput() = default;

	public:
		IConsoleOutput(const IConsoleOutput&) = delete;
		IConsoleOutput& operator=(const IConsoleOutput&) = delete;
		IConsoleOutput(IConsoleOutput&&) = default;
		IConsoleOutput& operator=(IConsoleOutput&&) = default;
		virtual ~IConsoleOutput() = default;

		virtual void Clear() = 0;
		virtual void Write(const std::string& message) = 0;
		virtual void WriteLine() = 0;
		virtual void WriteLine(const std::string& message) = 0;

		[[nodiscard]] virtual ConsoleColor GetForeground() const = 0;
		virtual void SetForeground(ConsoleColor color) = 0;
		[[nodiscard]] virtual ConsoleColor GetBackground() const = 0;
		virtual void SetBackground(ConsoleColor color) = 0;

		template<typename TContext>
		void WithForeground(ConsoleColor color, const std::function<void(IConsoleOutput*)>& action)
		{
			const auto old = GetForeground();
			SetForeground(color);
			action(this);
			SetForeground(old);
		}
	};

	class ConsoleCommandException final : public std::runtime_error
	{
	public:
		explicit ConsoleCommandException(const std::string& msg) : std::runtime_error(msg) {}
	};

	class ArgumentSource final
	{
		std::vector<std::string> m_args;
		size_t m_index;

		static int ToInt(const std::string& s)
		{
			try { return std::stoi(s); }
			catch (...) { throw ConsoleCommandException(Format(S_CouldNotParseAsWholeNumber, s)); }
		}

		static uint32_t ToUInt32(const std::string& s)
		{
			try { return static_cast<uint32_t>(std::stoul(s)); }
			catch (...) { throw ConsoleCommandException(Format(S_CouldNotParseAsUInt32, s)); }
		}

		static bool ToBool(const std::string& s)
		{
			if (s == "0") return false;
			if (s == "1") return true;
			if (s == "true") return true;
			if (s == "false") return false;
			throw ConsoleCommandException(Format(S_CouldNotParseAsBool, s));
		}

		template <class TFrom, class TTo>
		static std::optional<TTo> Map(const std::optional<TFrom>& from, const std::function<TTo(const TFrom&)>& converter)
		{
			return from.has_value()
				? std::optional<TTo>(converter(from.value()))
				: std::nullopt;
		}

	public:
		ArgumentSource(std::vector<std::string> args, size_t index)
			: m_args(std::move(args)), m_index(index)
		{
		}

		[[nodiscard]] int Remaining() const { return static_cast<int>(m_args.size() - m_index); }

		std::string Arg(const std::string& name)
		{
			const auto opt = Optional();
			if (!opt)
				throw ConsoleCommandException(Format(S_ExpectedParameter, name));

			return *opt;
		}

		std::optional<std::string> Optional()
		{
			if (m_index >= m_args.size())
				return std::nullopt;

			return m_args[m_index++];
		}

		int Int(const std::string& name)         { const std::string raw = Arg(name); return ToInt(raw); }
		uint32_t UInt32(const std::string& name) { const std::string raw = Arg(name); return ToUInt32(raw); }
		bool Bool(const std::string& name)       { const std::string raw = Arg(name); return ToBool(raw); }
		std::optional<int> OptionalInt()         { return Map<std::string, int>(Optional(), ToInt); }
		std::optional<bool> OptionalBool()       { return Map<std::string, bool>(Optional(), ToBool); }
	};

	class ICommand
	{
	protected:
		ICommand() = default;

	public:
		ICommand(const ICommand&) = delete;
		ICommand& operator=(const ICommand&) = delete;
		ICommand(ICommand&&) = default;
		ICommand& operator=(ICommand&&) = default;
		virtual ~ICommand() = default;

		[[nodiscard]] virtual std::vector<std::string> Names() const = 0;
		[[nodiscard]] virtual std::string Description() const { return ""; }
		[[nodiscard]] virtual std::string ShortDescription() const { return ""; }
		[[nodiscard]] virtual std::string Usage() const { return ""; }
	};

	class ICommandState
	{
	protected:
		ICommandState() = default;

	public:
		ICommandState(const ICommandState&) = delete;
		ICommandState& operator=(const ICommandState&) = delete;
		ICommandState(ICommandState&&) = default;
		ICommandState& operator=(ICommandState&&) = default;
		virtual ~ICommandState() = default;

		[[nodiscard]] virtual bool IsDone() const = 0;
	};

	class ISyncCommand : public ICommand
	{
	public:
		virtual void Invoke(ArgumentSource& args, IConsoleOutput& o) = 0;
	};

	template<typename TState>
	class ISyncCommandT : public ICommand
	{
	public:
		virtual void Invoke(ArgumentSource& args, IConsoleOutput& o, TState& state) = 0;
	};

	// For ad-hoc command definitions
	using SyncCommandMethod = std::function<void(ArgumentSource&, IConsoleOutput&)>;
	class SyncCommand final : public ISyncCommand
	{
		std::vector<std::string> m_names;
		SyncCommandMethod m_func;
		std::string m_description;
		std::string m_shortDescription;
		std::string m_usage;

	public:
		SyncCommand(const std::string_view name, const std::string_view description, SyncCommandMethod func)
			: m_names{ std::string(name) }, m_func(std::move(func)), m_description(description)
		{
		}

		SyncCommand(const std::vector<std::string_view>& names, const std::string_view description, SyncCommandMethod func)
			: m_names(names.begin(), names.end()), m_func(std::move(func)), m_description(description)
		{
		}

		SyncCommand(const std::string_view name, const std::string_view description, const std::string_view usage, SyncCommandMethod func)
			: m_names{ std::string(name) }, m_func(std::move(func)), m_description(description), m_usage(usage)
		{
		}

		SyncCommand(const std::vector<std::string_view>& names, const std::string_view description, const std::string_view usage, SyncCommandMethod func)
			: m_names(names.begin(), names.end()), m_func(std::move(func)), m_description(description), m_usage(usage)
		{
		}

		void Invoke(ArgumentSource& args, IConsoleOutput& o) override { m_func(args, o); }
		[[nodiscard]] std::vector<std::string> Names() const override { return m_names; }
		[[nodiscard]] std::string Description() const override { return m_description; }
		[[nodiscard]] std::string ShortDescription() const override { return m_shortDescription; }
		[[nodiscard]] std::string Usage() const override { return m_usage; }

		void SetDescription(const std::string& d) { m_description = d; }
		void SetShortDescription(const std::string& d) { m_shortDescription = d; }
		void SetUsage(const std::string& u) { m_usage = u; }
	};

	template<typename T>
	using SyncCommandMethodT = std::function<void(ArgumentSource&, IConsoleOutput&, T&)>;

	// For ad-hoc command definitions that interact with the program state
	template<typename T>
	class SyncCommandT final : public ISyncCommandT<T>
	{
		std::vector<std::string> m_names;
		SyncCommandMethodT<T> m_func;
		std::string m_description;
		std::string m_shortDescription;
		std::string m_usage;

	public:
		SyncCommandT(const std::string_view name, const std::string_view description, SyncCommandMethodT<T> func)
			: m_names{ std::string(name) }, m_func(std::move(func)), m_description(description)
		{
		}

		SyncCommandT(const std::vector<std::string_view>& names, const std::string_view description, SyncCommandMethodT<T> func)
			: m_names(names.begin(), names.end()), m_func(std::move(func)), m_description(description)
		{
		}

		SyncCommandT(const std::string_view name, const std::string_view description, const std::string_view usage, SyncCommandMethodT<T> func)
			: m_names{ std::string(name) }, m_func(std::move(func)), m_description(description), m_usage(usage)
		{
		}

		SyncCommandT(const std::vector<std::string_view>& names, const std::string_view description, const std::string_view usage, SyncCommandMethodT<T> func)
			: m_names(names.begin(), names.end()), m_func(std::move(func)), m_description(description), m_usage(usage)
		{
		}

		void Invoke(ArgumentSource& args, IConsoleOutput& o, T& state) override { m_func(args, o, state); }
		[[nodiscard]] std::vector<std::string> Names() const override { return m_names; }
		[[nodiscard]] std::string Description() const override { return m_description; }
		[[nodiscard]] std::string ShortDescription() const override { return m_shortDescription; }
		[[nodiscard]] std::string Usage() const override { return m_usage; }

		void AddAlias(std::string_view alias) { m_names.emplace_back(alias); }
		void SetDescription(const std::string& d) { m_description = d; }
		void SetShortDescription(const std::string& d) { m_shortDescription = d; }
		void SetUsage(const std::string& u) { m_usage = u; }
	};

	class ConsoleInput final : public IConsoleInput
	{
	public:
		ConsoleInput() = default;

		std::string ReadLine() override
		{
			std::string line;
			std::getline(std::cin, line);
			return line;
		}
		// Interrupt event: call Interrupt() when needed
	};

	class ConsoleOutput final : public IConsoleOutput
	{
		ConsoleColor m_foreground = ConsoleColor::White;
		ConsoleColor m_background = ConsoleColor::Black;

		static int ConsoleColorToAnsi(ConsoleColor color, bool isForeground)
		{
			switch (color) {
			case ConsoleColor::Black:        return isForeground ? 30 : 40;
			case ConsoleColor::DarkRed:      return isForeground ? 31 : 41;
			case ConsoleColor::DarkGreen:    return isForeground ? 32 : 42;
			case ConsoleColor::DarkYellow:   return isForeground ? 33 : 43;
			case ConsoleColor::DarkBlue:     return isForeground ? 34 : 44;
			case ConsoleColor::DarkMagenta:  return isForeground ? 35 : 45;
			case ConsoleColor::DarkCyan:     return isForeground ? 36 : 46;
			case ConsoleColor::Gray:         return isForeground ? 37 : 47;
			case ConsoleColor::DarkGray:     return isForeground ? 90 : 100;
			case ConsoleColor::Red:          return isForeground ? 91 : 101;
			case ConsoleColor::Green:        return isForeground ? 92 : 102;
			case ConsoleColor::Yellow:       return isForeground ? 93 : 103;
			case ConsoleColor::Blue:         return isForeground ? 94 : 104;
			case ConsoleColor::Magenta:      return isForeground ? 95 : 105;
			case ConsoleColor::Cyan:         return isForeground ? 96 : 106;
			case ConsoleColor::White:        return isForeground ? 97 : 107;
			}

			return isForeground ? 39 : 49; // Default
		}

		static void SetAnsiColor(ConsoleColor color, bool isForeground)
		{
			int code = ConsoleColorToAnsi(color, isForeground);
			std::cout << "\033[" << code << "m";
		}

	public:
		ConsoleOutput() = default;

		[[nodiscard]] ConsoleColor GetForeground() const override { return m_foreground; }
		void SetForeground(ConsoleColor color) override
		{
			m_foreground = color;
			SetAnsiColor(color, true);
		}

		[[nodiscard]] ConsoleColor GetBackground() const override { return m_background; }
		void SetBackground(ConsoleColor color) override
		{
			m_background = color;
			SetAnsiColor(color, false);
		}

		void Clear() override
		{
			// TODO: Better solution
	#if defined(_WIN32)
			system("cls");
	#else
			system("clear");
	#endif
		}

		void Write(const std::string& message) override { std::cout << message; }
		void WriteLine() override { std::cout << '\n'; }
		void WriteLine(const std::string& message) override { std::cout << message << '\n'; }
	};

	class ICommandParser // This interface is just so we can pass a state-agnostic type to HelpCommand.
	{
	protected:
		ICommandParser() = default;

	public:
		ICommandParser(const ICommandParser&) = delete;
		ICommandParser& operator=(const ICommandParser&) = delete;
		ICommandParser(ICommandParser&&) = default;
		ICommandParser& operator=(ICommandParser&&) = default;
		virtual ~ICommandParser() = default;

		[[nodiscard]] virtual std::vector<std::shared_ptr<ICommand>> Commands() const = 0;
		virtual bool TryGetCommand(const std::string& name, std::shared_ptr<ICommand>& command) const = 0;
	};

	class ClearCommand final : public ISyncCommand
	{
	public:
		[[nodiscard]] std::vector<std::string> Names() const override { return { "clear", "cls" }; }
		[[nodiscard]] std::string Description() const override { return "Clears the screen"; }
		void Invoke(ArgumentSource&, IConsoleOutput& o) override { o.Clear(); }
	};

	class HelpCommand final : public ISyncCommand
	{
		const ICommandParser& m_parser;

	public:
		explicit HelpCommand(const ICommandParser& parser) : m_parser(parser) {}
		explicit HelpCommand(const ICommandParser&& parser) = delete;
		HelpCommand(const HelpCommand&) = delete;
		HelpCommand& operator=(const HelpCommand&) = delete;
		HelpCommand(HelpCommand&&) = default;
		~HelpCommand() override = default;

		[[nodiscard]] std::vector<std::string> Names() const override { return { "help", "?", "h" }; }
		[[nodiscard]] std::string Description() const override { return std::string(GetString(S_HelpDescription)); }
		[[nodiscard]] std::string Usage() const override { return "[command]"; }

		void Invoke(ArgumentSource& args, IConsoleOutput& o) override
		{
			if (args.Remaining() == 0)
				ListCommands(o);
			else
				DescribeCommand(args.Arg("command"), o);
		}

		void DescribeCommand(const std::string& name, IConsoleOutput& o) const
		{
			std::shared_ptr<ICommand> command;
			if (!m_parser.TryGetCommand(name, command) || !command)
				throw ConsoleCommandException(Format(S_UnknownCommand, name));

			o.WriteLine(command->Names()[0] + " " + command->Usage());
			const std::string desc = !command->Description().empty() ? command->Description() : command->ShortDescription();

			if (!desc.empty())
			{
				o.WriteLine();
				o.WriteLine(desc);
			}
		}

		void ListCommands(IConsoleOutput& o) const
		{
			auto commands = m_parser.Commands();
			std::vector<std::pair<std::string, std::shared_ptr<ICommand>>> sorted;
			sorted.reserve(commands.size());
			for (auto& cmd : commands)
				sorted.emplace_back(cmd->Names()[0], cmd);

			std::ranges::sort(sorted);

			size_t maxLen = 0;
			for (const auto& key : sorted | std::views::keys)
				maxLen = std::max(maxLen, key.size());

			std::string line;
			for (const auto& [name, command] : sorted)
			{
				line.clear();
				auto inserter = std::back_inserter(line);

				std::string desc = !command->ShortDescription().empty() ? command->ShortDescription() : command->Description();
				std::format_to(inserter, "{:{}}: {}", name, maxLen, desc);

				auto names = command->Names();
				if (names.size() > 1)
				{
					for (size_t i = 1; i < names.size(); ++i)
					{
						line.append(i == 1 ? " [" : ", ");
						line.append(names[i]);
					}

					line.push_back(']');
				}

				o.WriteLine(line);
			}
		}
	};

	template<typename TState>
	class CommandParser final : public ICommandParser
	{
		mutable std::mutex m_mutex;
		std::map<std::string, std::shared_ptr<ICommand>> m_commands;

	public:
		[[nodiscard]] std::vector<std::shared_ptr<ICommand>> Commands() const override
		{
			std::lock_guard lock(m_mutex);
			std::set<std::shared_ptr<ICommand>> unique;
			for (const auto& val : m_commands | std::views::values)
				unique.insert(val);

			return { unique.begin(), unique.end() };
		}

		bool TryGetCommand(const std::string& name, std::shared_ptr<ICommand>& command) const override
		{
			std::lock_guard lock(m_mutex);
			if (const auto it = m_commands.find(name); it != m_commands.end())
			{
				command = it->second;
				return true;
			}

			command = nullptr;
			return false;
		}

		void Add(const std::shared_ptr<ICommand>& command)
		{
			std::lock_guard lock(m_mutex);
			for (const auto& alias : command->Names())
			{
				if (m_commands.contains(alias))
					throw ConsoleCommandException(Format(S_CommandAlreadyRegistered, alias));
			}

			for (const auto& alias : command->Names())
				m_commands[alias] = command;
		}

		void Handle(const std::vector<std::string>& args, IConsoleOutput& o, TState& state)
		{
			std::shared_ptr<ICommand> command;
			{
				std::lock_guard lock(m_mutex);
				if (args.empty() || !m_commands.contains(args[0]))
					throw ConsoleCommandException(Format(S_UnknownCommand, args.empty() ? "" : args[0]));

				command = m_commands.at(args[0]);
			}

			ArgumentSource argumentSource(args, 1);

			if (const auto sync = std::dynamic_pointer_cast<ISyncCommandT<TState>>(command))
			{
				sync->Invoke(argumentSource, o, state);
				return;
			}

			if (const auto sync = std::dynamic_pointer_cast<ISyncCommand>(command))
			{
				sync->Invoke(argumentSource, o);
				return;
			}

			throw ConsoleCommandException(std::string(GetString(S_InvocationUnsupported)));
		}
	};

	template<typename TState>
	class ConsoleLoop
	{
		CommandParser<TState> m_parser;
		std::unique_ptr<TState> m_state;
		std::function<void(CommandParser<TState>&, const std::vector<std::string>&, IConsoleOutput&, TState&)> m_handler;
		TState* m_rawState; // Non-owning pointer

	public:
		explicit ConsoleLoop(std::unique_ptr<TState> state) : m_state(std::move(state)), m_rawState(nullptr) { }
		explicit ConsoleLoop(TState* state) : m_rawState(state) {}

		void AddCommand(const std::shared_ptr<ICommand>& command) { m_parser.Add(command); }

		template<typename TCommand>
			requires(std::is_base_of_v<ICommand, TCommand>)
		void AddCommand(TCommand&& command)
		{
			m_parser.Add(std::make_shared<TCommand>(std::forward<TCommand>(command)));
		}

		const ICommandParser& GetParser() const { return m_parser; }
		TState& GetState() { return m_rawState ? *m_rawState : *m_state; }
		void SetInvoker(const std::function<void(CommandParser<TState>&, const std::vector<std::string>&, IConsoleOutput&, TState&)>& handler) { m_handler = handler; }

		void RunMain()
		{
			ConsoleInput input;
			ConsoleOutput output;
			RunMain(input, output);
		}

		void RunMain(IConsoleInput& i, IConsoleOutput& o)
		{
			while (!GetState().IsDone())
			{
				std::string line = i.ReadLine();
				if (line.empty())
					continue;

				std::vector<std::string> parts = SplitLine(line);
				try
				{
					if (m_handler != nullptr)
						m_handler(m_parser, parts, o, GetState());
					else
						m_parser.Handle(parts, o, GetState());
				}
				catch (const ConsoleCommandException& cce)
				{
					o.WithForeground<std::string>(
						ConsoleColor::Red,
						[&cce](IConsoleOutput* o2) { o2->WriteLine(cce.what()); });
				}
			}
		}

		static std::vector<std::string> SplitLine(const std::string& line)
		{
			std::vector<std::string> results;
			std::ostringstream sb;
			bool quoted = false, escaped = false;
			for (const char c : line)
			{
				switch (c)
				{
				case '\\':
					if (escaped) sb << '\\';
					else escaped = true;
					break;

				case '"':
					if (escaped) { escaped = false; sb << '"'; }
					else quoted = !quoted;
					break;

				case ' ':
					if (!quoted)
					{
						if (sb.tellp() > 0)
						{
							results.push_back(sb.str());
							sb.str(""); sb.clear();
						}
					}
					else sb << c;
					break;

				case 'n':
					sb << (escaped ? '\n' : c); escaped = false;
					break;

				case 'r':
					sb << (escaped ? '\r' : c); escaped = false;
					break;

				case 't':
					sb << (escaped ? '\t' : c); escaped = false;
					break;

				default:
					sb << c; escaped = false;
					break;
				}
			}

			if (sb.tellp() > 0)
				results.push_back(sb.str());

			return results;
		}
	};
}
