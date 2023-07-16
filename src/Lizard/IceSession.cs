using System.Globalization;
using Lizard.generated;

namespace Lizard;

public class IceSession : IDisposable
{
    readonly Ice.Communicator _communicator;
    public DebugHostPrx DebugHost { get; }
    public DebugClientI Client { get; }
    public DebugClientPrx ClientProxy { get; }

    public IceSession(string host, int port) // default host localhost, port 7243
    {
        var properties = Ice.Util.createProperties();
        properties.setProperty("Ice.MessageSizeMax", (2 * 1024 * 1024).ToString(CultureInfo.InvariantCulture));

        var initData = new Ice.InitializationData { properties = properties };
        _communicator = Ice.Util.initialize(initData);

        Ice.ObjectPrx? proxy = 
            _communicator.stringToProxy($"DebugHost:default -h {host} -p {port}")
            .ice_twoway()
            .ice_secure(false);

        DebugHost = DebugHostPrxHelper.uncheckedCast(proxy);

        if (DebugHost == null)
            throw new ApplicationException("Invalid proxy");

        var adapter = _communicator.createObjectAdapterWithEndpoints("Callback.Client", $"default -h {host}");
        Client = new DebugClientI();
        adapter.add(Client, Ice.Util.stringToIdentity("debugClient"));
        adapter.activate();

        ClientProxy = DebugClientPrxHelper.uncheckedCast(
            adapter.createProxy(Ice.Util.stringToIdentity("debugClient")));

        if (ClientProxy == null)
            throw new ApplicationException("Could not build client");
    }

    public void Dispose() => _communicator.Dispose();
}
