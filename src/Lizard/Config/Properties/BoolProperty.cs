using System.Text.Json;

namespace Lizard.Config.Properties;

public class BoolProperty : IProperty<bool>
{
    public BoolProperty(string ns, string name) => Name = $"{ns}/{name}";
    public string Name { get; }
    public bool DefaultValue => false;
    public object? FromJson(JsonElement elem) => elem.GetBoolean();
}
