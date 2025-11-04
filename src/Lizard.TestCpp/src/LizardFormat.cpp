#include "LizardFormat.h"
#include <algorithm>
#include <stdexcept>

using namespace LizardComms;

std::string ToUpper(std::string_view sv)
{
	std::string s(sv);
	std::ranges::transform(s, s.begin(), [](unsigned char c) { return std::toupper(c); });
	return s;
}

template<typename T>
T FromHex(std::string_view s)
{
	T value = 0;
	for (size_t i = 0; i < s.size(); i++)
	{
		char c = s[i];
		T digit;
		if (c >= '0' && c <= '9')
			digit = c - '0';
		else if (c >= 'A' && c <= 'F')
			digit = c - 'A' + 10;
		else if (c >= 'a' && c <= 'f')
			digit = c - 'a' + 10;
		else
			throw std::runtime_error("Invalid hex character: " + std::string(1, c));

		if (value > std::numeric_limits<T>::max() >> 4)
			throw std::runtime_error("Hex value overflow: " + std::string(s));

		value = static_cast<T>(value << 4);

		if (value > std::numeric_limits<T>::max() - digit)
			throw std::runtime_error("Hex value overflow: " + std::string(s));

		value |= digit;
	}
	return value;
}

void PrintRegs(CsConsole::IConsoleOutput& out, const LRegisters1& regs)
{
	out.WriteLine(std::format("EAX:{:x} EBX:{:x} ECX:{:x} EDX:{:x}", regs.eax, regs.ebx, regs.ecx, regs.edx));
	out.WriteLine(std::format("ESI:{:x} EDI:{:x} EBP:{:x} ESP:{:x} EIP:{:x}", regs.esi, regs.edi, regs.ebp, regs.esp, regs.eip));
	out.WriteLine(std::format("ES:{:x} CS:{:x} SS:{:x} DS:{:x} FS:{:x} GS:{:x}", regs.es, regs.cs, regs.ss, regs.ds, regs.fs, regs.gs));
	out.WriteLine(std::format("Flags: {:x}", regs.flags));
}

LRegister1 ParseLRegister1(std::string_view s)
{
	auto upper = ToUpper(s);
	if (upper == "FLAGS") return LRegister1::Flags;
	if (upper == "EAX")   return LRegister1::EAX;
	if (upper == "EBX")   return LRegister1::EBX;
	if (upper == "ECX")   return LRegister1::ECX;
	if (upper == "EDX")   return LRegister1::EDX;
	if (upper == "ESI")   return LRegister1::ESI;
	if (upper == "EDI")   return LRegister1::EDI;
	if (upper == "EBP")   return LRegister1::EBP;
	if (upper == "ESP")   return LRegister1::ESP;
	if (upper == "EIP")   return LRegister1::EIP;
	if (upper == "ES")    return LRegister1::ES;
	if (upper == "CS")    return LRegister1::CS;
	if (upper == "SS")    return LRegister1::SS;
	if (upper == "DS")    return LRegister1::DS;
	if (upper == "FS")    return LRegister1::FS;
	if (upper == "GS")    return LRegister1::GS;
	throw std::runtime_error("Unknown register name: " + std::string(s));
}

LBreakpointType1 ParseLBreakpointType1(std::string_view s)
{
	auto upper = ToUpper(s);
	if (upper == "UNKNOWN" || upper == "U")   return LBreakpointType1::Unknown;
	if (upper == "NORMAL" || upper == "N")   return LBreakpointType1::Normal;
	if (upper == "EPHEMERAL" || upper == "E")   return LBreakpointType1::Ephemeral;
	if (upper == "READ" || upper == "R")   return LBreakpointType1::Read;
	if (upper == "WRITE" || upper == "W")   return LBreakpointType1::Write;
	if (upper == "INTERRUPT" || upper == "I")   return LBreakpointType1::Interrupt;
	if (upper == "INTERRUPTWITHAH" || upper == "IAH") return LBreakpointType1::InterruptWithAH;
	if (upper == "INTERRUPTWITHAX" || upper == "IAX") return LBreakpointType1::InterruptWithAX;
	throw std::runtime_error("Unknown breakpoint type: " + std::string(s) + ", expected Normal, Ephemeral, Read, Write, Interrupt etc");
}

LAddress1 ParseLAddress1(std::string_view s)
{
	const size_t colonIndex = s.find(':');
	if (colonIndex == std::string::npos)
		throw std::runtime_error("Invalid address format, expected 'seg:offset'");

	// Accept any width for segment and offset, as long as they are valid hex
	std::string_view seg_str = s.substr(0, colonIndex);
	std::string_view off_str = s.substr(colonIndex + 1);

	if (seg_str.empty() || off_str.empty())
		throw std::runtime_error("Invalid address format, expected 'seg:offset'");

	try
	{
		LAddress1 result;
		result.segment = FromHex<uint16_t>(seg_str);
		result.offset = FromHex<uint32_t>(off_str);
		return result;
	}
	catch (const std::exception&)
	{
		throw std::runtime_error("Invalid hex in address format, expected 'seg:offset'");
	}
}
