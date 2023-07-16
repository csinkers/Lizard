using ImGuiNET;
using SharpFileDialog;

namespace Lizard.Gui.Windows;

public class ProgramDataWindow : SingletonWindow
{
    readonly ProgramDataManager _programDataManager;
    readonly ImText _path = new(260, "");
    string _selectedOffsetLabel = "";
    string _curOffsetLabel;
    int _offset;
    int Offset { get => _offset; set { _offset = value; _selectedOffsetLabel = FormatHex(value); } }

    static string FormatHex(int v) => "0x" + v.ToString("X8");

    public ProgramDataWindow(ProgramDataManager programDataManager) : base("Program Data")
    {
        _programDataManager = programDataManager ?? throw new ArgumentNullException(nameof(programDataManager));
        _path.Text = _programDataManager.DataPath ?? "";
        Offset = _programDataManager.Offset;
        _curOffsetLabel = FormatHex(Offset);

        _programDataManager.DataLoaded += _ =>
        {
            _path.Text = _programDataManager.DataPath ?? "";
            Offset = _programDataManager.Offset;
            _curOffsetLabel = FormatHex(Offset);
        };
    }

    protected override void DrawContents()
    {
        _path.Draw("Path");
        ImGui.SameLine();
        if (ImGui.Button("Browse"))
        {
            using var openFile = new OpenFileDialog();
            openFile.Open(x =>
            {
                if (x.Success)
                    _path.Text = x.FileName;
            }, "Ghidra Program Data XML (*.xml)|*.xml");
        }

        int fine = Offset & 0xfff;
        if (ImGui.SliderInt("Fine Offset", ref fine, 0, 0xfff, "%x"))
            Offset = (Offset & ~0xfff) | fine;

        int coarse = Offset & ~0xfff;
        if (ImGui.InputInt("Coarse Offset", ref coarse, 0x1000, 0x10000, ImGuiInputTextFlags.CharsHexadecimal))
            Offset = coarse | Offset & 0xfff;

        ImGui.LabelText("Current Offset", _curOffsetLabel);
        ImGui.LabelText("Selected Offset", _selectedOffsetLabel);

        if (ImGui.Button("Load"))
            _programDataManager.Load(_path.Text, _offset);
    }
}