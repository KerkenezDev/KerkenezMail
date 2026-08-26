using System;
using System.Linq;
using System.Windows.Forms;
using EmailSummarizer.Services;
using EmailSummarizer.UI;

namespace EmailSummarizer
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Handle --uninstall command-line switch
            if (args != null && args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("/uninstall", StringComparison.OrdinalIgnoreCase) ||
                                              a.Equals("-uninstall", StringComparison.OrdinalIgnoreCase)))
            {
                var res = MessageBox.Show(
                    "Are you sure you want to uninstall Email Summarizer and delete all configuration from %APPDATA%\\EmailSummarizer?",
                    "Uninstall Email Summarizer",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (res == DialogResult.Yes)
                {
                    bool success = ConfigService.Uninstall();
                    if (success)
                    {
                        MessageBox.Show(
                            "Email Summarizer configuration and data have been successfully removed from %APPDATA%.",
                            "Uninstall Complete",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show(
                            "Failed to completely remove %APPDATA%\\EmailSummarizer folder.",
                            "Uninstall Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
                return;
            }

            Application.Run(new MainForm());
        }
    }
}