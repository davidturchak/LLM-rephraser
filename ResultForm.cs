using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed class ResultForm : Form
{
    private readonly RichTextBox _suggestedBox;

    public string SuggestedText => _suggestedBox.Text;
    public bool Accepted { get; private set; }
    public bool Copied { get; private set; }

    // ── RTL detection ────────────────────────────────────────────────────
    private static bool IsRtl(string text) =>
        text.Any(c => c is (>= '\u0590' and <= '\u05FF')
                        or (>= '\u0600' and <= '\u06FF')
                        or (>= '\u0750' and <= '\u077F')
                        or (>= '\u08A0' and <= '\u08FF')
                        or (>= '\uFB50' and <= '\uFDFF')
                        or (>= '\uFE70' and <= '\uFEFF'));

    // ── Line count estimate ──────────────────────────────────────────────
    private static int MeasureLines(string text, Font font, int width)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        float cw = g.MeasureString("W", font).Width;
        int cpl = Math.Max(1, (int)(width / cw));
        return text.Split('\n')
            .Sum(raw => Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, raw.Length) / cpl)));
    }

    // ── Gold accent color ────────────────────────────────────────────────
    private static readonly Color Gold = Color.FromArgb(184, 149, 42);
    private static readonly Color GoldHover = Color.FromArgb(164, 132, 36);

    // ── Dark surface palette ─────────────────────────────────────────────
    private static Color BgForm      => ThemeColors.IsDark ? Color.FromArgb(30, 30, 33)  : Color.FromArgb(248, 249, 251);
    private static Color BgOrigField => ThemeColors.IsDark ? Color.FromArgb(14, 14, 14)  : Color.FromArgb(235, 237, 240);
    private static Color FgOrigText  => ThemeColors.IsDark ? Color.FromArgb(102, 102, 102) : Color.FromArgb(120, 130, 150);
    private static Color BgSuggField => ThemeColors.IsDark ? Color.FromArgb(38, 38, 42)  : Color.FromArgb(255, 255, 255);
    private static Color Separator   => ThemeColors.IsDark ? Color.FromArgb(55, 55, 60)  : Color.FromArgb(220, 224, 230);
    private static Color OrigAccent  => ThemeColors.IsDark ? Color.FromArgb(85, 85, 85)  : Color.FromArgb(180, 185, 195);

    // ── Accent bar references for resize sync ────────────────────────────
    private readonly Panel _origAccentBar;
    private readonly RichTextBox _originalBox;
    private readonly Panel _suggAccentBar;

    public ResultForm(string styleName, string originalText, string suggestedText,
                      bool isEditable = true)
    {
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = $"LLM-Rephraser \u2014 {styleName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = BgForm;
        ForeColor = ThemeColors.TextBody;
        Font = new Font("Segoe UI", 10f);
        DoubleBuffered = true;

        // ── DPI / screen metrics ─────────────────────────────────────────
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        float dpiScale;
        using (var gfx = Graphics.FromHwnd(IntPtr.Zero)) { dpiScale = gfx.DpiX / 96f; }
        int availW = (int)(workArea.Width / dpiScale);
        int availH = (int)(workArea.Height / dpiScale);

        int formW = Math.Clamp(availW * 2 / 5, 400, 600);

        // ── Fonts ────────────────────────────────────────────────────────
        var labelFont   = new Font("Segoe UI", 8f, FontStyle.Bold);
        var hintFont    = new Font("Segoe UI", 8f, FontStyle.Italic);
        var bodyFont    = new Font("Segoe UI", 10f);
        var charFont    = new Font("Segoe UI", 7.5f);
        var btnFont     = new Font("Segoe UI", 9.5f);
        var btnBoldFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        bool origRtl = IsRtl(originalText);
        bool suggRtl = IsRtl(suggestedText);

        const int pad = 20;
        const int accentW = 3;
        const int minLines = 2;
        const int maxLines = 10;
        const int lineH = 22;
        int textW = formW - pad * 2 - accentW - 6; // account for accent bar + gap

        int origLines = Math.Clamp(MeasureLines(originalText, bodyFont, textW), minLines, maxLines);
        int suggLines = Math.Clamp(MeasureLines(suggestedText, bodyFont, textW), minLines, maxLines);
        int origBoxH = origLines * lineH + 8;
        int suggBoxH = suggLines * lineH + 8;

        // ═══════════════════════════════════════════════════════════════════
        //  TableLayoutPanel — main vertical stack (8 rows)
        // ═══════════════════════════════════════════════════════════════════
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = false,
            Padding = new Padding(pad, pad, pad, pad),
            BackColor = BgForm
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        // ── Row 0: ORIGINAL label (auto height) ─────────────────────────
        var origLabel = new Label
        {
            Text = "ORIGINAL",
            Font = labelFont,
            ForeColor = ThemeColors.TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6),
            BackColor = BgForm
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(origLabel, 0, 0);

        // ── Row 1: Original accent bar + RichTextBox (fixed height) ─────
        var origContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgOrigField,
            Margin = new Padding(0)
        };

        // Gray accent bar for ORIGINAL
        _origAccentBar = new Panel
        {
            Width = accentW,
            Dock = DockStyle.Left,
            BackColor = BgOrigField
        };
        _origAccentBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            int r = 4;
            var rect = new Rectangle(0, 0, _origAccentBar.Width + r, _origAccentBar.Height);
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddLine(rect.Right, rect.Y, rect.Right, rect.Bottom);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            using var brush = new SolidBrush(OrigAccent);
            g.FillPath(brush, path);
        };

        _originalBox = new RichTextBox
        {
            Text = originalText,
            Font = bodyFont,
            ReadOnly = true,
            BackColor = BgOrigField,
            ForeColor = FgOrigText,
            BorderStyle = BorderStyle.None,
            ScrollBars = origLines >= maxLines ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None,
            WordWrap = true,
            DetectUrls = false,
            RightToLeft = origRtl ? RightToLeft.Yes : RightToLeft.No,
            TabStop = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 0, 0, 0)
        };

        origContainer.Controls.Add(_originalBox);
        origContainer.Controls.Add(_origAccentBar);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, origBoxH));
        layout.Controls.Add(origContainer, 0, 1);

        // ── Row 2: Spacer (fixed 16px) ──────────────────────────────────
        var spacer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgForm,
            Margin = new Padding(0)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));
        layout.Controls.Add(spacer, 0, 2);

        // ── Row 3: SUGGESTION label row (auto height) ───────────────────
        var suggLabelPanel = new Panel
        {
            Height = 20,
            Dock = DockStyle.Fill,
            BackColor = BgForm,
            Margin = new Padding(0, 0, 0, 6)
        };

        var suggLabel = new Label
        {
            Text = "SUGGESTION",
            Font = labelFont,
            ForeColor = Gold,
            AutoSize = true,
            Location = new Point(0, 0),
            BackColor = BgForm
        };

        var suggHint = new Label
        {
            Text = "you can edit before accepting",
            Font = hintFont,
            ForeColor = ThemeColors.TextMuted,
            AutoSize = true,
            BackColor = BgForm
        };
        suggHint.Location = new Point(suggLabel.PreferredWidth + 10, 1);

        suggLabelPanel.Controls.AddRange([suggLabel, suggHint]);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(suggLabelPanel, 0, 3);

        // ── Row 4: Suggestion accent bar + RichTextBox (fixed height) ───
        var suggContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgSuggField,
            Margin = new Padding(0)
        };

        // Gold accent bar with rounded left corners
        _suggAccentBar = new Panel
        {
            Width = accentW,
            Dock = DockStyle.Left,
            BackColor = BgSuggField
        };
        _suggAccentBar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath();
            int r = 4;
            var rect = new Rectangle(0, 0, _suggAccentBar.Width + r, _suggAccentBar.Height);
            path.AddArc(rect.X, rect.Y, r, r, 180, 90);
            path.AddLine(rect.Right, rect.Y, rect.Right, rect.Bottom);
            path.AddArc(rect.X, rect.Bottom - r, r, r, 90, 90);
            path.CloseFigure();
            using var brush = new SolidBrush(Gold);
            g.FillPath(brush, path);
        };

        _suggestedBox = new RichTextBox
        {
            Text = suggestedText,
            Font = bodyFont,
            BackColor = BgSuggField,
            ForeColor = ThemeColors.TextBody,
            BorderStyle = BorderStyle.None,
            ScrollBars = suggLines >= maxLines ? RichTextBoxScrollBars.Vertical : RichTextBoxScrollBars.None,
            WordWrap = true,
            DetectUrls = false,
            RightToLeft = suggRtl ? RightToLeft.Yes : RightToLeft.No,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 0, 0, 0)
        };

        suggContainer.Controls.Add(_suggestedBox);
        suggContainer.Controls.Add(_suggAccentBar);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, suggBoxH));
        layout.Controls.Add(suggContainer, 0, 4);

        // ── Row 5: Character count (auto height, right-aligned) ─────────
        var charLabel = new Label
        {
            Text = $"{suggestedText.Length} chars",
            Font = charFont,
            ForeColor = ThemeColors.TextMuted,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 10),
            BackColor = BgForm
        };
        _suggestedBox.TextChanged += (_, _) =>
            charLabel.Text = $"{_suggestedBox.TextLength} chars";

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(charLabel, 0, 5);

        // ── Row 6: Separator line (fixed 1px) ───────────────────────────
        var separator = new Panel
        {
            Height = 1,
            Dock = DockStyle.Fill,
            BackColor = Separator,
            Margin = new Padding(0, 0, 0, 14)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        layout.Controls.Add(separator, 0, 6);

        // ── Row 7: Buttons (FlowLayoutPanel, right-to-left) ─────────────
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            BackColor = BgForm,
            Margin = new Padding(0)
        };

        // Accept button — gold filled (will appear on the right via RightToLeft flow)
        var acceptButton = new Button
        {
            Text = isEditable ? "Accept && Replace" : "Copy to Clipboard",
            Size = new Size(isEditable ? 142 : 148, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Gold,
            ForeColor = Color.FromArgb(26, 18, 0),
            Font = btnBoldFont,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK,
            Margin = new Padding(0, 0, 0, 0)
        };
        acceptButton.FlatAppearance.BorderSize = 0;
        acceptButton.FlatAppearance.MouseOverBackColor = GoldHover;
        acceptButton.Click += (_, _) =>
        {
            if (isEditable) Accepted = true; else Copied = true;
            Close();
        };

        // Cancel button — ghost style (will appear on the left via RightToLeft flow)
        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(88, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = btnFont,
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 0, 8, 0)
        };
        cancelButton.FlatAppearance.BorderColor = Color.FromArgb(68, 68, 68);
        cancelButton.FlatAppearance.BorderSize = 1;
        cancelButton.FlatAppearance.MouseOverBackColor = ThemeColors.IsDark
            ? Color.FromArgb(50, 50, 55)
            : Color.FromArgb(238, 240, 244);
        cancelButton.Click += (_, _) => Close();

        // RightToLeft flow: first added = rightmost
        buttonPanel.Controls.Add(acceptButton);
        buttonPanel.Controls.Add(cancelButton);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(buttonPanel, 0, 7);

        // Final row count
        layout.RowCount = 8;

        Controls.Add(layout);

        // ── Form size ────────────────────────────────────────────────────
        int totalH = pad                           // top padding
            + origLabel.PreferredHeight + 6        // label + margin
            + origBoxH                             // original box
            + 16                                   // spacer row
            + 20 + 6                               // suggestion label row + margin
            + suggBoxH                             // suggestion box
            + charLabel.PreferredHeight + 12       // char count + margins
            + 1 + 14                               // separator + margin
            + 34                                   // buttons
            + pad;                                 // bottom padding

        int finalH = Math.Min(totalH, availH - 40);
        ClientSize = new Size(formW, finalH);

        // ── Keyboard ─────────────────────────────────────────────────────
        AcceptButton = acceptButton;
        CancelButton = cancelButton;

        _suggestedBox.TabIndex = 0;
        acceptButton.TabIndex = 1;
        cancelButton.TabIndex = 2;

        _suggestedBox.Select(suggestedText.Length, 0);
        ActiveControl = _suggestedBox;

        // Ensure blinking caret appears once the form is fully visible
        Load += (_, _) => _suggestedBox.Focus();
        Shown += (_, _) => _suggestedBox.Focus();
    }
}
