using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed class ResultForm : Form
{
    private readonly TextBox _suggestedBox;

    public string SuggestedText => _suggestedBox.Text;
    public bool Accepted { get; private set; }

    private static bool IsRtl(string text) =>
        text.Any(c => c is (>= '\u0590' and <= '\u05FF')
                        or (>= '\u0600' and <= '\u06FF')
                        or (>= '\u0750' and <= '\u077F')
                        or (>= '\u08A0' and <= '\u08FF')
                        or (>= '\uFB50' and <= '\uFDFF')
                        or (>= '\uFE70' and <= '\uFEFF'));

    private static int MeasureLines(string text, Font font, int boxWidth)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        float charWidth = g.MeasureString("W", font).Width;
        int charsPerLine = Math.Max(1, (int)(boxWidth / charWidth));
        int lines = 0;
        foreach (var raw in text.Split('\n'))
            lines += Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, raw.Length) / charsPerLine));
        return lines;
    }

    // Card panel with a colored left accent border
    private sealed class CardPanel : Panel
    {
        private readonly Color _accentColor;
        private const int AccentWidth = 4;

        public CardPanel(Color accentColor)
        {
            _accentColor = accentColor;
            BackColor = Color.White;
            Padding = new Padding(AccentWidth + 10, 8, 8, 8);
            BorderStyle = BorderStyle.None;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Left accent bar
            e.Graphics.FillRectangle(new SolidBrush(_accentColor),
                0, 0, AccentWidth, Height);
            // Subtle outer border
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle,
                Color.FromArgb(220, 220, 220), ButtonBorderStyle.Solid);
        }
    }

    public ResultForm(string styleName, string originalText, string suggestedText)
    {
        Text = $"LLM-Rephraser \u2014 {styleName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(245, 246, 248);
        Font = new Font("Segoe UI", 9.5f);

        const int pad    = 16;
        const int formW  = 560;
        const int innerW = formW - pad * 2;
        const int lineH  = 20;
        const int minLines = 2;
        const int maxLines = 8;

        var textFont = new Font("Segoe UI", 10f);
        bool origRtl = IsRtl(originalText);
        bool suggRtl = IsRtl(suggestedText);

        int origLines = Math.Clamp(MeasureLines(originalText, textFont, innerW - 30), minLines, maxLines);
        int suggLines = Math.Clamp(MeasureLines(suggestedText, textFont, innerW - 30), minLines, maxLines);

        int origBoxH = origLines * lineH + 20;
        int suggBoxH = suggLines * lineH + 20;
        int origCardH = origBoxH + 28; // label + box
        int suggCardH = suggBoxH + 28;

        int y = pad;

        // ── Original card ──
        var origCard = new CardPanel(Color.FromArgb(180, 180, 185))
        {
            Location = new Point(pad, y),
            Size = new Size(innerW, origCardH)
        };

        var origLabel = new Label
        {
            Text = "ORIGINAL",
            Location = new Point(14, 6),
            AutoSize = true,
            ForeColor = Color.FromArgb(140, 140, 145),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
        };

        var originalBox = new TextBox
        {
            Text = originalText,
            Location = new Point(14, 24),
            Size = new Size(innerW - 26, origBoxH),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = origLines >= maxLines ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(90, 90, 95),
            TabStop = false,
            RightToLeft = origRtl ? RightToLeft.Yes : RightToLeft.No,
            Font = textFont
        };

        origCard.Controls.AddRange([origLabel, originalBox]);
        y += origCardH + 12;

        // ── Suggestion card ──
        var suggCard = new CardPanel(Color.FromArgb(0, 120, 215))
        {
            Location = new Point(pad, y),
            Size = new Size(innerW, suggCardH)
        };

        var suggLabel = new Label
        {
            Text = "SUGGESTION  —  you can edit before accepting",
            Location = new Point(14, 6),
            AutoSize = true,
            ForeColor = Color.FromArgb(0, 100, 185),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold)
        };

        _suggestedBox = new TextBox
        {
            Text = suggestedText,
            Location = new Point(14, 24),
            Size = new Size(innerW - 26, suggBoxH),
            Multiline = true,
            ScrollBars = suggLines >= maxLines ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = SystemColors.ControlText,
            RightToLeft = suggRtl ? RightToLeft.Yes : RightToLeft.No,
            Font = textFont
        };

        suggCard.Controls.AddRange([suggLabel, _suggestedBox]);
        y += suggCardH + pad + 4;

        // ── Buttons ──
        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(88, 30),
        };
        cancelButton.Location = new Point(formW - pad - cancelButton.Width, y);
        cancelButton.Click += (_, _) => Close();

        var acceptButton = new Button
        {
            Text = "Accept \u0026 Replace",
            Size = new Size(125, 30),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            BackColor = Color.FromArgb(0, 120, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        acceptButton.FlatAppearance.BorderSize = 0;
        acceptButton.Location = new Point(cancelButton.Left - acceptButton.Width - 8, y);
        acceptButton.Click += (_, _) => { Accepted = true; Close(); };

        y += acceptButton.Height + pad;
        ClientSize = new Size(formW, y);

        Controls.AddRange([origCard, suggCard, acceptButton, cancelButton]);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;

        _suggestedBox.TabIndex = 0;
        acceptButton.TabIndex  = 1;
        cancelButton.TabIndex  = 2;

        _suggestedBox.Select(suggestedText.Length, 0);
        ActiveControl = _suggestedBox;
    }
}
