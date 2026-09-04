# Adding a New Language to Kerkenez Mail 🌐

Welcome to Kerkenez Mail localization! Kerkenez Mail features a modular, plug-and-play internationalization system. 

Adding support for your language requires **writing only a single `.cs` file** in the `Languages/` directory. You **do not** need to register it anywhere or touch any UI code—the application will automatically discover it via reflection, populate it in the Settings dropdown with its flag emoji, and make it instantly selectable.

---

## ⚡ Quick Start: 3 Simple Steps

1. **Create your language file** in the `Languages/` folder:
   ```
   Languages/
   ├── EnglishLanguage.cs
   ├── TurkishLanguage.cs
   └── SpanishLanguage.cs   <-- Your new file!
   ```
   Naming convention: `[LanguageName]Language.cs` (e.g., `GermanLanguage.cs`, `FrenchLanguage.cs`, `JapaneseLanguage.cs`).

2. **Inherit from `BaseLanguage`** and fill in your language metadata & translations.
3. **Build & Test**: Run `dotnet run` or `dotnet build`, open **Settings** ⚙️ $\rightarrow$ **Language & Region**, and select your language!

---

## 📝 Complete Boilerplate Template

Copy the template below into your new file (e.g., `Languages/SpanishLanguage.cs`) and replace the placeholders:

```csharp
using KerkenezMail.Languages;

namespace KerkenezMail.Languages;

/// <summary>
/// Spanish language localization for Kerkenez Mail.
/// </summary>
public sealed class SpanishLanguage : BaseLanguage
{
    // ISO 639-1 two-letter language code (lowercase)
    public override string Code => "es";

    // Native name of the language (how speakers of this language write it)
    public override string Name => "Español";

    // English name of the language
    public override string EnglishName => "Spanish";

    // Flag emoji representing the primary region/culture
    public override string FlagEmoji => "🇪🇸";

    protected override void InitTranslations()
    {
        // ==========================================
        // 1. Navigation & Sidebar
        // ==========================================
        Set(StringKeys.NavInbox,                     "Bandeja de entrada");
        Set(StringKeys.NavSendMail,                  "Enviar correo");
        Set(StringKeys.NavAccounts,                  "Cuentas");
        Set(StringKeys.NavSettings,                  "Configuración");
        Set(StringKeys.NavLiveLogs,                  "Registros en vivo");
        Set(StringKeys.NavSent,                      "Enviados");
        Set(StringKeys.NavArchived,                  "Archivados");
        Set(StringKeys.NavSpam,                      "Spam");
        Set(StringKeys.NavTrash,                     "Papelera");
        Set(StringKeys.NavLiveImap,                  "IMAP en vivo");
        Set(StringKeys.NavLiveImapActive,            "IMAP en vivo: Activo");
        Set(StringKeys.NavLiveImapOff,               "IMAP en vivo: Inactivo");
        Set(StringKeys.NavTipExpandSidebar,          "Expandir barra lateral");
        Set(StringKeys.NavTipCollapseSidebar,        "Contraer barra lateral");

        // ==========================================
        // 2. Main Window & Shell
        // ==========================================
        Set(StringKeys.AppTitle,                     "Kerkenez Mail (Win32)");
        Set(StringKeys.TrayOpen,                     "📬  Abrir Kerkenez Mail");
        Set(StringKeys.TrayCheckNow,                 "🔄  Comprobar ahora");
        Set(StringKeys.TraySettings,                 "⚙️  Configuración");
        Set(StringKeys.TrayExit,                     "❌  Salir");

        // ==========================================
        // 3. Inbox & Summaries View
        // ==========================================
        Set(StringKeys.InboxRefresh,                 "Actualizar");
        Set(StringKeys.InboxCopySummary,             "Copiar resumen");
        Set(StringKeys.InboxExport,                  "Exportar...");
        Set(StringKeys.InboxOpenInBrowser,           "Abrir en navegador");
        Set(StringKeys.InboxTipRefresh,              "Actualizar bandeja de entrada");
        Set(StringKeys.InboxTipCopySummary,          "Copiar resumen de IA al portapapeles");
        Set(StringKeys.InboxTipExport,               "Exportar correos y resúmenes");
        Set(StringKeys.InboxTipOpenInBrowser,        "Ver correo original en el navegador");
        Set(StringKeys.InboxTipArchive,              "Mover correos a la carpeta de archivo");
        Set(StringKeys.InboxTipDelete,               "Mover correos a la papelera");
        Set(StringKeys.InboxTipReply,                "Responder a este hilo");
        Set(StringKeys.InboxTipMoveToInbox,          "Mover a la bandeja de entrada");
        Set(StringKeys.InboxAccountLabel,            "Cuenta:");
        Set(StringKeys.InboxAllAccounts,             "Todas las cuentas");
        Set(StringKeys.InboxListHeader,              "{0} ({1} correos)");
        Set(StringKeys.InboxListHeaderUnread,        "{0} ({1} correos, {2} no leídos)");
        Set(StringKeys.InboxSubjectPrefix,           "Asunto:");
        Set(StringKeys.InboxNoEmailSelected,         "(Ningún correo seleccionado)");
        Set(StringKeys.InboxAiExecutiveSummary,      "Resumen ejecutivo de IA");
        Set(StringKeys.InboxAiGeneratedVram,         "(Generado por LLM local en VRAM)");
        Set(StringKeys.InboxAiSummaryPlaceholder,    "El resumen de IA aparecerá aquí...");
        Set(StringKeys.InboxAttachmentsTitle,        "Archivos adjuntos:");
        Set(StringKeys.InboxTagRead,                 "Leído");
        Set(StringKeys.InboxTagUnread,               "No leído");
        Set(StringKeys.InboxTagArchived,             "Archivado");
        Set(StringKeys.InboxSummarize,               "Resumir con IA");
        Set(StringKeys.InboxSearchPlaceholder,       "Buscar correos por remitente, asunto o contenido...");
        Set(StringKeys.InboxColAccount,              "Cuenta");
        Set(StringKeys.InboxColFrom,                 "De");
        Set(StringKeys.InboxColSubject,              "Asunto");
        Set(StringKeys.InboxColDate,                 "Fecha");
        Set(StringKeys.InboxColPriority,             "Prioridad");
        Set(StringKeys.InboxBtnReply,                "Responder");
        Set(StringKeys.InboxBtnMoveInbox,            "Mover a bandeja de entrada");
        Set(StringKeys.InboxBtnDelete,               "Eliminar");

        // ==========================================
        // 4. Send Mail / Compose
        // ==========================================
        Set(StringKeys.SendTitle,                    "Redactar correo");
        Set(StringKeys.SendFrom,                     "De:");
        Set(StringKeys.SendTo,                       "Para:");
        Set(StringKeys.SendCc,                       "CC:");
        Set(StringKeys.SendBcc,                      "CCO:");
        Set(StringKeys.SendSubject,                  "Asunto:");
        Set(StringKeys.SendBodyPlaceholder,          "Escriba su mensaje aquí...");
        Set(StringKeys.SendBtnSend,                  "Enviar");
        Set(StringKeys.SendBtnDiscard,               "Descartar");
        Set(StringKeys.SendAddAttachment,            "Adjuntar archivo");

        // ==========================================
        // 5. Accounts View & Dialog
        // ==========================================
        Set(StringKeys.AccountsTitle,                "Cuentas de correo");
        Set(StringKeys.AccountsBtnAdd,               "Agregar cuenta");
        Set(StringKeys.AccountsBtnEdit,              "Editar");
        Set(StringKeys.AccountsBtnDelete,            "Eliminar");
        Set(StringKeys.AccountsBtnTest,              "Probar");
        Set(StringKeys.AccountsColStatus,            "Estado");

        // ==========================================
        // 6. Settings View
        // ==========================================
        Set(StringKeys.SettingsTitle,                "Configuración");
        Set(StringKeys.SettingsSecLanguage,          "Idioma y región");
        Set(StringKeys.SettingsLanguageLabel,        "Idioma de la interfaz");
        Set(StringKeys.SettingsBtnSave,              "Guardar configuración");

        // ==========================================
        // 7. Live Logs View
        // ==========================================
        Set(StringKeys.LogsTitle,                    "Registros en vivo");
        Set(StringKeys.LogsSubtitle,                 "Registros de diagnóstico de IMAP y IA en tiempo real.");
        Set(StringKeys.LogsBtnCopy,                  "Copiar registros");
        Set(StringKeys.LogsBtnClear,                 "Limpiar registros");
        Set(StringKeys.LogsBtnExport,                "Exportar registros...");

        // ==========================================
        // 8. Common & Feedback
        // ==========================================
        Set(StringKeys.CommonSave,                   "Guardar");
        Set(StringKeys.CommonCancel,                 "Cancelar");
        Set(StringKeys.CommonDelete,                 "Eliminar");
        Set(StringKeys.CommonOk,                     "Aceptar");
        Set(StringKeys.CommonSuccess,                "Éxito");
        Set(StringKeys.CommonError,                  "Error");
        Set(StringKeys.StatusSyncComplete,           "Sincronización completa");
        Set(StringKeys.StatusReady,                  "Listo");
    }
}
```

---

## 💡 Important Rules & Best Practices

1. **Table-Style Alignment:**
   Keep keys and translated text neatly aligned with spaces so the file looks like a readable table. This makes reviewing git diffs and comparing versions clean and easy.

2. **Formatting Arguments (`{0}`, `{1}`):**
   Some keys support format arguments (e.g., `"{0} unread messages"`). Ensure you include the `{0}` placeholder in your translated text.

3. **Fallback Protection (Zero Crashes):**
   You don't have to worry about missing keys breaking the application! If any key is omitted in your translation, Kerkenez Mail automatically falls back:
   ```
   Your Language -> English (Default) -> Key String
   ```
   The UI will simply display the English string for any missing key.

4. **Available Keys Reference:**
   All official keys are declared in [`StringKeys.cs`](file:///c:/Users/Ismail/Programs/ProgramFiles/EmailSummerizer-dev/Languages/StringKeys.cs). Refer to [`EnglishLanguage.cs`](file:///c:/Users/Ismail/Programs/ProgramFiles/EmailSummerizer-dev/Languages/EnglishLanguage.cs) to see the reference baseline translations.

---

## 🚀 Submitting Your Translation (Pull Request)

1. Create a branch:
   ```bash
   git checkout -b lang/spanish
   ```
2. Commit your new file:
   ```bash
   git add Languages/SpanishLanguage.cs
   git commit -m "feat(i18n): add Spanish (es) language support"
   ```
3. Push to your fork and open a Pull Request on GitHub targeting the **`dev`** branch:
   👉 **https://github.com/KerkenezDev/KerkenezMail**

Thank you for helping make Kerkenez Mail accessible to everyone around the world! 🦅
