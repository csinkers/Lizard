#pragma once

// LizardProtocol.h
#include "Lizard1Protocol.h"

namespace LizardComms
{
	typedef LAddress1           LAddress;
	typedef LRegister1          LRegister;
	typedef LRegisters1         LRegisters;
	typedef LBreakpointType1    LBreakpointType;
	typedef LBreakpoint1        LBreakpoint;
	typedef LAssemblyLine1      LAssemblyLine;
	typedef LDescriptor1        LDescriptor;
	typedef ILizardClient1      ILizardClient;
	typedef ILizardProtocol1    ILizardProtocol;
	typedef LizardProtocol1Serializer   LizardSerializer;
	typedef LizardProtocol1Deserializer LizardDeserializer;
}
