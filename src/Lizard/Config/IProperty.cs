using System.Text.Json;

namespace Lizard.Config;

public interface IProperty
{
    string Name { get; }
    object? FromJson(JsonElement elem);
}

public interface IProperty<out T> : IProperty
{
    T DefaultValue { get; }
}
