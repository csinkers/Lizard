using ImGuiNET;

namespace Lizard.Gui.Windows;

public class CallStackWindow : SingletonWindow
{
    record Info(string Text, StackFrame Frame, StackFunction Function);

    readonly List<Info> _infos = new();
    readonly CommandContext _context;
    int _lastVersion;

    public CallStackWindow(CommandContext context) : base("Call Stack")
        => _context = context ?? throw new ArgumentNullException(nameof(context));

    protected override void DrawContents()
    {
        if (_context.Session.IsPaused && _context.Session.Version > _lastVersion)
        {
            _lastVersion = _context.Session.Version;
            _infos.Clear();

            var stack = _context.Stack;
            for (var frameIndex = 0; frameIndex < stack.Count; frameIndex++)
            {
                var frame = stack[frameIndex];
                foreach (var func in frame.Functions)
                {
                    var symbol = func.Symbol;
                    _infos.Add(new Info($"[{frameIndex:x}] {symbol.Name}+{func.Offset:X}", frame, func));
                }
            }
        }

        ImGui.BeginListBox("##Frames");
        for (var i = 0; i < _infos.Count; i++)
        {
            var info = _infos[i];
            bool isSelected = _context.SelectedAddress == info.Function.Address;
            if (ImGui.Selectable(info.Text, ref isSelected))
            {
                if (i == 0)
                {
                    _context.SelectedFrameIndex = null;
                    _context.SelectedAddress = null;
                }
                else
                {
                    _context.SelectedFrameIndex = i;
                    _context.SelectedAddress = info.Function.Address;
                }
            }
        }

        ImGui.EndListBox();
    }
}
