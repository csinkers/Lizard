using GhidraProgramData;
using GhidraProgramData.Types;
using ImGuiNET;

namespace Lizard.Gui.Windows.Watch.Renderers;

public class RString : IGhidraRenderer
{
    const int MaxStringLength = 1024;
    const uint InitialSize = 32;

    readonly GString _type;

    class StringHistory : History
    {
        public uint Size { get; set; }
        public StringHistory(string path, IGhidraType type) : base(path, type) { }
        public override string ToString() => $"StringH:{Path}:{Util.Timestamp(LastModifiedTicks):g3}";
    }

    public RString(GString type) => _type = type ?? throw new ArgumentNullException(nameof(type));
    public override string ToString() => $"R[{_type}]";
    public uint GetSize(History? history) => ((StringHistory?)history)?.Size ?? InitialSize;
    public History HistoryConstructor(string path, IHistoryCreationContext context) => new StringHistory(path, _type);
    public bool Draw(History history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer, DrawContext context)
        => Draw((StringHistory)history, address, buffer, previousBuffer);

    static bool Draw(StringHistory history, uint address, ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> previousBuffer)
    {
        history.LastAddress = address;
        int zeroIndex = -1;
        for (int i = 0; i < buffer.Length; i++)
        {
            if (buffer[i] == 0)
            {
                zeroIndex = i;
                break;
            }
        }

        if (zeroIndex == -1)
        {
            if (history.Size == 0)
                history.Size = InitialSize;
            else
                history.Size *= 2;

            if (history.Size > MaxStringLength)
                history.Size = MaxStringLength;

            zeroIndex = buffer.Length - 1;
        }
        else
            history.Size = (uint)zeroIndex + 1;

        if (zeroIndex == -1)
        {
            ImGui.TextUnformatted("");
            return false;
        }

        var text = Constants.Encoding.GetString(buffer[..zeroIndex]);
        ImGui.TextUnformatted("\"" + text + "\"");
        return !previousBuffer.IsEmpty && !buffer.SequenceEqual(previousBuffer);
    }
}