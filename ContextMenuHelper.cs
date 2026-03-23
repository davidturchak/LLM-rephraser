using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace LlmRephraser;

/// <summary>
/// Registers/unregisters a "Rephrase with LLM-Rephraser" entry
/// in the Windows Explorer / Desktop right-click context menu.
/// When clicked, it signals the running instance to start rephrase.
/// </summary>
public static class ContextMenuHelper
{
    private const string ShellKeyPath = @"Software\Classes\DesktopBackground\Shell\LLMRephraser";
    private const string DirBgKeyPath = @"Software\Classes\Directory\Background\Shell\LLMRephraser";

    public static void Register()
    {
        var exePath = Application.ExecutablePath;

        RegisterKey(ShellKeyPath, exePath);
        RegisterKey(DirBgKeyPath, exePath);
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(ShellKeyPath, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(DirBgKeyPath, throwOnMissingSubKey: false);
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(ShellKeyPath);
        return key != null;
    }

    private static void RegisterKey(string keyPath, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(keyPath);
        key.SetValue("", "Rephrase with LLM-Rephraser");
        key.SetValue("Icon", $"\"{exePath}\",0");

        using var cmdKey = key.CreateSubKey("command");
        cmdKey.SetValue("", $"\"{exePath}\" --rephrase-clipboard");
    }
}
