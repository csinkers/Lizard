#pragma once

#include "LizardComms.g.h"
#include "CsConsole.h"
#include <format>

void PrintRegs(CsConsole::IConsoleOutput& out, const LizardComms::LRegisters1& regs);

LizardComms::LAddress1        ParseLAddress1(std::string_view s);
LizardComms::LRegister1       ParseLRegister1(std::string_view s);
LizardComms::LBreakpointType1 ParseLBreakpointType1(std::string_view s);

template <>
struct std::formatter<LizardComms::LRegister1> : std::formatter<std::string_view>
{
	static constexpr auto parse(const std::format_parse_context& ctx)
	{
		auto it = ctx.begin();
		for (; it != ctx.end(); ++it)
			if (*it == '}')
				break;
		return it;
	}

	template <typename FormatContext>
	auto format(const LizardComms::LRegister1& reg, FormatContext& ctx) const
	{
		const char* name = "Unk";
		switch (reg)
		{
		case LizardComms::LRegister1::Flags: name = "Flags"; break;
		case LizardComms::LRegister1::EAX:   name = "EAX";   break;
		case LizardComms::LRegister1::EBX:   name = "EBX";   break;
		case LizardComms::LRegister1::ECX:   name = "ECX";   break;
		case LizardComms::LRegister1::EDX:   name = "EDX";   break;
		case LizardComms::LRegister1::ESI:   name = "ESI";   break;
		case LizardComms::LRegister1::EDI:   name = "EDI";   break;
		case LizardComms::LRegister1::EBP:   name = "EBP";   break;
		case LizardComms::LRegister1::ESP:   name = "ESP";   break;
		case LizardComms::LRegister1::EIP:   name = "EIP";   break;
		case LizardComms::LRegister1::ES:    name = "ES";    break;
		case LizardComms::LRegister1::CS:    name = "CS";    break;
		case LizardComms::LRegister1::SS:    name = "SS";    break;
		case LizardComms::LRegister1::DS:    name = "DS";    break;
		case LizardComms::LRegister1::FS:    name = "FS";    break;
		case LizardComms::LRegister1::GS:    name = "GS";    break;
		}

		return std::formatter<std::string_view>::format(name, ctx);
	}
};

template <>
struct std::formatter<LizardComms::LBreakpointType1> : std::formatter<std::string_view>
{
	static constexpr auto parse(const std::format_parse_context& ctx)
	{
		auto it = ctx.begin();
		for (; it != ctx.end(); ++it)
			if (*it == '}')
				break;
		return it;
	}

	template <typename FormatContext>
	auto format(const LizardComms::LBreakpointType1& reg, FormatContext& ctx) const
	{
		const char* name = "Unk";
		switch (reg)
		{
		case LizardComms::LBreakpointType1::Unknown:         name = "Unknown";         break;
		case LizardComms::LBreakpointType1::Normal:          name = "Normal";          break;
		case LizardComms::LBreakpointType1::Ephemeral:       name = "Ephemeral";       break;
		case LizardComms::LBreakpointType1::Read:            name = "Read";            break;
		case LizardComms::LBreakpointType1::Write:           name = "Write";           break;
		case LizardComms::LBreakpointType1::Interrupt:       name = "Interrupt";       break;
		case LizardComms::LBreakpointType1::InterruptWithAH: name = "InterruptWithAH"; break;
		case LizardComms::LBreakpointType1::InterruptWithAX: name = "InterruptWithAX"; break;
		}

		return std::formatter<std::string_view>::format(name, ctx);
	}
};

template <>
struct std::formatter<LizardComms::LAddress1>
{
	static constexpr auto parse(const std::format_parse_context& ctx)
	{
		auto it = ctx.begin();
		for (; it != ctx.end(); ++it)
			if (*it == '}')
				break;
		return it;
	}

	template <typename FormatContext>
	auto format(const LizardComms::LAddress1& addr, FormatContext& ctx) const
	{
		return std::format_to(ctx.out(), "{:x}:{:08x}", addr.segment, addr.offset);
	}
};
