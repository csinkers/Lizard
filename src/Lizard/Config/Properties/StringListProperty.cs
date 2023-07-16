using System.Text.Json;

namespace Lizard.Config.Properties;

public class StringListProperty : IProperty<List<string>>
{
    public StringListProperty(string ns, string name) => Name = $"{ns}/{name}";

    public string Name { get; }
    public List<string> DefaultValue => new();
    public object? FromJson(JsonElement elem)
    {
        if (elem.ValueKind != JsonValueKind.Array)
            throw new FormatException($"Property \"{Name}\" expects an array of strings");

        var list = new List<string?>();
        foreach (var s in elem.EnumerateArray())
            list.Add(s.GetString());

        return list;
    }
}
