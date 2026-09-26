using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    // ============================================================
    // Updater - chequeo de releases en GitHub
    // ============================================================
    static class Updater
    {
        public const string RepoOwner = "shTNT";
        public const string RepoName  = "Rage-2-Modding-Toolkit";

        public static string CurrentVersion
        {
            get
            {
                try
                {
                    Version v = Assembly.GetExecutingAssembly().GetName().Version;
                    if (v == null) return "0.0.0";
                    return v.Major + "." + v.Minor + "." + v.Build;
                }
                catch { return "0.0.0"; }
            }
        }

        public static string CurrentVersionDisplay
        {
            get { return "v" + CurrentVersion; }
        }

        // Lanza el chequeo en background. status() se llama desde el hilo del Timer de WinForms,
        // asi que el caller debe hacer Invoke si toca UI.
        public static void CheckAsync(Action<string> status, bool showPopup = false)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                string msg;
                try { msg = DoCheck(out bool hasNew, out string tag, out string url); if (hasNew && showPopup) OfferDownload(tag, url); }
                catch (Exception ex) { msg = "Autoupdate failed: " + ex.Message; }
                try { status?.Invoke(msg); } catch { }
            });
        }

        static string DoCheck(out bool hasNew, out string newTag, out string releaseUrl)
        {
            hasNew = false; newTag = null; releaseUrl = null;

            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }

            string api = "https://api.github.com/repos/" + RepoOwner + "/" + RepoName + "/releases/latest";
            using (WebClient wc = new WebClient())
            {
                wc.Headers["User-Agent"] = "RAGE2Toolkit/" + CurrentVersion;
                wc.Headers["Accept"]     = "application/vnd.github+json";
                wc.Encoding = System.Text.Encoding.UTF8;
                string json = wc.DownloadString(api);

                string tag = ExtractString(json, "tag_name");
                string url = ExtractString(json, "html_url");
                if (string.IsNullOrEmpty(tag)) return "Autoupdate: no tag_name in response";

                string clean = tag.TrimStart('v', 'V');
                newTag = tag;
                releaseUrl = url;

                if (clean == CurrentVersion)
                    return "Autoupdate: up to date (" + CurrentVersionDisplay + ")";

                hasNew = true;
                return "Autoupdate: new version available -> " + tag;
            }
        }

        static void OfferDownload(string tag, string url)
        {
            DialogResult r = MessageBox.Show(
                "New version available on GitHub.\n\n" +
                "  Latest : " + tag + "\n" +
                "  Yours  : " + CurrentVersionDisplay + "\n\n" +
                "Open the release page in your browser?",
                "Update available",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes && !string.IsNullOrEmpty(url))
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }
        }

        // Extrae el valor de "key":"value" sin dependencias JSON. Robusto contra escapes simples.
        static string ExtractString(string json, string key)
        {
            string pat = "\"" + key + "\"";
            int i = json.IndexOf(pat, StringComparison.Ordinal);
            if (i < 0) return null;
            i = json.IndexOf(':', i);
            if (i < 0) return null;
            i++;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            if (i >= json.Length || json[i] != '"') return null;
            i++;
            int j = i;
            while (j < json.Length && json[j] != '"')
            {
                if (json[j] == '\\') j++;
                j++;
            }
            return json.Substring(i, j - i);
        }
    }
}