using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed class TrayApplicationContext : ApplicationContext
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_SHIFT = 0x10;
    private const byte VK_C = 0x43;
    private const byte VK_V = 0x56;
    private const byte VK_A = 0x41;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly LlmClient _llmClient;
    private readonly ContextMenuStrip _styleMenu;
    private readonly ToolStripMenuItem _translateMenu;
    private readonly Form _helperForm;
    private readonly MouseHookWindow _mouseHook;
    private ToolStripMenuItem _profileMenuItem = null!;

    private IntPtr _sourceWindow;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    private static readonly (string Label, string StyleName, string Prompt)[] Styles =
    [
        ("Rephrase", "Rephrase",
            "You are a writing assistant. Rephrase the user's text to improve clarity and readability while preserving the original meaning. Return ONLY the rephrased text, nothing else."),
        ("Make Formal", "Make Formal",
            "You are a writing assistant. Rewrite the user's text in a more formal, professional tone. Preserve the original meaning. Return ONLY the rewritten text, nothing else."),
        ("Make Concise", "Make Concise",
            "You are a writing assistant. Rewrite the user's text to be more concise and to the point. Remove unnecessary words while preserving meaning. Return ONLY the rewritten text, nothing else."),
        ("Fix Grammar", "Fix Grammar",
            "You are a grammar checker. Fix any grammar, spelling, and punctuation errors in the user's text. Preserve the original tone and meaning. Return ONLY the corrected text, nothing else.")
    ];

    private static string TranslationPrompt(string lang) =>
        $"You are a translator. Translate the user's text into {lang}. Preserve the original tone and meaning. Return ONLY the translated text, nothing else.";

    public TrayApplicationContext()
    {
        _llmClient = new LlmClient();

        // Invisible helper form to own the context menu and ensure it gets foreground focus
        _helperForm = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            Size = new Size(1, 1),
            Opacity = 0,
            TopMost = true
        };
        _helperForm.Load += (_, _) => _helperForm.Hide();

        // Style picker context menu
        _styleMenu = new ContextMenuStrip();
        _styleMenu.Closed += (_, _) => _helperForm.Hide();

        foreach (var (label, styleName, prompt) in Styles)
        {
            var item = new ToolStripMenuItem(label) { Tag = (styleName, prompt) };
            item.Click += StyleItem_Click;
            _styleMenu.Items.Add(item);
        }

        _styleMenu.Items.Add(new ToolStripSeparator());

        _translateMenu = new ToolStripMenuItem("Translate");
        _styleMenu.Items.Add(_translateMenu);
        _styleMenu.Opening += (_, _) => RebuildTranslateMenu();

        // Tray icon
        _trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "LLM-Rephraser — Ctrl+Shift+R",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        // Hotkey
        _hotkeyWindow = new HotkeyWindow();
        _hotkeyWindow.HotkeyPressed += OnHotkeyPressed;
        if (!_hotkeyWindow.Register())
        {
            MessageBox.Show(
                "Failed to register global hotkey Ctrl+Shift+R.\nAnother application may be using it.",
                "LLM-Rephraser",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        // Shift+Right-Click mouse hook
        _mouseHook = new MouseHookWindow();
        _mouseHook.ShiftRightClickDetected += OnHotkeyPressed;
        ApplyMouseHookSetting();

        // Show settings on first run
        if (!AppConfig.Exists())
        {
            ShowSettings();
        }
    }

    private ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip();

        _profileMenuItem = new ToolStripMenuItem("Profile");
        menu.Items.Add(_profileMenuItem);
        menu.Opening += (_, _) => RebuildProfileSubmenu();

        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);
        menu.Items.Add(new ToolStripSeparator());
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);
        return menu;
    }

    private void RebuildProfileSubmenu()
    {
        _profileMenuItem.DropDownItems.Clear();
        var config = AppConfig.Load();
        foreach (var name in config.Profiles.Keys.OrderBy(k => k))
        {
            var item = new ToolStripMenuItem(name)
            {
                Checked = name == config.ActiveProfile,
                Tag = name
            };
            item.Click += (s, _) =>
            {
                var profileName = (string)((ToolStripMenuItem)s!).Tag!;
                var cfg = AppConfig.Load();
                cfg.ActiveProfile = profileName;
                cfg.Save();
                _trayIcon.ShowBalloonTip(1500, "LLM-Rephraser", $"Switched to \"{profileName}\"", ToolTipIcon.Info);
            };
            _profileMenuItem.DropDownItems.Add(item);
        }
    }

    private void RebuildTranslateMenu()
    {
        _translateMenu.DropDownItems.Clear();
        var config = AppConfig.Load();
        foreach (var lang in config.TranslationLanguages)
        {
            var item = new ToolStripMenuItem(lang)
            {
                Tag = ($"Translate to {lang}", TranslationPrompt(lang))
            };
            item.Click += StyleItem_Click;
            _translateMenu.DropDownItems.Add(item);
        }
        _translateMenu.Enabled = _translateMenu.DropDownItems.Count > 0;
    }

    private void ApplyMouseHookSetting()
    {
        var config = AppConfig.Load();
        if (config.ShiftRightClickEnabled)
            _mouseHook.Install();
        else
            _mouseHook.Uninstall();
    }

    private void ShowSettings()
    {
        var config = AppConfig.Load();
        using var form = new SettingsForm(config);
        form.ShowDialog();
        ApplyMouseHookSetting();
    }

    private async void OnHotkeyPressed()
    {
        if (_isBusy) return;

        _sourceWindow = GetForegroundWindow();

        // Save current clipboard
        IDataObject? savedClipboard = null;
        try
        {
            savedClipboard = Clipboard.GetDataObject();
        }
        catch { /* clipboard may be locked */ }

        // Release modifier keys that are still physically held from the hotkey combo
        ReleaseModifierKeys();
        await Task.Delay(150);

        // Simulate Ctrl+C to copy selected text
        SimulateCtrlC();
        await Task.Delay(200);

        // Read copied text
        string selectedText;
        try
        {
            if (!Clipboard.ContainsText())
            {
                _trayIcon.ShowBalloonTip(2000, "LLM-Rephraser", "No text selected", ToolTipIcon.Info);
                return;
            }
            selectedText = Clipboard.GetText();
        }
        catch
        {
            _trayIcon.ShowBalloonTip(2000, "LLM-Rephraser", "Could not access clipboard", ToolTipIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            _trayIcon.ShowBalloonTip(2000, "LLM-Rephraser", "No text selected", ToolTipIcon.Info);
            return;
        }

        // Store text and clipboard for later
        _styleMenu.Tag = (selectedText, savedClipboard);

        // Show style picker at cursor — use TopMost helper to ensure visibility
        var pos = Cursor.Position;
        _helperForm.Location = new Point(pos.X - 1, pos.Y - 1);
        _helperForm.Show();
        SetForegroundWindow(_helperForm.Handle);
        _styleMenu.Show(pos);
    }

    private async void StyleItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item) return;

        var (styleName, prompt) = ((string, string))item.Tag!;
        var (selectedText, savedClipboard) = ((string, IDataObject?))_styleMenu.Tag!;

        var config = AppConfig.Load();
        var profile = config.Active;
        if (string.IsNullOrWhiteSpace(profile.ApiEndpoint))
        {
            MessageBox.Show("Please configure the API endpoint in Settings.", "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ShowSettings();
            return;
        }

        _isBusy = true;
        _cts = new CancellationTokenSource();

        string suggestion;
        try
        {
            // Show loading feedback
            _trayIcon.ShowBalloonTip(1500, "LLM-Rephraser", $"Rephrasing ({config.ActiveProfile})...", ToolTipIcon.None);

            suggestion = await _llmClient.SendAsync(profile, prompt, selectedText, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (_cts?.IsCancellationRequested == true)
            {
                // User-initiated cancellation
                RestoreClipboard(savedClipboard);
                _isBusy = false;
                return;
            }
            // HTTP timeout
            MessageBox.Show("Request timed out. Check your API endpoint and try again.",
                "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RestoreClipboard(savedClipboard);
            _isBusy = false;
            return;
        }
        catch (LlmException ex)
        {
            MessageBox.Show(ex.Message, "LLM-Rephraser — API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RestoreClipboard(savedClipboard);
            _isBusy = false;
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unexpected error:\n{ex.Message}", "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RestoreClipboard(savedClipboard);
            _isBusy = false;
            return;
        }

        // Show result dialog
        using var resultForm = new ResultForm(styleName, selectedText, suggestion);
        resultForm.ShowDialog();

        if (resultForm.Accepted)
        {
            // Put suggestion on clipboard and paste
            Clipboard.SetText(resultForm.SuggestedText);
            await Task.Delay(100);

            SetForegroundWindow(_sourceWindow);
            await Task.Delay(100);

            // Select all in the field, then paste
            SimulateCtrlA();
            await Task.Delay(50);
            SimulateCtrlV();
            await Task.Delay(150);
        }

        // Restore original clipboard
        RestoreClipboard(savedClipboard);
        _isBusy = false;
    }

    private static void RestoreClipboard(IDataObject? saved)
    {
        try
        {
            if (saved != null)
            {
                // Best effort restore
                if (saved.GetDataPresent(DataFormats.UnicodeText))
                {
                    var text = saved.GetData(DataFormats.UnicodeText) as string;
                    if (text != null)
                        Clipboard.SetText(text);
                }
                else if (saved.GetDataPresent(DataFormats.Text))
                {
                    var text = saved.GetData(DataFormats.Text) as string;
                    if (text != null)
                        Clipboard.SetText(text);
                }
            }
        }
        catch { /* ignore clipboard errors during restore */ }
    }

    private static void ReleaseModifierKeys()
    {
        // Force-release Ctrl, Shift, Alt that may still be held from the hotkey
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(0x12 /* VK_MENU / Alt */, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SimulateCtrlC()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, 0, UIntPtr.Zero);
        keybd_event(VK_C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SimulateCtrlV()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, 0, UIntPtr.Zero);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SimulateCtrlA()
    {
        keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
        keybd_event(VK_A, 0, 0, UIntPtr.Zero);
        keybd_event(VK_A, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static Icon CreateTrayIcon()
    {
        const int sz = 32;
        var bmp = new Bitmap(sz, sz);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // Rounded-rect background with gradient
            var bgRect = new Rectangle(1, 1, sz - 2, sz - 2);
            using var bgPath = RoundedRect(bgRect, 7);
            using var bgBrush = new System.Drawing.Drawing2D.LinearGradientBrush(
                bgRect, Color.FromArgb(56, 132, 244), Color.FromArgb(28, 90, 200),
                System.Drawing.Drawing2D.LinearGradientMode.ForwardDiagonal);
            g.FillPath(bgBrush, bgPath);

            // Draw two curved arrows forming a cycle (rephrase symbol)
            using var pen = new Pen(Color.White, 2.4f)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Custom,
                CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(3f, 3f, true)
            };

            // Top arc: sweeps clockwise from left to right
            g.DrawArc(pen, 8, 6, 16, 14, 200, 160);
            // Bottom arc: sweeps clockwise from right to left
            g.DrawArc(pen, 8, 12, 16, 14, 20, 160);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private void ExitApplication()
    {
        _cts?.Cancel();
        _mouseHook.Dispose();
        _hotkeyWindow.Dispose();
        _llmClient.Dispose();
        _helperForm.Dispose();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Cancel();
            _mouseHook.Dispose();
            _hotkeyWindow.Dispose();
            _llmClient.Dispose();
            _helperForm.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
