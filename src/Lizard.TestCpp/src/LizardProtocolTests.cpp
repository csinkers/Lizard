#include <functional>
#include <cstdio>
#include <sstream>
#include <ostream>
#include "LizardComms.g.h"

using namespace LizardComms;

namespace std
{
	std::ostream& operator<<(std::ostream& lhs, const LAddress1& rhs)
	{
		return lhs << "{0x" << std::hex << rhs.segment << "; 0x" << std::hex << rhs.offset << "}";
	}

	std::ostream& operator<<(std::ostream& lhs, const LBreakpoint1& rhs)
	{
		return lhs << "{id: " << rhs.id
			<< ", address: " << rhs.address
			<< ", type: " << static_cast<uint32_t>(rhs.type)
			<< ", is_enabled: " << static_cast<uint32_t>(rhs.is_enabled)
			<< ", ah: " << static_cast<uint32_t>(rhs.ah)
			<< ", al: " << static_cast<uint32_t>(rhs.al) << "}";
	}

	std::string to_string(LRegister1 reg)
	{
		switch (reg)
		{
		case LRegister1::Flags: return "Flags";
		case LRegister1::EAX: return "EAX";
		case LRegister1::EBX: return "EBX";
		case LRegister1::ECX: return "ECX";
		case LRegister1::EDX: return "EDX";
		case LRegister1::ESI: return "ESI";
		case LRegister1::EDI: return "EDI";
		case LRegister1::EBP: return "EBP";
		case LRegister1::ESP: return "ESP";
		case LRegister1::EIP: return "EIP";
		case LRegister1::ES: return "ES";
		case LRegister1::CS: return "CS";
		case LRegister1::SS: return "SS";
		case LRegister1::DS: return "DS";
		case LRegister1::FS: return "FS";
		case LRegister1::GS: return "GS";
		default: return "UnkReg";
		}
	}
}

class LizardTestLogger : public ILizard1
{
	std::ostringstream m_log;
	ILizard1& m_inner;

public:
	explicit LizardTestLogger(ILizard1& inner) : m_inner(inner) {}

	std::string GetLog() const { return m_log.str(); }
	void ClearLog() { m_log.str(""); }

	void Continue() override
	{
		m_log << "Continue()";
		m_inner.Continue();
	}

	LRegisters1 Break() override
	{
		m_log << "Break()";
		return m_inner.Break();
	}

	LRegisters1 StepIn() override
	{
		m_log << "StepIn()";
		return m_inner.StepIn();
	}

	LRegisters1 StepOver() override
	{
		m_log << "StepOver()";
		return m_inner.StepOver();
	}

	LRegisters1 StepOut() override
	{
		m_log << "StepOut()";
		return m_inner.StepOut();
	}

	LRegisters1 StepMultiple(uint32_t cycles) override
	{
		m_log << "StepMultiple(" << cycles << ")";
		return m_inner.StepMultiple(cycles);
	}

	void RunToAddress(LAddress1& address) override
	{
		m_log << "RunToAddress(" << address << ")";
		m_inner.RunToAddress(address);
	}

	LRegisters1 GetState() override
	{
		m_log << "GetState()";
		return m_inner.GetState();
	}

	uint32_t GetMaxNonEmptyAddress(uint16_t seg) override
	{
		m_log << "GetMaxNonEmptyAddress(0x" << std::hex << seg << ")";
		return m_inner.GetMaxNonEmptyAddress(seg);
	}

	std::vector<LAddress1> SearchMemory(LAddress1& start, uint32_t length, std::vector<uint8_t>& pattern, uint32_t advance) override
	{
		m_log << "SearchMemory(" << start << ", " << length << ", [";

		for (size_t i = 0; i < pattern.size(); ++i)
		{
			if (i > 0) m_log << ", ";
			m_log << "0x" << std::hex << static_cast<int>(pattern[i]);
		}

		m_log << "], " << advance << ")";
		return m_inner.SearchMemory(start, length, pattern, advance);
	}

	std::vector<LAssemblyLine1> Disassemble(LAddress1& address, uint32_t length) override
	{
		m_log << "Disassemble(" << address << ", " << length << ")";
		return m_inner.Disassemble(address, length);
	}

	std::vector<uint8_t> GetMemory(LAddress1& address, uint32_t length) override
	{
		m_log << "GetMemory(" << address << ", " << length << ")";
		return m_inner.GetMemory(address, length);
	}

	void SetMemory(LAddress1& address, std::vector<uint8_t>& bytes) override
	{
		m_log << "SetMemory(" << address << ", [";
		for (size_t i = 0; i < bytes.size(); ++i)
		{
			if (i > 0) m_log << ", ";
			m_log << "0x" << std::hex << static_cast<int>(bytes[i]);
		}
		m_log << "])";
		m_inner.SetMemory(address, bytes);
	}

	std::vector<LBreakpoint1> ListBreakpoints() override
	{
		m_log << "ListBreakpoints()";
		return m_inner.ListBreakpoints();
	}

	void SetBreakpoint(LBreakpoint1& breakpoint) override
	{
		m_log << "SetBreakpoint(" << breakpoint << ")";
		m_inner.SetBreakpoint(breakpoint);
	}

	void EnableBreakpoint(uint32_t id, bool is_enabled) override
	{
		m_log << "EnableBreakpoint()";
		m_inner.EnableBreakpoint(id, is_enabled);
	}

	void DeleteBreakpoint(uint32_t id) override
	{
		m_log << "DeleteBreakpoint(" << id << ")";
		m_inner.DeleteBreakpoint(id);
	}

	void SetRegister(LRegister1 reg, uint32_t value) override
	{
		m_log << "SetRegister(" << std::to_string(reg) << ", 0x" << std::hex << value << ")";
		m_inner.SetRegister(reg, value);
	}

	std::vector<LDescriptor1> GetGdt() override
	{
		m_log << "GetGdt()";
		return m_inner.GetGdt();
	}

	std::vector<LDescriptor1> GetLdt() override
	{
		m_log << "GetLdt()";
		return m_inner.GetLdt();
	}
};

class LizardResponder : public ILizard1
{
public:
	LRegisters1 r_break;
	LRegisters1 r_step_in;
	LRegisters1 r_step_over;
	LRegisters1 r_step_out;
	LRegisters1 r_step_multiple;
	LRegisters1 r_get_state;
	uint32_t r_max_non_empty_address;
	std::vector<LAddress1> r_search_memory;
	std::vector<LAssemblyLine1> r_disassemble;
	std::vector<uint8_t> r_get_memory;
	std::vector<LBreakpoint1> r_list_breakpoints;
	std::vector<LDescriptor1> r_get_gdt;
	std::vector<LDescriptor1> r_get_ldt;

	void Continue() override {}
	LRegisters1 Break() override { return r_break; }
	LRegisters1 StepIn() override { return r_step_in; }
	LRegisters1 StepOver() override { return r_step_over; }
	LRegisters1 StepOut() override { return r_step_out; }
	LRegisters1 StepMultiple(uint32_t cycles) override { return r_step_multiple; }
	void RunToAddress(LAddress1& address) override {}
	LRegisters1 GetState() override { return r_get_state; }
	uint32_t GetMaxNonEmptyAddress(uint16_t seg) override { return r_max_non_empty_address; }
	std::vector<LAddress1> SearchMemory(LAddress1& start, uint32_t length, std::vector<uint8_t>& pattern, uint32_t advance) override { return r_search_memory; }
	std::vector<LAssemblyLine1> Disassemble(LAddress1& address, uint32_t length) override { return r_disassemble; }
	std::vector<uint8_t> GetMemory(LAddress1& address, uint32_t length) override { return r_get_memory; }
	void SetMemory(LAddress1& address, std::vector<uint8_t>& bytes) override {}
	std::vector<LBreakpoint1> ListBreakpoints() override { return r_list_breakpoints; }
	void SetBreakpoint(LBreakpoint1& breakpoint) override {}
	void EnableBreakpoint(uint32_t id, bool is_enabled) override {}
	void DeleteBreakpoint(uint32_t id) override {}
	void SetRegister(LRegister1 reg, uint32_t value) override {}
	std::vector<LDescriptor1> GetGdt() override { return r_get_gdt; }
	std::vector<LDescriptor1> GetLdt() override { return r_get_ldt; }
};

void roundtrip_param_test(
	const char* test_name,
	const std::function<void(ILizard1& target)>& invoke,
	LizardResponder& responder)
{
	try
	{
		LizardTestLogger logger(responder);
		invoke(logger);
		std::string expected = logger.GetLog();
		logger.ClearLog();

		std::unique_ptr<IDeserializer> deserializer(MakeLizard1Deserializer(logger));
		std::unique_ptr<ILizard1> serializer(MakeLizard1Serializer(
			[&deserializer](std::vector<uint8_t>& buffer)
			{
				deserializer->HandleMessage(buffer);
			}));

		invoke(*serializer);
		std::string actual = logger.GetLog();

		if (expected != actual)
		{
			printf("%s failed:\n", test_name);
			printf("  Expected: %s\n", expected.c_str());
			printf("  Actual:   %s\n\n", actual.c_str());
		}
		else
		{
			printf("%s passed.\n", test_name);
		}
	}
	catch (const std::runtime_error& ex)
	{
		printf("%s failed with exception \"%s\"\n", test_name, ex.what());
	}
}

void run_tests_with_responses(LizardResponder& responder)
{
	roundtrip_param_test("BreakTest", [](ILizard1& target) { target.Break(); }, responder);
	roundtrip_param_test("ContinueTest", [](ILizard1& target) { target.Continue(); }, responder);
	roundtrip_param_test("BreakTest", [](ILizard1& target) { target.Break(); }, responder);
	roundtrip_param_test("StepInTest", [](ILizard1& target) { target.StepIn(); }, responder);
	roundtrip_param_test("StepOverTest", [](ILizard1& target) { target.StepOver(); }, responder);
	roundtrip_param_test("StepMultipleTest", [](ILizard1& target) { target.StepMultiple(1); }, responder);
	roundtrip_param_test(
		"RunToAddressTest",
		[](ILizard1& target)
		{
			LAddress1 address = { .segment = 1, .offset = 2 };
			target.RunToAddress(address);
		}, responder);

	roundtrip_param_test("GetStateTest", [](ILizard1& target) { target.GetState(); }, responder);
	roundtrip_param_test("GetMaxNonEmptyAddressTest", [](ILizard1& target) { target.GetMaxNonEmptyAddress(0x7f); }, responder);

	roundtrip_param_test(
		"SearchMemoryTest",
		[](ILizard1& target)
		{
			std::vector<uint8_t> pattern = { 1, 2, 3, 4 };
			LAddress1 address = { .segment = 23, .offset = 0x12345 };
			target.SearchMemory(address, 16, pattern, 1);
		}, responder);

	roundtrip_param_test(
		"DisassembleTest",
		[](ILizard1& target)
		{
			LAddress1 address = { .segment = 1, .offset = 2 };
			target.Disassemble(address, 16);
		}, responder);

	roundtrip_param_test(
		"GetMemoryTest",
		[](ILizard1& target)
		{
			LAddress1 address = { .segment = 1, .offset = 2 };
			target.GetMemory(address, 16);
		}, responder);

	roundtrip_param_test(
		"SetMemoryTest",
		[](ILizard1& target)
		{
			std::vector<uint8_t> bytes = { 1, 2, 3, 4, 5 };
			LAddress1 address = { .segment = 1, .offset = 2 };
			target.SetMemory(address, bytes);
		}, responder);

	roundtrip_param_test("ListBreakpointsTest", [](ILizard1& target) { target.ListBreakpoints(); }, responder);

	roundtrip_param_test(
		"SetBreakpointTest",
		[](ILizard1& target)
		{
			LBreakpoint1 breakpoint{ .id = 1, .address = {.segment = 1, .offset = 2}, .type = LBreakpointType1::Normal, .is_enabled = true };
			target.SetBreakpoint(breakpoint);
		}, responder);

	roundtrip_param_test("EnableBreakpointTest", [](ILizard1& target) { target.EnableBreakpoint(1, 0); }, responder);
	roundtrip_param_test("DeleteBreakpointTest", [](ILizard1& target) { target.DeleteBreakpoint(1); }, responder);
	roundtrip_param_test("SetRegisterTest", [](ILizard1& target) { target.SetRegister(LRegister1::EAX, 0xb800); }, responder);
	roundtrip_param_test("GetGdtTest", [](ILizard1& target) { target.GetGdt(); }, responder);
	roundtrip_param_test("GetLdtTest", [](ILizard1& target) { target.GetLdt(); }, responder);
}

void RunTests()
{
	LizardResponder responder;
	run_tests_with_responses(responder); // Test with empty responses
	/*
		std::vector<LDescriptor1> r_get_gdt;
		std::vector<LDescriptor1> r_get_ldt;
		*/

	LRegisters1 state = {
		.flags = 0x202,
		.eax = 0x12345678, .ebx = 0x23456789, .ecx = 0x34567890, .edx = 0x45678901,
		.esi = 0x56789012, .edi = 0x67890123, .ebp = 0x78901234, .esp = 0x89012345, .eip = 0x90123456,
		.es = 0x0001, .cs = 0x0002, .ss = 0x0003, .ds = 0x0004, .fs = 0x0005, .gs = 0x0006,
		.is_stopped = false
	};

	responder.r_break = state;
	responder.r_step_in = state;
	responder.r_step_over = state;
	responder.r_step_multiple = state;
	responder.r_get_state = state;
	responder.r_max_non_empty_address = 0x10000;
	responder.r_search_memory = {
		{.segment = 0x1234, .offset = 0x5678 },
		{.segment = 0x1234, .offset = 0x567c }
	};

	responder.r_disassemble = {
		{.address = {.segment = 0x1234, .offset = 0x5678 }, .line = "mov eax, ebx", .bytes = { 0x89, 0xd8 } },
		{.address = {.segment = 0x1234, .offset = 0x567c }, .line = "add eax, ecx", .bytes = { 0x01, 0xc8 } }
	};

	responder.r_get_memory = { 0x1, 0x2, 0x3, 0x4, 0x5, 0x6, 0x7, 0x8, 0x9, 0xa, 0xb, 0xc, 0xd, 0xe, 0xf, 0x10 };

	responder.r_list_breakpoints = {
		{.id = 1, .address = {.segment = 0x1234, .offset = 0x5678 }, .type = LBreakpointType1::Normal, .is_enabled = true, .ah = 0, .al = 0 },
		{.id = 2, .address = {.segment = 0x1234, .offset = 0x567c }, .type = LBreakpointType1::Interrupt, .is_enabled = false, .ah = 1, .al = 2 }
	};

	std::vector<LDescriptor1> descriptors;
	LDescriptor1 seg1 = { .type = LDescriptorType1::Code, .offset = 0x12389, .selector = 0x23131, .dpl = 0, .is_big = true };
	descriptors.push_back(seg1);

	LDescriptor1 seg2 = { .type = LDescriptorType1::Sys386IntGate, .offset = 0x12354, .selector = 0x0008, .dpl = 3, .is_big = false };
	descriptors.push_back(seg2);

	responder.r_get_gdt = descriptors;
	responder.r_get_ldt = descriptors;

	run_tests_with_responses(responder); // Test with filled out responses
}
