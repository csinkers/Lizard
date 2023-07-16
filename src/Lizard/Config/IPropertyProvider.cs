namespace Lizard.Config;

public interface IPropertyProvider
{
    /// <summary>
    /// Retrieve a property's value
    /// </summary>
    /// <typeparam name="T">The type to interpret the property value as</typeparam>
    /// <param name="property">The property to retrieve</param>
    /// <returns>The parsed value, or defaultValue if no value existed or could be parsed.</returns>
    public T GetProperty<T>(IProperty<T> property);

    /// <summary>
    /// Retrieve a property's value
    /// </summary>
    /// <typeparam name="T">The type to interpret the property value as</typeparam>
    /// <param name="property">The property to retrieve</param>
    /// <param name="defaultValue">The default value to use when the property is not set</param>
    /// <returns>The parsed value, or defaultValue if no value existed or could be parsed.</returns>
    public T GetProperty<T>(IProperty<T> property, T defaultValue);

    /// <summary>
    /// Sets a property by name
    /// </summary>
    /// <param name="property">The property</param>
    /// <param name="value">The value to set the property to</param>
    public void SetProperty<T>(IProperty<T> property, T value);
}