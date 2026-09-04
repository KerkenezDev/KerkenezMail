# Kerkenez Mail (Win32) 📬⚡
*A Native Windows Email Client with Full IMAP/SMTP Support and Optional Local AI*

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6?logo=windows&logoColor=white)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-10.0%20(Desktop%20Runtime)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![WinGet](https://img.shields.io/badge/WinGet-KerkenezDev.Mail-0078D6?logo=windows-terminal&logoColor=white)](https://github.com/microsoft/winget-pkgs)
[![Mail Engine](https://img.shields.io/badge/IMAP%20%26%20SMTP-MailKit%204.17-orange)](https://github.com/jstedfast/MailKit)
[![AI Engine](https://img.shields.io/badge/AI%20Engine-Optional%20(Local%20%2F%20Cloud%20%2F%20Disabled)-blue)](https://github.com/ggerganov/llama.cpp)
[![Security](https://img.shields.io/badge/Security-Windows%20DPAPI%20Encrypted-green)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)
[![License](https://img.shields.io/badge/License-MIT-yellow)](LICENSE)

**Kerkenez Mail** is a native Win32/Windows desktop email client engineered for speed, privacy, and full control over your inbox. Built with .NET 10 and MailKit, it delivers full IMAP folder navigation, real-time push email (RFC 2177 IDLE), rich SMTP mail composing with Markdown, attachment handling, and modern OAuth2 authentication — paired with an **entirely optional modular AI engine** for intelligent priority triage and executive email summarization.

Whether you run it as a pure, lightweight email client with **zero AI overhead**, or supercharge it with local **llama.cpp / Ollama** or **Cloud APIs**, Kerkenez Mail adapts to your workflow with a native Windows footprint, background system tray daemon, and bank-grade DPAPI encryption.

---

## 🚀 Quick Start

### 📦 Option 1: Install via WinGet (Recommended)
Install directly using Windows Package Manager in Windows Terminal or PowerShell:
```powershell
winget install KerkenezDev.Mail
```

### ⚡ Option 2: Standalone Download
1. Download **`KerkenezMail.exe`** (or `KerkenezMail.zip`) from [GitHub Releases](https://github.com/KerkenezDev/KerkenezMail/releases/latest).
2. Run **`KerkenezMail.exe`** (no installation required).
3. In the **Accounts** tab, click **➕ Add Account**, connect your mailbox, and you're ready!

---

## 🌟 Core Email Client Features

### 📬 Complete IMAP Folder Navigation & Lazy Sync
- **Full Folder Tree**: Seamlessly switch between **Inbox**, **Sent**, **Archived**, **Spam**, and **Trash** with lazy, on-demand synchronization.
- **Triage & Organization**: Move emails between folders, archive correspondence, send unwanted messages to Trash, or recover false-positives with the dedicated **Move to Inbox** action in Spam.
- **Strict Folder Isolation**: Independent sync queues with immediate cancellation of previous operations when switching folders to eliminate race conditions.
- **Smart Refresh**: Parallel multi-account fetching with per-account and global sync triggers.

### ⚡ Real-Time Push Email (Live IMAP IDLE)
- **RFC 2177 IDLE Engine**: Keep a persistent, low-power socket connection to your IMAP mail server.
- **Instant Alerts**: Detects incoming emails the second they hit the server without waiting for polling intervals.
- **NOOP Keep-Alive**: Automatic heartbeat management to prevent connection drops across firewalls and NAT routers.

### ✉️ Full Compose & Send Mail (SMTP)
- **Dedicated Compose View**: Standalone Send Mail sidebar tab plus modal compose/reply interfaces.
- **Markdown & Multi-Part RFC Sending**: Compose in rich Markdown with real-time **Plaintext** and **HTML** previews, transmitting clean multi-part MIME messages.
- **Attachment Management**: File attachment picker, drag-and-drop support, and configurable local attachment download directory.
- **Smart Sent Sync**: Automatically appends outgoing messages to your IMAP `Sent` mailbox (with intelligent deduplication for Microsoft Outlook/Exchange servers).

### 👥 Multi-Account Management & Modern Authentication
- **Unified Credential Store**: Manage multiple independent email accounts concurrently.
- **Microsoft Outlook OAuth 2.0 (PKCE)**: Modern browser-based authentication with local loopback listener and automatic 45-minute token refresh gating.
- **App Passwords & IMAP/SMTP**: First-class support for Gmail (Google App Passwords), Yahoo Mail, iCloud, and custom enterprise IMAP/SMTP hosts.
- **Suite-Shared Credentials**: Encrypted account profiles (`accounts.dat`) are interoperable across the Kerkenez desktop suite (shared with **KerkenezCalendar**).

---

## 🤖 Optional AI Engine & Smart Triage

*AI in Kerkenez Mail is 100% optional. You can disable it entirely with a single click, or configure local/cloud models to fit your hardware.*

### 🎯 3-Tier Priority Ranking
Automatically scores and tags incoming unread emails so you can focus on what matters:
- **Priority 1 (High / Urgent)**: Critical deadlines, server outages, CI/CD build failures, and urgent escalations (**Bold Red**).
- **Priority 2 (Normal correspondence)**: Routine work requests, general discussions, project updates (**Bold Blue**).
- **Priority 3 (Low / Newsletters / Bulk)**: Marketing campaigns, digests, automated system reports, and newsletters (**Bold Slate**).

### 📝 Executive Summaries
- Generates concise 1–3 sentence executive briefs in an objective third-person perspective.
- Full anti-scratchpad parsing for **Reasoning Models** (`DeepSeek-R1`, `Qwen-R1`): automatically strips internal `<think>...</think>` monologues and delivers only the clean summary.
- Preserves full email text alongside the summary card for instant reference.

### 🔋 Dynamic Battery Saver & Power Management
- **Auto VRAM Unload**: Instantly frees GPU memory and unloads local models when your laptop is unplugged from AC power.
- **Inference Pause**: AI processing pauses gracefully on battery to maximize laptop runtime, resuming automatically when plugged back into power.

### 🔌 Multi-Backend AI Options
- **No AI / Disabled**: Complete bypass — runs purely as a lightning-fast native email client with zero CPU/VRAM usage.
- **Local llama.cpp (GGUF)**: Embedded `llama-server` management with full GPU layer offloading (`-ngl 99`), configurable context window, and instant VRAM unload.
- **Local Ollama**: Connects to local Ollama endpoints with one-click model suggestions (`llama3.2`, `qwen2.5`, `mistral`).
- **Cloud APIs**: OpenAI (`gpt-4o-mini`), Groq, DeepSeek, OpenRouter, or custom OpenAI-compatible endpoints with DPAPI-encrypted API keys.

---

## 🔒 Security & Privacy

> [!IMPORTANT]
> **Your Data Stays Yours:**
> - In **No AI**, **Local llama.cpp**, or **Local Ollama** modes, **100% of your email data, credentials, and message content remain strictly offline on your local machine**.
> - Zero telemetry, zero analytics, zero external tracking.

### 🛡️ Windows DPAPI Credential Protection
- All email passwords, OAuth tokens, and Cloud API keys are encrypted at rest using Windows Data Protection API (`ProtectedData.Protect` tied to your Windows user account).
- Plaintext credentials exist only in volatile RAM during active network handshakes.

---

## 🖥️ Native Windows Integration & UI

- **Lightweight System Tray Daemon (`--daemon`)**: Runs in the background under **< 5 MB RAM** with native Win32 message loop, polling accounts and pushing Windows Action Center notifications.
- **Persistent Action Center Notifications**: Registered with Windows Application User Model ID (AUMID) for clickable notifications in the Windows Notification Center.
- **Compact Aligned Sidebar**: Narrow 168px/60px sidebar with hamburger toggle glyph (`\uE700`) styled to match **Kerkenez Calendar**.
- **Responsive UI Scaling**: Automatic display DPI awareness with quick presets (`Compact`, `Default`, `Large`, `Near Max`) and live window dimension calculation.
- **Global Localization (i18n)**: Comprehensive multi-language support (English 🇬🇧, Turkish 🇹🇷) with live on-the-fly switching and community translation guide.
- **Native Hyperlink Security**: Clickable links with safe hover URL previews, tracking pixel detection, and `[Remote Content]` tags.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + B` | Toggle collapsible navigation sidebar |
| `Ctrl + R` | Refresh current folder / parallel account sync |
| `Ctrl + S` | Export current email summaries (`.md` / `.txt`) |
| `Ctrl + C` | Copy active summary to clipboard |

---

## ⚙️ Command Line Switches

| Switch | Description |
| :--- | :--- |
| `KerkenezMail.exe` | Launches the main GUI client. |
| `KerkenezMail.exe --daemon` *(or `--tray`)* | Launches the background system tray daemon directly. |
| `KerkenezMail.exe --uninstall` | Interactive clean uninstallation of shortcuts, startup keys, and mail data. |
| `KerkenezMail.exe --uninstall --quiet` *(or `-s`)* | Silent/unattended uninstallation (preserves shared suite accounts). |

---

## 📁 Storage Architecture

Kerkenez Mail stores all user state under standard Windows AppData:
- **`%APPDATA%\Kerkenez\mail\config.json`**: Application preferences, UI scale, and AI configuration.
- **`%APPDATA%\Kerkenez\accounts.dat`**: DPAPI-encrypted email account profiles (shared across the **Kerkenez** suite).
- **`%TEMP%\Kerkenez\mail\`**: Temporary preview cache, cleaned automatically upon process exit.

---

## 🛠️ Building from Source

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11 (64-bit).
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
# Clone the repository
git clone https://github.com/KerkenezDev/KerkenezMail.git
cd KerkenezMail

# Build Release
dotnet build -c Release

# Publish Standalone Single-File Executable
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

---

## 🌍 Community & Localization
Want to add your language? Check out the [Community Language Guide](Languages/add_language_guide.md) to add a new language in just a few minutes!

---

## 📄 License
This project is licensed under the [MIT License](LICENSE). Built with ❤️ using .NET 10, Windows Forms, and MailKit.
