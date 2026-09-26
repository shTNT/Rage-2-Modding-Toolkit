using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    // ============================================================
    // AppInfo - version y icono dinamicos
    // ============================================================
    static class AppInfo
    {
        public static string Version
        {
            get
            {
                try
                {
                    System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
                    if (v == null) return "0.0.0";
                    return v.Major + "." + v.Minor + "." + v.Build;
                }
                catch { return "0.0.0"; }
            }
        }

        public static string Display { get { return "v" + Version; } }

        static Icon _icon;
        public static Icon GetIcon()
        {
            if (_icon != null) return _icon;
            try
            {
                string exe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    Icon extracted = Icon.ExtractAssociatedIcon(exe);
                    if (extracted != null) { _icon = extracted; return _icon; }
                }
            }
            catch { }
            _icon = SystemIcons.Application;
            return _icon;
        }

        public static void ApplyTo(Form f)
        {
            try { f.Icon = GetIcon(); } catch { }
        }
    }

    // ============================================================
    // Paths - rutas resueltas contra el exe
    // ============================================================
    static class Paths
    {
        public static readonly string ExeDir;
        public static readonly string BinDir;
        public static readonly string DataDir;
        public static readonly string ConfigDir;
        public static readonly string ConfigFile;
        public static readonly string DefaultOutputDir;

        static Paths()
        {
            string dir = null;
            try { dir = Path.GetDirectoryName(Application.ExecutablePath); } catch { }
            if (string.IsNullOrEmpty(dir))
            {
                try { dir = AppContext.BaseDirectory; } catch { }
            }
            if (string.IsNullOrEmpty(dir)) dir = Environment.CurrentDirectory;
            dir = dir.TrimEnd('\\', '/');

            ExeDir = dir;
            BinDir = Path.Combine(dir, "bin");
            DataDir = Path.Combine(dir, "data");
            ConfigDir = Path.Combine(dir, "config");
            ConfigFile = Path.Combine(ConfigDir, "paths.txt");
            DefaultOutputDir = Path.Combine(dir, "RAGE2Toolkit_Output");
        }

        public static void EnsureAll()
        {
            try { Directory.CreateDirectory(BinDir); } catch { }
            try { Directory.CreateDirectory(DataDir); } catch { }
            try { Directory.CreateDirectory(ConfigDir); } catch { }
        }

        public static string OodleDllPath { get { return Path.Combine(BinDir, "oo2core_7_win64.dll"); } }

        public static bool OodlePresent { get { try { return File.Exists(OodleDllPath); } catch { return false; } } }
    }

    // ============================================================
    // Abbreviate - acortar rutas largas para la UI
    // ============================================================
    static class Abbreviate
    {
        public static string ShortPath(string p, int maxLen = 60)
        {
            if (string.IsNullOrEmpty(p)) return p;
            if (maxLen < 8) maxLen = 8;
            if (p.Length <= maxLen) return p;
            return p.Substring(0, maxLen - 3) + "...";
        }
    }

    // ============================================================
    // Oodle - carga dinamica desde el bin/ local
    // ============================================================
    static class Oodle
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate long DecompressFn(
            byte[] compBuf, long compSize, byte[] rawBuf, long rawSize,
            int fuzzSafe, int checkCRC, int verbosity,
            IntPtr decBufBase, long decBufSize,
            IntPtr fpCallback, IntPtr callbackUserData,
            IntPtr decoderMemory, long decoderMemorySize, int threadPhase);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate long GetCompressedBufferSizeNeededFn(long rawSize);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate long CompressFn(
            int compressor, byte[] raw, long rawLen, byte[] comp, int level,
            IntPtr opts, IntPtr dict, long dictSz, IntPtr decBase, long decSz, long decLen);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate IntPtr CompressOptions_GetDefaultFn(int compressor, int level);

        static IntPtr hLib = IntPtr.Zero;
        static DecompressFn fnDecompress;
        static CompressFn fnCompress;
        static GetCompressedBufferSizeNeededFn fnNeeded;
        static CompressOptions_GetDefaultFn fnOptsDefault;
        static string lastError = "";

        public static bool IsLoaded { get { return fnDecompress != null; } }
        public static string LastError { get { return lastError; } }

        public static bool TryLoad(string dllPath)
        {
            if (fnDecompress != null) return true;
            lastError = "";
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                lastError = "no existe: " + dllPath;
                return false;
            }
            try
            {
                if (hLib != IntPtr.Zero) { try { FreeLibrary(hLib); } catch { } hLib = IntPtr.Zero; }

                hLib = LoadLibraryW(dllPath);
                if (hLib == IntPtr.Zero)
                {
                    lastError = "LoadLibraryW fallo (err " + Marshal.GetLastWin32Error() + ")";
                    return false;
                }

                IntPtr pDec = GetProcAddress(hLib, "OodleLZ_Decompress");
                if (pDec == IntPtr.Zero)
                {
                    lastError = "OodleLZ_Decompress no exportada";
                    try { FreeLibrary(hLib); } catch { } hLib = IntPtr.Zero;
                    return false;
                }
                fnDecompress = (DecompressFn)Marshal.GetDelegateForFunctionPointer(pDec, typeof(DecompressFn));

                IntPtr pCmp = GetProcAddress(hLib, "OodleLZ_Compress");
                if (pCmp != IntPtr.Zero)
                    fnCompress = (CompressFn)Marshal.GetDelegateForFunctionPointer(pCmp, typeof(CompressFn));

                IntPtr pN = GetProcAddress(hLib, "OodleLZ_GetCompressedBufferSizeNeeded");
                if (pN != IntPtr.Zero)
                    fnNeeded = (GetCompressedBufferSizeNeededFn)Marshal.GetDelegateForFunctionPointer(pN, typeof(GetCompressedBufferSizeNeededFn));

                IntPtr pO = GetProcAddress(hLib, "OodleLZ_CompressOptions_GetDefault");
                if (pO != IntPtr.Zero)
                    fnOptsDefault = (CompressOptions_GetDefaultFn)Marshal.GetDelegateForFunctionPointer(pO, typeof(CompressOptions_GetDefaultFn));

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                return false;
            }
        }

        public static bool Decompress(byte[] comp, int compOff, int compLen, byte[] dst, int dstOff, int dstLen)
        {
            if (fnDecompress == null) throw new Exception("Oodle no cargado");
            if (comp == null || dst == null) return false;
            if (compOff < 0 || compLen < 0 || compOff + compLen > comp.Length) return false;
            if (dstOff < 0 || dstLen < 0 || dstOff + dstLen > dst.Length) return false;
            if (compOff == 0 && dstOff == 0 && comp.Length == compLen && dst.Length == dstLen) {
                long n = fnDecompress(comp, compLen, dst, dstLen, 0, 0, 0,
                    IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
                return n > 0;
            }
            byte[] cLocal = new byte[compLen];
            Array.Copy(comp, compOff, cLocal, 0, compLen);
            byte[] dLocal = new byte[dstLen];
            long r = fnDecompress(cLocal, compLen, dLocal, dstLen, 0, 0, 0,
                IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
            if (r <= 0) return false;
            Array.Copy(dLocal, 0, dst, dstOff, dstLen);
            return true;
        }

        public static byte[] Decompress(byte[] comp, int usz)
        {
            byte[] r = new byte[usz];
            if (!Decompress(comp, 0, comp.Length, r, 0, usz)) throw new Exception("Oodle decompress error");
            return r;
        }
        public const int KRAKEN = 8;

        public static byte[] Compress(byte[] raw, int compressor, int level)
        {
            if (fnCompress == null || fnNeeded == null) throw new Exception("Oodle compress no disponible");
            long needed = fnNeeded(raw.Length);
            if (needed < raw.Length + 65536) needed = raw.Length + 65536;
            byte[] comp = new byte[needed];
            IntPtr opts = fnOptsDefault != null ? fnOptsDefault(compressor, level) : IntPtr.Zero;
            long n = fnCompress(compressor, raw, raw.Length, comp, level,
                opts, IntPtr.Zero, 0, IntPtr.Zero, 0, 0);
            if (n <= 0) throw new Exception("Oodle compress error: " + n);
            Array.Resize(ref comp, (int)n);
            return comp;
        }
    }

    // ============================================================
    // Extractor - descomprime .tab + .arc
    // ============================================================
    static class Extractor
    {
        internal static string DetectExt(byte[] d)
        {
            if (d.Length < 4) return "unknown";
            string m4 = Encoding.ASCII.GetString(d, 0, 4);
            string m8 = d.Length >= 8 ? Encoding.ASCII.GetString(d, 0, 8) : "";
            string m16 = d.Length >= 16 ? Encoding.ASCII.GetString(d, 0, 16) : "";
            if (m4 == "OggS") return "ogg";
            if (m4 == "RIFF") return "riff";
            if (m4 == "DDS ") return "dds";
            if (m4 == "AVTX") return "avtx";
            if (m8.StartsWith("FSB5")) return "fsb";
            if (m4 == "SARC") return "sarc";
            if (m4 == "RTPC") return "rtpc";
            if (d.Length >= 8)
            {
                string tagAt4 = Encoding.ASCII.GetString(d, 4, 4);
                if (tagAt4 == "TAG0") return "tag";
            }
            if (m8.StartsWith("TAG0")) return "tag";
            if (m16.StartsWith("CFX ")) return "cfx";
            if (m4.StartsWith("BIK")) return "bik";
            if (m4.StartsWith("KB2")) return "bk2";
            if (d[0] == 0x20 && d[1] == 0x46 && d[2] == 0x44 && d[3] == 0x41) return "adf";
            if (d[0] == 0x04 && d[1] == 0x00 && d[2] == 0x00 && d[3] == 0x00) return "bin";
            return "unknown";
        }

        
        public static void ExtractAll(string gamePath, string outputBase, Action<string> log)
        {
            ExtractAll(gamePath, outputBase, log, null, null, true);
        }

        public static ExtractorOpt.Stats ExtractAll(string gamePath, string outputBase, Action<string> log,
            Action<int,int,string,int> onTabStart, Action<int,int,string,int,int> onTabDone, bool skipLanguages)
        {
            if (!Oodle.IsLoaded) throw new Exception("Oodle no cargado. Configura el juego primero.");
            return ExtractorOpt.Extract(gamePath, outputBase, skipLanguages,
                delegate(byte[] c, int co, int cl, byte[] d, int doff, int dl) { return Oodle.Decompress(c, co, cl, d, doff, dl); },
                onTabStart, onTabDone, log);
        }    }

    // ============================================================
    // UiHover - animacion suave al pasar el cursor por un boton
    // ============================================================
    static class UiHover
    {
        public static void Attach(Button b, Color normal, Color hover, int durationMs = 160)
        {
            if (b == null) return;
            b.BackColor = normal;

            Color from = normal, to = normal;
            DateTime t0 = DateTime.MinValue;
            Timer t = new Timer();
            t.Interval = 12;

            t.Tick += (s, e) =>
            {
                double k = durationMs > 0
                    ? Math.Min(1.0, (DateTime.Now - t0).TotalMilliseconds / durationMs)
                    : 1.0;
                int R = (int)(from.R + (to.R - from.R) * k);
                int G = (int)(from.G + (to.G - from.G) * k);
                int B = (int)(from.B + (to.B - from.B) * k);
                try { b.BackColor = Color.FromArgb(R, G, B); } catch { }
                if (k >= 1.0) t.Stop();
            };

            b.MouseEnter += (s, e) => { from = b.BackColor; to = hover; t0 = DateTime.Now; if (!t.Enabled) t.Start(); };
            b.MouseLeave += (s, e) => { from = b.BackColor; to = normal; t0 = DateTime.Now; if (!t.Enabled) t.Start(); };
            b.Disposed += (s, e) => { try { t.Stop(); t.Dispose(); } catch { } };
        }
    }

    // ============================================================
    // MainForm - herramientas avanzadas (extract, convert, classify, deploy)
    // ============================================================
    public class MainForm : Form
    {
        readonly Color C_BG      = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL   = Color.FromArgb(30, 30, 38);
        readonly Color C_LOG     = Color.FromArgb(10, 10, 15);
        readonly Color C_HEAD    = Color.FromArgb(15, 15, 20);
        readonly Color C_INFO    = Color.FromArgb(0, 200, 200);
        readonly Color C_OK      = Color.FromArgb(100, 220, 100);
        readonly Color C_WARN    = Color.FromArgb(230, 180, 74);
        readonly Color C_ERR     = Color.FromArgb(240, 100, 100);
        readonly Color C_GRAY    = Color.FromArgb(160, 160, 160);
        readonly Color C_TEXT    = Color.FromArgb(200, 200, 200);
        readonly Color C_BTN     = Color.FromArgb(50, 50, 60);
        readonly Color C_HOVER   = Color.FromArgb(70, 70, 90);
        readonly Color C_RED     = Color.FromArgb(140, 50, 50);
        readonly Color C_SECTION = Color.FromArgb(220, 80, 220);

        RichTextBox logBox;
        ToolStripStatusLabel statusGame, statusOodle, statusOutput;
        ProgressBar progress;
        List<Button> allButtons = new List<Button>();
        string gamePath = "";
        string outputPath = "";
        bool running = false;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public MainForm()
        {
            this.Text = "RAGE 2 Modding Toolkit " + AppInfo.Display + "  -  Advanced";
            this.ClientSize = new Size(1400, 900);
            this.MinimumSize = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            AppInfo.ApplyTo(this);

            this.HandleCreated += (s, e) =>
            {
                try
                {
                    int v = 1;
                    DwmSetWindowAttribute(this.Handle, 20, ref v, 4);
                    DwmSetWindowAttribute(this.Handle, 19, ref v, 4);
                }
                catch { }
            };

            LoadConfig();
            BuildUI();
            UpdateStatus();

            Log("RAGE 2 Modding Toolkit " + AppInfo.Display + "  -  by Kry0genik", C_INFO);
            Log("Ready. Follow the steps:", C_GRAY);
            Log("  1. Configure game path (auto-copies Oodle DLL)", C_GRAY);
            Log("  2. Configure output path", C_GRAY);
            Log("  3. Extract all .arc files", C_GRAY);
            Log("  4. Convert .avtx to .dds", C_GRAY);
            Log("  5. Classify files by type", C_GRAY);
            Log("  6. Modify with external tools", C_GRAY);
            Log("  7. Deploy to dropzone", C_GRAY);
        }

        void BuildUI()
        {
            int headerH = 90;
            int sidebarW = 320;

            Panel header = new Panel();
            header.Location = new Point(0, 0);
            header.Size = new Size(this.ClientSize.Width, headerH);
            header.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.BackColor = C_HEAD;
            this.Controls.Add(header);

            Label titleLbl = new Label();
            titleLbl.Text = "RAGE 2 MODDING TOOLKIT";
            titleLbl.Font = new Font("Segoe UI", 20, FontStyle.Bold);
            titleLbl.ForeColor = C_INFO;
            titleLbl.Location = new Point(25, 15);
            titleLbl.AutoSize = true;
            header.Controls.Add(titleLbl);

            Label subLbl = new Label();
            subLbl.Text = "Extract  .  Convert  .  Organize  .  Deploy  .  Mod";
            subLbl.Font = new Font("Segoe UI", 10);
            subLbl.ForeColor = C_GRAY;
            subLbl.Location = new Point(28, 56);
            subLbl.AutoSize = true;
            header.Controls.Add(subLbl);

            Label verLbl = new Label();
            verLbl.Text = AppInfo.Display;
            verLbl.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            verLbl.ForeColor = Color.FromArgb(220, 200, 100);
            verLbl.Location = new Point(this.ClientSize.Width - 80, 34);
            verLbl.AutoSize = true;
            verLbl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(verLbl);

            StatusStrip status = new StatusStrip();
            status.BackColor = C_HEAD;
            status.ForeColor = Color.White;
            status.SizingGrip = false;
            statusGame = new ToolStripStatusLabel("Game: (not configured)");
            statusGame.ForeColor = C_ERR;
            statusOodle = new ToolStripStatusLabel("  |  Oodle: MISSING");
            statusOodle.ForeColor = C_ERR;
            statusOutput = new ToolStripStatusLabel("  |  Output: (not configured)");
            statusOutput.ForeColor = C_ERR;
            status.Items.Add(statusGame);
            status.Items.Add(statusOodle);
            status.Items.Add(statusOutput);
            this.Controls.Add(status);

            Panel sidebar = new Panel();
            sidebar.Location = new Point(0, headerH);
            sidebar.Size = new Size(sidebarW, this.ClientSize.Height - headerH - 22);
            sidebar.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            sidebar.BackColor = C_PANEL;
            sidebar.AutoScroll = true;
            this.Controls.Add(sidebar);

            int y = 15;
            AddSection(sidebar, "EXTRACTION", ref y);
            AddButton(sidebar, "  Extract All .arc files", C_BTN, ref y, () => DoExtractAll());

            AddSection(sidebar, "TEXTURES", ref y);
            AddButton(sidebar, "  Convert .avtx to .dds", C_BTN, ref y, () => DoConvertAll());
            AddButton(sidebar, "  Texture summary", C_BTN, ref y, () => DoSummary());
            AddButton(sidebar, "  Validate files", C_BTN, ref y, () => DoValidate());

            AddSection(sidebar, "ORGANIZE", ref y);
            AddButton(sidebar, "  Classify all files", C_BTN, ref y, () => DoClassify());
            AddButton(sidebar, "  Full file summary", C_BTN, ref y, () => DoSummary());

            AddSection(sidebar, "REPACK .ARC", ref y);
            AddButton(sidebar, "  Modificar texturas (.arc)", C_INFO, ref y,
                () => { new RepackForm(this, gamePath, outputPath).ShowDialog(this); });

            AddSection(sidebar, "MOD DEPLOYMENT", ref y);
            AddButton(sidebar, "  Deploy to dropzone", C_RED, ref y, () => DoDeploy());

            AddSection(sidebar, "SETTINGS", ref y);
            AddButton(sidebar, "  Configure game path", C_BTN, ref y, () => DoConfigureGame());
            AddButton(sidebar, "  Configure output path", C_BTN, ref y, () => DoConfigureOutput());
            AddButton(sidebar, "  Open output folder", C_BTN, ref y, () => DoOpenOutput());
            AddButton(sidebar, "  Exit", C_RED, ref y, () => this.Close());

            Panel logPanel = new Panel();
            logPanel.Location = new Point(sidebarW, headerH);
            logPanel.Size = new Size(this.ClientSize.Width - sidebarW, this.ClientSize.Height - headerH - 22);
            logPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            logPanel.BackColor = Color.FromArgb(20, 20, 26);
            this.Controls.Add(logPanel);

            Label logTitle = new Label();
            logTitle.Text = "  CONSOLE OUTPUT";
            logTitle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            logTitle.ForeColor = C_INFO;
            logTitle.Dock = DockStyle.Top;
            logTitle.Height = 30;
            logTitle.TextAlign = ContentAlignment.MiddleLeft;
            logTitle.BackColor = C_HEAD;
            logPanel.Controls.Add(logTitle);

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.BackColor = C_LOG;
            logBox.ForeColor = C_TEXT;
            logBox.Font = new Font("Consolas", 10);
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.None;
            logBox.WordWrap = false;
            logBox.ScrollBars = RichTextBoxScrollBars.Both;
            logBox.DetectUrls = false;
            logPanel.Controls.Add(logBox);
            logBox.BringToFront();

            progress = new ProgressBar();
            progress.Location = new Point(sidebarW, this.ClientSize.Height - 30);
            progress.Size = new Size(this.ClientSize.Width - sidebarW, 8);
            progress.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            progress.Style = ProgressBarStyle.Continuous;
            this.Controls.Add(progress);
        }

        void AddSection(Panel parent, string text, ref int y)
        {
            Label lbl = new Label();
            lbl.Text = "  " + text;
            lbl.ForeColor = C_SECTION;
            lbl.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            lbl.Location = new Point(8, y);
            lbl.Size = new Size(300, 26);
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            parent.Controls.Add(lbl);
            y += 30;
        }

        void AddButton(Panel parent, string text, Color color, ref int y, Action onClick)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Size = new Size(300, 42);
            btn.Location = new Point(8, y);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = color;
            btn.ForeColor = Color.White;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(15, 0, 0, 0);
            btn.Font = new Font("Segoe UI", 10);
            btn.Cursor = Cursors.Hand;

            UiHover.Attach(btn, color, C_HOVER);
            btn.Click += (s, e) => { if (!running) onClick(); };

            parent.Controls.Add(btn);
            allButtons.Add(btn);
            y += 46;
        }

        void Log(string msg, Color color)
        {
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg, color))); return; }
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = color;
            logBox.AppendText(msg + Environment.NewLine);
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
        }
        void Log(string msg) { Log(msg, C_TEXT); }

        void LogSection(string title)
        {
            Log("");
            Log(new string('-', 70), C_INFO);
            Log("  " + title, C_INFO);
            Log(new string('-', 70), C_INFO);
        }

        void LoadConfig()
        {
            try
            {
                if (!File.Exists(Paths.ConfigFile)) return;
                string[] lines = File.ReadAllLines(Paths.ConfigFile);
                if (lines.Length > 0) gamePath = lines[0] ?? "";
                if (lines.Length > 1) outputPath = lines[1] ?? "";
            }
            catch { }
        }

        void SaveConfig()
        {
            try { File.WriteAllLines(Paths.ConfigFile, new[] { gamePath ?? "", outputPath ?? "" }); } catch { }
        }

        void UpdateStatus()
        {
            if (gamePath.Length > 0 && Directory.Exists(gamePath))
            {
                statusGame.Text = "Game: " + Abbreviate.ShortPath(gamePath, 40);
                statusGame.ForeColor = C_OK;
            }
            else
            {
                statusGame.Text = "Game: (not configured)";
                statusGame.ForeColor = C_ERR;
            }

            if (Oodle.IsLoaded)
            {
                statusOodle.Text = "  |  Oodle: loaded";
                statusOodle.ForeColor = C_OK;
            }
            else if (Paths.OodlePresent)
            {
                string loaded = Oodle.TryLoad(Paths.OodleDllPath) ? "loaded" : "error";
                if (Oodle.IsLoaded)
                {
                    statusOodle.Text = "  |  Oodle: loaded";
                    statusOodle.ForeColor = C_OK;
                }
                else
                {
                    statusOodle.Text = "  |  Oodle: " + loaded;
                    statusOodle.ForeColor = C_ERR;
                }
            }
            else
            {
                statusOodle.Text = "  |  Oodle: MISSING";
                statusOodle.ForeColor = C_ERR;
            }

            if (outputPath.Length > 0 && Directory.Exists(outputPath))
            {
                statusOutput.Text = "  |  Output: " + Abbreviate.ShortPath(outputPath, 40);
                statusOutput.ForeColor = C_OK;
            }
            else
            {
                statusOutput.Text = "  |  Output: (not configured)";
                statusOutput.ForeColor = C_ERR;
            }
        }

        void SetButtonsEnabled(bool e) { foreach (Button b in allButtons) b.Enabled = e; }

        void DoConfigureGame()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Select RAGE2.exe (the game executable)";
                dlg.Filter = "RAGE 2 executable|RAGE2.exe|Executables|*.exe|All files|*.*";
                dlg.CheckFileExists = true;
                dlg.CheckPathExists = true;
                if (gamePath.Length > 0 && Directory.Exists(gamePath))
                {
                    string cand = Path.Combine(gamePath, "RAGE2.exe");
                    if (File.Exists(cand)) dlg.InitialDirectory = gamePath;
                }
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string sel = dlg.FileName;
                if (!string.Equals(Path.GetFileName(sel), "RAGE2.exe", StringComparison.OrdinalIgnoreCase))
                {
                    LogSection("CONFIGURE GAME PATH");
                    Log("[!!] Selected file is not RAGE2.exe", C_ERR);
                    return;
                }

                string dir = Path.GetDirectoryName(sel);
                LogSection("CONFIGURE GAME PATH");
                gamePath = dir;
                SaveConfig();
                Log("[OK] Game path: " + gamePath, C_OK);

                string target = Paths.OodleDllPath;
                if (File.Exists(target))
                {
                    Log("[--] Oodle DLL already cached", C_GRAY);
                    if (!Oodle.IsLoaded)
                    {
                        if (Oodle.TryLoad(target)) Log("[OK] Oodle loaded from bin/", C_OK);
                        else Log("[!!] Oodle load failed: " + Oodle.LastError, C_ERR);
                    }
                }
                else
                {
                    string direct = Path.Combine(gamePath, "oo2core_7_win64.dll");
                    string found = File.Exists(direct) ? direct : null;
                    if (found == null)
                    {
                        Log("[..] Searching for oo2core_7_win64.dll...", C_WARN);
                        try
                        {
                            string[] r = Directory.GetFiles(gamePath, "oo2core_7_win64.dll", SearchOption.AllDirectories);
                            if (r.Length > 0) found = r[0];
                        }
                        catch { }
                    }
                    if (found != null)
                    {
                        try
                        {
                            Directory.CreateDirectory(Paths.BinDir);
                            File.Copy(found, target, true);
                            Log("[OK] Copied Oodle DLL from: " + found, C_OK);
                            if (Oodle.TryLoad(target)) Log("[OK] Oodle loaded", C_OK);
                            else Log("[!!] Oodle load failed: " + Oodle.LastError, C_ERR);
                        }
                        catch (Exception ex) { Log("[!!] Copy failed: " + ex.Message, C_ERR); }
                    }
                    else
                    {
                        Log("[!!] oo2core_7_win64.dll not found in game folder", C_ERR);
                        Log("     Copy it manually to: " + Paths.BinDir, C_WARN);
                    }
                }
                UpdateStatus();
            }
        }

        void DoConfigureOutput()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Choose where extracted files will be stored";
                dlg.ShowNewFolderButton = true;
                if (outputPath.Length > 0 && Directory.Exists(outputPath)) dlg.SelectedPath = outputPath;
                else if (Directory.Exists(Paths.ExeDir)) dlg.SelectedPath = Paths.ExeDir;
                if (dlg.ShowDialog() != DialogResult.OK) return;

                LogSection("CONFIGURE OUTPUT PATH");
                outputPath = dlg.SelectedPath;
                SaveConfig();
                Log("[OK] Output path: " + outputPath, C_OK);
                UpdateStatus();
            }
        }

        void DoExtractAll()
        {
            if (gamePath.Length == 0 || !Directory.Exists(gamePath))
            { LogSection("EXTRACT"); Log("[!!] Configure game path first.", C_ERR); return; }
            if (outputPath.Length == 0 || !Directory.Exists(outputPath))
            { LogSection("EXTRACT"); Log("[!!] Configure output path first.", C_ERR); return; }
            if (!Oodle.IsLoaded)
            { LogSection("EXTRACT"); Log("[!!] Oodle DLL not loaded. Re-configure game path.", C_ERR); return; }

            LogSection("EXTRACT ALL .ARC FILES");
            Log("[..] Output: " + outputPath, C_WARN);
            running = true; SetButtonsEnabled(false); progress.Style = ProgressBarStyle.Marquee;

            Task.Run(() =>
            {
                try
                {
                    Extractor.ExtractAll(gamePath, outputPath, m => Log(m, C_GRAY));
                    Log(""); Log("[OK] Extraction complete.", C_OK);
                }
                catch (Exception ex) { Log("[!!] " + ex.Message, C_ERR); }
                finally
                {
                    this.Invoke(new Action(() =>
                    {
                        progress.Style = ProgressBarStyle.Continuous;
                        running = false;
                        SetButtonsEnabled(true);
                        UpdateStatus();
                    }));
                }
            });
        }

        void RunProcess(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.WorkingDirectory = Path.GetDirectoryName(exe);
            var p = Process.Start(psi);
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
        }

        void DoConvertAll()
        {
            if (outputPath.Length == 0 || !Directory.Exists(outputPath))
            { Log("[!!] Configure output path first.", C_ERR); return; }
            string[] avtx = Directory.GetFiles(outputPath, "*.avtx", SearchOption.AllDirectories);
            if (avtx.Length == 0) { Log("[!!] No .avtx files found. Extract first.", C_ERR); return; }

            LogSection("CONVERT .AVTX TO .DDS");
            Log("[..] Converting " + avtx.Length + " files...", C_WARN);
            running = true; SetButtonsEnabled(false);
            progress.Style = ProgressBarStyle.Continuous;
            progress.Maximum = avtx.Length; progress.Value = 0;
            string exe = Path.Combine(Paths.BinDir, "ddscConvert.exe");

            Task.Run(() =>
            {
                int ok = 0, fail = 0;
                try
                {
                    for (int i = 0; i < avtx.Length; i++)
                    {
                        string f = avtx[i];
                        try
                        {
                            RunProcess(exe, "\"" + f + "\"");
                            string d = f.Substring(0, f.Length - 5) + ".dds";
                            if (File.Exists(d) && new FileInfo(d).Length > 0) ok++; else fail++;
                        }
                        catch { fail++; }
                        int idx = i;
                        this.Invoke(new Action(() =>
                        {
                            progress.Value = Math.Min(idx + 1, progress.Maximum);
                            if ((idx + 1) % 100 == 0) Log("  ... " + (idx + 1) + " / " + avtx.Length + "  OK: " + ok + "  Fail: " + fail, C_GRAY);
                        }));
                    }
                    Log(""); Log("[OK] Converted: " + ok + "  Failed: " + fail, C_OK);
                }
                finally
                {
                    this.Invoke(new Action(() =>
                    {
                        progress.Value = 0; running = false; SetButtonsEnabled(true);
                    }));
                }
            });
        }

        void DoValidate()
        {
            LogSection("VALIDATE FILES");
            string target = outputPath;
            if (target.Length == 0) { Log("[!!] Configure output path first.", C_ERR); return; }
            string[] dds = Directory.GetFiles(target, "*.dds", SearchOption.AllDirectories);
            string[] ddsc = Directory.GetFiles(target, "*.ddsc", SearchOption.AllDirectories);
            Log("  .dds  : " + dds.Length, C_GRAY);
            Log("  .ddsc : " + ddsc.Length, C_GRAY);
            int hdr = 0, sdr = 0, invalid = 0;
            foreach (string f in dds)
            {
                try
                {
                    byte[] b = File.ReadAllBytes(f);
                    if (b.Length < 128 || Encoding.ASCII.GetString(b, 0, 4) != "DDS ") { invalid++; continue; }
                    string cc = Encoding.ASCII.GetString(b, 84, 4);
                    if (cc == "DX10")
                    {
                        uint dxgi = BitConverter.ToUInt32(b, 128);
                        if (dxgi == 95 || dxgi == 96) hdr++; else sdr++;
                    }
                    else sdr++;
                }
                catch { invalid++; }
            }
            Log(""); Log("  SDR (BC1/3/5): " + sdr, C_OK);
            Log("  HDR (BC6H)   : " + hdr, hdr > 0 ? C_WARN : C_GRAY);
            if (invalid > 0) Log("  Invalid      : " + invalid, C_ERR);
            if (hdr > 0)
            {
                Log(""); Log("  [!] " + hdr + " HDR files cannot be edited by Paint.NET/SD.", C_WARN);
                Log("      Normalize with texconv -f BC3_UNORM before editing.", C_WARN);
            }
        }

        void DoClassify()
        {
            if (outputPath.Length == 0) { Log("[!!] Configure output path first.", C_ERR); return; }
            LogSection("CLASSIFY FILES BY TYPE");
            string dst = Path.Combine(outputPath, "_CLASSIFIED");
            var cats = new Dictionary<string, string[]>();
            cats["textures"] = new[] { ".avtx", ".dds", ".ddsc", ".atx" };
            cats["audio"] = new[] { ".ogg", ".riff", ".rtpc", ".fsb" };
            cats["video"] = new[] { ".bik", ".bk2", ".mp4" };
            cats["models"] = new[] { ".tag", ".meshc", ".hrmeshc" };
            cats["ui"] = new[] { ".cfx", ".gfx", ".swf" };
            cats["data"] = new[] { ".adf", ".blo", ".json", ".xml" };
            cats["miniarc"] = new[] { ".bl", ".ee", ".nl", ".fl" };
            foreach (string k in cats.Keys) Directory.CreateDirectory(Path.Combine(dst, k));
            int moved = 0;
            try
            {
                string[] all = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories);
                foreach (string f in all)
                {
                    if (f.Contains("_CLASSIFIED")) continue;
                    string ext = Path.GetExtension(f).ToLower();
                    foreach (string c in cats.Keys)
                    {
                        bool hit = false;
                        foreach (string e in cats[c]) { if (e == ext) { hit = true; break; } }
                        if (hit) { File.Copy(f, Path.Combine(dst, c, Path.GetFileName(f)), true); moved++; break; }
                    }
                }
                Log("[OK] Classified " + moved + " files into " + dst, C_OK);
            }
            catch (Exception ex) { Log("[!!] " + ex.Message, C_ERR); }
        }

        void DoSummary()
        {
            if (outputPath.Length == 0) { Log("[!!] Configure output path first.", C_ERR); return; }
            LogSection("FULL FILE SUMMARY");
            try
            {
                string[] all = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories);
                Log("  Total files: " + all.Length, C_INFO);
                var counts = new Dictionary<string, int>();
                foreach (string f in all)
                {
                    string e = Path.GetExtension(f).ToLower();
                    if (!counts.ContainsKey(e)) counts[e] = 0;
                    counts[e]++;
                }
                var list = new List<KeyValuePair<string, int>>(counts);
                list.Sort((a, b) => b.Value.CompareTo(a.Value));
                Log("");
                foreach (var kv in list) Log(string.Format("    {0,-14} {1,8}", kv.Key, kv.Value), C_GRAY);
            }
            catch (Exception ex) { Log("[!!] " + ex.Message, C_ERR); }
        }

        void DoDeploy()
        {
            LogSection("DEPLOY TO DROPZONE");
            if (gamePath.Length == 0 || !Directory.Exists(gamePath))
            { Log("[!!] Configure game path first.", C_ERR); return; }

            string source;
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select folder with modified files to deploy";
                if (outputPath.Length > 0) dlg.SelectedPath = outputPath;
                if (dlg.ShowDialog() != DialogResult.OK) return;
                source = dlg.SelectedPath;
            }
            string mapPath = null;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Select mapping.json (Cancel to skip)";
                dlg.Filter = "mapping.json|mapping.json|All files|*.*";
                if (dlg.ShowDialog() == DialogResult.OK) mapPath = dlg.FileName;
            }
            var n2h = new Dictionary<string, string>();
            if (mapPath != null)
            {
                Log("[..] Loading mapping...", C_WARN);
                string json = File.ReadAllText(mapPath);
                int idx = json.IndexOf("\"name_to_hash\"");
                if (idx >= 0)
                {
                    int s = json.IndexOf('{', idx);
                    int en = json.IndexOf('}', s);
                    if (s >= 0 && en > s)
                    {
                        foreach (string pair in json.Substring(s + 1, en - s - 1).Split(','))
                        {
                            int c = pair.IndexOf(':');
                            if (c < 0) continue;
                            string k = pair.Substring(0, c).Trim().Trim('"').Replace("\\\\", "\\");
                            string v = pair.Substring(c + 1).Trim().Trim('"');
                            n2h[k] = v;
                        }
                    }
                }
                Log("[OK] Loaded " + n2h.Count + " entries", C_OK);
            }
            string dropzone = Path.Combine(gamePath, "dropzone");
            Directory.CreateDirectory(dropzone);
            string[] dds = Directory.GetFiles(source, "*.dds", SearchOption.AllDirectories);
            Log("[..] Deploying " + dds.Length + " files...", C_WARN);

            running = true; SetButtonsEnabled(false);
            progress.Style = ProgressBarStyle.Continuous;
            progress.Maximum = Math.Max(1, dds.Length); progress.Value = 0;
            string exe = Path.Combine(Paths.BinDir, "ddscConvert.exe");
            int dep = 0, noHash = 0, fail = 0;

            Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < dds.Length; i++)
                    {
                        string f = dds[i];
                        string bn = Path.GetFileNameWithoutExtension(f);
                        string h = null;
                        if (bn.Length == 16 && Regex.IsMatch(bn, "^[0-9A-Fa-f]{16}$")) h = bn.ToUpper();
                        else
                        {
                            string rel = f.Substring(source.Length).TrimStart('\\').Replace("\\", "/");
                            if (n2h.ContainsKey(rel)) h = n2h[rel];
                        }
                        if (h == null) { noHash++; progress.Value = i + 1; continue; }
                        try
                        {
                            RunProcess(exe, "\"" + f + "\"");
                            string dir = Path.GetDirectoryName(f);
                            string avtx = Path.Combine(dir, bn + ".avtx");
                            string ddsc = Path.Combine(dir, bn + ".ddsc");
                            string payload = File.Exists(avtx) ? avtx : (File.Exists(ddsc) ? ddsc : f);
                            File.Copy(payload, Path.Combine(dropzone, h + ".ddsc"), true);
                            dep++;
                        }
                        catch { fail++; }
                        int idx = i;
                        this.Invoke(new Action(() => { progress.Value = Math.Min(idx + 1, progress.Maximum); }));
                    }
                    Log("");
                    Log("  Deployed : " + dep, C_OK);
                    Log("  Skipped  : " + noHash, C_WARN);
                    Log("  Failed   : " + fail, C_ERR);
                    Log("  Location : " + dropzone, C_INFO);
                    Log("");
                    Log("  Steam/Epic launch options:", C_WARN);
                    Log("    --vfs-fs dropzone --vfs-archive archives_win64 --vfs-archive patch_win64 --vfs-archive dlc_win64 --vfs-fs", C_GRAY);
                }
                finally
                {
                    this.Invoke(new Action(() =>
                    {
                        progress.Value = 0; running = false; SetButtonsEnabled(true);
                    }));
                }
            });
        }

        void DoOpenOutput()
        {
            if (outputPath.Length > 0 && Directory.Exists(outputPath))
                Process.Start("explorer.exe", outputPath);
            else
                Log("[!!] Output folder not configured.", C_ERR);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running)
            {
                var r = MessageBox.Show(
                    "An operation is still running.\n\nClose anyway?",
                    "Operation in progress", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }

    // ============================================================
    // Program
    // ============================================================
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main()
        {
            // Wire crash handler ANTES de cualquier otra cosa para capturar todo
            try { CrashHandler.Install(); } catch { }

            try { SetProcessDPIAware(); } catch { }
            try { Paths.EnsureAll(); } catch { }

            // v2.0: Oodle NUNCA debe quedar en disco entre sesiones. Si existe
            // (residuo de crash/kill), borrarlo al arrancar. Se autocopia desde el
            // juego al seleccionar RAGE2.exe.
            try { if (File.Exists(Paths.OodleDllPath)) File.Delete(Paths.OodleDllPath); } catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HomeForm());
        }
    }
}
