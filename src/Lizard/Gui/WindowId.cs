using System.Globalization;

namespace Lizard.Gui;

public class WindowId : IEquatable<WindowId>
{
    const char Separator = '_';
    string? _displayName;

    public string Prefix { get; }
    public int Id { get; }
    public string LogicalName => Prefix + Separator + Id.ToString(CultureInfo.InvariantCulture); // e.g. Prefix_1
    public string? ImGuiName { get; private set; } // e.g. "Some Display Name###Prefix_1"
    public string? DisplayName
    {
        get => _displayName;
        set
        {
            _displayName = value;
            ImGuiName = DisplayName != null ? DisplayName + "###" + LogicalName : LogicalName;
        }
    }

    public override string ToString() => ImGuiName ?? "";

    public WindowId(string prefix, int id)
    {
        Prefix = prefix;
        Id = id;
        DisplayName = null;
    }

    WindowId(string prefix, int id, string? displayName, string imgui)
    {
        Prefix = prefix;
        Id = id;
        _displayName = displayName;
        ImGuiName = imgui;
    }

    public static WindowId? TryParse(string name)
    {
        var hashedPart = name;
        int index = name.IndexOf("###", StringComparison.Ordinal);
        string? displayName = null;
        if (index >= 0)
        {
            displayName = name[..index];
            hashedPart = name[(index + 3)..];
        }

        index = hashedPart.IndexOf(Separator);
        string prefix,
            remainder;
        if (index >= 0)
        {
            prefix = hashedPart[..index];
            remainder = hashedPart[(index + 1)..];
        }
        else
        {
            prefix = hashedPart;
            remainder = "-1";
        }

        return int.TryParse(remainder, out var id) ? new WindowId(prefix, id, displayName, name) : null;
    }

    public static bool operator ==(WindowId? x, WindowId? y) => Equals(x, y);

    public static bool operator !=(WindowId? x, WindowId? y) => !(x == y);

    public bool Equals(WindowId? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return Prefix == other.Prefix && Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != GetType())
            return false;
        return Equals((WindowId)obj);
    }

    public override int GetHashCode() => HashCode.Combine(Prefix, Id);
}
