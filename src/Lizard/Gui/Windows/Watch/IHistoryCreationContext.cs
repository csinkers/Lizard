namespace Lizard.Gui.Windows.Watch;

public interface IHistoryCreationContext
{
    RendererCache Renderers { get; }
    string? ResolvePath(string path, string context);
    uint ToMemoryAddress(uint fileAddress);
    uint ToFileAddress(uint memoryAddress);
}
