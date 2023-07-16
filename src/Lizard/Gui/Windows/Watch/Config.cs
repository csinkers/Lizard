using System.Text.Json;

namespace Lizard.Gui.Windows.Watch
{
    public class Config
    {
        const string Filename = @"MemWatcher.json";
        public List<string> Watches { get; set; } = new();

        public static Config Load() => File.Exists(Filename)
            ? JsonSerializer.Deserialize<Config>(File.ReadAllText(Filename))!
            : new Config();

        public void Save() => File.WriteAllText(Filename, JsonSerializer.Serialize(this));
    }
}
