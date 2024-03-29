﻿using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lizard.Config;

public class ProjectConfig : PropertyProvider
{
    [JsonIgnore]
    public string? Path { get; set; }
    public Dictionary<string, WindowConfig> Windows { get; set; } = new();

    public static ProjectConfig Load(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        var result = Load(stream);
        result.Path = path;
        return result;
    }

    public static ProjectConfig Load(Stream stream)
    {
        var result =
            JsonSerializer.Deserialize<ProjectConfig>(stream) ?? throw new FormatException("Could not load project");

        foreach (var kvp in result.Windows)
        {
            kvp.Value.Id = kvp.Key;
            kvp.Value.Project = result;
        }

        return result;
    }

    public void Save(string path)
    {
        using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true, });
    }
}
