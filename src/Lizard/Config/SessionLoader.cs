using System.Text.Json;

namespace Lizard.Config;

public static class ProjectLoader
{
    public static ProjectConfig Load(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        return JsonSerializer.Deserialize<ProjectConfig>(stream) 
               ?? throw new FormatException($"Could not load project config from \"{path}\"");
    }

    public static void Save(string path, ProjectConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        File.WriteAllText(path, json);
    }
}