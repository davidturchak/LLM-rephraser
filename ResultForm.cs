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

    private static bool IsRtl(string text)
    {
        return text.Any(c =>
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            // Hebrew: \u0590-\u05FF, Arabic: \u0600-\u06FF \u0750-\u077F \u08A0-\u08FF \uFB50-\uFDFF \uFE70-\uFEFF
            return c is (>= '\u0590' and <= '\u05FF')
                     or (>= '\u0600' and <= '\u06FF')
                     or (>= '\u0750' and <= '\u077F')
                     or (>= '\u08A0' and <= '\u08FF')
                     or (>= '\uFB50' and <= '\uFDFF')
                     or (>= '\uFE70' and <= '\uFEFF');
        });
    }

    public ResultForm(string styleName, string originalText, string suggestedText)
    {
        Text = $"LLM-Rephraser \u2014 {styleName}";
        ClientSize = new Size(560, 410);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(12);

        // ── Original ──
        var originalGroup = new GroupBox
        {
            Text = "Original text",
            Location = new Point(12, 8),
            Size = new Size(536, 150)
        };

        bool originalRtl = IsRtl(originalText);
        var originalBox = new TextBox
        {
            Text = originalText,
            Location = new Point(10, 22),
            Size = new Size(516, 116),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = SystemColors.Control,
            TabStop = false,
            RightToLeft = originalRtl ? RightToLeft.Yes : RightToLeft.No
        };
        originalGroup.Controls.Add(originalBox);

        // ── Suggested ──
        var suggestedGroup = new GroupBox
        {
            Text = "Suggestion (you can edit before accepting)",
            Location = new Point(12, 166),
            Size = new Size(536, 150)
        };

        bool suggestedRtl = IsRtl(suggestedText);
        _suggestedBox = new TextBox
        {
            Text = suggestedText,
            Location = new Point(10, 22),
            Size = new Size(516, 116),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            RightToLeft = suggestedRtl ? RightToLeft.Yes : RightToLeft.No
        };
        suggestedGroup.Controls.Add(_suggestedBox);

        // ── Buttons ──
        var acceptButton = new Button
        {
            Text = "Accept && Replace",
            Location = new Point(348, 328),
            Size = new Size(115, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        acceptButton.Click += (_, _) =>
        {
            Accepted = true;
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(469, 328),
            Size = new Size(79, 30),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        cancelButton.Click += (_, _) => Close();

        Controls.AddRange([originalGroup, suggestedGroup, acceptButton, cancelButton]);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;

        _suggestedBox.TabIndex = 0;
        acceptButton.TabIndex = 1;
        cancelButton.TabIndex = 2;
    }
}
