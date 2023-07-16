using System.Text.Json;

namespace Lizard.Config.Properties;

public class StringProperty : IProperty<string?>
{
    public StringProperty(string name) => Name = name;
    public string Name { get; }
    public string? DefaultValue => null;
    public object? FromJson(JsonElement elem) => elem.GetString();
}
