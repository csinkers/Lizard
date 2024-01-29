using System.Text.Json;

namespace Lizard.Config.Properties;

public class Property<T> : IProperty<T>
{
    public Property(string ns, string name, T defaultValue)
    {
        Name = $"{ns}/{name}";
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public T DefaultValue { get; }
    public object? FromJson(JsonElement elem) => elem.Deserialize<T>();
}