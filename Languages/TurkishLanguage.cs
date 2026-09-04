namespace KerkenezMail.Languages
{
    /// <summary>
    /// Turkish (tr) language definition for Kerkenez Mail.
    /// Fully localized translation table for Turkish users.
    /// </summary>
    public class TurkishLanguage : BaseLanguage
    {
        public override string Code => "tr";
        public override string Name => "Türkçe";
        public override string EnglishName => "Turkish";
        public override string FlagEmoji => "🇹🇷";

        protected override void InitTranslations()
        {
            // ==============================================================================
            // KEY                                       TRANSLATION
            // ==============================================================================

            // --- 1. Navigation & Sidebar ---
            Set(StringKeys.NavInbox,                     "Gelen Kutusu");
            Set(StringKeys.NavSendMail,                  "E-posta Gönder");
            Set(StringKeys.NavAccounts,                  "Hesaplar");
            Set(StringKeys.NavSettings,                  "Ayarlar");
            Set(StringKeys.NavLiveLogs,                  "Canlı Kayıtlar");
            Set(StringKeys.NavSent,                      "Gönderilenler");
            Set(StringKeys.NavArchived,                  "Arşiv");
            Set(StringKeys.NavSpam,                      "İstenmeyen (Spam)");
            Set(StringKeys.NavTrash,                     "Çöp Kutusu");
            Set(StringKeys.NavLiveImap,                  "Canlı IMAP");
            Set(StringKeys.NavLiveImapActive,            "Canlı IMAP devrede (Kapatmak için tıklayın)");
            Set(StringKeys.NavLiveImapOff,               "Canlı IMAP kapalı (Açmak için tıklayın)");
            Set(StringKeys.NavTipExpandSidebar,          "Kenar çubuğunu genişlet (Ctrl+B)");
            Set(StringKeys.NavTipCollapseSidebar,        "Kenar çubuğunu daralt (Ctrl+B)");
            Set(StringKeys.NavTipExpandFolders,          "Klasörleri genişlet (Gönderilen, Arşiv, Çöp...)");
            Set(StringKeys.NavTipCollapseFolders,        "Klasörleri daralt");

            // --- 2. Main Window & Shell ---
            Set(StringKeys.AppTitle,                     "Kerkenez Mail (Win32)");
            Set(StringKeys.TrayOpen,                     "📬  Kerkenez Mail'i Aç");
            Set(StringKeys.TrayLiveImapActive,           "⚡  Canlı IMAP: Aktif (Aç/Kapat)");
            Set(StringKeys.TrayLiveImapOff,              "💤  Canlı IMAP: Boşta (Aç/Kapat)");
            Set(StringKeys.TrayCheckNow,                 "🔄  Şimdi Kontrol Et");
            Set(StringKeys.TraySettings,                 "⚙️  Ayarlar");
            Set(StringKeys.TrayExit,                     "❌  Çıkış");

            // --- 3. Inbox & Summaries View ---
            Set(StringKeys.InboxRefresh,                 "Yenile");
            Set(StringKeys.InboxCopySummary,             "Özeti Kopyala");
            Set(StringKeys.InboxExport,                  "Dışa Aktar...");
            Set(StringKeys.InboxOpenInBrowser,           "Tarayıcıda Aç");
            Set(StringKeys.InboxTipRefresh,              "Gelen Kutusunu Yenile: Yapılandırılan hesaplardan yeni e-postaları al");
            Set(StringKeys.InboxTipCopySummary,          "Özeti Kopyala: Yapay zeka özetini panoya kopyala");
            Set(StringKeys.InboxTipExport,               "Dışa Aktar: E-postaları ve özetleri JSON, CSV, Markdown veya HTML raporu olarak kaydet");
            Set(StringKeys.InboxTipOpenInBrowser,        "Tarayıcıda Aç: Orijinal e-postayı varsayılan tarayıcıda görüntüle (Tam HTML)");
            Set(StringKeys.InboxTipArchive,              "Arşivle: Seçilen e-postaları Arşiv klasörüne taşı");
            Set(StringKeys.InboxTipDelete,               "Sil: Seçilen e-postaları Çöp Kutusu klasörüne taşı");
            Set(StringKeys.InboxTipReply,                "Yanıtla: Bu e-posta dizisine yanıt yaz");
            Set(StringKeys.InboxTipMoveToInbox,          "Gelen Kutusuna Taşı: Seçilen e-postaları Gelen Kutusuna geri taşı");
            Set(StringKeys.InboxAccountLabel,            "Hesap:");
            Set(StringKeys.InboxAllAccounts,             "Tüm Hesaplar");
            Set(StringKeys.InboxListHeader,              "{0} ({1} e-posta)");
            Set(StringKeys.InboxListHeaderUnread,        "{0} ({1} e-posta, {2} okunmamış)");
            Set(StringKeys.InboxSubjectPrefix,           "Konu:");
            Set(StringKeys.InboxNoEmailSelected,         "(E-posta seçilmedi)");
            Set(StringKeys.InboxAiExecutiveSummary,      "Yapay Zeka Yönetici Özeti");
            Set(StringKeys.InboxAiGeneratedVram,         "(VRAM'deki yerel LLM tarafından üretildi)");
            Set(StringKeys.InboxAiSummaryPlaceholder,    "Yapay zeka özeti burada görünecek...");
            Set(StringKeys.InboxAttachmentsTitle,        "Ekler:");
            Set(StringKeys.InboxTagRead,                 "Okundu");
            Set(StringKeys.InboxTagUnread,               "Okunmadı");
            Set(StringKeys.InboxTagArchived,             "Arşivlendi");
            Set(StringKeys.InboxSummarize,               "Yapay Zeka ile Özetle");
            Set(StringKeys.InboxSearchPlaceholder,       "Gönderen, konu veya içeriğe göre ara...");
            Set(StringKeys.InboxColAccount,              "Hesap");
            Set(StringKeys.InboxColFrom,                 "Gönderen");
            Set(StringKeys.InboxColSubject,              "Konu");
            Set(StringKeys.InboxColDate,                 "Tarih");
            Set(StringKeys.InboxColPriority,             "Öncelik");
            Set(StringKeys.InboxPriorityHigh,            "YÜKSEK");
            Set(StringKeys.InboxPriorityNormal,          "NORMAL");
            Set(StringKeys.InboxPriorityLow,             "DÜŞÜK");
            Set(StringKeys.InboxEmptyTitle,              "E-posta bulunamadı");
            Set(StringKeys.InboxEmptySubtitle,           "Bir hesap seçin veya e-postaları getirmek için Yenile'ye tıklayın.");
            Set(StringKeys.InboxNoSelectionTitle,        "E-posta seçilmedi");
            Set(StringKeys.InboxNoSelectionSubtitle,     "Özetini ve ayrıntılarını görüntülemek için listeden bir e-posta seçin.");
            Set(StringKeys.InboxDetailFrom,              "Gönderen:");
            Set(StringKeys.InboxDetailTo,                "Alıcı:");
            Set(StringKeys.InboxDetailDate,              "Tarih:");
            Set(StringKeys.InboxDetailAccount,           "Hesap:");
            Set(StringKeys.InboxTabSummary,              "YZ Özeti");
            Set(StringKeys.InboxTabOriginal,             "Orijinal E-posta");
            Set(StringKeys.InboxBtnMarkRead,             "Okundu İşaretle");
            Set(StringKeys.InboxBtnMarkUnread,           "Okunmadı İşaretle");
            Set(StringKeys.InboxBtnDelete,               "Sil");
            Set(StringKeys.InboxBtnMoveInbox,            "Gelen Kutusuna Taşı");
            Set(StringKeys.InboxBtnReply,                "Yanıtla");
            Set(StringKeys.InboxBtnForward,              "İlet");
            Set(StringKeys.InboxBtnDownloadAttachments,  "Ekleri İndir");
            Set(StringKeys.InboxLoadingStatus,           "E-postalar alınıyor...");
            Set(StringKeys.InboxSummarizingStatus,       "Yapay zeka ile özetleniyor...");
            Set(StringKeys.InboxLiveActiveBadge,         "Canlı IMAP: Bağlı");
            Set(StringKeys.InboxMultiSelectCount,        "{0} e-posta seçildi");

            // --- 4. Send Mail / Compose ---
            Set(StringKeys.SendTitle,                    "E-posta Yaz");
            Set(StringKeys.SendFrom,                     "Gönderen:");
            Set(StringKeys.SendTo,                       "Kime:");
            Set(StringKeys.SendCc,                       "Bilgi (Cc):");
            Set(StringKeys.SendBcc,                      "Gizli (Bcc):");
            Set(StringKeys.SendSubject,                  "Konu:");
            Set(StringKeys.SendBodyPlaceholder,          "E-posta mesajınızı buraya yazın...");
            Set(StringKeys.SendAttachments,              "Ekler:");
            Set(StringKeys.SendAddAttachment,            "Dosya Ekle");
            Set(StringKeys.SendRemoveAttachment,         "Kaldır");
            Set(StringKeys.SendBtnSend,                  "E-posta Gönder");
            Set(StringKeys.SendBtnDiscard,               "Vazgeç");
            Set(StringKeys.SendBtnSaveDraft,             "Taslağı Kaydet");
            Set(StringKeys.SendSending,                  "E-posta gönderiliyor...");
            Set(StringKeys.SendSuccess,                  "E-posta başarıyla gönderildi!");
            Set(StringKeys.SendError,                    "E-posta gönderilemedi.");
            Set(StringKeys.SendAiAssist,                 "YZ Düzenle");
            Set(StringKeys.SendDraftsTitle,              "Taslaklar");

            // --- 5. Accounts View & Dialog ---
            Set(StringKeys.AccountsTitle,                "E-posta Hesapları");
            Set(StringKeys.AccountsSubtitle,             "Yapılandırılmış IMAP/SMTP hesaplarınızı ve kimlik bilgilerinizi yönetin.");
            Set(StringKeys.AccountsBtnAdd,               "Hesap Ekle");
            Set(StringKeys.AccountsBtnEdit,              "Düzenle");
            Set(StringKeys.AccountsBtnDelete,            "Sil");
            Set(StringKeys.AccountsBtnTest,              "Bağlantıyı Sına");
            Set(StringKeys.AccountsColEmail,             "E-posta Adresi");
            Set(StringKeys.AccountsColProvider,          "Sağlayıcı");
            Set(StringKeys.AccountsColServer,            "Gelen Sunucu");
            Set(StringKeys.AccountsColStatus,            "Durum");
            Set(StringKeys.AccountsStatusConnected,      "Bağlı");
            Set(StringKeys.AccountsStatusError,          "Bağlantı Hatası");
            Set(StringKeys.AccountsDeleteConfirm,        "'{0}' hesabını silmek istediğinize emin misiniz?");

            Set(StringKeys.AddAccTitle,                  "E-posta Hesabı Ekle");
            Set(StringKeys.AddAccEditTitle,              "E-posta Hesabını Düzenle");
            Set(StringKeys.AddAccProviderPreset,         "Sağlayıcı Şablonu:");
            Set(StringKeys.AddAccEmail,                  "E-posta Adresi:");
            Set(StringKeys.AddAccPassword,               "Parola / Uygulama Parolası:");
            Set(StringKeys.AddAccDisplayName,            "Görünen İsim:");
            Set(StringKeys.AddAccImapServer,             "IMAP Sunucusu:");
            Set(StringKeys.AddAccImapPort,               "IMAP Bağlantı Noktası:");
            Set(StringKeys.AddAccImapSsl,                "IMAP için SSL/TLS kullan");
            Set(StringKeys.AddAccSmtpServer,             "SMTP Sunucusu:");
            Set(StringKeys.AddAccSmtpPort,               "SMTP Bağlantı Noktası:");
            Set(StringKeys.AddAccSmtpSsl,                "SMTP için SSL/TLS kullan");
            Set(StringKeys.AddAccOAuthSignIn,            "Microsoft ile Giriş Yap");
            Set(StringKeys.AddAccOAuthSuccess,           "Microsoft OAuth girişi başarılı!");
            Set(StringKeys.AddAccTestConnection,         "Bağlantıyı Sına");
            Set(StringKeys.AddAccTesting,                "Bağlantı sınanıyor...");
            Set(StringKeys.AddAccSave,                   "Hesabı Kaydet");
            Set(StringKeys.AddAccCancel,                 "İptal");

            // --- 6. Settings View ---
            Set(StringKeys.SettingsTitle,                "Ayarlar");
            Set(StringKeys.SettingsSecAiBackend,         "🤖  Yapay Zeka Özetleme ve Önceliklendirme");
            Set(StringKeys.SettingsSecBattery,           "🔋  Güç ve Pil Tasarrufu");
            Set(StringKeys.SettingsSecEmail,             "📧  E-posta Getirme");
            Set(StringKeys.SettingsSecUi,                "🖥️  Arayüz ve Düzen");
            Set(StringKeys.SettingsSecLanguage,          "🌐  Dil ve Bölge");
            Set(StringKeys.SettingsSecTray,              "🔔  Bildirimler ve Arka Plan");
            Set(StringKeys.SettingsSecPrompt,            "📝  YZ İstem Şablonu");

            Set(StringKeys.SettingsLanguageLabel,        "Arayüz Dili:");
            Set(StringKeys.SettingsLanguageDesc,         "Kullanıcı arayüzünün görüntüleme dilini seçin. Değişiklikler anında uygulanır.");

            Set(StringKeys.SettingsBackendLlama,         "🦙  Yerel llama.cpp (Gömülü GGUF)");
            Set(StringKeys.SettingsBackendOllama,        "🦙  Yerel Ollama (localhost:11434)");
            Set(StringKeys.SettingsBackendCloud,         "☁️  Bulut / Özel API (OpenAI, OpenRouter, Groq, DeepSeek)");
            Set(StringKeys.SettingsBackendNoAi,          "🚫  Yapay Zeka Yok (Özetleme ve Önceliklendirmeyi Kapat)");

            Set(StringKeys.SettingsBatteryDisableAi,     "Pil gücünde çalışırken yapay zeka özetlemeyi otomatik olarak devre dışı bırak");
            Set(StringKeys.SettingsBatteryActiveWarning, "Cihaz pilde çalıştığı için yapay zeka şu anda askıya alındı.");

            Set(StringKeys.SettingsMaxEmails,            "Hesap başına alınacak en fazla e-posta:");
            Set(StringKeys.SettingsOnlyUnread,           "Yalnızca okunmamış e-postaları al");
            Set(StringKeys.SettingsMarkAsSeen,           "Alınan e-postaları otomatik olarak okundu işaretle");
            Set(StringKeys.SettingsDownloadPath,         "Varsayılan ek indirme dizini:");
            Set(StringKeys.SettingsBrowse,               "Gözat...");

            Set(StringKeys.SettingsCollapseSidebar,      "Kenar çubuğunu başlangıçta daraltılmış olarak aç");
            Set(StringKeys.SettingsWindowWidth,          "Pencere Genişlik Oranı (%):");
            Set(StringKeys.SettingsWindowHeight,         "Pencere Yükseklik Oranı (%):");
            Set(StringKeys.SettingsApplySize,            "Pencere Boyutunu Şimdi Uygula");

            Set(StringKeys.SettingsAlwaysKeepOn,         "Uygulamayı arka planda sistem tepsisinde çalışır durumda tut");
            Set(StringKeys.SettingsEnableTrayNotifs,      "Yeni e-postalar için Windows bildirimleri göster");
            Set(StringKeys.SettingsCheckInterval,        "Arka plan kontrol aralığı (dakika):");
            Set(StringKeys.SettingsStartWithWindows,     "Arka plan hizmetini Windows ile otomatik başlat");
            Set(StringKeys.SettingsRestartDaemon,        "Arka Plan Hizmetini Yeniden Başlat");

            Set(StringKeys.SettingsBtnSave,              "Ayarları Kaydet");
            Set(StringKeys.SettingsBtnTestLlm,           "YZ Arka Ucunu Sına");
            Set(StringKeys.SettingsSavedToast,           "Ayarlar başarıyla kaydedildi!");

            // --- 7. Live Logs View ---
            Set(StringKeys.LogsTitle,                    "Canlı Kayıtlar");
            Set(StringKeys.LogsSubtitle,                 "Gerçek zamanlı arka plan IMAP ve yapay zeka işlem kayıtları.");
            Set(StringKeys.LogsBtnCopy,                  "Kayıtları Kopyala");
            Set(StringKeys.LogsBtnClear,                 "Kayıtları Temizle");
            Set(StringKeys.LogsBtnExport,                "Kayıtları Dışa Aktar...");
            Set(StringKeys.LogsChkAutoScroll,            "En alta otomatik kaydır");
            Set(StringKeys.LogsCopiedMsg,                "Konsol kayıtları panoya kopyalandı!");

            // --- 8. Common & Dialog Messages ---
            Set(StringKeys.CommonSave,                   "Kaydet");
            Set(StringKeys.CommonCancel,                 "İptal");
            Set(StringKeys.CommonClose,                  "Kapat");
            Set(StringKeys.CommonDelete,                 "Sil");
            Set(StringKeys.CommonOk,                     "Tamam");
            Set(StringKeys.CommonYes,                    "Evet");
            Set(StringKeys.CommonNo,                     "Hayır");
            Set(StringKeys.CommonError,                  "Hata");
            Set(StringKeys.CommonWarning,                "Uyarı");
            Set(StringKeys.CommonSuccess,                "Başarılı");
            Set(StringKeys.CommonLoading,                "Yükleniyor...");
            Set(StringKeys.CommonConnecting,             "Bağlanıyor...");

            // --- 9. Status Bar Feedback ---
            Set(StringKeys.StatusSyncComplete,           "Senkronizasyon tamamlandı");
            Set(StringKeys.StatusReady,                  "Hazır");
        }
    }
}
