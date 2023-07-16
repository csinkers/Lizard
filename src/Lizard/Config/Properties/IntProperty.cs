using System.Text.Json;

namespace Lizard.Config.Properties;

public class IntProperty : IProperty<int>
{
    public IntProperty(string name, int defaultValue)
    {
        Name = name;
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public int DefaultValue { get; }
    public object? FromJson(JsonElement elem) => elem.GetInt32();
}
