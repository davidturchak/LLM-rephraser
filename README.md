# LLM-Rephraser

A Windows system tray tool for rephrasing, translating, and fixing text in **any application** using LLM APIs. Select text, trigger via hotkey or Shift+Right-Click, choose a style, review the suggestion, and replace.

Supports **OpenAI**, **Anthropic Claude**, **Google Gemini**, **NVIDIA**, **OpenRouter**, **Ollama**, **LM Studio**, **vLLM**, **LiteLLM**, and any OpenAI-compatible provider.

## Features

- **Global hotkey** (`Ctrl+Shift+R`) — works across all Windows applications
- **Shift+Right-Click trigger** — optional mouse-based activation (configurable in settings)
- **Auto-detect text selection** — select text in any app and the style picker appears automatically (Grammarly-style, configurable)
- **Windows right-click menu** — optional "Rephrase with LLM-Rephraser" entry in the Windows Explorer/Desktop context menu
- **Rephrasing styles** — Rephrase, Make Formal, Make Concise, Fix Grammar
- **Translation** — translate and rephrase to any language (customizable list in settings)
- **Editable suggestions** — review and tweak the AI suggestion before accepting
- **Auto-replace** — accepted text is pasted back into the original field automatically
- **Clipboard preservation** — your clipboard content is saved and restored after each operation
- **Multiple API profiles** — switch between providers from the tray menu
- **Built-in model browsers** — browse and select models from OpenRouter, Google AI Studio, and NVIDIA directly in settings
- **RTL support** — Hebrew, Arabic, and other RTL languages are right-aligned automatically
- **Request logging** — all requests and responses logged with timestamps to `%APPDATA%\LLM-Rephraser\logs\`
- **Syncfusion UI** — modern, DPI-aware interface with adaptive layout for all screen resolutions
- **Single instance** — mutex-protected, only one instance runs at a time

## How It Works

```
Select text → style picker appears automatically (or use Ctrl+Shift+R / Shift+Right-Click) →
Choose a style → API returns suggestion →
Review/edit in dialog → Accept: pastes replacement back | Cancel: nothing changes
```

## Installation

### MSI Installer (Recommended)

Download `LLM-Rephraser.msi` from the [`Installer/`](Installer/) directory and run it.

- Installs per-user to `%LocalAppData%\LLM-Rephraser\` (no admin required)
- Creates Start Menu and Desktop shortcuts
- Launches automatically after install
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

Settings has four tabs: **Settings**, **OpenRouter**, **Google AI Studio**, and **NVIDIA**.

### API Profiles

Create multiple profiles to switch between different LLM providers:

| Setting | Description |
|---|---|
| **Provider** | `OpenAI-Compatible` or `Anthropic Claude` |
| **Endpoint URL** | API endpoint |
| **API Key** | Bearer token (leave blank for local models like Ollama) |
| **Model** | Model name/ID |

Use **Test Connection** to verify your configuration before saving.

Switch between profiles quickly via **right-click tray icon > Profile**.

### Supported Providers

| Provider | Endpoint | Model Example |
|---|---|---|
| **OpenRouter** | `https://openrouter.ai/api/v1/chat/completions` | `anthropic/claude-sonnet-4-20250514` |
| **Anthropic Claude** | `https://api.anthropic.com/v1/messages` | `claude-sonnet-4-20250514` |
| **OpenAI** | `https://api.openai.com/v1/chat/completions` | `gpt-4o` |
| **Google Gemini** | `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions` | `gemini-2.5-flash` |
| **NVIDIA Build** | `https://integrate.api.nvidia.com/v1/chat/completions` | `meta/llama-3.1-70b-instruct` |
| **Ollama** (local) | `http://localhost:11434/v1/chat/completions` | `llama3` |
| **LM Studio** (local) | `http://localhost:1234/v1/chat/completions` | (your loaded model) |

### Model Browser Tabs

Instead of manually entering endpoints and model IDs, use the built-in model browser tabs:

- **OpenRouter** — fetches free models from OpenRouter.ai, select one and create a profile with one click
- **Google AI Studio** — enter your Gemini API key, browse all available Gemini models, create a profile
- **NVIDIA** — browse models from build.nvidia.com (no key needed to browse), create a profile

Each tab includes a link to get an API key from the respective provider.

### Translation Languages

Add or remove translation languages from **Settings > Translation Languages**. Any language name is supported — the app generates the appropriate prompt automatically.

Default languages: English, Hebrew, Arabic, Russian.

### Options

| Option | Description |
|---|---|
| **Shift+Right-Click** | Enable/disable the Shift+Right-Click trigger (off by default) |
| **Show floating toolbar on text selection** | Auto-show style picker when text is selected by dragging (on by default) |
| **Add to Windows right-click menu** | Register "Rephrase with LLM-Rephraser" in Windows Explorer/Desktop context menu |
| **Start with Windows** | Launch LLM-Rephraser automatically on Windows startup |

### Config File

Settings are stored in `%APPDATA%\LLM-Rephraser\config.json`.

## Rephrasing Styles

| Style | What it does |
|---|---|
| **Rephrase** | Improves clarity and readability while preserving meaning |
| **Make Formal** | Rewrites in a professional, formal tone |
| **Make Concise** | Removes unnecessary words, keeps it to the point |
| **Fix Grammar** | Corrects grammar, spelling, and punctuation |
| **Translate** | Translates and rephrases to sound natural in the target language |

## Project Structure

```
LLM-Rephraser/
├── Program.cs                  # Entry point, single-instance mutex
├── TrayApplicationContext.cs   # Tray icon, hotkey, menu, orchestration
├── AppConfig.cs                # Config load/save, profiles, JSON persistence
├── AppLogger.cs                # Request/response file logging
├── LlmClient.cs               # HTTP client for OpenAI and Anthropic APIs
├── SettingsForm.cs             # Settings dialog (profiles, model browsers, options)
├── ResultForm.cs               # Original vs Suggested comparison dialog
├── HotkeyWindow.cs             # NativeWindow subclass for WM_HOTKEY
├── MouseHookWindow.cs          # Low-level mouse hook for Shift+Right-Click
├── SelectionDetector.cs        # Detects text selection gestures (mouse drag)
├── ContextMenuHelper.cs        # Windows Explorer/Desktop context menu registration
├── app.ico                     # Application icon (multi-size)
├── build-release.sh            # Build, version bump, package, commit & push
├── Installer/
│   ├── Product.wxs             # WiX v6 installer source
│   ├── License.rtf             # License for installer UI
│   └── LLM-Rephraser.msi      # Built MSI installer
└── LICENSE                     # MIT License
```

## Building & Releasing

Use the release script to build, bump version, package MSI, commit, and push in one step:

```bash
./build-release.sh "commit message"           # patch bump (default)
./build-release.sh "commit message" minor      # minor bump
./build-release.sh "commit message" major      # major bump
```

### Manual Build

Requires [WiX Toolset v6](https://wixtoolset.org/):

```bash
dotnet tool install --global wix
wix extension add WixToolset.UI.wixext -g
wix extension add WixToolset.Util.wixext -g
```

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
cd Installer
wix build Product.wxs -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o LLM-Rephraser.msi -arch x64
```

## Requirements

- Windows 10/11 (x64)
- .NET 10 Runtime (included in self-contained builds and MSI installer)

## License

[MIT](LICENSE)
