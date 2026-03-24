using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Syncfusion.Windows.Forms.Tools;

namespace LlmRephraser;

public sealed class SettingsForm : Form
{
    // ── Settings tab fields ──
    private readonly ComboBoxAdv _profileBox;
    private readonly Button _addButton;
    private readonly Button _renameButton;
    private readonly Button _deleteButton;
    private readonly ComboBoxAdv _providerBox;
    private readonly TextBox _endpointBox;
    private readonly TextBox _apiKeyBox;
    private readonly TextBox _modelBox;
    private readonly Button _testButton;
    private readonly Label _testResultLabel;
    private readonly CheckBoxAdv _shiftRightClickBox;
    private readonly CheckBoxAdv _startWithWindowsBox;
    private readonly ListBox _langListBox;
    private readonly Button _langAddButton;
    private readonly Button _langRemoveButton;

    // ── OpenRouter tab fields ──
    private readonly ListView _modelListView;
    private readonly Button _fetchButton;
    private readonly Button _createProfileButton;
    private readonly Label _orStatusLabel;
    private readonly TextBox _orSearchBox;
    private List<OpenRouterModel> _allModels = [];
    private List<OpenRouterModel> _filteredModels = [];

    // ── Google AI Studio tab fields ──
    private readonly ListView _gaiModelListView;
    private readonly Button _gaiFetchButton;
    private readonly Button _gaiCreateProfileButton;
    private readonly Label _gaiStatusLabel;
    private readonly TextBox _gaiSearchBox;
    private readonly TextBox _gaiApiKeyBox;
    private List<GeminiModel> _allGaiModels = [];
    private List<GeminiModel> _filteredGaiModels = [];

    // ── NVIDIA tab fields ──
    private readonly ListView _nvModelListView;
    private readonly Button _nvFetchButton;
    private readonly Button _nvCreateProfileButton;
    private readonly Label _nvStatusLabel;
    private readonly TextBox _nvSearchBox;
    private List<NvidiaModel> _allNvModels = [];
    private List<NvidiaModel> _filteredNvModels = [];

    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    private AppConfig _config;
    private bool _suppressProfileSwitch;

    private static readonly (string Label, ApiProvider Value, string DefaultEndpoint, string DefaultModel)[] Providers =
    [
        ("OpenAI-Compatible", ApiProvider.OpenAICompatible, "http://localhost:11434/v1/chat/completions", "llama3"),
        ("Anthropic Claude",  ApiProvider.Anthropic,        "https://api.anthropic.com/v1/messages",      "claude-sonnet-4-20250514")
    ];

    // ── Palette ──
    private static readonly Color BgPage       = Color.FromArgb(248, 250, 252);
    private static readonly Color BgCard       = Color.White;
    private static readonly Color BorderCard   = Color.FromArgb(226, 232, 240);
    private static readonly Color AccentPrimary = Color.FromArgb(99, 102, 241);
    private static readonly Color AccentHover  = Color.FromArgb(79,  70, 229);
    private static readonly Color TextBody     = Color.FromArgb(51,  65,  85);
    private static readonly Color TextMuted    = Color.FromArgb(148, 163, 184);

    private sealed class SectionCard : Panel
    {
        public SectionCard() { BackColor = BgCard; DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using var pen = new Pen(BorderCard, 1f); e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1); }
    }

    private static Button MakePrimary(string text, int w, int h)
    {
        var btn = new Button { Text = text, Size = new Size(w, h), FlatStyle = FlatStyle.Flat, BackColor = AccentPrimary, ForeColor = Color.White, Font = new Font("Segoe UI", 9f, FontStyle.Bold), Cursor = Cursors.Hand };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = AccentHover;
        return btn;
    }

    private static Button MakeSecondary(string text, int w, int h)
    {
        var btn = new Button { Text = text, Size = new Size(w, h), FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = TextBody, Font = new Font("Segoe UI", 9f), Cursor = Cursors.Hand };
        btn.FlatAppearance.BorderColor = BorderCard;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.MouseOverBackColor = BgPage;
        return btn;
    }

    private static Label MakeSectionLabel(string text) => new() { Text = text, AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5f, FontStyle.Bold), BackColor = Color.Transparent };

    public SettingsForm(AppConfig config)
    {
        _config = config;

        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = "LLM-Rephraser Settings";
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        BackColor = BgPage;
        MinimumSize = new Size(420, 380);
        DoubleBuffered = true;

        var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        float dpiScale;
        using (var g = Graphics.FromHwnd(IntPtr.Zero)) { dpiScale = g.DpiX / 96f; }
        int availW = (int)((workArea.Width - 40) / dpiScale);
        int availH = (int)((workArea.Height - 40) / dpiScale);
        int formW = Math.Min(540, availW);
        int formH = Math.Min(580, availH);
        ClientSize = new Size(formW, formH);

        var tabControl = new TabControlAdv
        {
            Location = new Point(8, 8),
            Size = new Size(formW - 16, formH - 52),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            ActiveTabColor = Color.White,
            ActiveTabForeColor = AccentPrimary,
            ActiveTabFont = new Font("Segoe UI", 9f, FontStyle.Bold),
            InactiveTabColor = BgPage,
            BorderStyle = BorderStyle.None,
            FocusOnTabClick = false,
            TabGap = 4
        };

        // ═══ TAB 1: Settings ═══
        var settingsTab = new TabPageAdv("Settings") { BackColor = BgPage };
        var settingsPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BgPage };

        const int innerPad = 14;
        int cardW = formW - 56;
        int fieldW = cardW - 110 - innerPad;

        // Profile card
        var profileCard = new SectionCard { Location = new Point(4, 4), Size = new Size(cardW, 52) };
        _profileBox = new ComboBoxAdv { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(innerPad, 14), Size = new Size(220, 23) };
        _profileBox.SelectedIndexChanged += ProfileBox_Changed;

        _deleteButton = MakeSecondary("Delete", 60, 27);
        _deleteButton.Location = new Point(cardW - innerPad - 60, 13);
        _deleteButton.Click += DeleteProfile_Click;

        _renameButton = MakeSecondary("Rename...", 76, 27);
        _renameButton.Location = new Point(_deleteButton.Left - 76 - 6, 13);
        _renameButton.Click += RenameProfile_Click;

        _addButton = MakeSecondary("New...", 65, 27);
        _addButton.Location = new Point(_renameButton.Left - 65 - 6, 13);
        _addButton.Click += AddProfile_Click;

        profileCard.Controls.AddRange([_profileBox, _addButton, _renameButton, _deleteButton]);

        // Connection card
        var connectionCard = new SectionCard { Location = new Point(4, 64), Size = new Size(cardW, 210) };
        var connLabel = MakeSectionLabel("CONNECTION"); connLabel.Location = new Point(innerPad, 10);

        var providerLabel = new Label { Text = "Provider:", Location = new Point(innerPad, 36), Size = new Size(90, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _providerBox = new ComboBoxAdv { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, 34), Size = new Size(fieldW, 23) };
        foreach (var (label, _, _, _) in Providers) _providerBox.Items.Add(label);
        _providerBox.SelectedIndexChanged += ProviderBox_Changed;

        var endpointLabel = new Label { Text = "Endpoint:", Location = new Point(innerPad, 68), Size = new Size(90, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _endpointBox = new TextBox { Location = new Point(110, 66), Size = new Size(fieldW, 23) };

        var apiKeyLabel = new Label { Text = "API Key:", Location = new Point(innerPad, 100), Size = new Size(90, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _apiKeyBox = new TextBox { Location = new Point(110, 98), Size = new Size(fieldW, 23), UseSystemPasswordChar = true };
        var apiKeyHint = new Label { Text = "Leave blank if not required", Location = new Point(110, 122), AutoSize = true, ForeColor = TextMuted, Font = new Font("Segoe UI", 7.5f), BackColor = Color.Transparent };

        var modelLabel = new Label { Text = "Model:", Location = new Point(innerPad, 144), Size = new Size(90, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _modelBox = new TextBox { Location = new Point(110, 142), Size = new Size(fieldW, 23) };

        _testButton = MakeSecondary("&Test Connection", 120, 28);
        _testButton.Location = new Point(innerPad, 174);
        _testButton.Click += TestButton_Click;
        _testResultLabel = new Label { Text = "", Location = new Point(142, 180), Size = new Size(fieldW - 32, 17), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent };

        connectionCard.Controls.AddRange([connLabel, providerLabel, _providerBox, endpointLabel, _endpointBox, apiKeyLabel, _apiKeyBox, apiKeyHint, modelLabel, _modelBox, _testButton, _testResultLabel]);

        // Translation Languages card
        var langCard = new SectionCard { Location = new Point(4, 282), Size = new Size(cardW, 98) };
        var langLabel = MakeSectionLabel("TRANSLATION LANGUAGES"); langLabel.Location = new Point(innerPad, 10);

        _langAddButton = MakeSecondary("Add...", 72, 27);
        _langAddButton.Location = new Point(cardW - innerPad - 72, 32);
        _langAddButton.Click += LangAdd_Click;

        _langRemoveButton = MakeSecondary("Remove", 72, 27);
        _langRemoveButton.Location = new Point(cardW - innerPad - 72, 63);
        _langRemoveButton.Click += LangRemove_Click;

        _langListBox = new ListBox { Location = new Point(innerPad, 32), Size = new Size(_langAddButton.Left - innerPad - 8, 56), SelectionMode = SelectionMode.One, BorderStyle = BorderStyle.FixedSingle };
        foreach (var lang in _config.TranslationLanguages) _langListBox.Items.Add(lang);

        langCard.Controls.AddRange([langLabel, _langListBox, _langAddButton, _langRemoveButton]);

        // Options card
        var optionsCard = new SectionCard { Location = new Point(4, 388), Size = new Size(cardW, 72) };
        var optLabel = MakeSectionLabel("OPTIONS"); optLabel.Location = new Point(innerPad, 10);
        _shiftRightClickBox = new CheckBoxAdv { Text = "Enable Shift+Right-Click to open style picker", Location = new Point(innerPad, 30), AutoSize = true, Checked = _config.ShiftRightClickEnabled, ForeColor = TextBody, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f) };
        _startWithWindowsBox = new CheckBoxAdv { Text = "Start LLM-Rephraser with Windows", Location = new Point(innerPad, 50), AutoSize = true, Checked = AppConfig.ReadStartWithWindows(), ForeColor = TextBody, BackColor = Color.Transparent, Font = new Font("Segoe UI", 9f) };
        optionsCard.Controls.AddRange([optLabel, _shiftRightClickBox, _startWithWindowsBox]);

        settingsPanel.Controls.AddRange([profileCard, connectionCard, langCard, optionsCard]);
        settingsTab.Controls.Add(settingsPanel);

        // ═══ TAB 2: OpenRouter ═══
        var openRouterTab = new TabPageAdv("OpenRouter") { BackColor = BgPage };
        var orCard = new SectionCard { Location = new Point(4, 4), Size = new Size(cardW, formH - 96), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        var orSectionLabel = MakeSectionLabel("BROWSE FREE MODELS"); orSectionLabel.Location = new Point(innerPad, 10);
        var orDescription = new Label { Text = "Browse free models from OpenRouter.ai and create a profile with one click.", Location = new Point(innerPad, 28), Size = new Size(cardW - innerPad * 2, 18), ForeColor = TextMuted, Font = new Font("Segoe UI", 8f), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _fetchButton = MakeSecondary("Fetch Free Models", 140, 28); _fetchButton.Location = new Point(innerPad, 52); _fetchButton.Click += FetchModels_Click;
        _orStatusLabel = new Label { Text = "", Location = new Point(162, 58), Size = new Size(cardW - 162 - innerPad, 17), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        var searchLabel = new Label { Text = "Search:", Location = new Point(innerPad, 90), Size = new Size(50, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _orSearchBox = new TextBox { Location = new Point(68, 88), Size = new Size(200, 23) }; _orSearchBox.TextChanged += OrSearch_Changed;

        _modelListView = new ListView { Location = new Point(innerPad, 118), Size = new Size(cardW - innerPad * 2, 264), View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        _modelListView.Columns.Add("Model Name", 210); _modelListView.Columns.Add("ID", 160); _modelListView.Columns.Add("Context", 80, HorizontalAlignment.Right);

        _createProfileButton = MakePrimary("Create Profile from Selected", 210, 30); _createProfileButton.Location = new Point(innerPad, 390); _createProfileButton.Enabled = false; _createProfileButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; _createProfileButton.Click += CreateProfileFromModel_Click;
        _modelListView.SelectedIndexChanged += (_, _) => _createProfileButton.Enabled = _modelListView.SelectedItems.Count > 0;

        var orKeyLink = new LinkLabel { Text = "Get your OpenRouter API key", Location = new Point(234, 396), AutoSize = true, LinkColor = AccentPrimary, ActiveLinkColor = AccentHover, BackColor = Color.Transparent, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        orKeyLink.LinkClicked += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://openrouter.ai/workspaces/default/keys", UseShellExecute = true });

        orCard.Controls.AddRange([orSectionLabel, orDescription, _fetchButton, _orStatusLabel, searchLabel, _orSearchBox, _modelListView, _createProfileButton, orKeyLink]);
        openRouterTab.Controls.Add(orCard);

        // ═══ TAB 3: Google AI Studio ═══
        var gaiTab = new TabPageAdv("Google AI Studio") { BackColor = BgPage };
        var gaiCard = new SectionCard { Location = new Point(4, 4), Size = new Size(cardW, formH - 96), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        var gaiSectionLabel = MakeSectionLabel("BROWSE GEMINI MODELS"); gaiSectionLabel.Location = new Point(innerPad, 10);
        var gaiDescription = new Label { Text = "Browse Gemini models from Google AI Studio and create a profile with one click.", Location = new Point(innerPad, 28), Size = new Size(cardW - innerPad * 2, 18), ForeColor = TextMuted, Font = new Font("Segoe UI", 8f), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var gaiKeyLabel = new Label { Text = "API Key:", Location = new Point(innerPad, 56), Size = new Size(55, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _gaiApiKeyBox = new TextBox { Location = new Point(73, 54), Size = new Size(cardW - 73 - 100 - innerPad - 8, 23), UseSystemPasswordChar = true };
        _gaiFetchButton = MakeSecondary("Fetch Models", 100, 27); _gaiFetchButton.Location = new Point(cardW - innerPad - 100, 53); _gaiFetchButton.Click += GaiFetchModels_Click;
        _gaiStatusLabel = new Label { Text = "", Location = new Point(innerPad, 84), Size = new Size(cardW - innerPad * 2, 17), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var gaiSearchLabel = new Label { Text = "Search:", Location = new Point(innerPad, 108), Size = new Size(50, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _gaiSearchBox = new TextBox { Location = new Point(68, 106), Size = new Size(200, 23) }; _gaiSearchBox.TextChanged += GaiSearch_Changed;

        _gaiModelListView = new ListView { Location = new Point(innerPad, 136), Size = new Size(cardW - innerPad * 2, 244), View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        _gaiModelListView.Columns.Add("Display Name", 190); _gaiModelListView.Columns.Add("Model ID", 170); _gaiModelListView.Columns.Add("Context", 80, HorizontalAlignment.Right);

        _gaiCreateProfileButton = MakePrimary("Create Profile from Selected", 210, 30); _gaiCreateProfileButton.Location = new Point(innerPad, 388); _gaiCreateProfileButton.Enabled = false; _gaiCreateProfileButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; _gaiCreateProfileButton.Click += GaiCreateProfile_Click;
        _gaiModelListView.SelectedIndexChanged += (_, _) => _gaiCreateProfileButton.Enabled = _gaiModelListView.SelectedItems.Count > 0;

        var gaiKeyLink = new LinkLabel { Text = "Get your Google AI Studio API key", Location = new Point(234, 394), AutoSize = true, LinkColor = AccentPrimary, ActiveLinkColor = AccentHover, BackColor = Color.Transparent, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        gaiKeyLink.LinkClicked += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://aistudio.google.com/apikey", UseShellExecute = true });

        gaiCard.Controls.AddRange([gaiSectionLabel, gaiDescription, gaiKeyLabel, _gaiApiKeyBox, _gaiFetchButton, _gaiStatusLabel, gaiSearchLabel, _gaiSearchBox, _gaiModelListView, _gaiCreateProfileButton, gaiKeyLink]);
        gaiTab.Controls.Add(gaiCard);

        // ═══ TAB 4: NVIDIA ═══
        var nvTab = new TabPageAdv("NVIDIA") { BackColor = BgPage };
        var nvCard = new SectionCard { Location = new Point(4, 4), Size = new Size(cardW, formH - 96), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        var nvSectionLabel = MakeSectionLabel("BROWSE NVIDIA MODELS"); nvSectionLabel.Location = new Point(innerPad, 10);
        var nvDescription = new Label { Text = "Browse models from NVIDIA Build and create a profile with one click.", Location = new Point(innerPad, 28), Size = new Size(cardW - innerPad * 2, 18), ForeColor = TextMuted, Font = new Font("Segoe UI", 8f), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        _nvFetchButton = MakeSecondary("Fetch Models", 120, 28); _nvFetchButton.Location = new Point(innerPad, 52); _nvFetchButton.Click += NvFetchModels_Click;
        _nvStatusLabel = new Label { Text = "", Location = new Point(142, 58), Size = new Size(cardW - 142 - innerPad, 17), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

        var nvSearchLabel = new Label { Text = "Search:", Location = new Point(innerPad, 90), Size = new Size(50, 17), TextAlign = ContentAlignment.MiddleLeft, ForeColor = TextBody, BackColor = Color.Transparent };
        _nvSearchBox = new TextBox { Location = new Point(68, 88), Size = new Size(200, 23) }; _nvSearchBox.TextChanged += NvSearch_Changed;

        _nvModelListView = new ListView { Location = new Point(innerPad, 118), Size = new Size(cardW - innerPad * 2, 264), View = View.Details, FullRowSelect = true, GridLines = true, MultiSelect = false, HideSelection = false, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
        _nvModelListView.Columns.Add("Model ID", 290); _nvModelListView.Columns.Add("Owner", 160);

        _nvCreateProfileButton = MakePrimary("Create Profile from Selected", 210, 30); _nvCreateProfileButton.Location = new Point(innerPad, 390); _nvCreateProfileButton.Enabled = false; _nvCreateProfileButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left; _nvCreateProfileButton.Click += NvCreateProfile_Click;
        _nvModelListView.SelectedIndexChanged += (_, _) => _nvCreateProfileButton.Enabled = _nvModelListView.SelectedItems.Count > 0;

        var nvKeyLink = new LinkLabel { Text = "Get your NVIDIA API key", Location = new Point(234, 396), AutoSize = true, LinkColor = AccentPrimary, ActiveLinkColor = AccentHover, BackColor = Color.Transparent, Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
        nvKeyLink.LinkClicked += (_, _) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = "https://build.nvidia.com/models", UseShellExecute = true });

        nvCard.Controls.AddRange([nvSectionLabel, nvDescription, _nvFetchButton, _nvStatusLabel, nvSearchLabel, _nvSearchBox, _nvModelListView, _nvCreateProfileButton, nvKeyLink]);
        nvTab.Controls.Add(nvCard);

        // Assemble
        tabControl.Controls.Add(settingsTab); tabControl.Controls.Add(openRouterTab); tabControl.Controls.Add(gaiTab); tabControl.Controls.Add(nvTab);

        _saveButton = MakePrimary("OK", 80, 30); _saveButton.Location = new Point(formW - 168, formH - 36); _saveButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; _saveButton.Click += SaveButton_Click;
        _cancelButton = MakeSecondary("Cancel", 80, 30); _cancelButton.Location = new Point(formW - 84, formH - 36); _cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right; _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange([tabControl, _saveButton, _cancelButton]);
        AcceptButton = _saveButton; CancelButton = _cancelButton;

        RefreshProfileList(_config.ActiveProfile);
    }

    // ── OpenRouter ──
    private sealed class OpenRouterModel { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public int ContextLength { get; set; } }
    private async void FetchModels_Click(object? sender, EventArgs e)
    {
        _fetchButton.Enabled = false; _fetchButton.Text = "Fetching..."; _orStatusLabel.ForeColor = SystemColors.GrayText; _orStatusLabel.Text = "Downloading model list..."; _modelListView.Items.Clear(); _allModels.Clear();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; var json = await http.GetStringAsync("https://openrouter.ai/api/v1/models"); using var doc = JsonDocument.Parse(json);
            foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray())
            { if (!model.TryGetProperty("pricing", out var pricing)) continue; if ((pricing.TryGetProperty("prompt", out var pp) ? pp.GetString() : null) != "0" || (pricing.TryGetProperty("completion", out var cp) ? cp.GetString() : null) != "0") continue;
              _allModels.Add(new OpenRouterModel { Id = model.GetProperty("id").GetString() ?? "", Name = model.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "", ContextLength = model.TryGetProperty("context_length", out var cl) ? cl.GetInt32() : 0 }); }
            _allModels = _allModels.OrderBy(m => m.Name).ToList(); _filteredModels = _allModels; PopulateModelList();
            _orStatusLabel.ForeColor = Color.FromArgb(0, 128, 0); _orStatusLabel.Text = $"Found {_allModels.Count} free models.";
        }
        catch (Exception ex) { _orStatusLabel.ForeColor = Color.Red; _orStatusLabel.Text = ex.Message.Length > 60 ? ex.Message[..57] + "..." : ex.Message; }
        finally { _fetchButton.Enabled = true; _fetchButton.Text = "Fetch Free Models"; }
    }
    private void OrSearch_Changed(object? sender, EventArgs e) { var q = _orSearchBox.Text.Trim(); _filteredModels = string.IsNullOrEmpty(q) ? _allModels : _allModels.Where(m => m.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList(); PopulateModelList(); }
    private void PopulateModelList() { _modelListView.BeginUpdate(); _modelListView.Items.Clear(); foreach (var m in _filteredModels) { var item = new ListViewItem(m.Name); item.SubItems.Add(m.Id); item.SubItems.Add(m.ContextLength > 0 ? $"{m.ContextLength:N0}" : "\u2014"); item.Tag = m; _modelListView.Items.Add(item); } _modelListView.EndUpdate(); _createProfileButton.Enabled = false; }
    private void CreateProfileFromModel_Click(object? sender, EventArgs e) { if (_modelListView.SelectedItems.Count == 0) return; var model = (OpenRouterModel)_modelListView.SelectedItems[0].Tag!; var profileName = model.Name; if (_config.Profiles.ContainsKey(profileName) && MessageBox.Show(this, $"Profile \"{profileName}\" already exists. Overwrite?", "LLM-Rephraser", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; if (_profileBox.SelectedItem != null) SaveFieldsToProfile((string)_profileBox.SelectedItem); _config.Profiles[profileName] = new ProfileConfig { Provider = ApiProvider.OpenAICompatible, ApiEndpoint = "https://openrouter.ai/api/v1/chat/completions", ApiKey = "", ModelName = model.Id }; RefreshProfileList(profileName); MessageBox.Show(this, $"Profile \"{profileName}\" created.\n\nPlease enter your OpenRouter API key in the Settings tab.", "Profile Created", MessageBoxButtons.OK, MessageBoxIcon.Information); }

    // ── Google AI Studio ──
    private sealed class GeminiModel { public string Id { get; set; } = ""; public string DisplayName { get; set; } = ""; public int InputTokenLimit { get; set; } }
    private async void GaiFetchModels_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gaiApiKeyBox.Text)) { _gaiStatusLabel.ForeColor = Color.Red; _gaiStatusLabel.Text = "API key is required to fetch models."; return; }
        _gaiFetchButton.Enabled = false; _gaiFetchButton.Text = "Fetching..."; _gaiStatusLabel.ForeColor = SystemColors.GrayText; _gaiStatusLabel.Text = "Downloading model list..."; _gaiModelListView.Items.Clear(); _allGaiModels.Clear();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; var json = await http.GetStringAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={_gaiApiKeyBox.Text.Trim()}&pageSize=1000"); using var doc = JsonDocument.Parse(json);
            foreach (var model in doc.RootElement.GetProperty("models").EnumerateArray())
            { if (!model.TryGetProperty("supportedGenerationMethods", out var methods)) continue; bool ok = false; foreach (var m in methods.EnumerateArray()) { if (m.GetString() == "generateContent") { ok = true; break; } } if (!ok) continue;
              var name = model.GetProperty("name").GetString() ?? ""; var modelId = name.StartsWith("models/") ? name["models/".Length..] : name;
              _allGaiModels.Add(new GeminiModel { Id = modelId, DisplayName = model.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? name : name, InputTokenLimit = model.TryGetProperty("inputTokenLimit", out var il) ? il.GetInt32() : 0 }); }
            _allGaiModels = _allGaiModels.OrderBy(m => m.DisplayName).ToList(); _filteredGaiModels = _allGaiModels; PopulateGaiModelList();
            _gaiStatusLabel.ForeColor = Color.FromArgb(0, 128, 0); _gaiStatusLabel.Text = $"Found {_allGaiModels.Count} models.";
        }
        catch (Exception ex) { _gaiStatusLabel.ForeColor = Color.Red; _gaiStatusLabel.Text = ex.Message.Length > 70 ? ex.Message[..67] + "..." : ex.Message; }
        finally { _gaiFetchButton.Enabled = true; _gaiFetchButton.Text = "Fetch Models"; }
    }
    private void GaiSearch_Changed(object? sender, EventArgs e) { var q = _gaiSearchBox.Text.Trim(); _filteredGaiModels = string.IsNullOrEmpty(q) ? _allGaiModels : _allGaiModels.Where(m => m.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList(); PopulateGaiModelList(); }
    private void PopulateGaiModelList() { _gaiModelListView.BeginUpdate(); _gaiModelListView.Items.Clear(); foreach (var m in _filteredGaiModels) { var item = new ListViewItem(m.DisplayName); item.SubItems.Add(m.Id); item.SubItems.Add(m.InputTokenLimit > 0 ? $"{m.InputTokenLimit:N0}" : "\u2014"); item.Tag = m; _gaiModelListView.Items.Add(item); } _gaiModelListView.EndUpdate(); _gaiCreateProfileButton.Enabled = false; }
    private void GaiCreateProfile_Click(object? sender, EventArgs e) { if (_gaiModelListView.SelectedItems.Count == 0) return; var model = (GeminiModel)_gaiModelListView.SelectedItems[0].Tag!; var profileName = $"Google - {model.DisplayName}"; if (_config.Profiles.ContainsKey(profileName) && MessageBox.Show(this, $"Profile \"{profileName}\" already exists. Overwrite?", "LLM-Rephraser", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; if (_profileBox.SelectedItem != null) SaveFieldsToProfile((string)_profileBox.SelectedItem); _config.Profiles[profileName] = new ProfileConfig { Provider = ApiProvider.OpenAICompatible, ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions", ApiKey = _gaiApiKeyBox.Text.Trim(), ModelName = model.Id }; RefreshProfileList(profileName); MessageBox.Show(this, $"Profile \"{profileName}\" created.", "Profile Created", MessageBoxButtons.OK, MessageBoxIcon.Information); }

    // ── NVIDIA ──
    private sealed class NvidiaModel { public string Id { get; set; } = ""; public string Owner { get; set; } = ""; }
    private async void NvFetchModels_Click(object? sender, EventArgs e)
    {
        _nvFetchButton.Enabled = false; _nvFetchButton.Text = "Fetching..."; _nvStatusLabel.ForeColor = SystemColors.GrayText; _nvStatusLabel.Text = "Downloading model list..."; _nvModelListView.Items.Clear(); _allNvModels.Clear();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; var json = await http.GetStringAsync("https://integrate.api.nvidia.com/v1/models"); using var doc = JsonDocument.Parse(json);
            foreach (var model in doc.RootElement.GetProperty("data").EnumerateArray()) _allNvModels.Add(new NvidiaModel { Id = model.GetProperty("id").GetString() ?? "", Owner = model.TryGetProperty("owned_by", out var ob) ? ob.GetString() ?? "" : "" });
            _allNvModels = _allNvModels.OrderBy(m => m.Id).ToList(); _filteredNvModels = _allNvModels; PopulateNvModelList();
            _nvStatusLabel.ForeColor = Color.FromArgb(0, 128, 0); _nvStatusLabel.Text = $"Found {_allNvModels.Count} models.";
        }
        catch (Exception ex) { _nvStatusLabel.ForeColor = Color.Red; _nvStatusLabel.Text = ex.Message.Length > 70 ? ex.Message[..67] + "..." : ex.Message; }
        finally { _nvFetchButton.Enabled = true; _nvFetchButton.Text = "Fetch Models"; }
    }
    private void NvSearch_Changed(object? sender, EventArgs e) { var q = _nvSearchBox.Text.Trim(); _filteredNvModels = string.IsNullOrEmpty(q) ? _allNvModels : _allNvModels.Where(m => m.Id.Contains(q, StringComparison.OrdinalIgnoreCase) || m.Owner.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList(); PopulateNvModelList(); }
    private void PopulateNvModelList() { _nvModelListView.BeginUpdate(); _nvModelListView.Items.Clear(); foreach (var m in _filteredNvModels) { var item = new ListViewItem(m.Id); item.SubItems.Add(m.Owner); item.Tag = m; _nvModelListView.Items.Add(item); } _nvModelListView.EndUpdate(); _nvCreateProfileButton.Enabled = false; }
    private void NvCreateProfile_Click(object? sender, EventArgs e) { if (_nvModelListView.SelectedItems.Count == 0) return; var model = (NvidiaModel)_nvModelListView.SelectedItems[0].Tag!; var profileName = $"NVIDIA - {model.Id}"; if (_config.Profiles.ContainsKey(profileName) && MessageBox.Show(this, $"Profile \"{profileName}\" already exists. Overwrite?", "LLM-Rephraser", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; if (_profileBox.SelectedItem != null) SaveFieldsToProfile((string)_profileBox.SelectedItem); _config.Profiles[profileName] = new ProfileConfig { Provider = ApiProvider.OpenAICompatible, ApiEndpoint = "https://integrate.api.nvidia.com/v1/chat/completions", ApiKey = "", ModelName = model.Id }; RefreshProfileList(profileName); MessageBox.Show(this, $"Profile \"{profileName}\" created.\n\nPlease enter your NVIDIA API key in the Settings tab.", "Profile Created", MessageBoxButtons.OK, MessageBoxIcon.Information); }

    // ── Settings tab methods ──
    private void RefreshProfileList(string selectName) { _suppressProfileSwitch = true; _profileBox.Items.Clear(); foreach (var name in _config.Profiles.Keys.OrderBy(k => k)) _profileBox.Items.Add(name); var idx = _profileBox.Items.IndexOf(selectName); _profileBox.SelectedIndex = idx >= 0 ? idx : 0; _suppressProfileSwitch = false; LoadProfileIntoFields((string)_profileBox.SelectedItem!); _deleteButton.Enabled = _config.Profiles.Count > 1; }
    private void LoadProfileIntoFields(string profileName) { if (!_config.Profiles.TryGetValue(profileName, out var p)) p = new ProfileConfig(); _suppressProfileSwitch = true; _providerBox.SelectedIndex = p.Provider == ApiProvider.Anthropic ? 1 : 0; _endpointBox.Text = p.ApiEndpoint; _apiKeyBox.Text = p.ApiKey; _modelBox.Text = p.ModelName; _testResultLabel.Text = ""; _suppressProfileSwitch = false; }
    private void SaveFieldsToProfile(string profileName) { if (!_config.Profiles.ContainsKey(profileName)) _config.Profiles[profileName] = new ProfileConfig(); var p = _config.Profiles[profileName]; p.Provider = Providers[_providerBox.SelectedIndex].Value; p.ApiEndpoint = _endpointBox.Text.Trim(); p.ApiKey = _apiKeyBox.Text.Trim(); p.ModelName = _modelBox.Text.Trim(); }
    private void ProfileBox_Changed(object? sender, EventArgs e) { if (_suppressProfileSwitch || _profileBox.SelectedItem == null) return; LoadProfileIntoFields((string)_profileBox.SelectedItem); }
    private void AddProfile_Click(object? sender, EventArgs e) { var name = PromptForName("New Profile", "Profile name:"); if (name == null) return; if (_config.Profiles.ContainsKey(name)) { MessageBox.Show(this, $"A profile named \"{name}\" already exists.", "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; } SaveFieldsToProfile((string)_profileBox.SelectedItem!); _config.Profiles[name] = new ProfileConfig(); RefreshProfileList(name); }
    private void RenameProfile_Click(object? sender, EventArgs e) { var oldName = (string)_profileBox.SelectedItem!; var newName = PromptForName("Rename Profile", "New name:", oldName); if (newName == null || newName == oldName) return; if (_config.Profiles.ContainsKey(newName)) { MessageBox.Show(this, $"A profile named \"{newName}\" already exists.", "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; } SaveFieldsToProfile(oldName); var profile = _config.Profiles[oldName]; _config.Profiles.Remove(oldName); _config.Profiles[newName] = profile; if (_config.ActiveProfile == oldName) _config.ActiveProfile = newName; RefreshProfileList(newName); }
    private void DeleteProfile_Click(object? sender, EventArgs e) { if (_config.Profiles.Count <= 1) return; var name = (string)_profileBox.SelectedItem!; if (MessageBox.Show(this, $"Delete profile \"{name}\"?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; _config.Profiles.Remove(name); if (_config.ActiveProfile == name) _config.ActiveProfile = _config.Profiles.Keys.First(); RefreshProfileList(_config.ActiveProfile); }
    private void ProviderBox_Changed(object? sender, EventArgs e) { if (_suppressProfileSwitch) return; var idx = _providerBox.SelectedIndex; if (idx < 0 || idx >= Providers.Length) return; var (_, _, defaultEndpoint, defaultModel) = Providers[idx]; if (Providers.Any(p => _endpointBox.Text.Trim() == p.DefaultEndpoint) || string.IsNullOrWhiteSpace(_endpointBox.Text)) _endpointBox.Text = defaultEndpoint; if (Providers.Any(p => _modelBox.Text.Trim() == p.DefaultModel) || string.IsNullOrWhiteSpace(_modelBox.Text)) _modelBox.Text = defaultModel; }
    private async void TestButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_endpointBox.Text) || string.IsNullOrWhiteSpace(_modelBox.Text)) { _testResultLabel.ForeColor = Color.Red; _testResultLabel.Text = "Endpoint and Model are required."; return; }
        _testButton.Enabled = false; _testButton.Text = "Testing..."; _testResultLabel.ForeColor = SystemColors.GrayText; _testResultLabel.Text = "Sending test request...";
        var testProfile = new ProfileConfig { Provider = Providers[_providerBox.SelectedIndex].Value, ApiEndpoint = _endpointBox.Text.Trim(), ApiKey = _apiKeyBox.Text.Trim(), ModelName = _modelBox.Text.Trim() };
        using var client = new LlmClient(); using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await client.SendAsync(testProfile, "You are a test assistant. Reply with exactly: OK", "Say OK", cts.Token); _testResultLabel.ForeColor = Color.FromArgb(0, 128, 0); _testResultLabel.Text = "Connection successful."; }
        catch (OperationCanceledException) { _testResultLabel.ForeColor = Color.Red; _testResultLabel.Text = "Timed out (30 s)."; }
        catch (Exception ex) { _testResultLabel.ForeColor = Color.Red; _testResultLabel.Text = ex.Message.Length > 70 ? ex.Message[..67] + "..." : ex.Message; }
        finally { _testButton.Enabled = true; _testButton.Text = "&Test Connection"; }
    }
    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_endpointBox.Text)) { MessageBox.Show(this, "API Endpoint URL is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); _endpointBox.Focus(); return; }
        if (string.IsNullOrWhiteSpace(_modelBox.Text)) { MessageBox.Show(this, "Model Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); _modelBox.Focus(); return; }
        var profileName = (string)_profileBox.SelectedItem!; SaveFieldsToProfile(profileName); _config.ActiveProfile = profileName; _config.ShiftRightClickEnabled = _shiftRightClickBox.Checked; _config.StartWithWindows = _startWithWindowsBox.Checked; _config.TranslationLanguages = _langListBox.Items.Cast<string>().ToList(); _config.Save(); DialogResult = DialogResult.OK; Close();
    }
    private void LangAdd_Click(object? sender, EventArgs e) { var name = PromptForName("Add Language", "Language name:"); if (name == null) return; if (_langListBox.Items.Cast<string>().Any(l => l.Equals(name, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show(this, $"\"{name}\" is already in the list.", "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } _langListBox.Items.Add(name); }
    private void LangRemove_Click(object? sender, EventArgs e) { if (_langListBox.SelectedIndex >= 0) _langListBox.Items.RemoveAt(_langListBox.SelectedIndex); }

    private string? PromptForName(string title, string labelText, string defaultValue = "")
    {
        using var dlg = new Form { Text = title, ClientSize = new Size(340, 115), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, BackColor = BgPage, Font = Font };
        dlg.AutoScaleDimensions = new SizeF(7F, 15F); dlg.AutoScaleMode = AutoScaleMode.Font;
        var card = new SectionCard { Location = new Point(12, 8), Size = new Size(316, 60) };
        var lbl = new Label { Text = labelText, Location = new Point(12, 8), AutoSize = true, ForeColor = TextBody, BackColor = Color.Transparent };
        var txt = new TextBox { Text = defaultValue, Location = new Point(12, 30), Size = new Size(292, 23) }; txt.SelectAll();
        card.Controls.AddRange([lbl, txt]);
        var ok = MakePrimary("OK", 75, 27); ok.Location = new Point(172, 78); ok.DialogResult = DialogResult.OK;
        var cancel = MakeSecondary("Cancel", 75, 27); cancel.Location = new Point(253, 78); cancel.DialogResult = DialogResult.Cancel;
        dlg.Controls.AddRange([card, ok, cancel]); dlg.AcceptButton = ok; dlg.CancelButton = cancel;
        if (dlg.ShowDialog(this) != DialogResult.OK) return null; var name = txt.Text.Trim(); return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
