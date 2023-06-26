using System.Text;
using ImGuiNET;

namespace Lizard.Gui;

class ImText
{
    readonly byte[] _buf;
    public ImText(int maxLength) => _buf = new byte[maxLength];
    public ImText(int maxLength, string initialText)
    {
        _buf = new byte[maxLength];
        Encoding.ASCII.GetBytes(initialText.AsSpan(), _buf.AsSpan());
        _buf[initialText.Length] = 0;
    }

    public string Text
    {
        get
        {
            var text = Encoding.ASCII.GetString(_buf);
            int index = text.IndexOf((char)0, StringComparison.Ordinal);
            return text[..index];
        }
        set
        {
            if (value.Length > _buf.Length)
                value = value[..(_buf.Length - 1)];

            Encoding.ASCII.GetBytes(value.AsSpan(), _buf.AsSpan());
            _buf[value.Length] = 0;
        }
    }

    public bool Draw(string label) => ImGui.InputText("", _buf, (uint)_buf.Length);
    public bool Draw(string label, ImGuiInputTextFlags inputTextFlags) => ImGui.InputText("", _buf, (uint)_buf.Length, inputTextFlags);
}