using System.Drawing;
using System.Windows.Forms;

namespace LlmRephraser;

partial class ResultForm
{
    private System.ComponentModel.IContainer components = null;

    // ── Controls ─────────────────────────────────────────────────────────
    private TableLayoutPanel mainLayout;
    private Label lblOriginal;
    private Panel pnlOriginalRow;
    private RichTextBox rtbOriginal;
    private Panel pnlSpacer;
    private FlowLayoutPanel flpSuggLabels;
    private Label lblSuggestion;
    private Label lblHint;
    private Panel pnlSuggestionRow;
    private RichTextBox rtbSuggestion;
    private Label lblCharCount;
    private Panel pnlSeparator;
    private FlowLayoutPanel flpButtons;
    private Button btnCancel;
    private Button btnAccept;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        SuspendLayout();

        // ═════════════════════════════════════════════════════════════════
        //  Form
        // ═════════════════════════════════════════════════════════════════
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = ColorTranslator.FromHtml("#1e1e1e");
        ForeColor = ColorTranslator.FromHtml("#dddddd");
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;

        // ═════════════════════════════════════════════════════════════════
        //  Main TableLayoutPanel — 8 rows, 1 column
        // ═════════════════════════════════════════════════════════════════
        mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            AutoSize = false,
            Padding = new Padding(12, 10, 12, 10),
            BackColor = ColorTranslator.FromHtml("#1e1e1e")
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 0: ORIGINAL label
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // 1: Original RTB row
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 16));   // 2: Spacer
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 3: SUGGESTION labels
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));  // 4: Suggestion RTB row
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 5: Char count
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));    // 6: Separator
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // 7: Buttons

        // ─── Row 0: ORIGINAL label ──────────────────────────────────────
        lblOriginal = new Label
        {
            Text = "ORIGINAL",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = ColorTranslator.FromHtml("#888888"),
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 6)
        };
        mainLayout.Controls.Add(lblOriginal, 0, 0);

        // ─── Row 1: Original accent bar + RichTextBox ───────────────────
        // Container BackColor = accent gray, Padding left 3px exposes it as a bar
        rtbOriginal = new RichTextBox
        {
            ReadOnly = true,
            BackColor = ColorTranslator.FromHtml("#0e0e0e"),
            ForeColor = ColorTranslator.FromHtml("#666666"),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.None,
            WordWrap = true,
            DetectUrls = false,
            TabStop = false,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12f),
            Margin = new Padding(0)
        };

        pnlOriginalRow = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#0e0e0e"),
            Padding = new Padding(3, 0, 0, 0),
            Margin = new Padding(0)
        };
        pnlOriginalRow.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(ColorTranslator.FromHtml("#555555"));
            e.Graphics.FillRectangle(brush, 0, 0, 3, pnlOriginalRow.Height);
        };
        pnlOriginalRow.Controls.Add(rtbOriginal);
        mainLayout.Controls.Add(pnlOriginalRow, 0, 1);

        // ─── Row 2: Spacer (16px) ──────────────────────────────────────
        pnlSpacer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            Margin = new Padding(0)
        };
        mainLayout.Controls.Add(pnlSpacer, 0, 2);

        // ─── Row 3: SUGGESTION label + hint ─────────────────────────────
        flpSuggLabels = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            Margin = new Padding(0, 0, 0, 6)
        };

        lblSuggestion = new Label
        {
            Text = "SUGGESTION",
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = ColorTranslator.FromHtml("#b8952a"),
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            AutoSize = true,
            Margin = new Padding(0, 0, 10, 0)
        };

        lblHint = new Label
        {
            Text = "you can edit before accepting",
            Font = new Font("Segoe UI", 8f, FontStyle.Italic),
            ForeColor = ColorTranslator.FromHtml("#555555"),
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            AutoSize = true,
            Margin = new Padding(0, 1, 0, 0)
        };

        flpSuggLabels.Controls.Add(lblSuggestion);
        flpSuggLabels.Controls.Add(lblHint);
        mainLayout.Controls.Add(flpSuggLabels, 0, 3);

        // ─── Row 4: Suggestion accent bar + RichTextBox ─────────────────
        // Container BackColor = gold, Padding left 3px exposes it as a bar
        rtbSuggestion = new RichTextBox
        {
            ReadOnly = false,
            BackColor = ColorTranslator.FromHtml("#161616"),
            ForeColor = ColorTranslator.FromHtml("#dddddd"),
            SelectionBackColor = ColorTranslator.FromHtml("#161616"),
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = true,
            DetectUrls = false,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12f),
            Margin = new Padding(0)
        };

        pnlSuggestionRow = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#161616"),
            Padding = new Padding(3, 0, 0, 0),
            Margin = new Padding(0)
        };
        pnlSuggestionRow.Paint += (_, e) =>
        {
            using var brush = new SolidBrush(ColorTranslator.FromHtml("#b8952a"));
            e.Graphics.FillRectangle(brush, 0, 0, 3, pnlSuggestionRow.Height);
        };
        pnlSuggestionRow.Controls.Add(rtbSuggestion);
        mainLayout.Controls.Add(pnlSuggestionRow, 0, 4);

        // ─── Row 5: Char count ──────────────────────────────────────────
        lblCharCount = new Label
        {
            Text = "0 chars",
            Font = new Font("Segoe UI", 7.5f),
            ForeColor = ColorTranslator.FromHtml("#555555"),
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 0, 10)
        };
        mainLayout.Controls.Add(lblCharCount, 0, 5);

        // ─── Row 6: Separator ───────────────────────────────────────────
        pnlSeparator = new Panel
        {
            Height = 1,
            Dock = DockStyle.Fill,
            BackColor = ColorTranslator.FromHtml("#333333"),
            Margin = new Padding(0, 0, 0, 14)
        };
        mainLayout.Controls.Add(pnlSeparator, 0, 6);

        // ─── Row 7: Buttons (RightToLeft flow) ─────────────────────────
        flpButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = false,
            BackColor = ColorTranslator.FromHtml("#1e1e1e"),
            Margin = new Padding(0, 6, 0, 0)
        };

        btnAccept = new Button
        {
            Text = "Accept && Replace",
            Size = new Size(142, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = ColorTranslator.FromHtml("#b8952a"),
            ForeColor = ColorTranslator.FromHtml("#1a1200"),
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.OK,
            Margin = new Padding(0)
        };
        btnAccept.FlatAppearance.BorderSize = 0;
        btnAccept.FlatAppearance.MouseOverBackColor = Color.FromArgb(164, 132, 36);

        btnCancel = new Button
        {
            Text = "Cancel",
            Size = new Size(88, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = ColorTranslator.FromHtml("#aaaaaa"),
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand,
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 0, 10, 0)
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#444444");
        btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(50, 50, 55);

        // RightToLeft: first added = rightmost
        flpButtons.Controls.Add(btnAccept);
        flpButtons.Controls.Add(btnCancel);
        mainLayout.Controls.Add(flpButtons, 0, 7);

        mainLayout.RowCount = 8;
        Controls.Add(mainLayout);

        AcceptButton = btnAccept;
        CancelButton = btnCancel;

        rtbSuggestion.TabIndex = 0;
        btnAccept.TabIndex = 1;
        btnCancel.TabIndex = 2;

        ResumeLayout(false);
        PerformLayout();
    }
}
