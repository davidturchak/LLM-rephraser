using System;
using System.IO;

namespace LlmRephraser;

public static class AppLogger
{
    private static readonly string LogDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LLM-Rephraser", "logs");

    private static readonly object Lock = new();

    private static string LogFilePath()
    {
        Directory.CreateDirectory(LogDir);
        return Path.Combine(LogDir, $"{DateTime.Now:yyyy-MM-dd}.log");
    }

    public static void LogRequest(string endpoint, string model, string style, string userText)
    {
        var entry = $"""
            [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] REQUEST
              Endpoint: {endpoint}
              Model:    {model}
              Style:    {style}
              Input:    {Truncate(userText, 500)}
            """;
        Append(entry);
    }

    public static void LogResponse(string style, string result, TimeSpan elapsed)
    {
        var entry = $"""
            [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] RESPONSE ({elapsed.TotalSeconds:F2}s)
              Style:    {style}
              Output:   {Truncate(result, 500)}
            """;
        Append(entry);
    }

    public static void LogError(string style, string error, TimeSpan elapsed)
    {
        var entry = $"""
            [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ERROR ({elapsed.TotalSeconds:F2}s)
              Style:    {style}
              Error:    {Truncate(error, 500)}
            """;
        Append(entry);
    }

    private static string Truncate(string text, int maxLen)
    {
        var single = text.ReplaceLineEndings(" ");
        return single.Length > maxLen ? single[..maxLen] + "..." : single;
    }

    private static void Append(string entry)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogFilePath(), entry + Environment.NewLine);
            }
        }
        catch { /* never fail the app over logging */ }
    }
}
