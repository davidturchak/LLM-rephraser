using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmRephraser;

public enum ApiProvider
{
    OpenAICompatible,
    Anthropic
}

public sealed class ProfileConfig
{
    public ApiProvider Provider { get; set; } = ApiProvider.OpenAICompatible;
    public string ApiEndpoint { get; set; } = "http://localhost:11434/v1/chat/completions";
    public string ApiKey { get; set; } = "";
    public string ModelName { get; set; } = "llama3";
}

public sealed class AppConfig
{
    public string ActiveProfile { get; set; } = "Default";
    public bool ShiftRightClickEnabled { get; set; } = false;
    public List<string> TranslationLanguages { get; set; } = ["English", "Hebrew", "Arabic", "Russian"];
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new()
    {
        ["Default"] = new ProfileConfig()
    };

    [JsonIgnore]
    public ProfileConfig Active => Profiles.TryGetValue(ActiveProfile, out var p) ? p : new ProfileConfig();

    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LLM-Rephraser");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static bool Exists() => File.Exists(ConfigPath);

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config == null) return new AppConfig();

            // Migration: if old flat config fields are present, convert them
            if (config.Profiles.Count == 0)
            {
                config.Profiles["Default"] = new ProfileConfig();
                config.ActiveProfile = "Default";
            }

            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDir);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
