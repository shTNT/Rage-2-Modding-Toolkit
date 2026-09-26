// CrashHandler.cs - captura global de excepciones no controladas.
// Muestra dialog con stack trace copiable + boton "Open issue on GitHub".
// Todo cliente-side, sin telemetria automatica.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    internal static class CrashHandler
    {
        const string IssuesUrl = "https://github.com/shTNT/Rage-2-Modding-Toolkit/issues/new";

        public static void Install()
        {
            Application.ThreadException += (s, e) => Handle(e.Exception, "UI");
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Handle(ex ?? new Exception("Unknown exception (not Exception type)"), "Unhandled");
            };
        }

        public static void Handle(Exception ex, string source)
        {
            try
            {
                string report = BuildReport(ex, source);
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "last_crash.log"), report, Encoding.UTF8); } catch { }

                ShowDialog(report);
            }
            catch
            {
                // Si el propio handler falla, al menos intentamos un MessageBox minimo.
                try { MessageBox.Show(ex?.ToString() ?? "Unknown error", "RAGE 2 Toolkit - crash", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
            }
        }

        static string BuildReport(Exception ex, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RAGE 2 Modding Toolkit - crash report");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine("Source: " + source);
            sb.AppendLine("Toolkit version: " + (AppInfo.Display ?? "unknown"));
            sb.AppendLine("OS: " + Environment.OSVersion);
            sb.AppendLine(".NET: " + Environment.Version);
            sb.AppendLine("64-bit: " + Environment.Is64BitProcess);
            sb.AppendLine("Timestamp (UTC): " + DateTime.UtcNow.ToString("o"));
            sb.AppendLine();
            sb.AppendLine("Exception type: " + (ex?.GetType().FullName ?? "null"));
            sb.AppendLine("Message: " + (ex?.Message ?? "null"));
            sb.AppendLine();
            sb.AppendLine("Stack trace:");
            sb.AppendLine(ex?.StackTrace ?? "(no stack)");
            if (ex?.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("Inner exception: " + ex.InnerException.GetType().FullName);
                sb.AppendLine("Inner message: " + ex.InnerException.Message);
                sb.AppendLine("Inner stack trace:");
                sb.AppendLine(ex.InnerException.StackTrace);
            }
            return sb.ToString();
        }

        static void ShowDialog(string report)
        {
            Color C_BG      = Color.FromArgb(24, 24, 30);
            Color C_PANEL   = Color.FromArgb(30, 30, 38);
            Color C_HEAD    = Color.FromArgb(15, 15, 20);
            Color C_INFO    = Color.FromArgb(0, 200, 200);
            Color C_ERR     = Color.FromArgb(240, 80, 80);

            using (var f = new Form())
            {
                f.Text = "RAGE 2 Modding Toolkit - crash";
                f.BackColor = C_BG;
                f.ForeColor = Color.White;
                f.Size = new Size(820, 620);
                f.StartPosition = FormStartPosition.CenterScreen;
                f.MinimumSize = new Size(600, 400);
                f.Font = new Font("Segoe UI", 9F);

                var title = new Label
                {
                    Text = "Ups. The toolkit has crashed.",
                    Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                    ForeColor = C_ERR,
                    Location = new Point(20, 15),
                    AutoSize = true
                };
                f.Controls.Add(title);

                var sub = new Label
                {
                    Text = "Nothing has been modified in your game. The .original backup (if any) is intact.\r\n" +
                           "Please copy the report below and open an issue on GitHub.",
                    ForeColor = Color.FromArgb(200, 200, 200),
                    Location = new Point(20, 50),
                    Size = new Size(780, 45)
                };
                f.Controls.Add(sub);

                var txt = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Both,
                    WordWrap = false,
                    ReadOnly = true,
                    BackColor = C_HEAD,
                    ForeColor = Color.FromArgb(220, 220, 220),
                    Font = new Font("Consolas", 9F),
                    Location = new Point(20, 105),
                    Size = new Size(780, 420),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                    Text = report
                };
                f.Controls.Add(txt);

                var btnCopy = new Button
                {
                    Text = "Copy report",
                    Location = new Point(20, 540),
                    Size = new Size(150, 36),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                    BackColor = C_PANEL,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnCopy.FlatAppearance.BorderColor = C_INFO;
                btnCopy.Click += (s, e) =>
                {
                    try { Clipboard.SetText(report); MessageBox.Show("Report copied. Now open the GitHub issue and paste (Ctrl+V).", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                    catch { }
                };
                f.Controls.Add(btnCopy);

                var btnIssue = new Button
                {
                    Text = "Open issue on GitHub",
                    Location = new Point(180, 540),
                    Size = new Size(200, 36),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
                    BackColor = C_PANEL,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnIssue.FlatAppearance.BorderColor = C_INFO;
                btnIssue.Click += (s, e) =>
                {
                    try
                    {
                        string title = Uri.EscapeDataString("Crash: " + FirstLine(report));
                        string body = Uri.EscapeDataString(TruncateForUrl(report, 3800));
                        string url = IssuesUrl + "?title=" + title + "&body=" + body;
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    catch (Exception ex) { MessageBox.Show("Could not open browser: " + ex.Message); }
                };
                f.Controls.Add(btnIssue);

                var btnClose = new Button
                {
                    Text = "Close",
                    Location = new Point(680, 540),
                    Size = new Size(120, 36),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    BackColor = C_PANEL,
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btnClose.FlatAppearance.BorderColor = Color.Gray;
                btnClose.Click += (s, e) => f.Close();
                f.Controls.Add(btnClose);

                f.ShowDialog();
            }
        }

        static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            int nl = s.IndexOf('\n');
            return nl > 0 ? s.Substring(0, nl) : s;
        }

        static string TruncateForUrl(string s, int max)
        {
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "\r\n\r\n[...] truncated. Full report copied to clipboard.";
        }
    }
}
