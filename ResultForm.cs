using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed class ResultForm : Form
{
    private readonly TextBox _suggestedBox;

    public string SuggestedText => _suggestedBox.Text;
    public bool Accepted { get; private set; }

    // ── Palette ───────────────────────────────────────────────────────────
    private static readonly Color BgPage        = Color.FromArgb(248, 250, 252); // #F8FAFC
    private static readonly Color BgCard        = Color.White;
    private static readonly Color BorderCard    = Color.FromArgb(226, 232, 240); // #E2E8F0
    private static readonly Color AccentOrig    = Color.FromArgb(148, 163, 184); // slate-400
    private static readonly Color AccentSugg    = Color.FromArgb(99,  102, 241); // indigo-500
    private static readonly Color TextBody      = Color.FromArgb(51,  65,  85);  // slate-700
    private static readonly Color TextMuted     = Color.FromArgb(148, 163, 184); // slate-400
    private static readonly Color TextOrigBody  = Color.FromArgb(100, 116, 139); // slate-500
    private static readonly Color PrimaryBtn    = Color.FromArgb(99,  102, 241); // indigo-500
    private static readonly Color PrimaryHover  = Color.FromArgb(79,  70,  229); // indigo-600

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

    // ── Rounded card panel with drop shadow ─────────────────────────────
    private sealed class CardPanel : Panel
    {
        private readonly Color _accent;
        private const int R = 8;
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

            // 1px border
            using var borderPen = new Pen(BorderCard, 1f);
            g.DrawRectangle(borderPen, rect);

            // Left accent bar
            g.FillRectangle(new SolidBrush(_accent), 0, 0, AccentW, Height);
        }
    }

    // ── Rounded button ───────────────────────────────────────────────────
    private sealed class RoundedButton : Button
    {
        private readonly bool _primary;
        private bool _hovered;
        private const int R = 7;

        public RoundedButton(bool primary)
        {
            _primary = primary;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = primary ? PrimaryBtn : Color.Transparent;
            ForeColor = primary ? Color.White : TextBody;
            Font = new Font("Segoe UI", 9.5f, primary ? FontStyle.Bold : FontStyle.Regular);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int d = R * 2;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();

            if (_primary)
            {
                var fill = _hovered ? PrimaryHover : PrimaryBtn;
                g.FillPath(new SolidBrush(fill), path);
            }
            else
            {
                // Ghost: border only
                g.FillPath(new SolidBrush(Color.Transparent), path);
                g.DrawPath(new Pen(BorderCard, 1.2f), path);
            }

            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(Text, Font, new SolidBrush(ForeColor), rect, sf);
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
        Text = $"LLM-Rephraser \u2014 {styleName}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgPage;
        DoubleBuffered = true;

        const int pad       = 20;
        const int cardPadL  = 20;   // inside card left (after accent bar)
        const int cardPadR  = 14;
        const int cardPadV  = 12;
        const int labelH    = 18;
        const int formW     = 580;
        const int innerW    = formW - pad * 2;
        const int minLines  = 2;
        const int maxLines  = 8;
        const int lineH     = 20;

        var textFont  = new Font("Segoe UI", 10f);
        var labelFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);

        bool origRtl = IsRtl(originalText);
        bool suggRtl = IsRtl(suggestedText);

        int origLines = Math.Clamp(MeasureLines(originalText, textFont, innerW - cardPadL - cardPadR), minLines, maxLines);
        int suggLines = Math.Clamp(MeasureLines(suggestedText, textFont, innerW - cardPadL - cardPadR), minLines, maxLines);

        int origBoxH  = origLines * lineH + 8;
        int suggBoxH  = suggLines * lineH + 8;
        int origCardH = cardPadV + labelH + 6 + origBoxH + cardPadV + 0;
        int suggCardH = cardPadV + labelH + 6 + suggBoxH + cardPadV + 0;

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
            Size = new Size(innerW - cardPadL - cardPadR - 0, origBoxH),
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
            Size = new Size(innerW - cardPadL - cardPadR - 0, suggBoxH),
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

        // Diff RichTextBox (hidden by default)
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
                ClientSize = new Size(formW, y + 28 + diffBox.Height + pad);
            }
            else
            {
                diffBox.Visible = false;
                ClientSize = new Size(formW, y + pad);
            }
        };

        // ── Buttons ──
        var cancelButton = new RoundedButton(false)
        {
            Text = "Cancel",
            Size = new Size(90, 34),
        };
        cancelButton.Location = new Point(formW - pad - cancelButton.Width, y - 2);
        cancelButton.Click += (_, _) => Close();

        var acceptButton = new RoundedButton(true)
        {
            Text = "Accept & Replace",
            Size = new Size(138, 34),
        };
        acceptButton.Location = new Point(cancelButton.Left - acceptButton.Width - 8, y - 2);
        acceptButton.Click += (_, _) => { Accepted = true; Close(); };

        y += acceptButton.Height + pad;
        ClientSize = new Size(formW, y);

        Controls.AddRange([origCard, suggCard, diffLink, diffBox, acceptButton, cancelButton]);

        AcceptButton = acceptButton;
        CancelButton = cancelButton;

        _suggestedBox.TabIndex = 0;
        acceptButton.TabIndex  = 1;
        cancelButton.TabIndex  = 2;

        _suggestedBox.Select(suggestedText.Length, 0);
        ActiveControl = _suggestedBox;
    }
}
