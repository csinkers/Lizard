﻿using Ice;
using LizardProtocol;

namespace Lizard.Session.IceClient;

public class DebugClientI : DebugClientDisp_
{
    public event StoppedDelegate? StoppedEvent;

    public override void Stopped(Registers state, Current? current = null)
    {
        var handler = StoppedEvent;
        handler?.Invoke(state);
    }
}
