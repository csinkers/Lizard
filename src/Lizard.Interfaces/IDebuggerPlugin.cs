namespace Lizard.Interfaces;

public interface IDebuggerPlugin
{
    void Load(IDebugger debugger);
    void Unload();
}

