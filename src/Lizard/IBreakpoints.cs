namespace Lizard;

public interface IBreakpoints
{
    IList<IBreakpoint> GetAll();
    void Set(IBreakpoint breakpoint);
    void Delete(int number);
    void SetEnabled(int number, bool enabled);
}