using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

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
    public bool ContextMenuEnabled { get; set; } = false;
    public bool FloatingToolbarEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; } = false;
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
        ApplyStartWithWindows();
        ApplyContextMenu();
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "LLM-Rephraser";

    public void ApplyStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            if (StartWithWindows)
            {
                var exePath = Environment.ProcessPath ?? "";
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(RunValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
        catch { /* ignore registry errors */ }
    }

    private void ApplyContextMenu()
    {
        try
        {
            if (ContextMenuEnabled)
                ContextMenuHelper.Register();
            else
                ContextMenuHelper.Unregister();
        }
        catch { /* ignore registry errors */ }
    }

    public static bool ReadStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(RunValueName) != null;
        }
        catch { return false; }
    }
}
