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

    // Measure how many lines a string needs in a given TextBox width
    private static int MeasureLines(string text, Font font, int boxWidth)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        float charWidth = g.MeasureString("W", font).Width;
        int charsPerLine = Math.Max(1, (int)(boxWidth / charWidth));
        int lines = 0;
        foreach (var raw in text.Split('\n'))
        {
            lines += Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, raw.Length) / charsPerLine));
        }
        return lines;
    }

    public ResultForm(string styleName, string originalText, string suggestedText)
    {
        Text = $"LLM-Rephraser \u2014 {styleName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        const int pad = 16;
        const int formW = 560;
        const int innerW = formW - pad * 2;
        const int lineH = 18;
        const int minLines = 2;
        const int maxLines = 8;
        const int boxPadV = 10; // top+bottom inner padding

        bool origRtl = IsRtl(originalText);
        bool suggRtl = IsRtl(suggestedText);

        int origLines = Math.Clamp(MeasureLines(originalText, Font, innerW - 4), minLines, maxLines);
        int suggLines = Math.Clamp(MeasureLines(suggestedText, Font, innerW - 4), minLines, maxLines);

        int origH  = origLines * lineH + boxPadV * 2;
        int suggH  = suggLines * lineH + boxPadV * 2;

        // ── Layout ──
        int y = pad;

        // Original label
        var origLabel = new Label
        {
            Text = "Original",
            Location = new Point(pad, y),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
        };
        y += origLabel.PreferredHeight + 2;

        // Original panel (read-only, gray background, no border frame)
        var originalBox = new TextBox
        {
            Text = originalText,
            Location = new Point(pad, y),
            Size = new Size(innerW, origH),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = origLines >= maxLines ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(245, 245, 245),
            ForeColor = Color.FromArgb(80, 80, 80),
            TabStop = false,
            RightToLeft = origRtl ? RightToLeft.Yes : RightToLeft.No,
            Font = new Font("Segoe UI", 10f)
        };
        y += origH + pad;

        // Suggestion label
        var suggLabel = new Label
        {
            Text = "Suggestion  \u2014  you can edit before accepting",
            Location = new Point(pad, y),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Regular)
        };
        y += suggLabel.PreferredHeight + 2;

        // Suggestion textbox (editable)
        _suggestedBox = new TextBox
        {
            Text = suggestedText,
            Location = new Point(pad, y),
            Size = new Size(innerW, suggH),
            Multiline = true,
            ScrollBars = suggLines >= maxLines ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            ForeColor = SystemColors.ControlText,
            RightToLeft = suggRtl ? RightToLeft.Yes : RightToLeft.No,
            Font = new Font("Segoe UI", 10f)
        };
        y += suggH + pad + 4;

        // ── Buttons ──
        var cancelButton = new Button
        {
            Text = "Cancel",
            Size = new Size(88, 30),
            DialogResult = DialogResult.Cancel
        };
        cancelButton.Location = new Point(formW - pad - cancelButton.Width, y);
        cancelButton.Click += (_, _) => Close();

        var acceptButton = new Button
        {
            Text = "Accept \u0026 Replace",
            Size = new Size(120, 30),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        acceptButton.Location = new Point(cancelButton.Left - acceptButton.Width - 8, y);
        acceptButton.Click += (_, _) => { Accepted = true; Close(); };

        y += acceptButton.Height + pad;

        ClientSize = new Size(formW, y);

        Controls.AddRange([origLabel, originalBox, suggLabel, _suggestedBox, acceptButton, cancelButton]);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;

        _suggestedBox.TabIndex = 0;
        acceptButton.TabIndex = 1;
        cancelButton.TabIndex = 2;

        // Focus suggestion box and move caret to end
        _suggestedBox.Select(suggestedText.Length, 0);
        ActiveControl = _suggestedBox;
    }
}
