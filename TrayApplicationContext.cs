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
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyWindow _hotkeyWindow;
    private readonly LlmClient _llmClient;
    private readonly ContextMenuStrip _styleMenu;
    private readonly ToolStripMenuItem _translateMenu;
    private readonly Form _helperForm;
    private readonly EventWaitHandle _rephraseEvent;
    private readonly System.Windows.Forms.Timer _rephraseEventTimer;
    private ToolStripMenuItem _profileMenuItem = null!;

    private IntPtr _sourceWindow;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    private static readonly (string Label, string StyleName, string Prompt)[] Styles =
    [
        ("Rephrase", "Rephrase",
            "You are a writing assistant. Rephrase the user's text to improve clarity and readability while preserving the original meaning. IMPORTANT: First, carefully identify the exact language of the input (e.g. Hebrew and Arabic are distinct languages — do not confuse them). Your output MUST be in the same language as the input. Return ONLY the rephrased text, nothing else."),
        ("Make Formal", "Make Formal",
            "You are a writing assistant. Rewrite the user's text in a more formal, professional tone. Preserve the original meaning. IMPORTANT: First, carefully identify the exact language of the input (e.g. Hebrew and Arabic are distinct languages — do not confuse them). Your output MUST be in the same language as the input. Return ONLY the rewritten text, nothing else."),
        ("Make Concise", "Make Concise",
            "You are a writing assistant. Rewrite the user's text to be more concise and to the point. Remove unnecessary words while preserving meaning. IMPORTANT: First, carefully identify the exact language of the input (e.g. Hebrew and Arabic are distinct languages — do not confuse them). Your output MUST be in the same language as the input. Return ONLY the rewritten text, nothing else."),
        ("Fix Grammar", "Fix Grammar",
            "You are a grammar checker. Fix any grammar, spelling, and punctuation errors in the user's text. Preserve the original tone and meaning. IMPORTANT: First, carefully identify the exact language of the input (e.g. Hebrew and Arabic are distinct languages — do not confuse them). Your output MUST be in the same language as the input. Return ONLY the corrected text, nothing else.")
    ];

    private static string TranslationPrompt(string lang) =>
        $"You are a writing assistant and translator. Translate the user's text into {lang}, and rephrase it to sound natural and fluent in {lang} — not like a literal translation. Preserve the original meaning and tone. Return ONLY the translated and rephrased text, nothing else.";

    public TrayApplicationContext()
    {
        // Apply saved theme
        var savedConfig = AppConfig.Load();
        ThemeColors.SetMode(savedConfig.Theme);

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
        _styleMenu = new ContextMenuStrip { ShowImageMargin = true };
        _styleMenu.Closed += (_, _) => _helperForm.Hide();

        foreach (var (label, styleName, prompt) in Styles)
        {
            var item = new ToolStripMenuItem(label, CreateStyleIcon(styleName))
            {
                Tag = (styleName, prompt)
            };
            item.Click += StyleItem_Click;
            _styleMenu.Items.Add(item);
        }

        _styleMenu.Items.Add(new ToolStripSeparator());

        _translateMenu = new ToolStripMenuItem("Translate to:", CreateTranslateIcon());
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

        // IPC: listen for rephrase signal from context menu / second instance
        _rephraseEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.RephraseEventName);
        _rephraseEventTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _rephraseEventTimer.Tick += (_, _) =>
        {
            if (_rephraseEvent.WaitOne(0))
                OnClipboardRephrase();
        };
        _rephraseEventTimer.Start();

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

        var aboutItem = new ToolStripMenuItem("About...");
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

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
            var item = new ToolStripMenuItem(lang, CreateFlagIcon(lang))
            {
                Tag = ($"Translate to {lang}", TranslationPrompt(lang))
            };
            item.Click += StyleItem_Click;
            _translateMenu.DropDownItems.Add(item);
        }
        _translateMenu.Enabled = _translateMenu.DropDownItems.Count > 0;
    }

    private void ShowAbout()
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var ver = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        using var dlg = new Form();
        dlg.Text = "About LLM-Rephraser";
        dlg.ClientSize = new Size(400, 280);
        dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
        dlg.MaximizeBox = false;
        dlg.MinimizeBox = false;
        dlg.StartPosition = FormStartPosition.CenterScreen;
        dlg.BackColor = ThemeColors.BgPage;

        var card = new Panel
        {
            Location = new Point(16, 16),
            Size = new Size(368, 150),
            BackColor = ThemeColors.BgCard,
            BorderStyle = BorderStyle.None
        };
        card.Paint += (s, e) =>
        {
            var r = card.ClientRectangle;
            r.Width -= 1;
            r.Height -= 1;
            using var pen = new Pen(ThemeColors.BorderCard);
            e.Graphics.DrawRectangle(pen, r);
        };

        var iconBox = new PictureBox
        {
            Location = new Point(16, 12),
            Size = new Size(64, 64),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Image = new Icon(Path.Combine(AppContext.BaseDirectory, "rephrase-tool.ico"), 64, 64).ToBitmap()
        };

        var title = new Label
        {
            Text = "LLM-Rephraser",
            Location = new Point(90, 12),
            AutoSize = true,
            ForeColor = ThemeColors.TextBody,
            Font = new Font("Segoe UI", 16f, FontStyle.Bold),
            BackColor = Color.Transparent
        };

        var versionLabel = new Label
        {
            Text = $"v{ver}",
            Location = new Point(90, 48),
            AutoSize = true,
            Font = new Font("Segoe UI", 11f),
            ForeColor = ThemeColors.Accent,
            BackColor = Color.Transparent
        };

        var desc = new Label
        {
            Text = "A Windows system tray tool for rephrasing,\ntranslating, and fixing text in any application\nusing LLM APIs.",
            Location = new Point(16, 84),
            Size = new Size(340, 50),
            Font = new Font("Segoe UI", 9f),
            ForeColor = ThemeColors.TextBody,
            BackColor = Color.Transparent
        };

        var link = new LinkLabel
        {
            Text = "github.com/davidturchak/LLM-rephraser",
            Location = new Point(16, 130),
            AutoSize = true,
            LinkColor = ThemeColors.Accent,
            BackColor = Color.Transparent
        };
        link.LinkClicked += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/davidturchak/LLM-rephraser",
                UseShellExecute = true
            });
        };

        card.Controls.AddRange([iconBox, title, versionLabel, desc, link]);

        var okButton = new Button
        {
            Text = "OK",
            Size = new Size(80, 36),
            Location = new Point(304, 236),
            FlatStyle = FlatStyle.Flat,
            BackColor = ThemeColors.Accent,
            ForeColor = ThemeColors.AccentOnAccent,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            DialogResult = DialogResult.OK
        };
        okButton.FlatAppearance.BorderSize = 0;

        dlg.Controls.AddRange([card, okButton]);
        dlg.AcceptButton = okButton;
        dlg.ShowDialog();
    }

    private void ShowSettings()
    {
        var config = AppConfig.Load();
        using var form = new SettingsForm(config);
        form.ShowDialog();
    }

    private async void OnHotkeyPressed()
    {
        if (_isBusy) return;

        _sourceWindow = GetForegroundWindow();

        // Detect if the source is an editable text field
        bool isEditable = await EditableFieldDetector.IsEditableAsync(_sourceWindow);

        // Save current clipboard
        IDataObject? savedClipboard = null;
        try
        {
            savedClipboard = Clipboard.GetDataObject();
        }
        catch { /* clipboard may be locked */ }

        // Release modifier keys and wait until they are physically released
        ReleaseModifierKeys();
        await WaitForModifierRelease();

        // Ensure source window still has focus, then copy
        SetForegroundWindow(_sourceWindow);
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
        _styleMenu.Tag = (selectedText, savedClipboard, isEditable);

        // Show style picker at cursor — use TopMost helper to ensure visibility
        var pos = Cursor.Position;
        _helperForm.Location = new Point(pos.X - 1, pos.Y - 1);
        _helperForm.Show();
        SetForegroundWindow(_helperForm.Handle);
        _styleMenu.Show(pos);
        if (_styleMenu.Items.Count > 0)
            _styleMenu.Items[0].Select();
    }

    /// <summary>
    /// Called when the Windows context menu entry signals us.
    /// Uses whatever text is currently on the clipboard.
    /// </summary>
    private void OnClipboardRephrase()
    {
        if (_isBusy) return;

        _sourceWindow = GetForegroundWindow();

        string clipText;
        try
        {
            if (!Clipboard.ContainsText())
            {
                _trayIcon.ShowBalloonTip(2000, "LLM-Rephraser",
                    "Copy some text first, then use the context menu.", ToolTipIcon.Info);
                return;
            }
            clipText = Clipboard.GetText();
        }
        catch
        {
            _trayIcon.ShowBalloonTip(2000, "LLM-Rephraser",
                "Could not access clipboard.", ToolTipIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(clipText))
        {
            _trayIcon.ShowBalloonTip(2000, "LLM-Rephraser",
                "Clipboard is empty. Copy text first.", ToolTipIcon.Info);
            return;
        }

        // Store text (no saved clipboard to restore — user explicitly copied)
        _styleMenu.Tag = (clipText, (IDataObject?)null, false);

        // Show style picker at cursor
        var pos = Cursor.Position;
        _helperForm.Location = new Point(pos.X - 1, pos.Y - 1);
        _helperForm.Show();
        SetForegroundWindow(_helperForm.Handle);
        _styleMenu.Show(pos);
        if (_styleMenu.Items.Count > 0)
            _styleMenu.Items[0].Select();
    }

    private async void StyleItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item) return;

        var (styleName, prompt) = ((string, string))item.Tag!;
        var (selectedText, savedClipboard, isEditable) = ((string, IDataObject?, bool))_styleMenu.Tag!;

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
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Show loading feedback
            _trayIcon.ShowBalloonTip(1500, "LLM-Rephraser", $"Rephrasing ({config.ActiveProfile})...", ToolTipIcon.None);

            AppLogger.LogRequest(profile.ApiEndpoint, profile.ModelName, styleName, selectedText);
            suggestion = await _llmClient.SendAsync(profile, prompt, selectedText, _cts.Token);
            sw.Stop();
            AppLogger.LogResponse(styleName, suggestion, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            if (_cts?.IsCancellationRequested == true)
            {
                AppLogger.LogError(styleName, "Cancelled by user", sw.Elapsed);
                RestoreClipboard(savedClipboard);
                _isBusy = false;
                return;
            }
            AppLogger.LogError(styleName, "Request timed out", sw.Elapsed);
            MessageBox.Show("Request timed out. Check your API endpoint and try again.",
                "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            RestoreClipboard(savedClipboard);
            _isBusy = false;
            return;
        }
        catch (LlmException ex)
        {
            sw.Stop();
            AppLogger.LogError(styleName, ex.Message, sw.Elapsed);
            MessageBox.Show(ex.Message, "LLM-Rephraser — API Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RestoreClipboard(savedClipboard);
            _isBusy = false;
            return;
        }
        catch (Exception ex)
        {
            sw.Stop();
            AppLogger.LogError(styleName, ex.Message, sw.Elapsed);
            MessageBox.Show($"Unexpected error:\n{ex.Message}", "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Error);
            RestoreClipboard(savedClipboard);
            _isBusy = false;
            return;
        }

        // Show result dialog. Use the same helper-form foreground trick we
        // use for the style menu — after a long API wait, Windows often
        // denies foreground rights so the dialog ends up flashing in the
        // taskbar instead of appearing on top.
        var pos = Cursor.Position;
        _helperForm.Location = new Point(pos.X - 1, pos.Y - 1);
        _helperForm.Show();
        SetForegroundWindow(_helperForm.Handle);

        using var resultForm = new ResultForm(styleName, selectedText, suggestion, isEditable);
        resultForm.ShowDialog(_helperForm);
        _helperForm.Hide();

        if (resultForm.Accepted)
        {
            // Put suggestion on clipboard and paste
            Clipboard.SetText(resultForm.SuggestedText);
            await Task.Delay(100);

            SetForegroundWindow(_sourceWindow);
            await Task.Delay(100);

            // Paste over the still-active selection
            SimulateCtrlV();
            await Task.Delay(150);

            // Restore original clipboard
            RestoreClipboard(savedClipboard);
        }
        else if (resultForm.Copied)
        {
            // Just copy to clipboard, don't paste
            Clipboard.SetText(resultForm.SuggestedText);
            _trayIcon.ShowBalloonTip(1500, "LLM-Rephraser", "Suggestion copied to clipboard", ToolTipIcon.Info);
        }
        else
        {
            // Cancelled — restore original clipboard
            RestoreClipboard(savedClipboard);
        }

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

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static async Task WaitForModifierRelease()
    {
        // Wait until Ctrl, Shift, Alt are all released (max ~1.5s)
        for (int i = 0; i < 60; i++)
        {
            bool anyHeld = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0
                        || (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0
                        || (GetAsyncKeyState(0x12) & 0x8000) != 0;
            if (!anyHeld) break;
            await Task.Delay(25);
        }
        await Task.Delay(50); // small extra settle time
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

    private static Icon CreateTrayIcon()
    {
        var icoPath = Path.Combine(AppContext.BaseDirectory, "rephrase-tool.ico");
        if (File.Exists(icoPath))
            return new Icon(icoPath, 32, 32);

        // Fallback: extract from exe
        var exeIcon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        return exeIcon != null ? new Icon(exeIcon, 32, 32) : SystemIcons.Application;
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

    // ── Menu icon helpers ───────────────────────────────────────────────

    private static Bitmap CreateStyleIcon(string style)
    {
        var bmp = new Bitmap(18, 18);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        switch (style)
        {
            case "Rephrase":
                // Two curved arrows (cycle) — blue
                using (var pen = new Pen(Color.FromArgb(59, 130, 246), 1.8f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Custom;
                    pen.CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(2.5f, 2.5f, true);
                    g.DrawArc(pen, 3, 2, 12, 9, 200, 140);
                    g.DrawArc(pen, 3, 7, 12, 9, 20, 140);
                }
                break;

            case "Make Formal":
                // Tie shape — indigo
                var indigo = Color.FromArgb(99, 102, 241);
                using (var brush = new SolidBrush(indigo))
                {
                    // Knot
                    g.FillEllipse(brush, 7, 2, 5, 4);
                    // Tie body
                    var tie = new PointF[] { new(7, 5), new(11, 5), new(10, 15), new(9, 13), new(8, 15) };
                    g.FillPolygon(brush, tie);
                }
                break;

            case "Make Concise":
                // Inward arrows — orange
                using (var pen = new Pen(Color.FromArgb(234, 88, 12), 2f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Custom;
                    pen.CustomEndCap = new System.Drawing.Drawing2D.AdjustableArrowCap(2.5f, 2.5f, true);
                    g.DrawLine(pen, 2, 9, 7, 9);   // left arrow
                    g.DrawLine(pen, 16, 9, 11, 9);  // right arrow
                }
                break;

            case "Fix Grammar":
                // Checkmark — green
                using (var pen = new Pen(Color.FromArgb(22, 163, 74), 2.5f))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    g.DrawLines(pen, [new Point(3, 9), new Point(7, 14), new Point(15, 4)]);
                }
                break;
        }
        return bmp;
    }

    private static Bitmap CreateTranslateIcon()
    {
        // Globe icon — blue
        var bmp = new Bitmap(18, 18);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        var blue = Color.FromArgb(59, 130, 246);
        using var pen = new Pen(blue, 1.4f);

        // Circle
        g.DrawEllipse(pen, 2, 2, 14, 14);
        // Vertical meridian
        g.DrawEllipse(pen, 5, 2, 8, 14);
        // Horizontal lines (latitudes)
        g.DrawLine(pen, 2, 9, 16, 9);
        g.DrawLine(pen, 3, 5, 15, 5);
        g.DrawLine(pen, 3, 13, 15, 13);

        return bmp;
    }

    private static readonly Dictionary<string, string> FlagMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["english"]    = "🇬🇧", ["en"]      = "🇬🇧",
        ["hebrew"]     = "🇮🇱", ["he"]      = "🇮🇱", ["עברית"] = "🇮🇱",
        ["arabic"]     = "🇸🇦", ["ar"]      = "🇸🇦", ["العربية"] = "🇸🇦",
        ["russian"]    = "🇷🇺", ["ru"]      = "🇷🇺", ["русский"] = "🇷🇺",
        ["french"]     = "🇫🇷", ["fr"]      = "🇫🇷",
        ["german"]     = "🇩🇪", ["de"]      = "🇩🇪",
        ["spanish"]    = "🇪🇸", ["es"]      = "🇪🇸",
        ["italian"]    = "🇮🇹", ["it"]      = "🇮🇹",
        ["portuguese"] = "🇧🇷", ["pt"]      = "🇧🇷",
        ["chinese"]    = "🇨🇳", ["zh"]      = "🇨🇳",
        ["japanese"]   = "🇯🇵", ["ja"]      = "🇯🇵",
        ["korean"]     = "🇰🇷", ["ko"]      = "🇰🇷",
        ["turkish"]    = "🇹🇷", ["tr"]      = "🇹🇷",
        ["polish"]     = "🇵🇱", ["pl"]      = "🇵🇱",
        ["dutch"]      = "🇳🇱", ["nl"]      = "🇳🇱",
        ["swedish"]    = "🇸🇪", ["sv"]      = "🇸🇪",
        ["ukrainian"]  = "🇺🇦", ["uk"]      = "🇺🇦",
        ["hindi"]      = "🇮🇳", ["hi"]      = "🇮🇳",
        ["thai"]       = "🇹🇭", ["th"]      = "🇹🇭",
        ["vietnamese"]  = "🇻🇳", ["vi"]      = "🇻🇳",
        ["czech"]      = "🇨🇿", ["cs"]      = "🇨🇿",
        ["greek"]      = "🇬🇷", ["el"]      = "🇬🇷",
        ["romanian"]   = "🇷🇴", ["ro"]      = "🇷🇴",
        ["hungarian"]  = "🇭🇺", ["hu"]      = "🇭🇺",
        ["finnish"]    = "🇫🇮", ["fi"]      = "🇫🇮",
        ["danish"]     = "🇩🇰", ["da"]      = "🇩🇰",
        ["norwegian"]  = "🇳🇴", ["no"]      = "🇳🇴",
        ["persian"]    = "🇮🇷", ["fa"]      = "🇮🇷",
        ["indonesian"] = "🇮🇩", ["id"]      = "🇮🇩",
        ["malay"]      = "🇲🇾", ["ms"]      = "🇲🇾",
    };

    private static Bitmap CreateFlagIcon(string language)
    {
        // Try to find matching flag emoji
        if (!FlagMap.TryGetValue(language.Trim(), out var flag))
            flag = "🌐"; // globe fallback

        var bmp = new Bitmap(20, 18);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.Clear(Color.Transparent);

        using var font = new Font("Segoe UI Emoji", 11f);
        TextRenderer.DrawText(g, flag, font,
            new Rectangle(-2, -2, 24, 22), Color.Black,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

        return bmp;
    }

    private void ExitApplication()
    {
        _cts?.Cancel();
        _rephraseEventTimer.Stop();
        _rephraseEventTimer.Dispose();
        _rephraseEvent.Dispose();
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
            _rephraseEventTimer.Stop();
            _rephraseEventTimer.Dispose();
            _rephraseEvent.Dispose();
            _hotkeyWindow.Dispose();
            _llmClient.Dispose();
            _helperForm.Dispose();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
