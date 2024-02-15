using System.Text.Json;

namespace Lizard.Config.Properties;

public class EnumProperty<T> : IProperty<T>
    where T : struct, Enum
{
    public EnumProperty(string ns, string name, T defaultValue)
    {
        Name = $"{ns}/{name}";
        DefaultValue = defaultValue;
    }

    public string Name { get; }
    public T DefaultValue { get; }

    public object? FromJson(JsonElement elem)
    {
        var asString = elem.GetString();
        if (asString == null)
            throw new FormatException(
                $"Null is an invalid value for the \"{Name}\" property (must be {typeof(T).Name})"
            );

        return Enum.Parse<T>(asString);
    }
}
