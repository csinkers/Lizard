namespace Lizard.Config;

public class WindowConfig : PropertyProvider
{
    public string? Id { get; set; }
    public bool Open { get; set; }
    public WindowConfig(string id) => Id = id;
}