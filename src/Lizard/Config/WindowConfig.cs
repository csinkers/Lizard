using System.Text.Json.Serialization;

namespace Lizard.Config;

public class WindowConfig : PropertyProvider
{
    public string? Id { get; set; }
    public bool Open { get; set; }
    [JsonIgnore] public ProjectConfig? Project { get; set; }
    public WindowConfig(string id) => Id = id;
}