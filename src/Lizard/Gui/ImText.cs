using System.Text;
using ImGuiNET;

namespace Lizard.Gui;

class ImText
{
    readonly byte[] _buffer;
    public ImText(int maxLength) => _buffer = new byte[maxLength];
    public ImText(int maxLength, string initialText)
    {
        _buffer = new byte[maxLength];
        Encoding.ASCII.GetBytes(initialText.AsSpan(), _buffer.AsSpan());
        _buffer[initialText.Length] = 0;
    }

    public string Text
    {
        get
        {
            var text = Encoding.ASCII.GetString(_buffer);
            int index = text.IndexOf((char)0, StringComparison.Ordinal);
            return text[..index];
        }
        set
        {
            if (value.Length > _buffer.Length)
                value = value[..(_buffer.Length - 1)];

            Encoding.ASCII.GetBytes(value.AsSpan(), _buffer.AsSpan());
            _buffer[value.Length] = 0;
        }
    }

    public bool Draw(string label) => ImGui.InputText(label, _buffer, (uint)_buffer.Length);
    public bool Draw(string label, ImGuiInputTextFlags inputTextFlags) 
        => ImGui.InputText(label, _buffer, (uint)_buffer.Length, inputTextFlags);
    public bool Draw(string label, ImGuiInputTextFlags inputTextFlags, ImGuiInputTextCallback callback)
        => ImGui.InputText(label, _buffer, (uint)_buffer.Length, inputTextFlags, callback);
    public bool Draw(string label, ImGuiInputTextFlags inputTextFlags, ImGuiInputTextCallback callback, IntPtr data)
        => ImGui.InputText(label, _buffer, (uint)_buffer.Length, inputTextFlags, callback, data);
}