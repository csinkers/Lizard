using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lizard.Config;

public class PropertyProvider : IPropertyProvider
{
    [JsonInclude]
    [JsonExtensionData]
    public Dictionary<string, object?> Properties { get; set; } = new();

    /// <summary>
    /// Retrieve a property's value
    /// </summary>
    /// <typeparam name="T">The type to interpret the property value as</typeparam>
    /// <param name="property">The property to retrieve</param>
    /// <returns>The parsed value, or defaultValue if no value existed or could be parsed.</returns>
    public T GetProperty<T>(IProperty<T> property)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        return GetProperty(property, property.DefaultValue);
    }

    /// <summary>
    /// Retrieve a property's value
    /// </summary>
    /// <typeparam name="T">The type to interpret the property value as</typeparam>
    /// <param name="property">The property to retrieve</param>
    /// <param name="defaultValue">The default value to use when the property is not set</param>
    /// <returns>The parsed value, or defaultValue if no value existed or could be parsed.</returns>
    public T GetProperty<T>(IProperty<T> property, T defaultValue)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        var name = property.Name;

        if (!Properties.TryGetValue(name, out var value))
            return defaultValue;

        if (value is JsonElement elem)
        {
            value = property.FromJson(elem);
            Properties[name] = value;
        }

        return (T)value!;
    }

    /// <summary>
    /// Sets a property by name
    /// </summary>
    /// <param name="property">The property</param>
    /// <param name="value">The value to set the property to</param>
    public void SetProperty<T>(IProperty<T> property, T value)
    {
        if (property == null) throw new ArgumentNullException(nameof(property));
        Properties[property.Name] = value;
    }
}