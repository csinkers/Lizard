using System.Numerics;
using ImGuiNET;

namespace Lizard.Gui.Windows;

class RegistersWindow : SingletonWindow
{
    const uint WhiteUInt = uint.MaxValue;
    static readonly Vector4 White = new(1.0f, 1.0f, 1.0f, 1.0f);
    static readonly Vector4 Red = new(1.0f, 0.0f, 0.0f, 1.0f);
    static readonly Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
    static readonly Vector4 Cyan = new(0.0f, 1.0f, 1.0f, 1.0f);
    static readonly Vector4 Yellow = new(1.0f, 1.0f, 0.0f, 1.0f);

    readonly CommandContext _context;
    Vector2 _regTxtSize;
    Vector2 _regChildSize;
    Vector2 _segTxtSize;
    Vector2 _segChildSize;
    Vector2 _flagTxtSize;
    Vector2 _flagChildSize;
    bool _initialised;

    public RegistersWindow(CommandContext context)
        : base("Registers") => _context = context ?? throw new ArgumentNullException(nameof(context));

    void FirstDraw()
    {
        _initialised = true;
        var style = ImGui.GetStyle();

        const int regLine = 4,
            regCol = 1;
        _regTxtSize = ImGui.CalcTextSize("EXX=FFFFFFFF", false, 0.0f);
        _regChildSize = new(
            _regTxtSize.X * regCol + style.FramePadding.X * 2.0f + style.ItemSpacing.X * 2.0f,
            _regTxtSize.Y * regLine
                + style.FramePadding.Y * 2.0f
                + style.ItemSpacing.Y * 2.0f
                + style.ItemInnerSpacing.Y * regLine
        );

        const int segLine = 3,
            segCol = 2;
        _segTxtSize = ImGui.CalcTextSize("DS=FFFF", false, 0.0f);
        _segChildSize = new(
            _segTxtSize.X * segCol
                + style.FramePadding.X * 2.0f
                + style.ItemSpacing.X * 2.0f
                + style.ItemInnerSpacing.X * segCol,
            _segTxtSize.Y * segLine
                + style.FramePadding.Y * 2.0f
                + style.ItemSpacing.Y * 2.0f
                + style.ItemInnerSpacing.Y * segLine
        );

        const int flagLine = 3,
            flagCol = 3;
        _flagTxtSize = ImGui.CalcTextSize("CF=0", false, 0.0f);
        _flagChildSize = new(
            _flagTxtSize.X * flagCol
                + style.FramePadding.X * 2.0f
                + style.ItemSpacing.X * 2.0f
                + style.ItemInnerSpacing.X * flagCol,
            _flagTxtSize.Y * flagLine
                + style.FramePadding.Y * 2.0f
                + style.ItemSpacing.Y * 2.0f
                + style.ItemInnerSpacing.Y * flagLine
        );
    }

    protected override void DrawContents()
    {
        if (!_initialised)
            FirstDraw();

        var session = _context.Session;
        var oldRegs = session.OldRegisters;
        var regs = session.Registers;

        ImGui.PushStyleColor(ImGuiCol.Border, Green);
        ImGui.BeginChild("exx_regs", _regChildSize, ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);
        DrawReg8("EAX", regs.Eax, oldRegs.Eax);
        DrawReg8("EBX", regs.Ebx, oldRegs.Ebx);
        DrawReg8("ECX", regs.Ecx, oldRegs.Ecx);
        DrawReg8("EDX", regs.Edx, oldRegs.Edx);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Border, Cyan);
        ImGui.BeginChild("exi_exp_regs", _regChildSize, ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);
        DrawReg8("ESI", regs.Esi, oldRegs.Esi);
        DrawReg8("EDI", regs.Edi, oldRegs.Edi);
        DrawReg8("EBP", regs.Ebp, oldRegs.Ebp);
        DrawReg8("ESP", regs.Esp, oldRegs.Esp);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Spacing();

        ImGui.PushStyleColor(ImGuiCol.Border, Yellow);
        ImGui.BeginChild("segments", _segChildSize, ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);
        DrawReg4("DS", regs.Ds, oldRegs.Ds);
        ImGui.SameLine();
        DrawReg4("FS", regs.Fs, oldRegs.Fs);
        DrawReg4("ES", regs.Es, oldRegs.Es);
        ImGui.SameLine();
        DrawReg4("GS", regs.Gs, oldRegs.Gs);
        DrawReg4("CS", regs.Cs, oldRegs.Cs);
        ImGui.SameLine();
        DrawReg4("SS", regs.Ss, oldRegs.Ss);
        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Border, Red);
        ImGui.BeginChild("flags", _flagChildSize, ImGuiChildFlags.Borders, ImGuiWindowFlags.NoScrollbar);
        DrawFlag("CF", regs.Flags, oldRegs.Flags, CpuFlags.CF);
        ImGui.SameLine();
        DrawFlag("ZF", regs.Flags, oldRegs.Flags, CpuFlags.ZF);
        ImGui.SameLine();
        DrawFlag("SF", regs.Flags, oldRegs.Flags, CpuFlags.SF);

        DrawFlag("Oj", regs.Flags, oldRegs.Flags, CpuFlags.OF);
        ImGui.SameLine();
        DrawFlag("AF", regs.Flags, oldRegs.Flags, CpuFlags.AF);
        ImGui.SameLine();
        DrawFlag("PF", regs.Flags, oldRegs.Flags, CpuFlags.PF);

        DrawFlag("Dj", regs.Flags, oldRegs.Flags, CpuFlags.DF);
        ImGui.SameLine();
        DrawFlag("IF", regs.Flags, oldRegs.Flags, CpuFlags.IF);
        ImGui.SameLine();
        DrawFlag("TF", regs.Flags, oldRegs.Flags, CpuFlags.TF);

        ImGui.EndChild();
        ImGui.PopStyleColor();

        ImGui.Spacing();

        DrawReg8("EIP", regs.Eip, oldRegs.Eip);

        GetPaddedRect(out var rectMinPos, out var rectMaxPos);
        ImGui.GetWindowDrawList().AddRect(rectMinPos, rectMaxPos, WhiteUInt, 5.0f);

        ImGui.Spacing();

        GetPaddedRect(out rectMinPos, out rectMaxPos);
        ImGui.GetWindowDrawList().AddRect(rectMinPos, rectMaxPos, WhiteUInt, 5.0f);

        // DrawReg8("Version", _debugger.Version, _debugger.Version);
    }

    static void DrawReg4(string name, int value, int oldValue)
    {
        var color = value == oldValue ? White : Red;
        ImGui.TextColored(color, $"{name}={value:X4}");
    }

    static void DrawReg8(string name, uint value, uint oldValue)
    {
        var color = value == oldValue ? White : Red;
        ImGui.TextColored(color, $"{name}={value:X8}");
    }

    static void DrawFlag(string name, uint flags, uint oldFlags, CpuFlags flag)
    {
        int val = (flags & (int)flag) == 0 ? 0 : 1;
        int oldVal = (oldFlags & (int)flag) == 0 ? 0 : 1;
        var color = val == oldVal ? White : Red;
        ImGui.TextColored(color, $"{name}={val}");
    }

    static void GetPaddedRect(out Vector2 outItemMinRect, out Vector2 outItemMaxRect)
    {
        const float rectPaddingOffsetMin = 2.0f;
        const float rectPaddingOffsetMax = 4.0f;

        outItemMinRect = ImGui.GetItemRectMin();
        outItemMaxRect = ImGui.GetItemRectMax();
        outItemMaxRect.X = outItemMinRect.X + ImGui.GetContentRegionAvail().X;

        outItemMinRect.X -= rectPaddingOffsetMin;
        outItemMinRect.Y -= rectPaddingOffsetMin;
        outItemMaxRect.X += rectPaddingOffsetMax;
        outItemMaxRect.Y += rectPaddingOffsetMax;
    }
}
