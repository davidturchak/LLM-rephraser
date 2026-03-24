using System;
using System.Drawing;
using Microsoft.Win32;

namespace LlmRephraser;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public static class ThemeColors
{
    private static ThemeMode _userMode = ThemeMode.System;
    private static Color _accent;
    private static bool _isDark;

    public static event Action? Changed;

    static ThemeColors()
    {
        Refresh();
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (_userMode == ThemeMode.System)
            {
                Refresh();
                Changed?.Invoke();
            }
        };
    }

    public static void SetMode(ThemeMode mode)
    {
        _userMode = mode;
        Refresh();
        Changed?.Invoke();
    }

    public static ThemeMode Mode => _userMode;
    public static bool IsDark => _isDark;

    // ── Surface colors ──
    public static Color BgPage => _isDark ? Color.FromArgb(25, 25, 28) : Color.FromArgb(248, 250, 252);
    public static Color BgCard => _isDark ? Color.FromArgb(40, 40, 43) : Color.White;
    public static Color BgCardAlt => _isDark ? Color.FromArgb(50, 50, 54) : Color.White;
    public static Color BorderCard => _isDark ? Color.FromArgb(55, 58, 65) : Color.FromArgb(226, 232, 240);

    // ── Accent (from Windows) ──
    public static Color Accent => _accent;
    public static Color AccentHover => _isDark ? Lighten(_accent, 0.15f) : Darken(_accent, 0.15f);
    public static Color AccentMuted => _isDark ? Color.FromArgb(100, 110, 130) : Color.FromArgb(148, 163, 184);
    public static Color AccentOnAccent => Color.White;

    // ── Text ──
    public static Color TextBody => _isDark ? Color.FromArgb(225, 228, 232) : Color.FromArgb(51, 65, 85);
    public static Color TextMuted => _isDark ? Color.FromArgb(140, 145, 155) : Color.FromArgb(148, 163, 184);
    public static Color TextOrigBody => _isDark ? Color.FromArgb(170, 178, 190) : Color.FromArgb(100, 116, 139);

    // ── Input controls ──
    public static Color BgInput => _isDark ? Color.FromArgb(30, 30, 30) : SystemColors.Window;
    public static Color TextInput => _isDark ? Color.FromArgb(220, 220, 220) : SystemColors.WindowText;

    // ── Semantic ──
    public static Color Success => Color.FromArgb(0, 128, 0);
    public static Color Error => Color.FromArgb(234, 88, 12); // orange

    public static void Refresh()
    {
        _isDark = _userMode switch
        {
            ThemeMode.Light => false,
            ThemeMode.Dark => true,
            _ => ReadSystemDarkMode()
        };
        _accent = ReadAccentColor();
    }

    private static bool ReadSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var val = key?.GetValue("AppsUseLightTheme");
            if (val is int i) return i == 0;
        }
        catch { /* ignore */ }
        return false; // default to light
    }

    private static Color ReadAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\DWM");
            var val = key?.GetValue("AccentColor");
            if (val is int raw)
            {
                // DWM stores as ABGR
                uint v = (uint)raw;
                int a = (int)((v >> 24) & 0xFF);
                int b = (int)((v >> 16) & 0xFF);
                int g = (int)((v >> 8) & 0xFF);
                int r = (int)(v & 0xFF);
                if (a == 0) a = 255;
                return Color.FromArgb(a, r, g, b);
            }
        }
        catch { /* ignore */ }
        return Color.FromArgb(99, 102, 241); // fallback indigo
    }

    private static Color Darken(Color c, float amount)
    {
        float factor = 1f - amount;
        return Color.FromArgb(c.A,
            (int)(c.R * factor),
            (int)(c.G * factor),
            (int)(c.B * factor));
    }

    private static Color Lighten(Color c, float amount)
    {
        return Color.FromArgb(c.A,
            c.R + (int)((255 - c.R) * amount),
            c.G + (int)((255 - c.G) * amount),
            c.B + (int)((255 - c.B) * amount));
    }
}
