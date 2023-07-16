namespace Lizard;

public interface IDebuggerPlugin
{
    void Load(IDebugger debugger);
    void Unload();
}

