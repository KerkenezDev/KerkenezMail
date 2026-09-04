using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using KerkenezMail.Languages;
using KerkenezMail.Models;
using KerkenezMail.Services;
using KerkenezMail.UI.Tabs;

namespace KerkenezMail.UI
{
    public class SendMailForm : Form
    {
        public SendMailView MailView { get; }

        public SendMailForm(ConfigService configService, SmtpService? smtpService = null, EmailItem? replyEmail = null, EmailAccount? senderAccount = null)
        {
            this.Text = replyEmail != null ? $"{Lang.T(StringKeys.InboxBtnReply)}: {replyEmail.Subject}" : Lang.T(StringKeys.SendTitle);
            this.Size = new Size(820, 640);
            this.MinimumSize = new Size(580, 440);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            this.BackColor = Color.FromArgb(248, 249, 250);

            try
            {
                using var stream = typeof(MainForm).Assembly.GetManifestResourceStream("KerkenezMail.app.ico");
                if (stream != null) this.Icon = new Icon(stream);
                else if (File.Exists("app.ico")) this.Icon = new Icon("app.ico");
            }
            catch { }

            MailView = new SendMailView(configService, smtpService)
            {
                Dock = DockStyle.Fill
            };

            MailView.BackToInboxRequested += (s, e) => this.Close();
            MailView.EmailSentSuccessfully += (s, e) => this.Close();

            this.Controls.Add(MailView);

            if (replyEmail != null)
            {
                MailView.SetReplyEmail(replyEmail, senderAccount);
            }
            else
            {
                MailView.SetNewEmail(senderAccount);
            }
        }
    }
}
