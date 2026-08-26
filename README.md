# Email Summarizer (Win32) — C# & MailKit with llama.cpp

A high-performance **C# Windows desktop application** built with native Win32 aesthetics (Segoe UI typography, clean control borders, crisp status strip, and sleek side navigation tab). It connects concurrently to configured Gmail & IMAP accounts using **MailKit**, retrieves unread emails, cleans the email body, and generates concise 2-3 sentence executive summaries using a local **llama.cpp** LLM (`Qwen2.5-VL-3B.gguf` or any GGUF model).

It features **instant VRAM unloading** upon completion or application shutdown to keep your GPU memory completely free.

---

## 📸 Key Features

- **Modern Win32 Design**: Clean, crisp, lightweight native Windows UI matching the design philosophy of Kokoro TTS & Local Image Generator.
- **Sleek Side Tab Navigation**:
  - 📬 **Summaries**: Fetch & summarize queue, split view with email list and preview, Markdown export, and one-click copy.
  - 👥 **Accounts**: Visual account manager with "➕ Add Account" dialog, provider presets (Gmail, Outlook, Yahoo, iCloud, Custom IMAP), and live IMAP connection testing.
  - ⚙️ **Settings**: Configure model path, GPU layers (`-ngl 99`), server port, auto-start, instant VRAM unload, unread filters, and prompt template.
  - 📜 **Live Logs**: Real-time color-coded terminal console with timestamped IMAP and llama.cpp output.
- **MailKit High-Performance IMAP**: Fully RFC-compliant, multi-account parallel fetching with SSL/TLS support and HTML body sanitizer.
- **Auto-Managed llama-server**: Automatically launches `llama-server.exe` on demand and terminates it after batch completion to free 100% of GPU VRAM.
- **Configuration Persistence**: Saves all accounts and settings to `config.json`.

---

## 🚀 Quick Start

### 1. Launch the Application
Double-click `run.bat` or run:
```bash
dotnet run -c Release
```
Or execute the standalone binary:
```bash
bin\Release\net8.0-windows\EmailSummarizer.exe
```

### 2. Gmail App Password Setup
Gmail IMAP requires a 16-character **Google App Password**:
1. Open your [Google Account Security Settings](https://myaccount.google.com/security).
2. Ensure **2-Step Verification** is turned ON.
3. Search for **App Passwords** (or visit `https://myaccount.google.com/apppasswords`).
4. Name your app (e.g. `Email Summarizer`) and click **Create**.
5. Copy the generated 16-character password (e.g., `abcd efgh ijkl mnop`).
6. In the app, switch to the **Accounts** tab, click **➕ Add Account**, enter your email and App Password, and click **⚡ Test Connection**!

### 3. Keyboard Shortcuts
- `Ctrl + Enter`: Start Fetch & Summarize
- `Ctrl + S`: Export summaries to Markdown / Text
- `Ctrl + C`: Copy selected summary (or all summaries) to clipboard
