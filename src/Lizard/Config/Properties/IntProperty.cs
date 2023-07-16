using System.Text.Json;

namespace Lizard.Config.Properties;

public class IntProperty : IProperty<int>
{
    public IntProperty(string ns, string name, int defaultValue)
    {
        Name = $"{ns}/{name}";
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public int DefaultValue { get; }
    public object? FromJson(JsonElement elem) => elem.GetInt32();
}
