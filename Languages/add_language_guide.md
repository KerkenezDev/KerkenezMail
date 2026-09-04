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
        // Navigation & Sidebar
        // ==========================================
        Set(StringKeys.Nav.Inbox,                   "Bandeja de entrada");
        Set(StringKeys.Nav.SendMail,                "Enviar correo");
        Set(StringKeys.Nav.Accounts,                "Cuentas");
        Set(StringKeys.Nav.Settings,                "Configuración");
        Set(StringKeys.Nav.LiveLogs,                "Registros en vivo");
        Set(StringKeys.Nav.LiveImap,                "IMAP en vivo");
        Set(StringKeys.Nav.CollapseSidebar,         "Contraer barra lateral");
        Set(StringKeys.Nav.ExpandSidebar,           "Expandir barra lateral");
        Set(StringKeys.Nav.StatusLive,              "IMAP en vivo: Activo");
        Set(StringKeys.Nav.StatusIdle,              "IMAP en vivo: Inactivo");

        // Subfolders
        Set(StringKeys.Nav.SubFolderAll,            "Todos los correos");
        Set(StringKeys.Nav.SubFolderUnread,         "No leídos");
        Set(StringKeys.Nav.SubFolderStarred,        "Destacados");
        Set(StringKeys.Nav.SubFolderSent,           "Enviados");
        Set(StringKeys.Nav.SubFolderTrash,          "Papelera");

        // ==========================================
        // Shell & Windows
        // ==========================================
        Set(StringKeys.Shell.AppTitle,              "Kerkenez Mail");
        Set(StringKeys.Shell.Close,                 "Cerrar");
        Set(StringKeys.Shell.Minimize,              "Minimizar");
        Set(StringKeys.Shell.Maximize,              "Maximizar");
        Set(StringKeys.Shell.Quit,                  "Salir");

        // ==========================================
        // Inbox & Message List
        // ==========================================
        Set(StringKeys.Inbox.SearchPlaceholder,     "Buscar correos...");
        Set(StringKeys.Inbox.ColFrom,               "De");
        Set(StringKeys.Inbox.ColSubject,            "Asunto");
        Set(StringKeys.Inbox.ColDate,               "Fecha");
        Set(StringKeys.Inbox.ColPriority,           "Prioridad");
        Set(StringKeys.Inbox.ColCategory,           "Categoría");
        Set(StringKeys.Inbox.BtnRefresh,            "Actualizar");
        Set(StringKeys.Inbox.BtnReply,              "Responder");
        Set(StringKeys.Inbox.BtnForward,            "Reenviar");
        Set(StringKeys.Inbox.BtnArchive,            "Archivar");
        Set(StringKeys.Inbox.BtnDelete,             "Eliminar");
        Set(StringKeys.Inbox.BtnStar,               "Destacar");
        Set(StringKeys.Inbox.BtnMoveToInbox,        "Mover a bandeja de entrada");
        Set(StringKeys.Inbox.NoMessages,            "No hay mensajes para mostrar.");
        Set(StringKeys.Inbox.LoadingMessages,       "Cargando correos...");

        // ==========================================
        // Send Mail / Compose
        // ==========================================
        Set(StringKeys.Send.Title,                  "Redactar nuevo correo");
        Set(StringKeys.Send.From,                   "De");
        Set(StringKeys.Send.To,                     "Para");
        Set(StringKeys.Send.Cc,                     "CC");
        Set(StringKeys.Send.Bcc,                    "CCO");
        Set(StringKeys.Send.Subject,                "Asunto");
        Set(StringKeys.Send.BtnSend,                "Enviar");
        Set(StringKeys.Send.BtnDiscard,             "Descartar");
        Set(StringKeys.Send.BtnAttach,              "Adjuntar");
        Set(StringKeys.Send.Sending,                "Enviando...");
        Set(StringKeys.Send.SentSuccess,            "¡Correo enviado con éxito!");
        Set(StringKeys.Send.SentFailed,             "Error al enviar el correo.");

        // ==========================================
        // Accounts
        // ==========================================
        Set(StringKeys.Accounts.Title,              "Cuentas de correo");
        Set(StringKeys.Accounts.BtnAddAccount,      "Agregar cuenta");
        Set(StringKeys.Accounts.BtnEditAccount,     "Editar cuenta");
        Set(StringKeys.Accounts.BtnDeleteAccount,   "Eliminar cuenta");
        Set(StringKeys.Accounts.BtnTestConnection,  "Probar conexión");
        Set(StringKeys.Accounts.SharedWithSuite,    "Compartido con la suite Kerkenez");

        // ==========================================
        // Add / Edit Account Dialog
        // ==========================================
        Set(StringKeys.AddAcc.DialogTitleAdd,       "Agregar cuenta de correo");
        Set(StringKeys.AddAcc.DialogTitleEdit,      "Editar cuenta de correo");
        Set(StringKeys.AddAcc.AccountName,          "Nombre de cuenta");
        Set(StringKeys.AddAcc.EmailAddress,         "Dirección de correo");
        Set(StringKeys.AddAcc.Password,             "Contraseña / Clave de app");
        Set(StringKeys.AddAcc.ImapServer,           "Servidor IMAP");
        Set(StringKeys.AddAcc.ImapPort,             "Puerto IMAP");
        Set(StringKeys.AddAcc.ImapSsl,              "Usar SSL/TLS para IMAP");
        Set(StringKeys.AddAcc.SmtpServer,           "Servidor SMTP");
        Set(StringKeys.AddAcc.SmtpPort,             "Puerto SMTP");
        Set(StringKeys.AddAcc.SmtpSsl,              "Usar SSL/TLS para SMTP");
        Set(StringKeys.AddAcc.BtnSave,              "Guardar");
        Set(StringKeys.AddAcc.BtnCancel,            "Cancelar");
        Set(StringKeys.AddAcc.BtnTest,              "Probar");

        // ==========================================
        // Settings View
        // ==========================================
        Set(StringKeys.Settings.Title,              "Configuración");
        Set(StringKeys.Settings.SectionLanguage,    "Idioma y región");
        Set(StringKeys.Settings.Language,           "Idioma de la interfaz");
        Set(StringKeys.Settings.SectionGeneral,     "General");
        Set(StringKeys.Settings.SectionAppearance,  "Apariencia");
        Set(StringKeys.Settings.SectionAi,          "Resumen por IA");
        Set(StringKeys.Settings.SectionNetwork,     "Red e IMAP");
        Set(StringKeys.Settings.CheckInterval,      "Intervalo de verificación");
        Set(StringKeys.Settings.AutoStart,          "Iniciar con Windows");
        Set(StringKeys.Settings.MinimizeToTray,     "Minimizar a la bandeja del sistema");
        Set(StringKeys.Settings.BtnSaveSettings,    "Guardar configuración");

        // ==========================================
        // Logs View
        // ==========================================
        Set(StringKeys.Logs.Title,                  "Registros en vivo");
        Set(StringKeys.Logs.BtnCopy,                "Copiar");
        Set(StringKeys.Logs.BtnClear,               "Limpiar registros");
        Set(StringKeys.Logs.BtnExport,              "Exportar");

        // ==========================================
        // Common Actions & Feedback
        // ==========================================
        Set(StringKeys.Common.Ok,                   "Aceptar");
        Set(StringKeys.Common.Cancel,               "Cancelar");
        Set(StringKeys.Common.Save,                 "Guardar");
        Set(StringKeys.Common.Delete,               "Eliminar");
        Set(StringKeys.Common.Edit,                 "Editar");
        Set(StringKeys.Common.Success,              "Éxito");
        Set(StringKeys.Common.Error,                "Error");
        Set(StringKeys.Common.Warning,              "Advertencia");
        Set(StringKeys.Common.Info,                 "Información");
        Set(StringKeys.Common.Yes,                  "Sí");
        Set(StringKeys.Common.No,                   "No");
        Set(StringKeys.Common.Loading,              "Cargando...");
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
