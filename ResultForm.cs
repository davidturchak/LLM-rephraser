using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Syncfusion.WinForms.Controls;

namespace LlmRephraser;

public sealed class ResultForm : SfForm
{
    private readonly TextBox _suggestedBox;

    public string SuggestedText => _suggestedBox.Text;
    public bool Accepted { get; private set; }

    // ── Palette ───────────────────────────────────────────────────────────
    private static readonly Color BgPage        = Color.FromArgb(248, 250, 252);
    private static readonly Color BgCard        = Color.White;
    private static readonly Color BorderCard    = Color.FromArgb(226, 232, 240);
    private static readonly Color AccentOrig    = Color.FromArgb(148, 163, 184);
    private static readonly Color AccentSugg    = Color.FromArgb(99,  102, 241);
    private static readonly Color TextBody      = Color.FromArgb(51,  65,  85);
    private static readonly Color TextMuted     = Color.FromArgb(148, 163, 184);
    private static readonly Color TextOrigBody  = Color.FromArgb(100, 116, 139);
    private static readonly Color PrimaryBtn    = Color.FromArgb(99,  102, 241);
    private static readonly Color PrimaryHover  = Color.FromArgb(79,  70,  229);

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
        return text.Split('\n').Sum(raw => Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, raw.Length) / cpl)));
    }

    // ── Rounded card panel with accent bar ──────────────────────────────
    private sealed class CardPanel : Panel
    {
        private readonly Color _accent;
        private const int AccentW = 3;

        public CardPanel(Color accent)
        {
            _accent = accent;
            BackColor = BgCard;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var borderPen = new Pen(BorderCard, 1f);
            g.DrawRectangle(borderPen, rect);
            using var accentBrush = new SolidBrush(_accent);
            g.FillRectangle(accentBrush, 0, 0, AccentW, Height);
        }
    }

    // ── Word-level diff ──────────────────────────────────────────────────
    private static List<(string word, bool added, bool removed)> WordDiff(string original, string suggested)
    {
        var origWords = original.Split(' ');
        var suggWords = suggested.Split(' ');
        int[,] dp = new int[origWords.Length + 1, suggWords.Length + 1];
        for (int i = 1; i <= origWords.Length; i++)
            for (int j = 1; j <= suggWords.Length; j++)
                dp[i, j] = origWords[i - 1] == suggWords[j - 1]
                    ? dp[i - 1, j - 1] + 1
                    : Math.Max(dp[i - 1, j], dp[i, j - 1]);

        var result = new List<(string, bool, bool)>();
        int oi = origWords.Length, si = suggWords.Length;
        while (oi > 0 || si > 0)
        {
            if (oi > 0 && si > 0 && origWords[oi - 1] == suggWords[si - 1])
            {
                result.Insert(0, (origWords[oi - 1], false, false));
                oi--; si--;
            }
            else if (si > 0 && (oi == 0 || dp[oi, si - 1] >= dp[oi - 1, si]))
            {
                result.Insert(0, (suggWords[si - 1], true, false));
                si--;
            }
            else
            {
                result.Insert(0, (origWords[oi - 1], false, true));
                oi--;
            }
        }
        return result;
    }

    public ResultForm(string styleName, string originalText, string suggestedText)
    {
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;

        Text = $"LLM-Rephraser \u2014 {styleName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgPage;
        ShowIcon = false;

        Style.TitleBar.BackColor = AccentSugg;
        Style.TitleBar.ForeColor = Color.White;

        // Convert screen pixels to design units for DPI awareness
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        float dpiScale;
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) { dpiScale = g.DpiX / 96f; }
        int availW = (int)(workArea.Width / dpiScale);
        int availH = (int)(workArea.Height / dpiScale);

        const int pad       = 20;
        const int cardPadL  = 20;
        const int cardPadR  = 14;
        const int cardPadV  = 12;
        const int labelH    = 18;
        const int minLines  = 2;
        const int maxLines  = 8;
        const int lineH     = 20;

        int formW  = Math.Clamp(availW * 2 / 5, 360, 580);
        int innerW = formW - pad * 2;

        var textFont  = new Font("Segoe UI", 10f);
        var labelFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);

        bool origRtl = IsRtl(originalText);
        bool suggRtl = IsRtl(suggestedText);

        int origLines = Math.Clamp(MeasureLines(originalText, textFont, innerW - cardPadL - cardPadR), minLines, maxLines);
        int suggLines = Math.Clamp(MeasureLines(suggestedText, textFont, innerW - cardPadL - cardPadR), minLines, maxLines);

        int origBoxH  = origLines * lineH + 8;
        int suggBoxH  = suggLines * lineH + 8;
        int origCardH = cardPadV + labelH + 6 + origBoxH + cardPadV;
        int suggCardH = cardPadV + labelH + 6 + suggBoxH + cardPadV;

        int y = pad;

        // ── Original card ──
        var origCard = new CardPanel(AccentOrig)
        {
            Location = new Point(pad, y),
            Size = new Size(innerW, origCardH)
        };

        var origLabel = new Label
        {
            Text = "ORIGINAL",
            Location = new Point(cardPadL, cardPadV),
            AutoSize = true,
            ForeColor = TextMuted,
            Font = labelFont,
            BackColor = Color.Transparent
        };

        var originalBox = new TextBox
        {
            Text = originalText,
            Location = new Point(cardPadL, cardPadV + labelH + 6),
            Size = new Size(innerW - cardPadL - cardPadR, origBoxH),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = origLines >= maxLines ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = TextOrigBody,
            TabStop = false,
            RightToLeft = origRtl ? RightToLeft.Yes : RightToLeft.No,
            Font = textFont
        };

        origCard.Controls.AddRange([origLabel, originalBox]);
        y += origCardH + 12;

        // ── Suggestion card ──
        var suggCard = new CardPanel(AccentSugg)
        {
            Location = new Point(pad, y),
            Size = new Size(innerW, suggCardH)
        };

        var suggLabel = new Label
        {
            Text = "SUGGESTION",
            Location = new Point(cardPadL, cardPadV),
            AutoSize = true,
            ForeColor = AccentSugg,
            Font = labelFont,
            BackColor = Color.Transparent
        };

        var suggHint = new Label
        {
            Text = "you can edit before accepting",
            Location = new Point(cardPadL + 90, cardPadV + 1),
            AutoSize = true,
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 7.5f, FontStyle.Regular),
            BackColor = Color.Transparent
        };

        _suggestedBox = new TextBox
        {
            Text = suggestedText,
            Location = new Point(cardPadL, cardPadV + labelH + 6),
            Size = new Size(innerW - cardPadL - cardPadR, suggBoxH),
            Multiline = true,
            ScrollBars = suggLines >= maxLines ? ScrollBars.Vertical : ScrollBars.None,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = TextBody,
            RightToLeft = suggRtl ? RightToLeft.Yes : RightToLeft.No,
            Font = textFont
        };

        suggCard.Controls.AddRange([suggLabel, suggHint, _suggestedBox]);
        y += suggCardH + pad;

        // ── Diff toggle ──
        var diffLink = new LinkLabel
        {
            Text = "Show diff",
            Location = new Point(pad, y + 6),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = AccentSugg,
            ActiveLinkColor = PrimaryHover
        };

        var diffBox = new RichTextBox
        {
            Location = new Point(pad, y + 28),
            Size = new Size(innerW, 0),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            Font = textFont,
            Visible = false,
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        bool diffVisible = false;
        diffLink.Click += (_, _) =>
        {
            diffVisible = !diffVisible;
            diffLink.Text = diffVisible ? "Hide diff" : "Show diff";
            if (diffVisible)
            {
                diffBox.Clear();
                var diffs = WordDiff(originalText, suggestedText);
                foreach (var (word, added, removed) in diffs)
                {
                    if (removed)
                    {
                        diffBox.SelectionColor = Color.FromArgb(220, 38, 38);
                        diffBox.SelectionFont = new Font(textFont, FontStyle.Strikeout);
                        diffBox.AppendText(word + " ");
                    }
                    else if (added)
                    {
                        diffBox.SelectionColor = Color.FromArgb(22, 163, 74);
                        diffBox.SelectionFont = textFont;
                        diffBox.AppendText(word + " ");
                    }
                    else
                    {
                        diffBox.SelectionColor = TextBody;
                        diffBox.SelectionFont = textFont;
                        diffBox.AppendText(word + " ");
                    }
                }
                int diffLines = Math.Min(6, diffBox.Lines.Length + 1);
                diffBox.Height = diffLines * lineH + 12;
                diffBox.Visible = true;
                var newH = y + 28 + diffBox.Height + pad;
                ClientSize = new Size(formW, Math.Min(newH, availH - 40));
            }
            else
            {
                diffBox.Visible = false;
                ClientSize = new Size(formW, y + pad);
            }
        };

        // ── Buttons (SfButton) ──
        var cancelButton = new SfButton
        {
            Text = "Cancel",
            Size = new Size(88, 32),
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        cancelButton.Style.BackColor = Color.White;
        cancelButton.Style.ForeColor = TextBody;
        cancelButton.Style.HoverBackColor = BgPage;
        cancelButton.Style.HoverForeColor = TextBody;
        cancelButton.Style.Border = new Pen(BorderCard, 1);
        cancelButton.Click += (_, _) => Close();

        var acceptButton = new SfButton
        {
            Text = "Accept && Replace",
            Size = new Size(138, 32),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        acceptButton.Style.BackColor = PrimaryBtn;
        acceptButton.Style.ForeColor = Color.White;
        acceptButton.Style.HoverBackColor = PrimaryHover;
        acceptButton.Style.HoverForeColor = Color.White;
        acceptButton.Style.PressedBackColor = PrimaryHover;
        acceptButton.Style.Border = new Pen(PrimaryBtn, 0);
        acceptButton.Click += (_, _) => { Accepted = true; Close(); };

        cancelButton.Location  = new Point(formW - pad - cancelButton.Width, y);
        acceptButton.Location  = new Point(cancelButton.Left - acceptButton.Width - 8, y);

        y += acceptButton.Height + pad;

        int finalH = Math.Min(y, availH - 40);
        ClientSize = new Size(formW, finalH);

        Controls.AddRange([origCard, suggCard, diffLink, diffBox, acceptButton, cancelButton]);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;

        _suggestedBox.TabIndex = 0;
        acceptButton.TabIndex  = 1;
        cancelButton.TabIndex  = 2;

        // Shown fires AFTER SfForm chrome + AutoScaleMode are fully applied
        Shown += (_, _) =>
        {
            var screen = Screen.FromControl(this).WorkingArea;
            if (Bottom > screen.Bottom)
                Height -= (Bottom - screen.Bottom + 5);
            if (Right > screen.Right)
                Width -= (Right - screen.Right + 5);
            if (Top < screen.Top) Top = screen.Top;
            if (Left < screen.Left) Left = screen.Left;
        };

        _suggestedBox.Select(suggestedText.Length, 0);
        ActiveControl = _suggestedBox;
    }
}
