# LLM-Rephraser

A Windows system tray tool for rephrasing, translating, and fixing text in **any application** using LLM APIs. Select text, trigger via hotkey or Shift+Right-Click, choose a style, review the suggestion, and replace.

Supports **OpenAI**, **Anthropic Claude**, **Ollama**, **LM Studio**, **vLLM**, **LiteLLM**, and any OpenAI-compatible provider.

## Features

- **Global hotkey** (`Ctrl+Shift+R`) — works across all Windows applications
- **Shift+Right-Click trigger** — optional mouse-based activation (configurable in settings)
- **Rephrasing styles** — Rephrase, Make Formal, Make Concise, Fix Grammar
- **Translation** — translate to any language (customizable list in settings)
- **Editable suggestions** — review and tweak the AI suggestion before accepting
- **Auto-replace** — accepted text is pasted back into the original field automatically
- **Clipboard preservation** — your clipboard content is saved and restored after each operation
- **Multiple API profiles** — switch between Claude, OpenAI, Ollama, etc. from the tray menu
- **RTL support** — Hebrew, Arabic, and other RTL languages are right-aligned automatically
- **Dark-free native UI** — standard Windows look and feel
- **Single instance** — mutex-protected, only one instance runs at a time
- **System tray only** — no main window, minimal footprint

## How It Works

```
Select text → Ctrl+Shift+R (or Shift+Right-Click) →
Style picker appears → Choose a style →
API returns suggestion → Review/edit in dialog →
Accept: pastes replacement back | Cancel: nothing changes
```

## Installation

### MSI Installer (Recommended)

Download `LLM-Rephraser.msi` from the [`Installer/`](Installer/) directory and run it.

- Installs per-user to `%LocalAppData%\LLM-Rephraser\` (no admin required)
- Creates Start Menu and Desktop shortcuts
- Optional: Run at Windows startup
- Uninstall via Windows Settings > Apps

### Build from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or later).

```bash
git clone https://github.com/davidturchak/LLM-rephraser.git
cd LLM-rephraser
dotnet build -c Release
```

Run the executable:

```bash
dotnet run -c Release
```

Or publish as a self-contained single file:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: `bin/Release/net10.0-windows/win-x64/publish/LLM-Rephraser.exe`

## Configuration

On first run, the Settings dialog opens automatically. You can also access it anytime via **right-click tray icon > Settings**.

### API Profiles

Create multiple profiles to switch between different LLM providers:

| Setting | Description |
|---|---|
| **Provider** | `OpenAI-Compatible` or `Anthropic Claude` |
| **Endpoint URL** | API endpoint (e.g., `https://api.anthropic.com/v1/messages`) |
| **API Key** | Bearer token (leave blank for local models like Ollama) |
| **Model** | Model name (e.g., `claude-sonnet-4-20250514`, `gpt-4o`, `llama3`) |

Use **Test Connection** to verify your configuration before saving.

Switch between profiles quickly via **right-click tray icon > Profile**.

### Provider Examples

| Provider | Endpoint | Model |
|---|---|---|
| Ollama (local) | `http://localhost:11434/v1/chat/completions` | `llama3` |
| Anthropic Claude | `https://api.anthropic.com/v1/messages` | `claude-sonnet-4-20250514` |
| OpenAI | `https://api.openai.com/v1/chat/completions` | `gpt-4o` |
| LM Studio | `http://localhost:1234/v1/chat/completions` | (your loaded model) |

### Translation Languages

Add or remove translation languages from **Settings > Translation Languages**. Any language name is supported — the app generates the appropriate prompt automatically.

Default languages: English, Hebrew, Arabic, Russian.

### Options

| Option | Description |
|---|---|
| **Shift+Right-Click** | Enable/disable the Shift+Right-Click trigger (off by default) |

### Config File

Settings are stored in `%APPDATA%\LLM-Rephraser\config.json`.

## Rephrasing Styles

| Style | What it does |
|---|---|
| **Rephrase** | Improves clarity and readability while preserving meaning |
| **Make Formal** | Rewrites in a professional, formal tone |
| **Make Concise** | Removes unnecessary words, keeps it to the point |
| **Fix Grammar** | Corrects grammar, spelling, and punctuation |
| **Translate** | Translates to the selected language |

## Project Structure

```
LLM-Rephraser/
├── Program.cs                  # Entry point, single-instance mutex
├── TrayApplicationContext.cs   # Tray icon, hotkey, menu, orchestration
├── AppConfig.cs                # Config load/save, profiles, JSON persistence
├── LlmClient.cs               # HTTP client for OpenAI and Anthropic APIs
├── SettingsForm.cs             # Settings dialog (profiles, languages, options)
├── ResultForm.cs               # Original vs Suggested comparison dialog
├── HotkeyWindow.cs             # NativeWindow subclass for WM_HOTKEY
├── MouseHookWindow.cs          # Low-level mouse hook for Shift+Right-Click
├── app.ico                     # Application icon (multi-size)
├── Installer/
│   ├── Product.wxs             # WiX v6 installer source
│   ├── License.rtf             # License for installer UI
│   └── LLM-Rephraser.msi      # Built MSI installer
└── LICENSE                     # MIT License
```

## Building the MSI Installer

Requires [WiX Toolset v6](https://wixtoolset.org/):

```bash
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext -g
```

First publish the app, then build the MSI:

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
cd Installer
wix build Product.wxs -ext WixToolset.UI.wixext -o LLM-Rephraser.msi -arch x64
```

## Requirements

- Windows 10/11 (x64)
- .NET 10 Runtime (included in self-contained builds and MSI installer)

## License

[MIT](LICENSE)
