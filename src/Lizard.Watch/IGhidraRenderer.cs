namespace Lizard.Watch;

public interface IGhidraRenderer
{
    uint GetSize(History? history);
    History HistoryConstructor(string path, IHistoryCreationContext context);
    bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context);
}