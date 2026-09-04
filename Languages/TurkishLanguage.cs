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
            Set(StringKeys.MainShortcutsPromptTitle,     "Kerkenez Mail Kısayolları");
            Set(StringKeys.MainShortcutsPromptDesc,      "Kerkenez Mail'e Hoş Geldiniz!\r\n\r\nKolay erişim için Masaüstü ve Başlat Menüsünde kısayol oluşturmak ister misiniz?");
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
            Set(StringKeys.InboxSavedAttachmentsToast,   "{0}/{1} ek şuraya kaydedildi:\r\n{2}\r\n\r\nKlasörü açmak ister misiniz?");
            Set(StringKeys.InboxSavedAttachmentsStatus,  "{0}/{1} ek kaydedildi");
            Set(StringKeys.InboxDownloadComplete,        "İndirme Tamamlandı");
            Set(StringKeys.InboxDownloadError,           "Ekler indirilirken hata oluştu: {0}");
            Set(StringKeys.InboxSummaryCopiedToast,      "Özet panoya kopyalandı!");
            Set(StringKeys.InboxAllSummariesCopiedToast, "Tüm özetler Markdown formatında panoya kopyalandı!");
            Set(StringKeys.InboxNoSummaryToCopy,         "Kopyalanacak özet bulunamadı.");
            Set(StringKeys.InboxNoEmailsToExport,        "Dışa aktarılacak e-posta bulunamadı.");
            Set(StringKeys.InboxExportSuccessToast,      "{0} dosyasına başarıyla aktarıldı!");
            Set(StringKeys.InboxExportSuccessTitle,      "Dışa Aktarma Başarılı");
            Set(StringKeys.InboxExportErrorToast,        "Dosya dışa aktarılamadı: {0}");
            Set(StringKeys.InboxExportErrorTitle,        "Dışa Aktarma Hatası");
            Set(StringKeys.InboxBrowserErrorToast,       "E-posta tarayıcıda açılamadı:\n{0}");
            Set(StringKeys.InboxBrowserErrorTitle,       "Tarayıcı Hatası");
            Set(StringKeys.InboxAccountNotFound,         "'{0}' ({1}) için yapılandırılmış e-posta hesabı bulunamadı.");
            Set(StringKeys.InboxDeletePermanentlyTip,    "Kalıcı Olarak Sil: Seçilen e-postaları kalıcı olarak sil");
            Set(StringKeys.InboxDeleteMoveTip,           "Sil: Seçilen e-postaları Çöp Kutusu'na taşı");
            Set(StringKeys.InboxDownloadedSingleToast,   "'{0}' başarıyla indirildi!\r\n\r\nKaydedilen konum: {1}\r\n\r\nİçeren klasörü açmak ister misiniz?");
            Set(StringKeys.InboxDownloadedSingleStatus,  "'{0}' indirildi");
            Set(StringKeys.InboxDownloadFailedStatus,    "İndirme başarısız");
            Set(StringKeys.InboxDownloadFailed,          "İndirme Başarısız");

            // --- 4. Send Mail / Compose ---
            Set(StringKeys.SendTitle,                    "Yeni E-posta Yaz");
            Set(StringKeys.SendThreadedReply,            "Konu Yanıtı");
            Set(StringKeys.SendBackToInbox,              "Gelen Kutusu");
            Set(StringKeys.SendPopOut,                   "Ayrı Pencere");
            Set(StringKeys.SendFrom,                     "Gönderen:");
            Set(StringKeys.SendCcBccToggle,              "+ Cc / Bcc");
            Set(StringKeys.SendTo,                       "Kime:");
            Set(StringKeys.SendCc,                       "Cc:");
            Set(StringKeys.SendBcc,                      "Bcc:");
            Set(StringKeys.SendSubject,                  "Konu:");
            Set(StringKeys.SendToPlaceholder,            "alici@ornek.com (birden çok ise virgülle ayırın)");
            Set(StringKeys.SendCcPlaceholder,            "Cc alıcıları...");
            Set(StringKeys.SendBccPlaceholder,           "Bcc alıcıları...");
            Set(StringKeys.SendSubjectPlaceholder,       "Konu");
            Set(StringKeys.SendBodyPlaceholder,          "E-posta mesajınızı buraya Markdown kullanarak yazın...\r\n\r\n# Başlık\r\n**Kalın**, *İtalik*, `kod`\r\n- Madde işaretleri\r\n> Alıntı metin\r\n[Bağlantı Başlığı](https://example.com)");
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
            Set(StringKeys.SendTabMarkdown,              "Markdown Düzenle");
            Set(StringKeys.SendTabPlaintext,             "Düz Metin Önizleme");
            Set(StringKeys.SendTabHtml,                  "HTML Önizleme");
            Set(StringKeys.SendFormatMultipart,          "Markdown (Metin + HTML Çok Parçalı)");
            Set(StringKeys.SendFormatPlaintext,          "Yalnızca Düz Metin (Ham RFC Metni)");
            Set(StringKeys.SendDropHint,                 "Ekleri buraya sürükleyip bırakın (veya Dosya Seç'e tıklayın)");
            Set(StringKeys.SendBrowseFiles,              "Dosya Seç...");
            Set(StringKeys.SendAttachmentSummary,        "{0} ek ({1} KB)");
            Set(StringKeys.SendStatusHint,               "Yazmaya hazır. Markdown biçimlendirme desteklenir.");
            Set(StringKeys.SendConfirmDiscard,           "Bu taslağı silmek istediğinizden emin misiniz?");
            Set(StringKeys.SendDiscardTitle,             "Taslaktan Vazgeç");
            Set(StringKeys.SendToolbarBold,              "Kalın (**metin**)");
            Set(StringKeys.SendToolbarItalic,            "İtalik (*metin*)");
            Set(StringKeys.SendToolbarHeader,            "Başlık (### metin)");
            Set(StringKeys.SendToolbarLink,              "Bağlantı Ekle ([metin](url))");
            Set(StringKeys.SendToolbarBulletList,        "Madde İşaretli Liste (- madde)");
            Set(StringKeys.SendToolbarNumberedList,      "Numaralı Liste (1. madde)");
            Set(StringKeys.SendToolbarQuote,             "Alıntı Bloğu (> metin)");
            Set(StringKeys.SendToolbarCode,              "Kod Bloğu (```)");
            Set(StringKeys.SendToolbarRule,              "Yatay Çizgi (---)");
            Set(StringKeys.SendMissingRecipient,         "Lütfen 'Kime' alanına en az bir alıcı girin.");
            Set(StringKeys.SendMissingRecipientTitle,    "Eksik Alıcı");
            Set(StringKeys.SendMissingAccount,           "Lütfen göndereceğiniz hesabı seçin.");
            Set(StringKeys.SendMissingAccountTitle,      "Eksik Hesap");
            Set(StringKeys.SendNoSubjectPrompt,          "Bu mesajı konu olmadan göndermek istiyor musunuz?");
            Set(StringKeys.SendNoSubjectTitle,           "Konu Yok");
            Set(StringKeys.SendSentTitle,                "E-posta Gönderildi");
            Set(StringKeys.SendFailedTitle,              "E-posta Gönderilemedi");

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
            Set(StringKeys.AccountsStatusUntested,        "Sınanmadı");
            Set(StringKeys.AccountsStatusConnected,      "Bağlandı");
            Set(StringKeys.AccountsStatusConnectedUnread,"Bağlandı ({0} okunmamış)");
            Set(StringKeys.AccountsStatusFailed,         "Başarısız");
            Set(StringKeys.AccountsStatusError,          "Bağlantı Hatası");
            Set(StringKeys.AccountsEmptyDesc,            "Henüz yapılandırılmış hesap yok.\r\nGmail veya IMAP hesabınızı bağlamak için yukarıdaki '+ Hesap Ekle' düğmesine tıklayın.");
            Set(StringKeys.AccountsDeleteConfirm,        "'{0}' ({1}) hesabını kaldırmak istediğinizden emin misiniz?");

            Set(StringKeys.AddAccTitle,                  "E-posta Hesabı Ekle");
            Set(StringKeys.AddAccEditTitle,              "E-posta Hesabını Düzenle");
            Set(StringKeys.AddAccProviderPreset,         "Sağlayıcı Şablonu:");
            Set(StringKeys.AddAccEmail,                  "E-posta Adresi:");
            Set(StringKeys.AddAccPassword,               "Parola / Uygulama Parolası:");
            Set(StringKeys.AddAccDisplayName,            "Hesap Adı:");
            Set(StringKeys.AddAccImapServer,             "IMAP Sunucusu ve Port:");
            Set(StringKeys.AddAccImapPort,               "IMAP Port:");
            Set(StringKeys.AddAccImapSsl,                "IMAP için SSL/TLS kullan");
            Set(StringKeys.AddAccSmtpServer,             "SMTP Sunucusu ve Port:");
            Set(StringKeys.AddAccSmtpPort,               "SMTP Port:");
            Set(StringKeys.AddAccSmtpSsl,                "SMTP için SSL/TLS kullan");
            Set(StringKeys.AddAccOAuthSignIn,            "Microsoft ile Giriş Yap");
            Set(StringKeys.AddAccOAuthSuccess,           "Microsoft OAuth girişi başarılı!");
            Set(StringKeys.AddAccOAuthClickHelp,         "Web tarayıcınız üzerinden oturum açmak için yukarıya tıklayın.");
            Set(StringKeys.AddAccHelpGmail,              "💡 Gmail için: 16 karakterlik bir Google Uygulama Şifresi kullanın (myaccount.google.com/apppasswords). Standard Google şifreleri 2FA açıkken çalışmaz.");
            Set(StringKeys.AddAccTestConnection,         "Bağlantıyı Sına");
            Set(StringKeys.AddAccTesting,                "Bağlantı sınanıyor...");
            Set(StringKeys.AddAccSave,                   "Hesabı Kaydet");
            Set(StringKeys.AddAccCancel,                 "İptal");

            // --- 6. Settings View ---
            Set(StringKeys.SettingsTitle,                "Ayarlar");
            Set(StringKeys.SettingsSecAiBackend,         "🤖  Yapay Zeka Motoru ve LLM Arka Ucu");
            Set(StringKeys.SettingsBackendSelect,        "Çıkarım Arka Ucunu Seçin:");
            Set(StringKeys.SettingsBackendLlama,         "🦙  Yerel llama.cpp");
            Set(StringKeys.SettingsBackendOllama,        "🦙  Yerel Ollama");
            Set(StringKeys.SettingsBackendCloud,         "☁️  Bulut / Özel API");
            Set(StringKeys.SettingsBackendNoAi,          "🚫  Yapay Zeka Yok (Devre Dışı)");
            Set(StringKeys.SettingsBatteryWarningTitle,  "⚡  Yapay Zeka Devre Dışı Modunda Çalışıyor (Pil Tasarrufu Aktif)");
            Set(StringKeys.SettingsBatteryWarningDesc,   "Bu cihaz pil gücüyle çalıştığı için yapay zeka özetleme ve öncelik sıralaması geçici olarak askıya alındı. Aşağıda yapılandırılmış ayarlarınız korunur ve prize takıldığında otomatik olarak devam eder.");
            Set(StringKeys.SettingsLlamaModelPath,       "GGUF Model Dosyası Yolu:");
            Set(StringKeys.SettingsLlamaLayers,          "GPU Katmanları (-ngl):");
            Set(StringKeys.SettingsLlamaPort,            "Sunucu Portu:");
            Set(StringKeys.SettingsLlamaContext,         "Bağlam Boyutu (-c):");
            Set(StringKeys.SettingsLlamaUrl,             "OpenAI Sohbet Uç Noktası URL'si:");
            Set(StringKeys.SettingsLlamaAutoStart,       "İstek üzerine llama-server'ı otomatik başlat");
            Set(StringKeys.SettingsLlamaInstantVram,     "Anında VRAM Boşaltma (toplu işlem bittiğinde GPU belleğini boşalt; modelin bellekte kalması için işareti kaldırın)");
            Set(StringKeys.SettingsOllamaInfo,           "💡 Doğrudan yerel Ollama'ya bağlanır. Ollama'nın çalıştığından emin olun (`ollama serve` veya masaüstü uygulaması).");
            Set(StringKeys.SettingsOllamaUrl,            "Ollama Uç Noktası URL'si:");
            Set(StringKeys.SettingsOllamaModel,          "Ollama Model Adı (örn. llama3.2, qwen2.5:3b, mistral):");
            Set(StringKeys.SettingsSuggestions,          "Öneriler:");
            Set(StringKeys.SettingsCloudPreset,          "Sağlayıcı Ön Ayarı:");
            Set(StringKeys.SettingsCloudPresetSelect,    "(Sağlayıcı Ön Ayarı Seçin...)");
            Set(StringKeys.SettingsCloudUrl,             "API Uç Noktası URL'si (/v1/chat/completions):");
            Set(StringKeys.SettingsCloudKey,             "API Anahtarı (Bearer Belirteci):");
            Set(StringKeys.SettingsCloudShow,            "Göster");
            Set(StringKeys.SettingsCloudHide,            "Gizle");
            Set(StringKeys.SettingsCloudModel,           "Model Kimliği / Adı (örn. gpt-4o-mini, deepseek-chat):");
            Set(StringKeys.SettingsNoAiTitle,            "🚫  Yapay Zekasız Mod (Klasik E-posta İstemci Modu)");
            Set(StringKeys.SettingsNoAiDisclaimer,       "Bilgilendirme ve Mod Detayları:\r\n• Sıfır YZ Yükü: LLM sunucuları, arka plan çıkarımı ve GPU/VRAM model yükleme tamamen devre dışıdır.\r\n• Tam Okuma Alanı: YZ Özet kutusu gizlenerek e-posta gövdesi görünümü tam yüksekliğe genişletilir.\r\n• Temiz Gelen Kutusu: Sade ve klasik bir görünüm için Öncelik sütunu (⚡) gelen kutusundan kaldırılır.\r\n• Anında Getirme: E-postalar istem oluşturma veya özetleme gecikmesi olmadan doğrudan IMAP üzerinden eşitlenir.\r\n\r\nBu yapılandırmayı uygulamak ve kaydetmek için alttaki '💾 Ayarları Kaydet' düğmesine tıklayın.");
            Set(StringKeys.SettingsTemp,                 "Sıcaklık:");
            Set(StringKeys.SettingsTempDesc,             "0.0 = Belirleyici, 1.0+ = Yaratıcı");
            Set(StringKeys.SettingsMaxTokens,            "Maksimum Yanıt Belirteçleri:");
            Set(StringKeys.SettingsMaxTokensDesc,        "Özet uzunluğu bütçesi");
            Set(StringKeys.SettingsTokenTip,             "💡 İpucu: Düşük sıcaklık (0.1 - 0.3) tutarlı, tarafsız yönetici özetleri üretir.");
            Set(StringKeys.SettingsEmailLimitHeader,     "E-posta Alımı Karakter Sınırı:");
            Set(StringKeys.SettingsEmailLimitDesc,       "Yapay zeka modeline gönderilen ham e-posta gövdesi uzunluğunu sınırlar. Kırpma, istemleri hızlı tutar ve VRAM bağlam taşmalarını önler.");
            Set(StringKeys.SettingsUnlimited,            "Sınırsız (Tüm Gövdeyi Gönder)");
            Set(StringKeys.SettingsPresets,              "Ön Ayarlar:");
            Set(StringKeys.SettingsCharsDefault,         "4.000 karakter (Varsayılan)");
            Set(StringKeys.SettingsChars8k,              "8.000 karakter");
            Set(StringKeys.SettingsChars16k,             "16.000 karakter");
            Set(StringKeys.SettingsChars32k,             "32.000 karakter");
            Set(StringKeys.SettingsCharsUnlimited,       "♾️ Sınırsız");
            Set(StringKeys.SettingsSecBattery,           "🔋  Pil Tasarrufu Modu");
            Set(StringKeys.SettingsBatteryDisableAi,     "Pildeyken yapay zekayı devre dışı bırak (Otomatik Yapay Zekasız Mod)");
            Set(StringKeys.SettingsBatteryDesc,          "Etkinleştirildiğinde, pil ömrünü korumak için dizüstü bilgisayarınız pil gücüyle çalışırken uygulama otomatik olarak Yapay Zekasız moda geçer. Yapılandırılmış yapay zeka arka ucunuz korunur ve prize bağlandığında devam eder.");
            Set(StringKeys.SettingsBatteryActive,        "● Pil gücüyle çalışıyor: YZ askıya alındı");
            Set(StringKeys.SettingsBatteryAc,            "● Şebeke elektriğine bağlı: YZ devrede");
            Set(StringKeys.SettingsSecLanguage,          "🌐  Dil ve Bölge");
            Set(StringKeys.SettingsLanguageLabel,        "Arayüz Dili:");
            Set(StringKeys.SettingsLanguageDesc,         "Kerkenez Mail için arayüz dilini seçin.");
            Set(StringKeys.SettingsSecEmail,             "📬  E-posta Alma Yapılandırması");
            Set(StringKeys.SettingsMaxEmails,            "Hesap Başına Maks. E-posta:");
            Set(StringKeys.SettingsOnlyUnread,           "Yalnızca okunmamış iletileri al (Gelen Kutusundan tüm son e-postaları çekmek için işareti kaldırın)");
            Set(StringKeys.SettingsMarkAsSeen,           "E-postaları alındığında IMAP sunucusunda okundu olarak işaretle (\\Seen bayrağı)");
            Set(StringKeys.SettingsMultiSelectPreview,   "Çoklu seçimde önizlenecek e-posta (Ctrl+tıklama):");
            Set(StringKeys.SettingsMultiSelectLast,      "Son seçilen e-postayı göster (Varsayılan)");
            Set(StringKeys.SettingsMultiSelectFirst,     "İlk seçilen e-postayı göster");
            Set(StringKeys.SettingsSecAttachments,       "📁  Ek İndirmeleri");
            Set(StringKeys.SettingsDownloadPathHeader,   "Varsayılan İndirme Klasörü:");
            Set(StringKeys.SettingsDownloadPathDesc,     "E-posta eklerinin varsayılan olarak kaydedileceği konum (varsayılan olarak Windows İndirilenler klasörünüzdür).");
            Set(StringKeys.SettingsDownloadPathSelectDesc, "Varsayılan Ek İndirme Klasörünü Seçin");
            Set(StringKeys.SettingsBrowse,               "Gözat...");
            Set(StringKeys.SettingsDefault,              "Varsayılan");
            Set(StringKeys.SettingsSecUi,                "🖥️  Arayüz ve Düzen");
            Set(StringKeys.SettingsCollapseSidebar,      "Başlangıçta sol kenar çubuğunu daraltılmış olarak başlat (açılışta kompakt simge çubuğu)");
            Set(StringKeys.SettingsScalingHeader,        "Varsayılan Açılış Penceresi Ölçeklendirmesi (Ekrana Göre):");
            Set(StringKeys.SettingsScalingDesc,          "Açılışta aktif monitörün kullanılabilir masaüstü alanının (çalışma alanı) hedef oranı (Varsayılan: %60 genişlik × %56 yükseklik).");
            Set(StringKeys.SettingsWidthScale,           "Genişlik Ölçeği (%):");
            Set(StringKeys.SettingsHeightScale,          "Yükseklik Ölçeği (%):");
            Set(StringKeys.SettingsResizeActive,         "Aktif Pencereyi Boyutlandır");
            Set(StringKeys.SettingsPresetDefault,        "%60 × %56 (Varsayılan)");
            Set(StringKeys.SettingsPresetCompact,        "%50 × %50 (Kompakt)");
            Set(StringKeys.SettingsPresetLarge,          "%75 × %70 (Geniş)");
            Set(StringKeys.SettingsPresetMax,            "%95 × %90 (Neredeyse Tam)");
            Set(StringKeys.SettingsLaunchDimensions,     "Kalıcı açılış boyutları: {0} × {1} px (Ekran çalışma alanı: {2} × {3} px)");
            Set(StringKeys.SettingsAddShortcuts,         "Masaüstü ve Başlat Menüsü Kısayolları Ekle");
            Set(StringKeys.SettingsShortcutsSuccess,     "Masaüstü ve Başlat Menüsünde kısayollar başarıyla oluşturuldu!");
            Set(StringKeys.SettingsShortcutsError,       "Kısayollar oluşturulamadı.");
            Set(StringKeys.SettingsSecTray,              "🔔  Sistem Tepsisi Hizmeti ve Bildirimler");
            Set(StringKeys.SettingsAlwaysKeepOn,         "Her zaman açık tut (Sistem tepsisi hizmetini arka planda çalıştır)");
            Set(StringKeys.SettingsEnableTrayNotifs,      "Windows masaüstü bildirimlerini etkinleştir");
            Set(StringKeys.SettingsCheckInterval,        "Kontrol aralığı (dakika):");
            Set(StringKeys.SettingsStartWithWindows,     "Kullanıcı oturum açtığında sistem tepsisi hizmetini başlat");
            Set(StringKeys.SettingsRestartDaemon,        "Tepsi Hizmetini Yeniden Başlat / Başlat");
            Set(StringKeys.SettingsSecPrompt,            "✍️  Yapay Zeka Sistem İstemi Şablonu");
            Set(StringKeys.SettingsBtnSave,              "Ayarları Kaydet");
            Set(StringKeys.SettingsBtnReset,             "Varsayılanlara Sıfırla");
            Set(StringKeys.SettingsBtnTestLlm,           "LLM Bağlantısını Sına");
            Set(StringKeys.SettingsSavedToast,           "Ayarlar başarıyla kaydedildi!");
            Set(StringKeys.SettingsResetConfirm,         "Tüm ayarları varsayılanlara sıfırlamak istediğinizden emin misiniz?");

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

            // --- 9. Status Bar & Metrics ---
            Set(StringKeys.StatusSyncComplete,           "Senkronizasyon tamamlandı");
            Set(StringKeys.StatusReady,                  "Hazır");
            Set(StringKeys.StatusReadyEmails,            "Hazır • Gelen kutusunda {0} e-posta ({1} okunmamış)");
            Set(StringKeys.StatusReadyFolder,            "Hazır • {1} klasöründe {0} e-posta");
            Set(StringKeys.StatusAccountsCount,          "Hesaplar: {0} | VRAM: {1}");
            Set(StringKeys.StatusAccountsBackend,        "Hesaplar: {0} | Arka Uç: {1}");
            Set(StringKeys.StatusReadyBackend,           "Hazır • Arka Uç: {0}");
            Set(StringKeys.StatusVramFree,               "VRAM Boş");
            Set(StringKeys.StatusModelLoaded,            "Model VRAM'e Yüklendi");
            Set(StringKeys.StatusOllamaActive,           "Ollama Aktif");
            Set(StringKeys.StatusCloudActive,            "Bulut Aktif");
            Set(StringKeys.StatusAiDisabled,             "Yapay Zeka Devre Dışı");
            Set(StringKeys.StatusBatterySaverNoAi,       "Pil Tasarrufu (Yapay Zekasız)");
            Set(StringKeys.StatusOnDemandVram,           "İsteğe Bağlı (VRAM Boşaltma)");
            Set(StringKeys.StatusSummaryReady,           "Özet hazır");
            Set(StringKeys.StatusSyncingFolder,          "{0} eşitleniyor...");
            Set(StringKeys.StatusNoAccounts,             "Yapılandırılmış hesap yok");
            Set(StringKeys.StatusLiveConnecting,         "Canlı IMAP: Bağlanıyor...");
            Set(StringKeys.StatusLiveListening,          "Canlı IMAP: Bağlandı ve Dinleniyor");
            Set(StringKeys.StatusLiveStopped,            "Canlı IMAP: Durduruldu");
            Set(StringKeys.StatusLiveDone,               "Canlı IMAP: DONE sinyalleri iletiliyor...");
            Set(StringKeys.StatusLiveNewEmail,           "Canlı IMAP: {0} için yeni e-posta geldi");
            Set(StringKeys.StatusStartingUp,             "Başlatılıyor...");
            Set(StringKeys.StatusDisabledClassic,        "Devre Dışı (Klasik Posta)");
        }
    }
}
