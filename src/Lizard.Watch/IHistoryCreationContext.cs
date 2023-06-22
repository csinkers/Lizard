namespace Lizard.Watch;

public interface IHistoryCreationContext
{
    string? ResolvePath(string path, string context);
    RendererCache Renderers { get; }
}