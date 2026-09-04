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
        Set(StringKeys.MainShortcutsPromptTitle,     "Accesos directos de Kerkenez Mail");
        Set(StringKeys.MainShortcutsPromptDesc,      "¡Bienvenido a Kerkenez Mail!\r\n\r\n¿Desea crear accesos directos en su Escritorio y Menú Inicio para un acceso rápido?");
        Set(StringKeys.TrayOpen,                     "📬  Abrir Kerkenez Mail");
        Set(StringKeys.TrayLiveImapActive,           "⚡  IMAP en vivo: Activo (Clic para alternar)");
        Set(StringKeys.TrayLiveImapOff,              "💤  IMAP en vivo: Inactivo (Clic para alternar)");
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
        Set(StringKeys.InboxPriorityHigh,            "Alta");
        Set(StringKeys.InboxPriorityNormal,          "Normal");
        Set(StringKeys.InboxPriorityLow,             "Baja");
        Set(StringKeys.InboxEmptyTitle,              "Carpeta vacía");
        Set(StringKeys.InboxEmptySubtitle,           "No hay mensajes en esta carpeta.");
        Set(StringKeys.InboxNoSelectionTitle,        "Ningún correo seleccionado");
        Set(StringKeys.InboxNoSelectionSubtitle,     "Seleccione un correo de la lista de la derecha para previsualizarlo.");
        Set(StringKeys.InboxDetailFrom,              "De");
        Set(StringKeys.InboxDetailDate,              "Fecha");
        Set(StringKeys.InboxDetailAccount,           "Cuenta");
        Set(StringKeys.InboxTabSummary,              "Resumen");
        Set(StringKeys.InboxTabOriginal,             "Correo original");
        Set(StringKeys.InboxBtnMarkRead,             "Marcar como leído");
        Set(StringKeys.InboxBtnMarkUnread,           "Marcar como no leído");
        Set(StringKeys.InboxBtnDelete,               "Eliminar");
        Set(StringKeys.InboxBtnMoveInbox,            "Mover a bandeja de entrada");
        Set(StringKeys.InboxBtnReply,                "Responder");
        Set(StringKeys.InboxBtnForward,              "Reenviar");
        Set(StringKeys.InboxBtnDownloadAttachments,  "Descargar adjuntos");
        Set(StringKeys.InboxLoadingStatus,           "Obteniendo correos...");
        Set(StringKeys.InboxSummarizingStatus,       "Resumiendo con IA...");
        Set(StringKeys.InboxLiveActiveBadge,         "IMAP en vivo: Conectado");
        Set(StringKeys.InboxMultiSelectCount,        "{0} correos seleccionados");
        Set(StringKeys.InboxSavedAttachmentsToast,   "¡Se guardaron {0} de {1} adjuntos en:\r\n{2}\r\n\r\n¿Desea abrir la carpeta?");
        Set(StringKeys.InboxSavedAttachmentsStatus,  "{0}/{1} adjuntos guardados");
        Set(StringKeys.InboxDownloadComplete,        "Descarga completa");
        Set(StringKeys.InboxDownloadError,           "Error al descargar adjuntos: {0}");
        Set(StringKeys.InboxSummaryCopiedToast,      "¡Resumen copiado al portapapeles!");
        Set(StringKeys.InboxAllSummariesCopiedToast, "¡Todos los resúmenes copiados en formato Markdown!");
        Set(StringKeys.InboxNoSummaryToCopy,         "No hay resumen disponible para copiar.");
        Set(StringKeys.InboxNoEmailsToExport,        "No hay correos disponibles para exportar.");
        Set(StringKeys.InboxExportSuccessToast,      "¡Exportado exitosamente a {0}!");
        Set(StringKeys.InboxExportSuccessTitle,      "Exportación exitosa");
        Set(StringKeys.InboxExportErrorToast,        "Error al exportar archivo: {0}");
        Set(StringKeys.InboxExportErrorTitle,        "Error de exportación");
        Set(StringKeys.InboxBrowserErrorToast,       "Error al abrir correo en el navegador:\n{0}");
        Set(StringKeys.InboxBrowserErrorTitle,       "Error del navegador");
        Set(StringKeys.InboxAccountNotFound,         "No se pudo encontrar la cuenta para '{0}' ({1}).");
        Set(StringKeys.InboxDeletePermanentlyTip,    "Eliminar permanentemente: Borrar mensaje(s) de forma definitiva");
        Set(StringKeys.InboxDeleteMoveTip,           "Eliminar: Mover mensaje(s) a la Papelera");
        Set(StringKeys.InboxDownloadedSingleToast,   "¡'{0}' descargado con éxito!\r\n\r\nGuardado en: {1}\r\n\r\n¿Desea abrir la carpeta?");
        Set(StringKeys.InboxDownloadedSingleStatus,  "Descargado '{0}'");
        Set(StringKeys.InboxDownloadFailedStatus,    "Descarga fallida");
        Set(StringKeys.InboxDownloadFailed,          "Descarga fallida");

        // ==========================================
        // 4. Send Mail / Compose
        // ==========================================
        Set(StringKeys.SendTitle,                    "Redactar nuevo correo");
        Set(StringKeys.SendThreadedReply,            "Respuesta en hilo");
        Set(StringKeys.SendBackToInbox,              "Bandeja de entrada");
        Set(StringKeys.SendPopOut,                   "Pestaña emergente");
        Set(StringKeys.SendFrom,                     "De:");
        Set(StringKeys.SendCcBccToggle,              "+ Cc / Cco");
        Set(StringKeys.SendTo,                       "Para:");
        Set(StringKeys.SendCc,                       "Cc:");
        Set(StringKeys.SendBcc,                      "Cco:");
        Set(StringKeys.SendSubject,                  "Asunto:");
        Set(StringKeys.SendToPlaceholder,            "destinatario@ejemplo.com (separar varios con coma)");
        Set(StringKeys.SendCcPlaceholder,            "Destinatarios Cc...");
        Set(StringKeys.SendBccPlaceholder,           "Destinatarios Cco...");
        Set(StringKeys.SendSubjectPlaceholder,       "Asunto");
        Set(StringKeys.SendBodyPlaceholder,          "Escriba su mensaje aquí usando Markdown...");
        Set(StringKeys.SendAttachments,              "Adjuntos:");
        Set(StringKeys.SendAddAttachment,            "Adjuntar archivo");
        Set(StringKeys.SendRemoveAttachment,         "Eliminar");
        Set(StringKeys.SendBtnSend,                  "Enviar correo");
        Set(StringKeys.SendBtnDiscard,               "Descartar");
        Set(StringKeys.SendBtnSaveDraft,             "Guardar borrador");
        Set(StringKeys.SendSending,                  "Enviando correo...");
        Set(StringKeys.SendSuccess,                  "¡Correo enviado con éxito!");
        Set(StringKeys.SendError,                    "Error al enviar correo.");
        Set(StringKeys.SendAiAssist,                 "Asistente IA");
        Set(StringKeys.SendDraftsTitle,              "Borradores");
        Set(StringKeys.SendTabMarkdown,              "Editar Markdown");
        Set(StringKeys.SendTabPlaintext,             "Vista previa texto plano");
        Set(StringKeys.SendTabHtml,                  "Vista previa HTML");
        Set(StringKeys.SendFormatMultipart,          "Markdown (Multiparte Texto + HTML)");
        Set(StringKeys.SendFormatPlaintext,          "Solo texto plano (RFC sin formato)");
        Set(StringKeys.SendDropHint,                 "Adjuntar archivos");
        Set(StringKeys.SendBrowseFiles,              "Examinar archivos...");
        Set(StringKeys.SendAttachmentSummary,        "{0} adjunto(s) ({1} KB)");
        Set(StringKeys.SendStatusHint,               "Listo para redactar. Admite formato Markdown.");
        Set(StringKeys.SendConfirmDiscard,           "¿Está seguro de que desea descartar este borrador?");
        Set(StringKeys.SendDiscardTitle,             "Descartar borrador");
        Set(StringKeys.SendToolbarBold,              "Negrita (**texto**)");
        Set(StringKeys.SendToolbarItalic,            "Cursiva (*texto*)");
        Set(StringKeys.SendToolbarHeader,            "Encabezado (### texto)");
        Set(StringKeys.SendToolbarLink,              "Insertar enlace ([texto](url))");
        Set(StringKeys.SendToolbarBulletList,        "Lista con viñetas (- elemento)");
        Set(StringKeys.SendToolbarNumberedList,      "Lista numerada (1. elemento)");
        Set(StringKeys.SendToolbarQuote,             "Bloque de cita (> texto)");
        Set(StringKeys.SendToolbarCode,              "Bloque de código (```)");
        Set(StringKeys.SendToolbarRule,              "Línea divisoria (---)");
        Set(StringKeys.SendMissingRecipient,         "Por favor ingrese al menos un destinatario en el campo 'Para'.");
        Set(StringKeys.SendMissingRecipientTitle,    "Destinatario faltante");
        Set(StringKeys.SendMissingAccount,           "Por favor seleccione una cuenta remitente.");
        Set(StringKeys.SendMissingAccountTitle,      "Cuenta faltante");
        Set(StringKeys.SendNoSubjectPrompt,          "¿Desea enviar este mensaje sin asunto?");
        Set(StringKeys.SendNoSubjectTitle,           "Sin asunto");
        Set(StringKeys.SendSentTitle,                "Correo enviado");
        Set(StringKeys.SendFailedTitle,              "Error al enviar correo");

        // ==========================================
        // 5. Accounts View & Dialog
        // ==========================================
        Set(StringKeys.AccountsTitle,                "Cuentas de correo");
        Set(StringKeys.AccountsSubtitle,             "Administre sus cuentas IMAP/SMTP y credenciales configuradas.");
        Set(StringKeys.AccountsBtnAdd,               "Agregar cuenta");
        Set(StringKeys.AccountsBtnEdit,              "Editar");
        Set(StringKeys.AccountsBtnDelete,            "Eliminar");
        Set(StringKeys.AccountsBtnTest,              "Probar conexión");
        Set(StringKeys.AccountsColEmail,             "Dirección de correo");
        Set(StringKeys.AccountsColProvider,          "Proveedor");
        Set(StringKeys.AccountsColServer,            "Servidor entrante");
        Set(StringKeys.AccountsColStatus,            "Estado");
        Set(StringKeys.AccountsStatusUntested,        "Sin probar");
        Set(StringKeys.AccountsStatusConnected,      "Conectado");
        Set(StringKeys.AccountsStatusConnectedUnread,"Conectado ({0} no leídos)");
        Set(StringKeys.AccountsStatusFailed,         "Fallido");
        Set(StringKeys.AccountsStatusError,          "Error de conexión");
        Set(StringKeys.AccountsEmptyDesc,            "No hay cuentas configuradas aún.\r\nHaga clic en '+ Agregar cuenta' arriba para conectar su correo.");
        Set(StringKeys.AccountsDeleteConfirm,        "¿Está seguro de que desea eliminar la cuenta '{0}' ({1})?");

        Set(StringKeys.AddAccTitle,                  "Agregar cuenta de correo");
        Set(StringKeys.AddAccEditTitle,              "Editar cuenta de correo");
        Set(StringKeys.AddAccProviderPreset,         "Plantilla de proveedor:");
        Set(StringKeys.AddAccCustomImap,             "IMAP personalizado");
        Set(StringKeys.AddAccLabel,                  "Etiqueta de la cuenta:");
        Set(StringKeys.AddAccEmail,                  "Dirección de correo:");
        Set(StringKeys.AddAccPassword,               "Contraseña de la aplicación:");
        Set(StringKeys.AddAccImapHost,               "Servidor IMAP:");
        Set(StringKeys.AddAccImapPort,               "Puerto IMAP:");
        Set(StringKeys.AddAccSmtpHost,               "Servidor SMTP:");
        Set(StringKeys.AddAccSmtpPort,               "Puerto SMTP:");
        Set(StringKeys.AddAccUseSsl,                 "Usar SSL / TLS");
        Set(StringKeys.AddAccBtnSave,                "Guardar cuenta");
        Set(StringKeys.AddAccBtnCancel,              "Cancelar");
        Set(StringKeys.AddAccBtnTest,                "Probar conexión");
        Set(StringKeys.AddAccBtnMsOAuth,             "Iniciar sesión con Microsoft");
        Set(StringKeys.AddAccMsOAuthHelp,            "Haz clic para iniciar sesión mediante el explorador web con Microsoft OAuth 2.0");
        Set(StringKeys.AddAccGmail2FaNote,           "Para Gmail con 2FA, use una contraseña de aplicación de 16 caracteres generada en https://myaccount.google.com/apppasswords");

        // ==========================================
        // 6. Settings View
        // ==========================================
        Set(StringKeys.SettingsTitle,                "Configuración");
        Set(StringKeys.SettingsSecAiBackend,         "🧠  Motor de IA y Selección de Inferencia");
        Set(StringKeys.SettingsBackendSelect,        "Seleccionar motor de inferencia:");
        Set(StringKeys.SettingsBackendLlama,         "llama.cpp (Local)");
        Set(StringKeys.SettingsBackendOllama,        "Ollama (Local)");
        Set(StringKeys.SettingsBackendCloud,         "OpenAI / Nube");
        Set(StringKeys.SettingsBackendNoAi,          "Sin IA (Solo correo)");
        Set(StringKeys.SettingsBatteryWarningTitle,  "⚡  Modo Sin IA en batería activo");
        Set(StringKeys.SettingsBatteryWarningDesc,   "El resumen por IA está pausado mientras el equipo funcione con batería.");
        Set(StringKeys.SettingsLlamaModelPath,       "Ruta del modelo GGUF:");
        Set(StringKeys.SettingsBrowse,               "Examinar...");
        Set(StringKeys.SettingsLlamaLayers,          "Capas en GPU (-ngl):");
        Set(StringKeys.SettingsLlamaPort,            "Puerto del servidor:");
        Set(StringKeys.SettingsLlamaContext,         "Tamaño de contexto (-c):");
        Set(StringKeys.SettingsLlamaUrl,             "URL del endpoint OpenAI Chat:");
        Set(StringKeys.SettingsLlamaAutoStart,       "Iniciar llama-server automáticamente");
        Set(StringKeys.SettingsLlamaInstantVram,     "Descarga instantánea de VRAM tras completar lote");
        Set(StringKeys.SettingsOllamaInfo,           "💡 Se conecta a Ollama local. Asegúrese de que Ollama esté ejecutándose.");
        Set(StringKeys.SettingsOllamaUrl,            "URL del endpoint de Ollama:");
        Set(StringKeys.SettingsOllamaModel,          "Nombre del modelo de Ollama:");
        Set(StringKeys.SettingsSuggestions,          "Sugerencias:");
        Set(StringKeys.SettingsCloudPreset,          "Plantilla de proveedor:");
        Set(StringKeys.SettingsCloudPresetSelect,    "-- Seleccionar plantilla --");
        Set(StringKeys.SettingsCloudUrl,             "URL del endpoint de API:");
        Set(StringKeys.SettingsCloudKey,             "Clave de API (Bearer Token):");
        Set(StringKeys.SettingsCloudShow,            "Mostrar");
        Set(StringKeys.SettingsCloudHide,            "Ocultar");
        Set(StringKeys.SettingsCloudModel,           "ID o nombre del modelo:");
        Set(StringKeys.SettingsNoAiTitle,            "🚫  Modo Sin IA (Cliente de correo clásico)");
        Set(StringKeys.SettingsNoAiDisclaimer,       "El modo Sin IA desactiva todas las funciones de IA.");
        Set(StringKeys.SettingsTemp,                 "Temperatura:");
        Set(StringKeys.SettingsTempDesc,             "0.0 = Determinista, 1.0+ = Creativo");
        Set(StringKeys.SettingsMaxTokens,            "Máx tokens de respuesta:");
        Set(StringKeys.SettingsMaxTokensDesc,        "Límite para la extensión del resumen");
        Set(StringKeys.SettingsTokenTip,             "💡 Una temperatura más baja (0.1 - 0.3) produce resúmenes más consistentes.");
        Set(StringKeys.SettingsEmailLimitHeader,     "Límite de caracteres por correo:");
        Set(StringKeys.SettingsEmailLimitDesc,       "Limita la longitud del cuerpo enviado a la IA para evitar desbordes de contexto.");
        Set(StringKeys.SettingsUnlimited,            "Ilimitado (Cuerpo completo)");
        Set(StringKeys.SettingsPresets,              "Valores predefinidos:");
        Set(StringKeys.SettingsCharsDefault,         "4K (Predeterminado)");
        Set(StringKeys.SettingsChars8k,              "8K");
        Set(StringKeys.SettingsChars16k,             "16K");
        Set(StringKeys.SettingsChars32k,             "32K");
        Set(StringKeys.SettingsCharsUnlimited,       "Ilimitado");
        Set(StringKeys.SettingsSecBattery,           "🔋  Ahorro de batería y energía");
        Set(StringKeys.SettingsBatteryDisableAi,     "Desactivar IA con batería (Modo Sin IA automático)");
        Set(StringKeys.SettingsBatteryDesc,          "Cambia automáticamente a Sin IA cuando el equipo usa batería para ahorrar energía.");
        Set(StringKeys.SettingsBatteryActive,        "Batería activa (IA pausada)");
        Set(StringKeys.SettingsBatteryAc,            "Conectado a CA (IA lista)");
        Set(StringKeys.SettingsSecLanguage,          "🌐  Idioma y región");
        Set(StringKeys.SettingsLanguageLabel,        "Idioma de la interfaz");
        Set(StringKeys.SettingsLanguageDesc,         "Seleccione su idioma preferido para la interfaz.");
        Set(StringKeys.SettingsSecEmail,             "📬  Opciones de recuperación de correos");
        Set(StringKeys.SettingsMaxEmails,            "Máx correos por cuenta:");
        Set(StringKeys.SettingsOnlyUnread,           "Recuperar solo no leídos");
        Set(StringKeys.SettingsMarkAsSeen,           "Marcar como leídos en el servidor IMAP al recuperar");
        Set(StringKeys.SettingsMultiSelectPreview,   "Vista previa en selección múltiple (Ctrl+clic):");
        Set(StringKeys.SettingsMultiSelectLast,      "Último correo seleccionado");
        Set(StringKeys.SettingsMultiSelectFirst,     "Primer correo seleccionado");
        Set(StringKeys.SettingsSecAttachments,       "📁  Descargas de adjuntos");
        Set(StringKeys.SettingsDownloadPathHeader,   "Carpeta de descargas predeterminada:");
        Set(StringKeys.SettingsDownloadPathDesc,     "Ubicación donde se guardarán los adjuntos de correos.");
        Set(StringKeys.SettingsDownloadPathSelectDesc, "Seleccionar carpeta de descargas de adjuntos");
        Set(StringKeys.SettingsDefault,              "Predeterminado");
        Set(StringKeys.SettingsSecUi,                "🖥️  Interfaz y diseño");
        Set(StringKeys.SettingsCollapseSidebar,      "Iniciar con barra lateral contraída por defecto");
        Set(StringKeys.SettingsScalingHeader,        "Escala predeterminada de ventana (Relativa a la pantalla):");
        Set(StringKeys.SettingsScalingDesc,          "Proporción objetivo del área de trabajo del monitor al iniciar.");
        Set(StringKeys.SettingsWidthScale,           "Escala ancho (%):");
        Set(StringKeys.SettingsHeightScale,          "Escala alto (%):");
        Set(StringKeys.SettingsResizeActive,         "Redimensionar ventana activa");
        Set(StringKeys.SettingsPresetDefault,        "60% × 56% (Predeterminado)");
        Set(StringKeys.SettingsPresetCompact,        "50% × 50% (Compacto)");
        Set(StringKeys.SettingsPresetLarge,          "75% × 70% (Grande)");
        Set(StringKeys.SettingsPresetMax,            "95% × 90% (Casi máximo)");
        Set(StringKeys.SettingsLaunchDimensions,     "Dimensiones persistentes: {0} × {1} px (Área útil: {2} × {3} px)");
        Set(StringKeys.SettingsAddShortcuts,         "Crear accesos directos en Escritorio y Menú Inicio");
        Set(StringKeys.SettingsShortcutsSuccess,     "¡Accesos directos creados exitosamente!");
        Set(StringKeys.SettingsShortcutsError,       "No se pudieron crear los accesos directos.");
        Set(StringKeys.SettingsSecTray,              "🔔  Servicio de bandeja del sistema y notificaciones");
        Set(StringKeys.SettingsAlwaysKeepOn,         "Mantener siempre activo (Ejecutar servicio en segundo plano)");
        Set(StringKeys.SettingsEnableTrayNotifs,      "Habilitar notificaciones de escritorio de Windows");
        Set(StringKeys.SettingsCheckInterval,        "Intervalo de comprobación (minutos):");
        Set(StringKeys.SettingsStartWithWindows,     "Iniciar servicio en la bandeja al iniciar sesión");
        Set(StringKeys.SettingsRestartDaemon,        "Reiniciar / Iniciar servicio de bandeja");
        Set(StringKeys.SettingsSecPrompt,            "✍️  Plantilla del indicador del sistema IA");
        Set(StringKeys.SettingsBtnSave,              "Guardar configuración");
        Set(StringKeys.SettingsBtnReset,             "Restablecer valores");
        Set(StringKeys.SettingsBtnTestLlm,           "Probar conexión LLM");
        Set(StringKeys.SettingsSavedToast,           "¡Configuración guardada exitosamente!");
        Set(StringKeys.SettingsResetConfirm,         "¿Está seguro de que desea restablecer toda la configuración?");

        // ==========================================
        // 7. Live Logs View
        // ==========================================
        Set(StringKeys.LogsTitle,                    "Registros en vivo");
        Set(StringKeys.LogsSubtitle,                 "Eventos de diagnóstico en tiempo real, conexiones IMAP e inferencia de IA.");
        Set(StringKeys.LogsBtnCopy,                  "Copiar registros");
        Set(StringKeys.LogsBtnClear,                 "Limpiar registros");
        Set(StringKeys.LogsBtnExport,                "Exportar registros...");
        Set(StringKeys.LogsChkAutoScroll,            "Desplazamiento automático");
        Set(StringKeys.LogsCopiedMsg,                "¡Registro copiado al portapapeles!");

        // ==========================================
        // 8. Common & Dialog Messages
        // ==========================================
        Set(StringKeys.CommonSave,                   "Guardar");
        Set(StringKeys.CommonCancel,                 "Cancelar");
        Set(StringKeys.CommonClose,                  "Cerrar");
        Set(StringKeys.CommonDelete,                 "Eliminar");
        Set(StringKeys.CommonOk,                     "Aceptar");
        Set(StringKeys.CommonYes,                    "Sí");
        Set(StringKeys.CommonNo,                     "No");
        Set(StringKeys.CommonError,                  "Error");
        Set(StringKeys.CommonWarning,                "Advertencia");
        Set(StringKeys.CommonSuccess,                "Éxito");
        Set(StringKeys.CommonLoading,                "Cargando...");
        Set(StringKeys.CommonConnecting,             "Conectando...");

        // ==========================================
        // 9. Status Bar & Metrics
        // ==========================================
        Set(StringKeys.StatusSyncComplete,           "Sincronización completa");
        Set(StringKeys.StatusReady,                  "Listo");
        Set(StringKeys.StatusReadyEmails,            "Listo • {0} correos en bandeja de entrada ({1} no leídos)");
        Set(StringKeys.StatusReadyFolder,            "Listo • {0} correos en {1}");
        Set(StringKeys.StatusAccountsCount,          "Cuentas: {0} | VRAM: {1}");
        Set(StringKeys.StatusAccountsBackend,        "Cuentas: {0} | Motor: {1}");
        Set(StringKeys.StatusReadyBackend,           "Listo • Motor: {0}");
        Set(StringKeys.StatusVramFree,               "VRAM libre");
        Set(StringKeys.StatusModelLoaded,            "Modelo cargado en VRAM");
        Set(StringKeys.StatusOllamaActive,           "Ollama activo");
        Set(StringKeys.StatusCloudActive,            "Nube activa");
        Set(StringKeys.StatusAiDisabled,             "IA desactivada");
        Set(StringKeys.StatusBatterySaverNoAi,       "Ahorro de batería (Sin IA)");
        Set(StringKeys.StatusOnDemandVram,           "Bajo demanda (Descarga VRAM)");
        Set(StringKeys.StatusSummaryReady,           "Resumen listo");
        Set(StringKeys.StatusSyncingFolder,          "Sincronizando {0}...");
        Set(StringKeys.StatusNoAccounts,             "Sin cuentas configuradas");
        Set(StringKeys.StatusLiveConnecting,         "IMAP en vivo: Conectando...");
        Set(StringKeys.StatusLiveListening,          "IMAP en vivo: Conectado y escuchando");
        Set(StringKeys.StatusLiveStopped,            "IMAP en vivo: Detenido");
        Set(StringKeys.StatusLiveDone,               "IMAP en vivo: Transmitiendo señales DONE...");
        Set(StringKeys.StatusLiveNewEmail,           "IMAP en vivo: Nuevo correo para {0}");
        Set(StringKeys.StatusStartingUp,             "Iniciando...");
        Set(StringKeys.StatusDisabledClassic,        "Desactivado (Correo clásico)");
        Set(StringKeys.StatusModelLoadFailed,        "No se pudo cargar el modelo");
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
