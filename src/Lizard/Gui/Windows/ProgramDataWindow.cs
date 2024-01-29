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

    readonly CommandContext _context;
    readonly ImText _path = new(260, "");
    readonly ImText _codePath = new(260, "");
    readonly List<TempRegion> _tempRegions = new();
    string _lastError = "";

    static string FormatHex(uint v) => v.ToString("X8");

    public ProgramDataWindow(CommandContext context) : base("Program Data")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _path.Text = _context.Symbols.DataPath ?? "";
        _codePath.Text = _context.Symbols.CodePath ?? "";
        LoadFromMapping(_context.Mapping);

        _context.Symbols.DataLoaded += _ =>
        {
            _path.Text = _context.Symbols.DataPath ?? "";
            _codePath.Text = _context.Symbols.CodePath ?? "";
            LoadFromMapping(_context.Mapping);
        };
    }

    void LoadFromMapping(MemoryMapping mapping)
    {
        _tempRegions.Clear();
        foreach (var region in mapping.Regions)
        {
            var temp = new TempRegion();
            Encoding.UTF8.GetBytes(FormatHex(region.FileStart), temp.FileAddress);
            Encoding.UTF8.GetBytes(FormatHex(region.MemoryStart), temp.MemAddress);
            Encoding.UTF8.GetBytes(FormatHex(region.Length), temp.Length);
            temp.Type = (int)region.Type;
            _tempRegions.Add(temp);
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
            try { _context.Symbols.Load(_path.Text, _codePath.Text); }
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

        for (var i = 0; i < _tempRegions.Count; i++)
        {
            ImGui.PushID(i);

            ImGui.NextColumn();
            var tempRegion = _tempRegions[i];
            if (ImGui.Button("-"))
                indexToRemove = i;

            ImGui.SameLine();   ImGui.InputText("##file", tempRegion.FileAddress, (uint)tempRegion.FileAddress.Length, ImGuiInputTextFlags.CharsHexadecimal);
            ImGui.NextColumn(); ImGui.InputText("##mem", tempRegion.MemAddress, (uint)tempRegion.MemAddress.Length, ImGuiInputTextFlags.CharsHexadecimal);
            ImGui.NextColumn(); ImGui.InputText("##len", tempRegion.Length, (uint)tempRegion.Length.Length, ImGuiInputTextFlags.CharsHexadecimal);
            ImGui.NextColumn();
            int type = tempRegion.Type;
            if (ImGui.Combo("##type", ref type, MemoryTypes, MemoryTypes.Length))
                tempRegion.Type = type;

            ImGui.PopID();
        }

        ImGui.Columns(0);
        if (ImGui.Button("+"))
            _tempRegions.Add(new TempRegion());

        if (indexToRemove != -1)
            _tempRegions.RemoveAt(indexToRemove);

        if (ImGui.Button("Apply Mapping"))
        {
            try
            {
                var regions = new List<MemoryRegion>();
                foreach (var tempRegion in _tempRegions)
                {
                    var fileOffset = ParseHex(tempRegion.FileAddress, "file offset");
                    var memOffset = ParseHex(tempRegion.MemAddress, "memory offset");
                    var length = ParseHex(tempRegion.Length, "length");
                    var type = (MemoryType)tempRegion.Type;
                    regions.Add( new MemoryRegion(memOffset, fileOffset, length, type));
                }

                _context.Mapping.Update(regions);
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