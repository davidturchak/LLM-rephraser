using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace LlmRephraser;

public sealed class SettingsForm : Form
{
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
    private readonly ListBox _langListBox;
    private readonly Button _langAddButton;
    private readonly Button _langRemoveButton;
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
        ClientSize = new Size(484, 508);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);
        Padding = new Padding(12);

        // ── Profile GroupBox ──
        var profileGroup = new GroupBox
        {
            Text = "Profile",
            Location = new Point(12, 8),
            Size = new Size(460, 60),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _profileBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 24),
            Size = new Size(220, 23)
        };
        _profileBox.SelectedIndexChanged += ProfileBox_Changed;

        _addButton = new Button
        {
            Text = "New...",
            Location = new Point(242, 23),
            Size = new Size(65, 25)
        };
        _addButton.Click += AddProfile_Click;

        _renameButton = new Button
        {
            Text = "Rename...",
            Location = new Point(313, 23),
            Size = new Size(72, 25)
        };
        _renameButton.Click += RenameProfile_Click;

        _deleteButton = new Button
        {
            Text = "Delete",
            Location = new Point(391, 23),
            Size = new Size(57, 25)
        };
        _deleteButton.Click += DeleteProfile_Click;

        profileGroup.Controls.AddRange([_profileBox, _addButton, _renameButton, _deleteButton]);

        // ── Connection GroupBox ──
        var connectionGroup = new GroupBox
        {
            Text = "Connection",
            Location = new Point(12, 76),
            Size = new Size(460, 222),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var providerLabel = new Label
        {
            Text = "&Provider:",
            Location = new Point(12, 24),
            Size = new Size(100, 17),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _providerBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(120, 22),
            Size = new Size(326, 23)
        };
        foreach (var (label, _, _, _) in Providers)
            _providerBox.Items.Add(label);
        _providerBox.SelectedIndexChanged += ProviderBox_Changed;

        var endpointLabel = new Label
        {
            Text = "&Endpoint URL:",
            Location = new Point(12, 58),
            Size = new Size(100, 17),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _endpointBox = new TextBox
        {
            Location = new Point(120, 56),
            Size = new Size(326, 23)
        };

        var apiKeyLabel = new Label
        {
            Text = "API &Key:",
            Location = new Point(12, 92),
            Size = new Size(100, 17),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _apiKeyBox = new TextBox
        {
            Location = new Point(120, 90),
            Size = new Size(326, 23),
            UseSystemPasswordChar = true
        };

        var apiKeyHint = new Label
        {
            Text = "Leave blank if not required",
            Location = new Point(120, 115),
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Font = new Font("Segoe UI", 8f)
        };

        var modelLabel = new Label
        {
            Text = "&Model:",
            Location = new Point(12, 140),
            Size = new Size(100, 17),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _modelBox = new TextBox
        {
            Location = new Point(120, 138),
            Size = new Size(326, 23)
        };

        // ── Test section inside connection group ──
        var separator = new Label
        {
            BorderStyle = BorderStyle.Fixed3D,
            Location = new Point(12, 178),
            Size = new Size(436, 2)
        };

        _testButton = new Button
        {
            Text = "&Test Connection",
            Location = new Point(12, 192),
            Size = new Size(120, 28)
        };
        _testButton.Click += TestButton_Click;

        _testResultLabel = new Label
        {
            Text = "",
            Location = new Point(140, 198),
            Size = new Size(306, 17),
            TextAlign = ContentAlignment.MiddleLeft
        };

        connectionGroup.Controls.AddRange([
            providerLabel, _providerBox,
            endpointLabel, _endpointBox,
            apiKeyLabel, _apiKeyBox, apiKeyHint,
            modelLabel, _modelBox,
            separator, _testButton, _testResultLabel
        ]);

        // ── Translation Languages GroupBox ──
        var langGroup = new GroupBox
        {
            Text = "Translation Languages",
            Location = new Point(12, 306),
            Size = new Size(460, 100),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _langListBox = new ListBox
        {
            Location = new Point(12, 20),
            Size = new Size(360, 68),
            SelectionMode = SelectionMode.One
        };
        foreach (var lang in _config.TranslationLanguages)
            _langListBox.Items.Add(lang);

        _langAddButton = new Button
        {
            Text = "Add...",
            Location = new Point(380, 20),
            Size = new Size(68, 25)
        };
        _langAddButton.Click += LangAdd_Click;

        _langRemoveButton = new Button
        {
            Text = "Remove",
            Location = new Point(380, 50),
            Size = new Size(68, 25)
        };
        _langRemoveButton.Click += LangRemove_Click;

        langGroup.Controls.AddRange([_langListBox, _langAddButton, _langRemoveButton]);

        // ── Options GroupBox ──
        var optionsGroup = new GroupBox
        {
            Text = "Options",
            Location = new Point(12, 414),
            Size = new Size(460, 48),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _shiftRightClickBox = new CheckBox
        {
            Text = "Enable Shift+Right-Click to open style picker",
            Location = new Point(12, 20),
            AutoSize = true,
            Checked = _config.ShiftRightClickEnabled
        };

        optionsGroup.Controls.Add(_shiftRightClickBox);

        // ── Bottom buttons ──
        _saveButton = new Button
        {
            Text = "OK",
            Location = new Point(316, 470),
            Size = new Size(75, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Cancel",
            Location = new Point(397, 470),
            Size = new Size(75, 28),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right
        };
        _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.AddRange([profileGroup, connectionGroup, langGroup, optionsGroup, _saveButton, _cancelButton]);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        // Tab order
        _profileBox.TabIndex = 0;
        _addButton.TabIndex = 1;
        _renameButton.TabIndex = 2;
        _deleteButton.TabIndex = 3;
        _providerBox.TabIndex = 4;
        _endpointBox.TabIndex = 5;
        _apiKeyBox.TabIndex = 6;
        _modelBox.TabIndex = 7;
        _testButton.TabIndex = 8;
        _saveButton.TabIndex = 9;
        _cancelButton.TabIndex = 10;

        RefreshProfileList(_config.ActiveProfile);
    }

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

        // Auto-save edits to the previous profile before switching
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

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(152, 68),
            Size = new Size(75, 25)
        };
        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(233, 68),
            Size = new Size(75, 25)
        };

        dlg.Controls.AddRange([lbl, txt, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;

        if (dlg.ShowDialog(this) != DialogResult.OK) return null;
        var name = txt.Text.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }
}
