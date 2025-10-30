using Lizard.Protocol.ProtocolGen;

namespace Lizard.Session.IceClient;

public class DebugClientI : ILizardClient1
{
    public event StoppedDelegate? StoppedEvent;

    public void Stopped(LRegisters1 state)
    {
        var handler = StoppedEvent;
        handler?.Invoke(state);
    }
}
