# Email Summarizer (Win32) 📬🤖
*Local AI-Powered IMAP Email Assistant with llama.cpp & Encrypted Security*

[![Target](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078D6?logo=windows&logoColor=white)](https://microsoft.com)
[![Framework](https://img.shields.io/badge/.NET-8.0%20(Desktop%20Runtime)-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com)
[![Mail Engine](https://img.shields.io/badge/IMAP-MailKit%204.17-orange)](https://github.com/jstedfast/MailKit)
[![LLM Backend](https://img.shields.io/badge/AI%20Engine-llama.cpp%20%2F%20GGUF-blue)](https://github.com/ggerganov/llama.cpp)
[![Security](https://img.shields.io/badge/Security-Windows%20DPAPI%20Encrypted-green)](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.protecteddata)

A high-performance, privacy-first **Windows desktop application** built with native Win32 aesthetics (Segoe UI typography, clean control borders, crisp status strip, and sleek side navigation). It connects concurrently to configured Gmail, Yahoo, iCloud, and custom IMAP accounts using **MailKit**, retrieves incoming emails, sanitizes HTML bodies, and generates concise 2-3 sentence executive summaries using a local **llama.cpp** LLM (`Qwen2.5-VL-3B.gguf` or any GGUF model) entirely offline on your GPU/CPU with zero cloud dependencies.

---

## 🚀 Quick Start

Get the latest version from [GitHub Releases](https://github.com/ismlEraslan/email-summarizer-win32/releases) directly (`EmailSummarizer.exe` or `EmailSummarizer.zip`).

1. Download `EmailSummarizer.exe`.
2. Run `EmailSummarizer.exe`.
3. In the **Accounts** tab, click **➕ Add Account**, enter your credentials, and click **⚡ Test Connection**!

---

## ✨ Key Features

### 📬 Summaries Queue & Dual-Pane Inbox
- **Streamlined Inbox**: Parallel multi-account inbox fetch streaming incoming emails to the UI in real time.
- **Smart AI Summarization**: Automatically batches and summarizes unread messages into clean, objective 2-3 sentence executive briefs.
- **Side-by-Side Dual Pane**: Inspect full cleaned email text alongside AI-generated summaries.
- **Export & Copy**: One-click Markdown export (`.md`), plain-text export (`.txt`), and clipboard copy options.

### 👥 Visual Account Manager
- **Provider Presets**: Instant configuration templates for **Gmail**, **Yahoo Mail**, **iCloud**, and **Custom IMAP** servers.
- **Live Connection Testing**: Test IMAP credentials and SSL/TLS handshakes before saving.
- **Per-Account Controls**: Enable/disable individual mailboxes on the fly.

### 🔒 Bank-Grade Encrypted Credential Security
- **Windows DPAPI Encryption**: All account credentials and App Passwords are encrypted using native Windows Data Protection API (`ProtectedData.Protect` / `DataProtectionScope.CurrentUser`) and stored in `%APPDATA%\EmailSummarizer\accounts.dat`.
- **In-Memory Decryption**: Passwords exist in memory *only* during active IMAP fetch cycles.
- **Clean Config**: The unencrypted `config.json` stores only general preferences and `AccountIds` references.
- **Seamless Backward Compatibility**: Automatically detects and migrates legacy plain-text configs into encrypted storage on startup.

### 🔔 Low-Footprint System Tray Daemon
- **Continuous Background Monitoring**: Periodically polls configured accounts on a configurable timer (1–120 min).
- **Windows Notifications**: Rich desktop notifications for newly received unread emails.
- **Ultra-Low Memory Consumption**: Native Win32 message pump host with aggressive working-set memory trimming maintaining **< 5 MB RAM** idle footprint.
- **System Startup**: Optional automatic start with Windows logon (`HKCU\...\Run`).

### 🤖 Local llama.cpp Engine & VRAM Control
- **Auto-Managed llama-server**: Starts `llama-server.exe` on demand and connects via OpenAI-compatible endpoint (`/v1/chat/completions`).
- **GPU Layer Offload**: Full control over GPU acceleration (`-ngl 99`).
- **Instant VRAM Unloading**: Option to automatically unload model weights when idle or maintain warm VRAM for instant inference.

### 📜 Real-Time Logs
- Color-coded live diagnostic console tracking IMAP connection states, MailKit handshakes, and LLM completions.

---

## 💻 System Requirements

- **Operating System**: Windows 10 (Build 19041+) or Windows 11 (64-bit).
- **Runtime**: [.NET 8.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64).
- **AI Model Backend**: Any GGUF LLM (Recommended: `Qwen2.5-VL-3B-Instruct.gguf` or `Llama-3.2-3B-Instruct.gguf`) with `llama-server.exe` in PATH or specified in settings.

---

## ⚙️ Command Line Arguments

| Switch | Description |
| :--- | :--- |
| `EmailSummarizer.exe` | Launches the main graphical interface. |
| `EmailSummarizer.exe --daemon` *(or `--tray`)* | Launches the background system tray daemon directly. |
| `EmailSummarizer.exe --uninstall` | Prompts to cleanly remove `%APPDATA%\EmailSummarizer` and startup registry keys. |

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
