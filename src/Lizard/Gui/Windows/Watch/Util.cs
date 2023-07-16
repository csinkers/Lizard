using System.Numerics;

namespace Lizard.Gui.Windows.Watch;

public static class Util
{
    public static readonly long StartTimeTicks = DateTime.UtcNow.Ticks;

    public static float Timestamp(long ticks)
    {
        var dt = new DateTime(ticks);
        var startTime = new DateTime(StartTimeTicks);
        return (float)(dt - startTime).TotalSeconds;
    }

    public static ReadOnlySpan<T> SafeSlice<T>(ReadOnlySpan<T> span, uint from, uint size) => SafeSlice(span, (int)from, (int)size);
    public static ReadOnlySpan<T> SafeSlice<T>(ReadOnlySpan<T> span, int from, int size)
    {
        from = Math.Min(span.Length, from);
        size = Math.Min(span.Length - from, size);
        return span.Slice(from, size);
    }

    static readonly long MaxAgeTicks = TimeSpan.FromSeconds(3).Ticks;
    public static Vector4 ColorForAge(long ageInTicks)
    {
        if (ageInTicks >= MaxAgeTicks)
            return Vector4.One;

        var t = (float)ageInTicks / MaxAgeTicks;
        return new Vector4(1.0f, t, t, 1.0f);
    }

    public static int FindNearest<T>(IList<(uint Address, T)> collection, uint address) // Binary search
    {
        int first = 0;
        int last = collection.Count - 1;
        int mid;

        do
        {
            mid = first + (last - first) / 2;
            if (address > collection[mid].Address)
                first = mid + 1;
            else
                last = mid - 1;

            if (collection[mid].Address == address)
                return mid;
        } while (first <= last);

        if (collection[mid].Address > address && mid != 0)
            mid--;

        return mid;
    }
}