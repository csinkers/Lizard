using System.Text.Json;

namespace Lizard.Config.Properties;

public class StringProperty : IProperty<string?>
{
    public StringProperty(string ns, string name) => Name = $"{ns}/{name}";
    public StringProperty(string ns, string name, string? defaultValue)
    {
        Name = $"{ns}/{name}";
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public string? DefaultValue { get; }
    public object? FromJson(JsonElement elem) => elem.GetString();
}