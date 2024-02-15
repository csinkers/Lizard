namespace Lizard;

public interface IMemoryCache
{
    /// <summary>
    /// Returns cached memory if it exists, otherwise immediately fetches entire pages that cover the range.
    /// Note: there is no guarantee that the backingArray will be used, only values in the returned span should be used.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="backingArray"></param>
    /// <returns></returns>
    ReadOnlySpan<byte> Read(uint offset, uint size, Span<byte> backingArray);

    /// <summary>
    /// Returns cached memory if it exists, otherwise returns an empty span.
    /// The affected pages will be marked for acquisition on the next update
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="backingArray"></param>
    /// <returns></returns>
    ReadOnlySpan<byte> TryReadPrevious(uint offset, uint size, Span<byte> backingArray);

    /// <summary>
    /// Returns cached memory if it exists, otherwise immediately fetches entire pages that cover the range.
    /// The result will always be copied into the supplied span.
    /// </summary>
    /// <param name="offset"></param>
    /// <param name="size"></param>
    /// <param name="span"></param>
    void ReadIntoSpan(uint offset, uint size, Span<byte> span);
    void Dirty();
}
