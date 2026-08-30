# Email Summarizer (Win32) 📬🤖
*Local AI-Powered IMAP Email Assistant with llama.cpp, Ollama, Cloud LLMs & Encrypted Security*

[![Target](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6?logo=windows&logoColor=white)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0%20(Desktop%20Runtime)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![WinGet](https://img.shields.io/badge/WinGet-ismlEraslan.EmailSummarizer-0078D6?logo=windows-terminal&logoColor=white)](https://github.com/microsoft/winget-pkgs)
[![Mail Engine](https://img.shields.io/badge/IMAP-MailKit%204.17-orange)](https://github.com/jstedfast/MailKit)
[![LLM Backend](https://img.shields.io/badge/AI%20Engine-llama.cpp%20%2F%20Ollama%20%2F%20Cloud-blue)](https://github.com/ggerganov/llama.cpp)
[![Security](https://img.shields.io/badge/Security-Windows%20DPAPI%20Encrypted-green)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)

A high-performance, privacy-first **Windows desktop application** built with native Win32 aesthetics (Segoe UI typography, clean control borders, crisp status strip, and sleek collapsible side navigation). It connects concurrently to configured Gmail, Yahoo, iCloud, and custom IMAP accounts using **MailKit**, retrieves incoming emails, sanitizes HTML bodies, and generates concise 2-3 sentence executive summaries and priority rankings (`1` to `3`) using local **llama.cpp** (`Qwen2.5-VL-3B.gguf` or any GGUF model), local **Ollama**, or **Cloud APIs**.

---

## 🔒 Privacy & Cloud Usage Notice

> [!IMPORTANT]
> **Data Privacy and Cloud API Notice:**
> - **Local llama.cpp (GGUF) & Local Ollama**: All email processing, HTML sanitization, summarization, and priority scoring run **100% offline on your local machine (CPU/GPU)**. Zero email data or metadata ever leaves your device.
> - **Cloud APIs (OpenAI / OpenRouter / Groq / DeepSeek / Custom endpoints)**: When utilizing a Cloud backend, email subjects, senders, and sanitized message bodies are transmitted securely via HTTPS to the selected third-party cloud service for inference.
> - **Recommendation**: For maximum privacy, confidentiality, and air-gapped operation, select **Local llama.cpp** or **Local Ollama** in the AI Engine settings.

---

## 🚀 Quick Start

### 📦 Option 1: Install via WinGet (Recommended)
Install directly using Windows Package Manager in Windows Terminal / PowerShell:
```powershell
winget install ismlEraslan.EmailSummarizer
```

### ⚡ Option 2: Standalone Download (GitHub Releases)
1. Download **`EmailSummarizer.zip`** (or `EmailSummarizer.exe`) from [GitHub Releases](https://github.com/ismlEraslan/email-summarizer-win32/releases/latest).
2. Unzip and run **`EmailSummarizer.exe`**.
3. In the **Accounts** tab, click **➕ Add Account**, enter your credentials, and click **⚡ Test Connection**!

---

## ✨ Key Features

### 📬 Dual-Pane Inbox & AI Summaries Queue
- **Streamlined Inbox**: Parallel multi-account inbox fetch streaming incoming emails to the UI in real time.
- **⚡ AI Priority Ranking (1–3)**: Automatically classifies incoming unread emails by urgency/importance:
  - **`1` (High / Urgent)**: Direct personal requests, critical deadlines, urgent action items (**Bold Red**).
  - **`2` (Normal / Medium)**: Project updates, status notices, general correspondence (**Bold Blue**).
  - **`3` (Low / Newsletter)**: Marketing emails, digests, newsletters, automated system notices (**Bold Slate**).
- **Side-by-Side Dual Pane**: Inspect full cleaned email text alongside AI-generated summaries.
- **Smart Auto-Summarization**: Summarizes unread messages automatically during fetch; on-demand summarization for read emails upon selection.
- **Export & Copy**: One-click Markdown export (`.md`), plain-text export (`.txt`), and clipboard copy options.

### 🧠 Native Thinking & Reasoning Model Support
- **Automatic CoT Support**: Full out-of-the-box compatibility with reasoning/thinking models (e.g. `DeepSeek-R1`, `QwQ`, etc.) across local and cloud backends.
- **Zero Configuration**: No flags or manual switches needed — the engine automatically strips internal `<think>...</think>` blocks and isolates the clean summary and priority score.
- **Adaptive Token Headroom**: Dynamically allocates token headroom to prevent thinking models from truncating mid-thought.

### 🖥️ Responsive Layout & Custom Scaling
- **Collapsible Sidebar**: Compact icon rail with ultra-fast ~90ms micro-animation (`Ctrl + B` shortcut) and startup preference setting.
- **Dynamic Display Scaling**: Scales proportionally relative to the active display's usable desktop area (Default: `60% × 56%`).
- **Live Preview & Preset Chips**: Includes quick scaling presets (`Compact`, `Default`, `Large`, `Near Max`) and an instant `⚡ Resize Active Window` testing button.

### 👥 Visual Account Manager
- **Provider Presets**: Instant configuration templates for **Gmail**, **Yahoo Mail**, **iCloud**, and **Custom IMAP** servers.
- **Live Connection Testing**: Test IMAP credentials and SSL/TLS handshakes before saving.
- **Per-Account Controls**: Enable/disable individual mailboxes on the fly.

### 🔒 Bank-Grade Encrypted Credential Security
- **Windows DPAPI Encryption**: All email account credentials, App Passwords, and Cloud AI API keys are encrypted using native Windows Data Protection API (`ProtectedData.Protect` / `DataProtectionScope.CurrentUser`).
- **Encrypted Storage**: Credentials and keys are stored encrypted on your SSD in `%APPDATA%\EmailSummarizer\accounts.dat` and `%APPDATA%\EmailSummarizer\config.json`.
- **In-Memory Decryption**: Decrypted credentials exist in memory *only* during active API / IMAP requests.
- **Clean Config**: The unencrypted portions of `config.json` store only general preferences and `AccountIds` references.
- **Seamless Backward Compatibility**: Automatically detects and migrates legacy plain-text configs into encrypted storage on startup.

### 🔔 Low-Footprint System Tray Daemon
- **Continuous Background Monitoring**: Periodically polls configured accounts on a configurable timer (1–120 min).
- **Windows Notifications**: Rich desktop notifications for newly received unread emails.
- **Ultra-Low Memory Consumption**: Native Win32 message pump host maintaining **< 5 MB RAM** idle footprint.
- **Independent from AI Backend**: The background daemon polls IMAP headers without waking or loading LLMs, keeping CPU at 0%.
- **System Startup**: Optional automatic start with Windows logon (`HKCU\...\Run`).

### 🤖 Multi-Backend AI Engine
- **Local llama.cpp (Embedded GGUF)**: Starts `llama-server.exe` on demand with full GPU offloading (`-ngl 99`) and instant VRAM unload.
- **Local Ollama**: Out-of-the-box support for local Ollama instances (`http://127.0.0.1:11434/v1/chat/completions`) with model suggestion chips (`llama3.2`, `qwen2.5:3b`, `deepseek-r1:1.5b`, `mistral`, etc.).
- **Cloud & Custom APIs**: Native compatibility with OpenAI (`gpt-4o-mini`), OpenRouter, Groq (`llama-3.1-8b-instant`), DeepSeek, and custom OpenAI-compatible endpoints with DPAPI-encrypted Bearer tokens.
- **Inference Control**: Global temperature and max token ceiling controls.

### 📜 Real-Time Logs
- Color-coded live diagnostic console tracking IMAP connection states, MailKit handshakes, and LLM completions.

---

## 💻 System Requirements

- **Operating System**: Windows 10 (Build 19041+) or Windows 11 (64-bit).
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).
- **AI Model Backend**: Any GGUF LLM for local inference (`llama-server.exe`), local **Ollama**, or any OpenAI-compatible Cloud API endpoint.

---

## ⚙️ Command Line Arguments

| Switch | Description |
| :--- | :--- |
| `EmailSummarizer.exe` | Launches the main graphical interface. |
| `EmailSummarizer.exe --daemon` *(or `--tray`)* | Launches the background system tray daemon directly. |
| `EmailSummarizer.exe --uninstall` | Prompts to cleanly remove Desktop & Start Menu shortcuts, startup registry keys, Windows Add/Remove registration, and `%APPDATA%\EmailSummarizer`. |
| `EmailSummarizer.exe --uninstall --quiet` *(or `--silent`)* | Performs a silent/unattended uninstall without dialog prompts. |

---

## 📧 Email Provider Setup

### 🔹 Gmail Setup (Google App Password)
Gmail IMAP requires a 16-character **Google App Password** when 2FA is active:
1. Open your [Google Account Security Settings](https://myaccount.google.com/security).
2. Ensure **2-Step Verification** is enabled.
3. Navigate to **App Passwords** ([myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)).
4. Create an app password named `Email Summarizer`.
5. Copy the 16-letter password (e.g. `abcd efgh ijkl mnop`).
6. In **Email Summarizer**, switch to **Accounts** ➔ **➕ Add Account** ➔ Select **Gmail** ➔ Paste App Password ➔ Click **⚡ Test Connection**.

### 🔹 Yahoo Mail Setup
- Generate an App Password in your Yahoo Account Security settings.
- Host: `imap.mail.yahoo.com`, Port: `993`, SSL: `Enabled`.

### 🔹 iCloud Mail Setup
- Generate an app-specific password at [appleid.apple.com](https://appleid.apple.com).
- Host: `imap.mail.me.com`, Port: `993`, SSL: `Enabled`.

### 🔹 Custom IMAP Server
- Enter your server's IMAP host, port (typically 993 for SSL/TLS or 143 with STARTTLS), and account credentials.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + B` | Toggle collapsible left navigation sidebar |
| `Ctrl + R` | Refresh inbox and trigger parallel auto-summarize |
| `Ctrl + S` | Export current email list / summaries |
| `Ctrl + C` | Copy summary to clipboard in Markdown format |

---

## 📁 Configuration & Storage Paths

All persistent application data is isolated within standard Windows user roaming profile:
- **`%APPDATA%\EmailSummarizer\config.json`**: Application preferences, model settings, and account IDs.
- **`%APPDATA%\EmailSummarizer\accounts.dat`**: DPAPI-encrypted email account credentials and passwords.

---

## 🛠️ Building from Source

To build and compile the standalone portable binary:

```powershell
# Restore & build project
dotnet build -c Release

# Compile portable release
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./publish"
```

---

## 📄 License
MIT License. Built with ❤️ using .NET 8, MailKit, and llama.cpp.
