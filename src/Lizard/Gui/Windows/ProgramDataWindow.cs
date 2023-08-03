using System.Globalization;
using System.Numerics;
using System.Text;
using ImGuiNET;
using SharpFileDialog;

namespace Lizard.Gui.Windows;

public class ProgramDataWindow : SingletonWindow
{
    static readonly string[] MemoryTypes = Enum.GetValues<MemoryType>().Select(x => x.ToString()).ToArray();
    class TempRegion
    {
        const int MaxLength = 9;
        public readonly byte[] FileAddress = new byte[MaxLength];
        public readonly byte[] MemAddress = new byte[MaxLength];
        public readonly byte[] Length = new byte[MaxLength];
        public int Type;
    }

    readonly ProgramDataManager _programDataManager;
    readonly ImText _path = new(260, "");
    readonly ImText _codePath = new(260, "");
    readonly List<TempRegion> _mapping = new();
    string _lastError = "";

    static string FormatHex(uint v) => v.ToString("X8");

    public ProgramDataWindow(ProgramDataManager programDataManager) : base("Program Data")
    {
        _programDataManager = programDataManager ?? throw new ArgumentNullException(nameof(programDataManager));
        _path.Text = _programDataManager.DataPath ?? "";
        _codePath.Text = _programDataManager.CodePath ?? "";
        LoadFromMapping(_programDataManager.Mapping);

        _programDataManager.DataLoaded += _ =>
        {
            _path.Text = _programDataManager.DataPath ?? "";
            _codePath.Text = _programDataManager.CodePath ?? "";
            LoadFromMapping(_programDataManager.Mapping);
        };
    }

    void LoadFromMapping(MemoryMapping mapping)
    {
        _mapping.Clear();
        foreach (var region in mapping.Regions)
        {
            var temp = new TempRegion();
            Encoding.UTF8.GetBytes(FormatHex(region.FileStart), temp.FileAddress);
            Encoding.UTF8.GetBytes(FormatHex(region.MemoryStart), temp.MemAddress);
            Encoding.UTF8.GetBytes(FormatHex(region.Length), temp.Length);
            temp.Type = (int)region.Type;
            _mapping.Add(temp);
        }
    }

    protected override void DrawContents()
    {
        _path.Draw("Program XML Data Path");
        ImGui.SameLine();
        if (ImGui.Button("Browse##1"))
        {
            using var openFile = new OpenFileDialog();
            openFile.Open(x =>
            {
                if (x.Success)
                    _path.Text = x.FileName;
            }, "Ghidra Program Data XML (*.xml)|*.xml");
        }

        _codePath.Draw("Decompiled Code Path");
        ImGui.SameLine();
        if (ImGui.Button("Browse##2"))
        {
            using var openFile = new OpenFileDialog();
            openFile.Open(x =>
            {
                if (x.Success)
                    _codePath.Text = x.FileName;
            }, "Decompiled C code (*.c)|*.c");
        }

        if (ImGui.Button("Load Program Data"))
        {
            try { _programDataManager.Load(_path.Text, _codePath.Text); }
            catch (Exception ex) { _lastError = ex.Message; }
        }

        ImGui.Text("Memory Mapping");
        int indexToRemove = -1;

        ImGui.Columns(4);
        ImGui.Text("File Offset");
        ImGui.NextColumn();
        ImGui.Text("Mem Offset");
        ImGui.NextColumn();
        ImGui.Text("Length");
        ImGui.NextColumn();
        ImGui.Text("Type");

        for (var i = 0; i < _mapping.Count; i++)
        {
            ImGui.PushID(i);

            ImGui.NextColumn();
            var region = _mapping[i];
            if (ImGui.Button("-"))
                indexToRemove = i;

            ImGui.SameLine();   ImGui.InputText("##file", region.FileAddress, (uint)region.FileAddress.Length, ImGuiInputTextFlags.CharsHexadecimal);
            ImGui.NextColumn(); ImGui.InputText("##mem", region.MemAddress, (uint)region.MemAddress.Length, ImGuiInputTextFlags.CharsHexadecimal);
            ImGui.NextColumn(); ImGui.InputText("##len", region.Length, (uint)region.Length.Length, ImGuiInputTextFlags.CharsHexadecimal);
            ImGui.NextColumn();
            int type = region.Type;
            if (ImGui.Combo("##type", ref type, MemoryTypes, MemoryTypes.Length))
                region.Type = type;

            ImGui.PopID();
        }

        ImGui.Columns(0);
        if (ImGui.Button("+"))
            _mapping.Add(new TempRegion());

        if (indexToRemove != -1)
            _mapping.RemoveAt(indexToRemove);

        if (ImGui.Button("Apply Mapping"))
        {
            try
            {
                var mapping = new MemoryMapping();
                foreach (var region in _mapping)
                {
                    var fileOffset = ParseHex(region.FileAddress, "file offset");
                    var memOffset = ParseHex(region.MemAddress, "memory offset");
                    var length = ParseHex(region.Length, "length");
                    var type = (MemoryType)region.Type;
                    mapping.Add(memOffset, fileOffset, length, type);
                }

                _programDataManager.Mapping = mapping;
                _lastError = "";
            }
            catch (FormatException ex) { _lastError = ex.Message; }
            catch (InvalidOperationException ex) { _lastError = ex.Message; }
        }

        if (!string.IsNullOrEmpty(_lastError))
        {
            ImGui.PushTextWrapPos(ImGui.GetWindowSize().X);
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1,0,0,1));
            ImGui.TextWrapped(_lastError);
            ImGui.PopStyleColor();
            ImGui.PopTextWrapPos();
        }
    }

    static uint ParseHex(byte[] bytes, string description)
    {
        var s = Encoding.UTF8.GetString(bytes);
        int index = s.IndexOf('\0');
        if (index != -1)
            s = s[..index];

        if (string.IsNullOrEmpty(s)) throw new FormatException($"No value supplied for {description}");
        return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}