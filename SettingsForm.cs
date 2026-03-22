using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed class SettingsForm : Form
{
    // ── Settings tab fields ──
    private readonly ComboBox _profileBox;
    private readonly Button _addButton;
    private readonly Button _renameButton;
    private readonly Button _deleteButton;
    private readonly ComboBox _providerBox;
    private readonly TextBox _endpointBox;
    private readonly TextBox _apiKeyBox;
    private readonly TextBox _modelBox;
    private readonly Button _testButton;
    private readonly Label _testResultLabel;
    private readonly CheckBox _shiftRightClickBox;
    private readonly CheckBox _startWithWindowsBox;
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

    // ── Bottom buttons ──
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    private AppConfig _config;
    private bool _suppressProfileSwitch;

    private static readonly (string Label, ApiProvider Value, string DefaultEndpoint, string DefaultModel)[] Providers =
    [
        ("OpenAI-Compatible", ApiProvider.OpenAICompatible, "http://localhost:11434/v1/chat/completions", "llama3"),
        ("Anthropic Claude",  ApiProvider.Anthropic,        "https://api.anthropic.com/v1/messages",      "claude-sonnet-4-20250514")
    ];

    public SettingsForm(AppConfig config)
    {
        _config = config;

        Text = "LLM-Rephraser Settings";
        ClientSize = new Size(520, 560);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var tabControl = new TabControl
        {
            Location = new Point(8, 8),
            Size = new Size(504, 510),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        // ════════════════════════════════════════
        //  TAB 1: Settings
        // ════════════════════════════════════════
        var settingsTab = new TabPage("Settings") { Padding = new Padding(4) };

        // ── Profile GroupBox ──
        var profileGroup = new GroupBox
        {
            Text = "Profile",
            Location = new Point(8, 8),
            Size = new Size(480, 60)
        };

        _profileBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 24),
            Size = new Size(220, 23)
        };
        _profileBox.SelectedIndexChanged += ProfileBox_Changed;

        _addButton = new Button { Text = "New...", Location = new Point(242, 23), Size = new Size(65, 25) };
        _addButton.Click += AddProfile_Click;

        _renameButton = new Button { Text = "Rename...", Location = new Point(313, 23), Size = new Size(72, 25) };
        _renameButton.Click += RenameProfile_Click;

        _deleteButton = new Button { Text = "Delete", Location = new Point(391, 23), Size = new Size(57, 25) };
        _deleteButton.Click += DeleteProfile_Click;

        profileGroup.Controls.AddRange([_profileBox, _addButton, _renameButton, _deleteButton]);

        // ── Connection GroupBox ──
        var connectionGroup = new GroupBox
        {
            Text = "Connection",
            Location = new Point(8, 76),
            Size = new Size(480, 222)
        };

        var providerLabel = new Label { Text = "&Provider:", Location = new Point(12, 24), Size = new Size(100, 17), TextAlign = ContentAlignment.MiddleLeft };
        _providerBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(120, 22), Size = new Size(346, 23) };
        foreach (var (label, _, _, _) in Providers) _providerBox.Items.Add(label);
        _providerBox.SelectedIndexChanged += ProviderBox_Changed;

        var endpointLabel = new Label { Text = "&Endpoint URL:", Location = new Point(12, 58), Size = new Size(100, 17), TextAlign = ContentAlignment.MiddleLeft };
        _endpointBox = new TextBox { Location = new Point(120, 56), Size = new Size(346, 23) };

        var apiKeyLabel = new Label { Text = "API &Key:", Location = new Point(12, 92), Size = new Size(100, 17), TextAlign = ContentAlignment.MiddleLeft };
        _apiKeyBox = new TextBox { Location = new Point(120, 90), Size = new Size(346, 23), UseSystemPasswordChar = true };
        var apiKeyHint = new Label { Text = "Leave blank if not required", Location = new Point(120, 115), AutoSize = true, ForeColor = SystemColors.GrayText, Font = new Font("Segoe UI", 8f) };

        var modelLabel = new Label { Text = "&Model:", Location = new Point(12, 140), Size = new Size(100, 17), TextAlign = ContentAlignment.MiddleLeft };
        _modelBox = new TextBox { Location = new Point(120, 138), Size = new Size(346, 23) };

        var separator = new Label { BorderStyle = BorderStyle.Fixed3D, Location = new Point(12, 178), Size = new Size(456, 2) };
        _testButton = new Button { Text = "&Test Connection", Location = new Point(12, 192), Size = new Size(120, 28) };
        _testButton.Click += TestButton_Click;
        _testResultLabel = new Label { Text = "", Location = new Point(140, 198), Size = new Size(326, 17), TextAlign = ContentAlignment.MiddleLeft };

        connectionGroup.Controls.AddRange([providerLabel, _providerBox, endpointLabel, _endpointBox, apiKeyLabel, _apiKeyBox, apiKeyHint, modelLabel, _modelBox, separator, _testButton, _testResultLabel]);

        // ── Translation Languages ──
        var langGroup = new GroupBox { Text = "Translation Languages", Location = new Point(8, 306), Size = new Size(480, 100) };
        _langListBox = new ListBox { Location = new Point(12, 20), Size = new Size(376, 68), SelectionMode = SelectionMode.One };
        foreach (var lang in _config.TranslationLanguages) _langListBox.Items.Add(lang);
        _langAddButton = new Button { Text = "Add...", Location = new Point(396, 20), Size = new Size(72, 25) };
        _langAddButton.Click += LangAdd_Click;
        _langRemoveButton = new Button { Text = "Remove", Location = new Point(396, 50), Size = new Size(72, 25) };
        _langRemoveButton.Click += LangRemove_Click;
        langGroup.Controls.AddRange([_langListBox, _langAddButton, _langRemoveButton]);

        // ── Options ──
        var optionsGroup = new GroupBox { Text = "Options", Location = new Point(8, 414), Size = new Size(480, 62) };
        _shiftRightClickBox = new CheckBox { Text = "Enable Shift+Right-Click to open style picker", Location = new Point(12, 18), AutoSize = true, Checked = _config.ShiftRightClickEnabled };
        _startWithWindowsBox = new CheckBox { Text = "Start LLM-Rephraser with Windows", Location = new Point(12, 40), AutoSize = true, Checked = AppConfig.ReadStartWithWindows() };
        optionsGroup.Controls.AddRange([_shiftRightClickBox, _startWithWindowsBox]);

        settingsTab.Controls.AddRange([profileGroup, connectionGroup, langGroup, optionsGroup]);

        // ════════════════════════════════════════
        //  TAB 2: OpenRouter Free Models
        // ════════════════════════════════════════
        var openRouterTab = new TabPage("OpenRouter") { Padding = new Padding(4) };

        var orDescription = new Label
        {
            Text = "Browse free models from OpenRouter.ai and create a profile with one click. You only need to supply your OpenRouter API key.",
            Location = new Point(8, 8),
            Size = new Size(480, 34),
            ForeColor = SystemColors.GrayText
        };

        _fetchButton = new Button { Text = "Fetch Free Models", Location = new Point(8, 48), Size = new Size(140, 28) };
        _fetchButton.Click += FetchModels_Click;

        _orStatusLabel = new Label { Text = "", Location = new Point(156, 54), Size = new Size(330, 17), TextAlign = ContentAlignment.MiddleLeft };

        var searchLabel = new Label { Text = "Search:", Location = new Point(8, 86), Size = new Size(50, 17), TextAlign = ContentAlignment.MiddleLeft };
        _orSearchBox = new TextBox { Location = new Point(60, 84), Size = new Size(200, 23) };
        _orSearchBox.TextChanged += OrSearch_Changed;

        _modelListView = new ListView
        {
            Location = new Point(8, 114),
            Size = new Size(480, 310),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false
        };
        _modelListView.Columns.Add("Model Name", 220);
        _modelListView.Columns.Add("ID", 160);
        _modelListView.Columns.Add("Context", 80, HorizontalAlignment.Right);

        _createProfileButton = new Button
        {
            Text = "Create Profile from Selected",
            Location = new Point(8, 432),
            Size = new Size(200, 30),
            Enabled = false
        };
        _createProfileButton.Click += CreateProfileFromModel_Click;
        _modelListView.SelectedIndexChanged += (_, _) => _createProfileButton.Enabled = _modelListView.SelectedItems.Count > 0;

        var orKeyLink = new LinkLabel
        {
            Text = "Get your OpenRouter API key",
            Location = new Point(220, 438),
            AutoSize = true
        };
        orKeyLink.LinkClicked += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://openrouter.ai/workspaces/default/keys",
                UseShellExecute = true
            });
        };

        openRouterTab.Controls.AddRange([orDescription, _fetchButton, _orStatusLabel, searchLabel, _orSearchBox, _modelListView, _createProfileButton, orKeyLink]);

        // ════════════════════════════════════════
        //  TAB 3: Google AI Studio
        // ════════════════════════════════════════
        var gaiTab = new TabPage("Google AI Studio") { Padding = new Padding(4) };

        var gaiDescription = new Label
        {
            Text = "Browse Gemini models from Google AI Studio and create a profile with one click.",
            Location = new Point(8, 8),
            Size = new Size(480, 20),
            ForeColor = SystemColors.GrayText
        };

        var gaiKeyLabel = new Label { Text = "API Key:", Location = new Point(8, 38), Size = new Size(55, 17), TextAlign = ContentAlignment.MiddleLeft };
        _gaiApiKeyBox = new TextBox { Location = new Point(65, 36), Size = new Size(260, 23), UseSystemPasswordChar = true };

        _gaiFetchButton = new Button { Text = "Fetch Models", Location = new Point(335, 35), Size = new Size(100, 25) };
        _gaiFetchButton.Click += GaiFetchModels_Click;

        _gaiStatusLabel = new Label { Text = "", Location = new Point(8, 66), Size = new Size(480, 17), TextAlign = ContentAlignment.MiddleLeft };

        var gaiSearchLabel = new Label { Text = "Search:", Location = new Point(8, 90), Size = new Size(50, 17), TextAlign = ContentAlignment.MiddleLeft };
        _gaiSearchBox = new TextBox { Location = new Point(60, 88), Size = new Size(200, 23) };
        _gaiSearchBox.TextChanged += GaiSearch_Changed;

        _gaiModelListView = new ListView
        {
            Location = new Point(8, 118),
            Size = new Size(480, 296),
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false
        };
        _gaiModelListView.Columns.Add("Display Name", 200);
        _gaiModelListView.Columns.Add("Model ID", 170);
        _gaiModelListView.Columns.Add("Context", 80, HorizontalAlignment.Right);

        _gaiCreateProfileButton = new Button
        {
            Text = "Create Profile from Selected",
            Location = new Point(8, 422),
            Size = new Size(200, 30),
            Enabled = false
        };
        _gaiCreateProfileButton.Click += GaiCreateProfile_Click;
        _gaiModelListView.SelectedIndexChanged += (_, _) => _gaiCreateProfileButton.Enabled = _gaiModelListView.SelectedItems.Count > 0;

        var gaiKeyLink = new LinkLabel
        {
            Text = "Get your Google AI Studio API key",
            Location = new Point(220, 428),
            AutoSize = true
        };
        gaiKeyLink.LinkClicked += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/apikey",
                UseShellExecute = true
            });
        };

        gaiTab.Controls.AddRange([gaiDescription, gaiKeyLabel, _gaiApiKeyBox, _gaiFetchButton, _gaiStatusLabel, gaiSearchLabel, _gaiSearchBox, _gaiModelListView, _gaiCreateProfileButton, gaiKeyLink]);

        tabControl.TabPages.Add(settingsTab);
        tabControl.TabPages.Add(openRouterTab);
        tabControl.TabPages.Add(gaiTab);

        // ── Bottom buttons ──
        _saveButton = new Button { Text = "OK", Location = new Point(352, 524), Size = new Size(75, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button { Text = "Cancel", Location = new Point(433, 524), Size = new Size(75, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange([tabControl, _saveButton, _cancelButton]);
        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        RefreshProfileList(_config.ActiveProfile);
    }

    // ──────────────────────────────────────────
    //  OpenRouter methods
    // ──────────────────────────────────────────

    private sealed class OpenRouterModel
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int ContextLength { get; set; }
    }

    private async void FetchModels_Click(object? sender, EventArgs e)
    {
        _fetchButton.Enabled = false;
        _fetchButton.Text = "Fetching...";
        _orStatusLabel.ForeColor = SystemColors.GrayText;
        _orStatusLabel.Text = "Downloading model list...";
        _modelListView.Items.Clear();
        _allModels.Clear();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var json = await http.GetStringAsync("https://openrouter.ai/api/v1/models");
            using var doc = JsonDocument.Parse(json);

            var data = doc.RootElement.GetProperty("data");
            foreach (var model in data.EnumerateArray())
            {
                // Check if free: pricing.prompt == "0" and pricing.completion == "0"
                if (!model.TryGetProperty("pricing", out var pricing)) continue;

                var promptPrice = pricing.TryGetProperty("prompt", out var pp) ? pp.GetString() : null;
                var completionPrice = pricing.TryGetProperty("completion", out var cp) ? cp.GetString() : null;

                if (promptPrice != "0" || completionPrice != "0") continue;

                var id = model.GetProperty("id").GetString() ?? "";
                var name = model.TryGetProperty("name", out var n) ? n.GetString() ?? id : id;
                var ctx = model.TryGetProperty("context_length", out var cl) ? cl.GetInt32() : 0;

                _allModels.Add(new OpenRouterModel { Id = id, Name = name, ContextLength = ctx });
            }

            _allModels = _allModels.OrderBy(m => m.Name).ToList();
            _filteredModels = _allModels;
            PopulateModelList();

            _orStatusLabel.ForeColor = Color.FromArgb(0, 128, 0);
            _orStatusLabel.Text = $"Found {_allModels.Count} free models.";
        }
        catch (Exception ex)
        {
            _orStatusLabel.ForeColor = Color.Red;
            _orStatusLabel.Text = ex.Message.Length > 60 ? ex.Message[..57] + "..." : ex.Message;
        }
        finally
        {
            _fetchButton.Enabled = true;
            _fetchButton.Text = "Fetch Free Models";
        }
    }

    private void OrSearch_Changed(object? sender, EventArgs e)
    {
        var query = _orSearchBox.Text.Trim();
        _filteredModels = string.IsNullOrEmpty(query)
            ? _allModels
            : _allModels.Where(m =>
                m.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        PopulateModelList();
    }

    private void PopulateModelList()
    {
        _modelListView.BeginUpdate();
        _modelListView.Items.Clear();
        foreach (var m in _filteredModels)
        {
            var item = new ListViewItem(m.Name);
            item.SubItems.Add(m.Id);
            item.SubItems.Add(m.ContextLength > 0 ? $"{m.ContextLength:N0}" : "—");
            item.Tag = m;
            _modelListView.Items.Add(item);
        }
        _modelListView.EndUpdate();
        _createProfileButton.Enabled = false;
    }

    private void CreateProfileFromModel_Click(object? sender, EventArgs e)
    {
        if (_modelListView.SelectedItems.Count == 0) return;
        var model = (OpenRouterModel)_modelListView.SelectedItems[0].Tag!;

        // Generate a profile name from the model name
        var profileName = model.Name;
        if (_config.Profiles.ContainsKey(profileName))
        {
            var result = MessageBox.Show(this,
                $"Profile \"{profileName}\" already exists. Overwrite it?",
                "LLM-Rephraser", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
        }

        // Save current profile edits first
        if (_profileBox.SelectedItem != null)
            SaveFieldsToProfile((string)_profileBox.SelectedItem);

        _config.Profiles[profileName] = new ProfileConfig
        {
            Provider = ApiProvider.OpenAICompatible,
            ApiEndpoint = "https://openrouter.ai/api/v1/chat/completions",
            ApiKey = "",
            ModelName = model.Id
        };

        RefreshProfileList(profileName);

        MessageBox.Show(this,
            $"Profile \"{profileName}\" created.\n\nEndpoint and model are pre-filled.\nPlease enter your OpenRouter API key in the Settings tab.",
            "Profile Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ──────────────────────────────────────────
    //  Google AI Studio methods
    // ──────────────────────────────────────────

    private sealed class GeminiModel
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public int InputTokenLimit { get; set; }
    }

    private async void GaiFetchModels_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_gaiApiKeyBox.Text))
        {
            _gaiStatusLabel.ForeColor = Color.Red;
            _gaiStatusLabel.Text = "API key is required to fetch models.";
            return;
        }

        _gaiFetchButton.Enabled = false;
        _gaiFetchButton.Text = "Fetching...";
        _gaiStatusLabel.ForeColor = SystemColors.GrayText;
        _gaiStatusLabel.Text = "Downloading model list...";
        _gaiModelListView.Items.Clear();
        _allGaiModels.Clear();

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={_gaiApiKeyBox.Text.Trim()}&pageSize=1000";
            var json = await http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var models = doc.RootElement.GetProperty("models");
            foreach (var model in models.EnumerateArray())
            {
                // Only include models that support generateContent
                if (!model.TryGetProperty("supportedGenerationMethods", out var methods)) continue;
                bool supportsGenerate = false;
                foreach (var m in methods.EnumerateArray())
                {
                    if (m.GetString() == "generateContent") { supportsGenerate = true; break; }
                }
                if (!supportsGenerate) continue;

                var name = model.GetProperty("name").GetString() ?? "";
                var displayName = model.TryGetProperty("displayName", out var dn) ? dn.GetString() ?? name : name;
                var inputLimit = model.TryGetProperty("inputTokenLimit", out var il) ? il.GetInt32() : 0;

                // Strip "models/" prefix for the ID used in API calls
                var modelId = name.StartsWith("models/") ? name["models/".Length..] : name;

                _allGaiModels.Add(new GeminiModel { Id = modelId, DisplayName = displayName, InputTokenLimit = inputLimit });
            }

            _allGaiModels = _allGaiModels.OrderBy(m => m.DisplayName).ToList();
            _filteredGaiModels = _allGaiModels;
            PopulateGaiModelList();

            _gaiStatusLabel.ForeColor = Color.FromArgb(0, 128, 0);
            _gaiStatusLabel.Text = $"Found {_allGaiModels.Count} models.";
        }
        catch (Exception ex)
        {
            _gaiStatusLabel.ForeColor = Color.Red;
            _gaiStatusLabel.Text = ex.Message.Length > 70 ? ex.Message[..67] + "..." : ex.Message;
        }
        finally
        {
            _gaiFetchButton.Enabled = true;
            _gaiFetchButton.Text = "Fetch Models";
        }
    }

    private void GaiSearch_Changed(object? sender, EventArgs e)
    {
        var query = _gaiSearchBox.Text.Trim();
        _filteredGaiModels = string.IsNullOrEmpty(query)
            ? _allGaiModels
            : _allGaiModels.Where(m =>
                m.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                m.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        PopulateGaiModelList();
    }

    private void PopulateGaiModelList()
    {
        _gaiModelListView.BeginUpdate();
        _gaiModelListView.Items.Clear();
        foreach (var m in _filteredGaiModels)
        {
            var item = new ListViewItem(m.DisplayName);
            item.SubItems.Add(m.Id);
            item.SubItems.Add(m.InputTokenLimit > 0 ? $"{m.InputTokenLimit:N0}" : "—");
            item.Tag = m;
            _gaiModelListView.Items.Add(item);
        }
        _gaiModelListView.EndUpdate();
        _gaiCreateProfileButton.Enabled = false;
    }

    private void GaiCreateProfile_Click(object? sender, EventArgs e)
    {
        if (_gaiModelListView.SelectedItems.Count == 0) return;
        var model = (GeminiModel)_gaiModelListView.SelectedItems[0].Tag!;

        var profileName = $"Google - {model.DisplayName}";
        if (_config.Profiles.ContainsKey(profileName))
        {
            var result = MessageBox.Show(this,
                $"Profile \"{profileName}\" already exists. Overwrite it?",
                "LLM-Rephraser", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;
        }

        if (_profileBox.SelectedItem != null)
            SaveFieldsToProfile((string)_profileBox.SelectedItem);

        _config.Profiles[profileName] = new ProfileConfig
        {
            Provider = ApiProvider.OpenAICompatible,
            ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions",
            ApiKey = _gaiApiKeyBox.Text.Trim(),
            ModelName = model.Id
        };

        RefreshProfileList(profileName);

        MessageBox.Show(this,
            $"Profile \"{profileName}\" created.\n\nEndpoint, model, and API key are pre-filled.",
            "Profile Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ──────────────────────────────────────────
    //  Settings tab methods
    // ──────────────────────────────────────────

    private void RefreshProfileList(string selectName)
    {
        _suppressProfileSwitch = true;
        _profileBox.Items.Clear();
        foreach (var name in _config.Profiles.Keys.OrderBy(k => k))
            _profileBox.Items.Add(name);

        var idx = _profileBox.Items.IndexOf(selectName);
        _profileBox.SelectedIndex = idx >= 0 ? idx : 0;
        _suppressProfileSwitch = false;

        LoadProfileIntoFields((string)_profileBox.SelectedItem!);
        _deleteButton.Enabled = _config.Profiles.Count > 1;
    }

    private void LoadProfileIntoFields(string profileName)
    {
        if (!_config.Profiles.TryGetValue(profileName, out var p))
            p = new ProfileConfig();

        _suppressProfileSwitch = true;
        _providerBox.SelectedIndex = p.Provider == ApiProvider.Anthropic ? 1 : 0;
        _endpointBox.Text = p.ApiEndpoint;
        _apiKeyBox.Text = p.ApiKey;
        _modelBox.Text = p.ModelName;
        _testResultLabel.Text = "";
        _suppressProfileSwitch = false;
    }

    private void SaveFieldsToProfile(string profileName)
    {
        if (!_config.Profiles.ContainsKey(profileName))
            _config.Profiles[profileName] = new ProfileConfig();

        var p = _config.Profiles[profileName];
        p.Provider = Providers[_providerBox.SelectedIndex].Value;
        p.ApiEndpoint = _endpointBox.Text.Trim();
        p.ApiKey = _apiKeyBox.Text.Trim();
        p.ModelName = _modelBox.Text.Trim();
    }

    private void ProfileBox_Changed(object? sender, EventArgs e)
    {
        if (_suppressProfileSwitch || _profileBox.SelectedItem == null) return;
        LoadProfileIntoFields((string)_profileBox.SelectedItem);
    }

    private void AddProfile_Click(object? sender, EventArgs e)
    {
        var name = PromptForName("New Profile", "Profile name:");
        if (name == null) return;

        if (_config.Profiles.ContainsKey(name))
        {
            MessageBox.Show(this, $"A profile named \"{name}\" already exists.",
                "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveFieldsToProfile((string)_profileBox.SelectedItem!);
        _config.Profiles[name] = new ProfileConfig();
        RefreshProfileList(name);
    }

    private void RenameProfile_Click(object? sender, EventArgs e)
    {
        var oldName = (string)_profileBox.SelectedItem!;
        var newName = PromptForName("Rename Profile", "New name:", oldName);
        if (newName == null || newName == oldName) return;

        if (_config.Profiles.ContainsKey(newName))
        {
            MessageBox.Show(this, $"A profile named \"{newName}\" already exists.",
                "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SaveFieldsToProfile(oldName);
        var profile = _config.Profiles[oldName];
        _config.Profiles.Remove(oldName);
        _config.Profiles[newName] = profile;

        if (_config.ActiveProfile == oldName)
            _config.ActiveProfile = newName;

        RefreshProfileList(newName);
    }

    private void DeleteProfile_Click(object? sender, EventArgs e)
    {
        if (_config.Profiles.Count <= 1) return;

        var name = (string)_profileBox.SelectedItem!;
        if (MessageBox.Show(this, $"Delete profile \"{name}\"?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _config.Profiles.Remove(name);
        if (_config.ActiveProfile == name)
            _config.ActiveProfile = _config.Profiles.Keys.First();

        RefreshProfileList(_config.ActiveProfile);
    }

    private void ProviderBox_Changed(object? sender, EventArgs e)
    {
        if (_suppressProfileSwitch) return;

        var idx = _providerBox.SelectedIndex;
        if (idx < 0 || idx >= Providers.Length) return;

        var (_, _, defaultEndpoint, defaultModel) = Providers[idx];

        bool isDefaultEndpoint = Providers.Any(p => _endpointBox.Text.Trim() == p.DefaultEndpoint);
        bool isDefaultModel = Providers.Any(p => _modelBox.Text.Trim() == p.DefaultModel);

        if (isDefaultEndpoint || string.IsNullOrWhiteSpace(_endpointBox.Text))
            _endpointBox.Text = defaultEndpoint;
        if (isDefaultModel || string.IsNullOrWhiteSpace(_modelBox.Text))
            _modelBox.Text = defaultModel;
    }

    private async void TestButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_endpointBox.Text) || string.IsNullOrWhiteSpace(_modelBox.Text))
        {
            _testResultLabel.ForeColor = Color.Red;
            _testResultLabel.Text = "Endpoint and Model are required.";
            return;
        }

        _testButton.Enabled = false;
        _testButton.Text = "Testing...";
        _testResultLabel.ForeColor = SystemColors.GrayText;
        _testResultLabel.Text = "Sending test request...";

        var testProfile = new ProfileConfig
        {
            Provider = Providers[_providerBox.SelectedIndex].Value,
            ApiEndpoint = _endpointBox.Text.Trim(),
            ApiKey = _apiKeyBox.Text.Trim(),
            ModelName = _modelBox.Text.Trim()
        };

        using var client = new LlmClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            await client.SendAsync(testProfile,
                "You are a test assistant. Reply with exactly: OK", "Say OK", cts.Token);

            _testResultLabel.ForeColor = Color.FromArgb(0, 128, 0);
            _testResultLabel.Text = "Connection successful.";
        }
        catch (OperationCanceledException)
        {
            _testResultLabel.ForeColor = Color.Red;
            _testResultLabel.Text = "Timed out (30 s).";
        }
        catch (LlmException ex)
        {
            _testResultLabel.ForeColor = Color.Red;
            _testResultLabel.Text = ex.Message.Length > 70 ? ex.Message[..67] + "..." : ex.Message;
        }
        catch (Exception ex)
        {
            _testResultLabel.ForeColor = Color.Red;
            _testResultLabel.Text = ex.Message.Length > 70 ? ex.Message[..67] + "..." : ex.Message;
        }
        finally
        {
            _testButton.Enabled = true;
            _testButton.Text = "&Test Connection";
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_endpointBox.Text))
        {
            MessageBox.Show(this, "API Endpoint URL is required.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _endpointBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_modelBox.Text))
        {
            MessageBox.Show(this, "Model Name is required.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _modelBox.Focus();
            return;
        }

        var profileName = (string)_profileBox.SelectedItem!;
        SaveFieldsToProfile(profileName);
        _config.ActiveProfile = profileName;
        _config.ShiftRightClickEnabled = _shiftRightClickBox.Checked;
        _config.StartWithWindows = _startWithWindowsBox.Checked;
        _config.TranslationLanguages = _langListBox.Items.Cast<string>().ToList();
        _config.Save();

        DialogResult = DialogResult.OK;
        Close();
    }

    private void LangAdd_Click(object? sender, EventArgs e)
    {
        var name = PromptForName("Add Language", "Language name:");
        if (name == null) return;

        if (_langListBox.Items.Cast<string>().Any(l =>
            l.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(this, $"\"{name}\" is already in the list.",
                "LLM-Rephraser", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _langListBox.Items.Add(name);
    }

    private void LangRemove_Click(object? sender, EventArgs e)
    {
        if (_langListBox.SelectedIndex < 0) return;
        _langListBox.Items.RemoveAt(_langListBox.SelectedIndex);
    }

    private string? PromptForName(string title, string labelText, string defaultValue = "")
    {
        using var dlg = new Form
        {
            Text = title,
            ClientSize = new Size(320, 105),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            StartPosition = FormStartPosition.CenterParent,
            Font = Font
        };

        var lbl = new Label { Text = labelText, Location = new Point(12, 12), AutoSize = true };
        var txt = new TextBox { Text = defaultValue, Location = new Point(12, 32), Size = new Size(296, 23) };
        txt.SelectAll();

        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(152, 68), Size = new Size(75, 25) };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(233, 68), Size = new Size(75, 25) };

        dlg.Controls.AddRange([lbl, txt, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(this) != DialogResult.OK) return null;
        var name = txt.Text.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
