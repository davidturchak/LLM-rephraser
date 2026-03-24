using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed partial class ResultForm : Form
{
    private readonly bool _isEditable;

    public string SuggestedText => rtbSuggestion.Text;
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

    public ResultForm(string styleName, string originalText, string suggestedText,
                      bool isEditable = true)
    {
        _isEditable = isEditable;

        InitializeComponent();

        // ── Button label — hardcoded, never conditional ─────────────────
        btnAccept.Text = "Accept && Replace";

        // ── Form title ──────────────────────────────────────────────────
        Text = $"LLM-Rephraser \u2014 {styleName}";

        // ── DPI / screen metrics ────────────────────────────────────────
        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        float dpiScale;
        using (var gfx = Graphics.FromHwnd(IntPtr.Zero)) { dpiScale = gfx.DpiX / 96f; }
        int availW = (int)(workArea.Width / dpiScale);
        int formW = Math.Clamp(availW * 2 / 5, 400, 600);
        ClientSize = new Size(formW, 420);

        // ── RTL ─────────────────────────────────────────────────────────
        if (IsRtl(originalText))
            rtbOriginal.RightToLeft = RightToLeft.Yes;
        if (IsRtl(suggestedText))
            rtbSuggestion.RightToLeft = RightToLeft.Yes;

        // ── Populate text ───────────────────────────────────────────────
        rtbOriginal.Text = originalText;
        rtbSuggestion.Text = suggestedText;
        lblCharCount.Text = suggestedText.Length + " chars";

        // ── Place caret at end ──────────────────────────────────────────
        rtbSuggestion.Select(suggestedText.Length, 0);
        ActiveControl = rtbSuggestion;

        // ── Event wiring ────────────────────────────────────────────────
        rtbSuggestion.TextChanged += RtbSuggestion_TextChanged;

        rtbOriginal.SizeChanged += (_, _) =>
            pnlOriginalAccent.Height = rtbOriginal.Height;

        rtbSuggestion.SizeChanged += (_, _) =>
            pnlSuggestionAccent.Height = rtbSuggestion.Height;

        btnAccept.Click += BtnAccept_Click;
        btnCancel.Click += (_, _) => Close();

        Load += ResultForm_Load;
        Shown += (_, _) => rtbSuggestion.Focus();
    }

    // ── Form_Load ────────────────────────────────────────────────────────
    private void ResultForm_Load(object? sender, EventArgs e)
    {
        // Triple-enforce button label
        btnAccept.Text = "Accept && Replace";

        // Sync accent bar heights after layout
        pnlOriginalAccent.Height = rtbOriginal.Height;
        pnlSuggestionAccent.Height = rtbSuggestion.Height;

        // Focus suggestion box so caret appears immediately
        rtbSuggestion.Focus();
    }

    // ── TextChanged — char count update ──────────────────────────────────
    private void RtbSuggestion_TextChanged(object? sender, EventArgs e)
    {
        lblCharCount.Text = rtbSuggestion.TextLength + " chars";
    }

    // ── Accept button click ──────────────────────────────────────────────
    private void BtnAccept_Click(object? sender, EventArgs e)
    {
        if (_isEditable)
            Accepted = true;
        else
            Copied = true;
        Close();
    }
}
