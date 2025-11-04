#pragma once
#include "CsConsole.h"
#include "LizardComms.g.h"
#include <string>

void RunTests();
int RunServer(uint16_t port, const std::shared_ptr<LizardComms::ILogger>& log);
int RunClient(const std::string& target, uint16_t port, const std::shared_ptr<LizardComms::ILogger>& log);

LizardComms::ISocket* CreateSdlNetClientSocket(const std::string& target, uint16_t port);
LizardComms::ISocket* CreateSdlNetServerSocket(uint16_t port);

