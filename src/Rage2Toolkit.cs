using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    static class Paths
    {
        // Local-first: everything is next to the .exe
        public static readonly string ExeDir = Path.GetDirectoryName(Application.ExecutablePath);
        public static readonly string BinDir = Path.Combine(ExeDir, "bin");
        public static readonly string DataDir = Path.Combine(ExeDir, "data");
        public static readonly string ConfigDir = Path.Combine(ExeDir, "config");
        public static readonly string ConfigFile = Path.Combine(ConfigDir, "paths.txt");

        public static void EnsureAll()
        {
            Directory.CreateDirectory(BinDir);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(ConfigDir);
        }
    }

    static class Embedded
    {
        static readonly string[] BinFiles = { "ddscConvert.exe", "ddscConvert.exe.config", "texconv.exe", "R2SmallArchive.exe" };
        static readonly string[] DataFiles = { "filelist.txt", "filelist_raw.txt", "filelist_extra.txt", "filelist_supplemental.txt" };

        public static void ExtractAll()
        {
            Paths.EnsureAll();
            var asm = Assembly.GetExecutingAssembly();
            foreach (string rn in asm.GetManifestResourceNames())
            {
                string tdir = null, fname = null;
                foreach (string b in BinFiles)
                    if (rn.EndsWith("." + b) || rn.EndsWith(b)) { tdir = Paths.BinDir; fname = b; break; }
                if (tdir == null)
                    foreach (string d in DataFiles)
                        if (rn.EndsWith("." + d) || rn.EndsWith(d)) { tdir = Paths.DataDir; fname = d; break; }
                if (tdir == null) continue;
                string t = Path.Combine(tdir, fname);
                if (File.Exists(t)) continue;
                using (Stream s = asm.GetManifestResourceStream(rn))
                using (FileStream fs = File.Create(t))
                    s.CopyTo(fs);
            }
        }
    }

    static class Oodle
    {
        [DllImport("oo2core_7_win64.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern long OodleLZ_Decompress(
            byte[] compBuf, long compSize, byte[] rawBuf, long rawSize,
            int fuzzSafe, int checkCRC, int verbosity,
            IntPtr decBufBase, long decBufSize,
            IntPtr fpCallback, IntPtr callbackUserData,
            IntPtr decoderMemory, long decoderMemorySize, int threadPhase);
    }

    static class Extractor
    {
        static byte[] DecompressOodle(byte[] comp, int usz)
        {
            byte[] r = new byte[usz];
            long n = Oodle.OodleLZ_Decompress(comp, comp.Length, r, usz, 1, 0, 0,
                IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, 3);
            if (n <= 0) throw new Exception("Oodle error: " + n);
            return r;
        }

        static string DetectExt(byte[] d)
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
            // TAG0 can appear at offset 4 (after 4-byte header)
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

        static void ExtractOne(string tabPath, string arcPath, string outDir, out int extracted, out int failed)
        {
            extracted = 0; failed = 0;
            byte[] tab = File.ReadAllBytes(tabPath);
            if (BitConverter.ToUInt16(tab, 4) != 3 || BitConverter.ToUInt16(tab, 6) != 1) return;
            uint fcount = BitConverter.ToUInt32(tab, 12);
            uint bcount = BitConverter.ToUInt32(tab, 16);
            int bStart = 32;
            int fStart = 32 + (int)bcount * 8;
            uint[,] bt = new uint[bcount, 2];
            for (int i = 0; i < bcount; i++) { bt[i, 0] = BitConverter.ToUInt32(tab, bStart + i*8); bt[i, 1] = BitConverter.ToUInt32(tab, bStart + i*8 + 4); }
            byte[] arc = File.ReadAllBytes(arcPath);
            Directory.CreateDirectory(outDir);
            for (uint i = 0; i < fcount; i++)
            {
                int eo = fStart + (int)i * 24;
                ulong hash = BitConverter.ToUInt64(tab, eo);
                uint off = BitConverter.ToUInt32(tab, eo + 8);
                uint csz = BitConverter.ToUInt32(tab, eo + 12);
                uint usz = BitConverter.ToUInt32(tab, eo + 16);
                ushort bidx = BitConverter.ToUInt16(tab, eo + 20);
                byte ctype = tab[eo + 22];
                try
                {
                    byte[] payload; bool blocks = false;
                    if (bidx != 0xFFFF && bidx < bcount && !(bt[bidx,0] == 0xFFFFFFFF && bt[bidx,1] == 0xFFFFFFFF)) blocks = true;
                    if (!blocks)
                    {
                        byte[] c = new byte[csz];
                        Array.Copy(arc, off, c, 0, csz);
                        if (ctype == 4) payload = DecompressOodle(c, (int)usz);
                        else if (ctype == 0 && csz == usz) payload = c;
                        else throw new Exception("ctype=" + ctype);
                    }
                    else
                    {
                        MemoryStream ms = new MemoryStream((int)usz);
                        uint rem = csz; uint cur = bidx; long ac = off;
                        while (rem > 0 && cur < bcount)
                        {
                            uint bc = bt[cur,0], bu = bt[cur,1];
                            if (bc == 0xFFFFFFFF) break;
                            byte[] c = new byte[bc];
                            Array.Copy(arc, ac, c, 0, bc);
                            byte[] dec = DecompressOodle(c, (int)bu);
                            ms.Write(dec, 0, dec.Length);
                            ac += bc; rem -= bc; cur++;
                        }
                        payload = ms.ToArray();
                    }
                    File.WriteAllBytes(Path.Combine(outDir, hash.ToString("X16") + "." + DetectExt(payload)), payload);
                    extracted++;
                }
                catch { failed++; }
            }
        }

        public static void ExtractAll(string gamePath, string outputBase, Action<string> log)
        {
            string initDir = Path.Combine(gamePath, "archives_win64", "initial");
            string suppDir = Path.Combine(gamePath, "archives_win64", "supplemental");
            if (!Directory.Exists(initDir) && !Directory.Exists(suppDir)) throw new DirectoryNotFoundException("Cannot find: " + initDir + " nor " + suppDir);
            var tabList = new System.Collections.Generic.List<string>();
            if (Directory.Exists(initDir)) tabList.AddRange(Directory.GetFiles(initDir, "*.tab", SearchOption.AllDirectories));
            if (Directory.Exists(suppDir)) tabList.AddRange(Directory.GetFiles(suppDir, "*.tab", SearchOption.AllDirectories));
            string arcDir = initDir;
            string[] tabs = tabList.ToArray();
            log("Found " + tabs.Length + " archives. Using " + Environment.ProcessorCount + " threads.");
            object lk = new object();
            int tE = 0, tF = 0;
            Parallel.ForEach(tabs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, tabPath =>
            {
                string bn = Path.GetFileNameWithoutExtension(tabPath);
                string ap = Path.Combine(Path.GetDirectoryName(tabPath), bn + ".arc");
                if (!File.Exists(ap)) return;
                string relDir = Path.GetDirectoryName(tabPath).Substring(Path.Combine(gamePath, "archives_win64").Length).TrimStart('\\');
                string od = string.IsNullOrEmpty(relDir) ? Path.Combine(outputBase, bn) : Path.Combine(outputBase, relDir, bn);
                int e, f;
                ExtractOne(tabPath, ap, od, out e, out f);
                lock (lk) { tE += e; tF += f; log(string.Format("  {0,-12} {1,6} ok  {2,5} fail", bn, e, f)); }
            });
            log(""); log("Total: " + tE + " extracted, " + tF + " failed");
        }
    }

    public class MainForm : Form
    {
        readonly Color C_BG = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL = Color.FromArgb(30, 30, 38);
        readonly Color C_LOG = Color.FromArgb(10, 10, 15);
        readonly Color C_HEAD = Color.FromArgb(15, 15, 20);
        readonly Color C_INFO = Color.FromArgb(0, 200, 200);
        readonly Color C_OK = Color.FromArgb(100, 220, 100);
        readonly Color C_WARN = Color.FromArgb(230, 180, 74);
        readonly Color C_ERR = Color.FromArgb(240, 100, 100);
        readonly Color C_GRAY = Color.FromArgb(160, 160, 160);
        readonly Color C_TEXT = Color.FromArgb(200, 200, 200);
        readonly Color C_BTN = Color.FromArgb(50, 50, 60);
        readonly Color C_RED = Color.FromArgb(140, 50, 50);
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
            this.Text = "RAGE 2 Modding Toolkit v1.1  -  Advanced";
            this.ClientSize = new Size(1400, 900);
            this.MinimumSize = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.Icon = SystemIcons.Application;

            this.HandleCreated += (s, e) =>
            {
                try { int v = 1; DwmSetWindowAttribute(this.Handle, 20, ref v, 4); DwmSetWindowAttribute(this.Handle, 19, ref v, 4); }
                catch { }
            };

            LoadConfig();
            BuildUI();
            UpdateStatus();

            Log("RAGE 2 Modding Toolkit v1.1  -  by Kry0genik", C_INFO);
            Log("Ready. Follow the steps:", C_GRAY);
            Log("  1. Configure game path (auto-copies Oodle DLL)", C_GRAY);
            Log("  2. Configure output path", C_GRAY);
            Log("  3. Extract all .arc files", C_GRAY);
            Log("  4. Convert .avtx to .dds", C_GRAY);
            Log("  5. Classify files by type", C_GRAY);
            Log("  6. Modify with external tools", C_GRAY);
            Log("  7. Deploy to dropzone", C_GRAY);
            Log("");
            Log("Click [Help] for the full modding guide.", C_WARN);
        }

        void BuildUI()
        {
            int headerH = 90;
            int sidebarW = 320;

            // ============ HEADER ============
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

            Button helpBtn = new Button();
            helpBtn.Text = "  ?  Help  ";
            helpBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            helpBtn.BackColor = C_BTN;
            helpBtn.ForeColor = Color.White;
            helpBtn.FlatStyle = FlatStyle.Flat;
            helpBtn.FlatAppearance.BorderSize = 0;
            helpBtn.Size = new Size(100, 34);
            helpBtn.Location = new Point(this.ClientSize.Width - 240, 28);
            helpBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            helpBtn.Cursor = Cursors.Hand;
            helpBtn.Click += (s, e) => ShowHelp();
            header.Controls.Add(helpBtn);

            Label verLbl = new Label();
            verLbl.Text = "v1.1";
            verLbl.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            verLbl.ForeColor = Color.FromArgb(220, 200, 100);
            verLbl.Location = new Point(this.ClientSize.Width - 70, 34);
            verLbl.AutoSize = true;
            verLbl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            header.Controls.Add(verLbl);

            // ============ STATUS STRIP ============
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

            // ============ SIDEBAR ============
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
            AddButton(sidebar, "  Extract One .arc", C_BTN, ref y, () => Log("Use console mode for single-arc extraction.", C_GRAY));

            AddSection(sidebar, "TEXTURES", ref y);
            AddButton(sidebar, "  Convert .avtx to .dds", C_BTN, ref y, () => DoConvertAll());
            AddButton(sidebar, "  Texture summary", C_BTN, ref y, () => DoSummary());
            AddButton(sidebar, "  Validate files", C_BTN, ref y, () => DoValidate());

            AddSection(sidebar, "ORGANIZE", ref y);
            AddButton(sidebar, "  Classify all files", C_BTN, ref y, () => DoClassify());
            AddButton(sidebar, "  Full file summary", C_BTN, ref y, () => DoSummary());

            AddSection(sidebar, "REPACK .ARC", ref y);
            AddButton(sidebar, "  Modificar texturas (.arc)", C_INFO, ref y, () => { new RepackForm(this, gamePath, outputPath).ShowDialog(); });

            AddSection(sidebar, "MOD DEPLOYMENT", ref y);
            AddButton(sidebar, "  Deploy to dropzone", C_RED, ref y, () => DoDeploy());

            AddSection(sidebar, "SETTINGS", ref y);
            AddButton(sidebar, "  Configure game path", C_BTN, ref y, () => DoConfigureGame());
            AddButton(sidebar, "  Configure output path", C_BTN, ref y, () => DoConfigureOutput());
            AddButton(sidebar, "  Open output folder", C_BTN, ref y, () => DoOpenOutput());
            AddButton(sidebar, "  Exit", C_RED, ref y, () => this.Close());

            // ============ LOG PANEL ============
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

            // ============ PROGRESS BAR ============
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
            Color orig = color;
            btn.MouseEnter += (s, e) => { if (!running) btn.BackColor = Color.FromArgb(70, 70, 85); };
            btn.MouseLeave += (s, e) => { btn.BackColor = orig; };
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
            Log(new string('─', 70), C_INFO);
            Log("  " + title, C_INFO);
            Log(new string('─', 70), C_INFO);
        }

        void LoadConfig()
        {
            if (!File.Exists(Paths.ConfigFile)) return;
            try
            {
                string[] lines = File.ReadAllLines(Paths.ConfigFile);
                if (lines.Length > 0) gamePath = lines[0];
                if (lines.Length > 1) outputPath = lines[1];
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
            { statusGame.Text = "Game: " + gamePath; statusGame.ForeColor = C_OK; }
            else { statusGame.Text = "Game: (not configured)"; statusGame.ForeColor = C_ERR; }

            string dll = Path.Combine(Paths.BinDir, "oo2core_7_win64.dll");
            if (File.Exists(dll)) { statusOodle.Text = "  |  Oodle: present"; statusOodle.ForeColor = C_OK; }
            else { statusOodle.Text = "  |  Oodle: MISSING"; statusOodle.ForeColor = C_ERR; }

            if (outputPath.Length > 0 && Directory.Exists(outputPath))
            { statusOutput.Text = "  |  Output: " + outputPath; statusOutput.ForeColor = C_OK; }
            else { statusOutput.Text = "  |  Output: (not configured)"; statusOutput.ForeColor = C_ERR; }
        }

        void SetButtonsEnabled(bool e) { foreach (Button b in allButtons) b.Enabled = e; }

        void DoConfigureGame()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select RAGE 2 installation folder (where RAGE2.exe lives)";
                dlg.ShowNewFolderButton = false;
                if (gamePath.Length > 0 && Directory.Exists(gamePath)) dlg.SelectedPath = gamePath;
                if (dlg.ShowDialog() != DialogResult.OK) return;

                LogSection("CONFIGURE GAME PATH");
                gamePath = dlg.SelectedPath;
                if (!File.Exists(Path.Combine(gamePath, "RAGE2.exe")))
                {
                    Log("[!!] RAGE2.exe not found in: " + gamePath, C_ERR);
                    Log("     This doesn't look like a RAGE 2 install.", C_WARN);
                    gamePath = ""; return;
                }
                SaveConfig();
                Log("[OK] Game path: " + gamePath, C_OK);

                string target = Path.Combine(Paths.BinDir, "oo2core_7_win64.dll");
                if (File.Exists(target)) { Log("[--] Oodle DLL already cached", C_GRAY); }
                else
                {
                    string direct = Path.Combine(gamePath, "oo2core_7_win64.dll");
                    string found = File.Exists(direct) ? direct : null;
                    if (found == null)
                    {
                        Log("[..] Searching for oo2core_7_win64.dll...", C_WARN);
                        try { string[] r = Directory.GetFiles(gamePath, "oo2core_7_win64.dll", SearchOption.AllDirectories); if (r.Length > 0) found = r[0]; }
                        catch { }
                    }
                    if (found != null)
                    {
                        File.Copy(found, target, true);
                        Log("[OK] Copied Oodle DLL from: " + found, C_OK);
                    }
                    else { Log("[!!] oo2core_7_win64.dll not found in game folder", C_ERR); Log("     Copy it manually to: " + Paths.BinDir, C_WARN); }
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
                string exeFolder = Path.GetDirectoryName(Application.ExecutablePath);
                if (outputPath.Length > 0 && Directory.Exists(outputPath)) dlg.SelectedPath = outputPath;
                else if (Directory.Exists(exeFolder)) dlg.SelectedPath = exeFolder;
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
            if (gamePath.Length == 0 || !Directory.Exists(gamePath)) { LogSection("EXTRACT"); Log("[!!] Configure game path first.", C_ERR); return; }
            if (outputPath.Length == 0 || !Directory.Exists(outputPath)) { LogSection("EXTRACT"); Log("[!!] Configure output path first.", C_ERR); return; }
            if (!File.Exists(Path.Combine(Paths.BinDir, "oo2core_7_win64.dll"))) { LogSection("EXTRACT"); Log("[!!] Oodle DLL missing.", C_ERR); return; }

            LogSection("EXTRACT ALL .ARC FILES");
            Log("[..] Output: " + outputPath, C_WARN);
            running = true; SetButtonsEnabled(false); progress.Style = ProgressBarStyle.Marquee;

            Task.Run(() =>
            {
                try { Extractor.ExtractAll(gamePath, outputPath, m => Log(m, C_GRAY)); Log(""); Log("[OK] Extraction complete.", C_OK); }
                catch (Exception ex) { Log("[!!] " + ex.Message, C_ERR); }
                finally { this.Invoke(new Action(() => { progress.Style = ProgressBarStyle.Continuous; running = false; SetButtonsEnabled(true); UpdateStatus(); })); }
            });
        }

        void RunProcess(string exe, string args)
        {
            var psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false; psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            psi.CreateNoWindow = true; psi.WorkingDirectory = Path.GetDirectoryName(exe);
            var p = Process.Start(psi);
            p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
        }

        void DoConvertAll()
        {
            if (outputPath.Length == 0 || !Directory.Exists(outputPath)) { Log("[!!] Configure output path first.", C_ERR); return; }
            string[] avtx = Directory.GetFiles(outputPath, "*.avtx", SearchOption.AllDirectories);
            if (avtx.Length == 0) { Log("[!!] No .avtx files found. Extract first.", C_ERR); return; }

            LogSection("CONVERT .AVTX TO .DDS");
            Log("[..] Converting " + avtx.Length + " files...", C_WARN);
            running = true; SetButtonsEnabled(false); progress.Style = ProgressBarStyle.Continuous; progress.Maximum = avtx.Length; progress.Value = 0;
            string exe = Path.Combine(Paths.BinDir, "ddscConvert.exe");

            Task.Run(() =>
            {
                int ok = 0, fail = 0;
                try
                {
                    for (int i = 0; i < avtx.Length; i++)
                    {
                        string f = avtx[i];
                        try { RunProcess(exe, "\"" + f + "\""); string d = f.Substring(0, f.Length - 5) + ".dds"; if (File.Exists(d) && new FileInfo(d).Length > 0) ok++; else fail++; }
                        catch { fail++; }
                        int idx = i;
                        this.Invoke(new Action(() => { progress.Value = Math.Min(idx + 1, progress.Maximum); if ((idx + 1) % 100 == 0) Log("  ... " + (idx + 1) + " / " + avtx.Length + "  OK: " + ok + "  Fail: " + fail, C_GRAY); }));
                    }
                    Log(""); Log("[OK] Converted: " + ok + "  Failed: " + fail, C_OK);
                }
                finally { this.Invoke(new Action(() => { progress.Value = 0; running = false; SetButtonsEnabled(true); })); }
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
            if (hdr > 0) { Log(""); Log("  [!] " + hdr + " HDR files cannot be edited by Paint.NET/SD.", C_WARN); Log("      Normalize with texconv -f BC3_UNORM before editing.", C_WARN); }
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
                foreach (string f in all) { string e = Path.GetExtension(f).ToLower(); if (!counts.ContainsKey(e)) counts[e] = 0; counts[e]++; }
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
            if (gamePath.Length == 0 || !Directory.Exists(gamePath)) { Log("[!!] Configure game path first.", C_ERR); return; }

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
                    int s = json.IndexOf('{', idx); int en = json.IndexOf('}', s);
                    if (s >= 0 && en > s)
                    {
                        foreach (string pair in json.Substring(s + 1, en - s - 1).Split(','))
                        {
                            int c = pair.IndexOf(':'); if (c < 0) continue;
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

            running = true; SetButtonsEnabled(false); progress.Style = ProgressBarStyle.Continuous; progress.Maximum = dds.Length; progress.Value = 0;
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
                        if (bn.Length == 16 && System.Text.RegularExpressions.Regex.IsMatch(bn, "^[0-9A-Fa-f]{16}$")) h = bn.ToUpper();
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
                    Log(""); Log("  Deployed : " + dep, C_OK); Log("  Skipped  : " + noHash, C_WARN); Log("  Failed   : " + fail, C_ERR);
                    Log("  Location : " + dropzone, C_INFO);
                    Log(""); Log("  Steam/Epic launch options:", C_WARN);
                    Log("    --vfs-fs dropzone --vfs-archive archives_win64 --vfs-archive patch_win64 --vfs-archive dlc_win64 --vfs-fs", C_GRAY);
                }
                finally { this.Invoke(new Action(() => { progress.Value = 0; running = false; SetButtonsEnabled(true); })); }
            });
        }

        void DoOpenOutput()
        {
            if (outputPath.Length > 0 && Directory.Exists(outputPath)) Process.Start("explorer.exe", outputPath);
            else Log("[!!] Output folder not configured.", C_ERR);
        }

        void ShowHelp()
        {
            Form h = new Form();
            h.Text = "RAGE 2 Modding - How to";
            h.ClientSize = new Size(900, 720);
            h.StartPosition = FormStartPosition.CenterParent;
            h.BackColor = C_BG;
            h.ForeColor = Color.White;
            h.Font = new Font("Segoe UI", 10);
            h.Icon = SystemIcons.Application;
            h.HandleCreated += (s, e) => { try { int v = 1; DwmSetWindowAttribute(h.Handle, 20, ref v, 4); DwmSetWindowAttribute(h.Handle, 19, ref v, 4); } catch { } };

            RichTextBox r = new RichTextBox();
            r.Dock = DockStyle.Fill;
            r.BackColor = C_HEAD;
            r.ForeColor = Color.White;
            r.Font = new Font("Consolas", 10);
            r.ReadOnly = true;
            r.BorderStyle = BorderStyle.None;
            r.WordWrap = true;
            h.Controls.Add(r);

            r.Text = string.Join("\n", new[] {
                "RAGE 2 MODDING TOOLKIT — USER GUIDE",
                "════════════════════════════════════════════════════════════════",
                "",
                "STEP 1 — CONFIGURE GAME PATH",
                "────────────────────────────",
                "   Click \"Configure game path\" and select your RAGE 2 folder",
                "   (the one that contains RAGE2.exe).",
                "",
                "   The toolkit will automatically locate and copy the required",
                "   Oodle runtime library (oo2core_7_win64.dll) from the game",
                "   folder. You only need to do this once.",
                "",
                "STEP 2 — CONFIGURE OUTPUT PATH",
                "──────────────────────────────",
                "   Click \"Configure output path\" and choose where extracted",
                "   files will be stored. The toolkit needs ~25 GB free there.",
                "",
                "STEP 3 — EXTRACT GAME ASSETS",
                "────────────────────────────",
                "   Click \"Extract All .arc files\". Extracts roughly 14,000",
                "   files from the game's archives. Takes ~30 seconds.",
                "",
                "STEP 4 — CONVERT TEXTURES",
                "─────────────────────────",
                "   Click \"Convert .avtx to .dds\". Converts the game's texture",
                "   format to standard DDS files editable in Paint.NET, GIMP,",
                "   Photoshop, or Stable Diffusion / Chainner.",
                "",
                "STEP 5 — ORGANIZE",
                "─────────────────",
                "   Click \"Classify all files\" to sort content by type into",
                "   subfolders (textures, audio, video, ui, data, etc.).",
                "",
                "   Click \"Full file summary\" for statistics on everything",
                "   extracted.",
                "",
                "STEP 6 — MODIFY (external tools)",
                "────────────────────────────────",
                "   Textures   : upscale with Stable Diffusion or Chainner.",
                "                Keep output as BC3_UNORM (albedo) or",
                "                BC5_UNORM (normal maps).",
                "",
                "   Audio      : use fsbext or vgmstream for FSB5 files.",
                "   Video      : use RAD Video Tools for Bink files.",
                "   UI         : use JPEXS Free Flash Decompiler for .gfx.",
                "",
                "STEP 7 — DEPLOY TO DROPZONE",
                "───────────────────────────",
                "   Click \"Deploy to dropzone\".",
                "",
                "   Creates a dropzone\\ folder next to RAGE2.exe and copies",
                "   modified files there using their original hash filenames.",
                "",
                "   The game reads loose files from dropzone without needing",
                "   to repack the .arc archives.",
                "",
                "STEP 8 — LAUNCH WITH DROPZONE",
                "─────────────────────────────",
                "   In Steam or Epic, edit RAGE 2's launch options and paste",
                "   the following as a single line:",
                "",
                "   --vfs-fs dropzone --vfs-archive archives_win64",
                "   --vfs-archive patch_win64 --vfs-archive dlc_win64 --vfs-fs",
                "",
                "VALIDATION",
                "──────────",
                "   Use \"Validate files\" before deploying to check format",
                "   problems:",
                "",
                "   • DDS  : detects BC1 / BC3 / BC5 (SDR) vs BC6H / BC7 (HDR)",
                "   • Video: verifies Bink signature (BIK / KB2)",
                "   • Audio: verifies OggS, RIFF, FSB5 signatures",
                "",
                "TROUBLESHOOTING",
                "───────────────",
                "   \"Game NOT FOUND\"",
                "       Re-run Configure game path and select the folder",
                "       that contains RAGE2.exe.",
                "",
                "   \"Oodle MISSING\"",
                "       Auto-copy failed. Manually copy oo2core_7_win64.dll",
                "       from your game folder into: " + Paths.BinDir,
                "",
                "   \"Extract failed\"",
                "       Check the log for the failing archive. Verify that",
                "       your game installation is complete and not corrupted.",
                "",
                "   Game crashes with mods",
                "       Remove the dropzone\\ folder and try again with fewer",
                "       files to identify which one is causing the issue.",
                "",
                "SUPPORT",
                "───────",
                "   Nexus Mods : https://www.nexusmods.com/profile/Kry0genik",
                "   GitHub     : https://github.com/shTNT",
                ""
            });
            h.ShowDialog();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running)
            {
                var r = MessageBox.Show("An operation is still running.\n\nClose anyway?", "Operation in progress", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { e.Cancel = true; return; }
            }
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        public static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            try { Paths.EnsureAll(); Embedded.ExtractAll(); }
            catch (Exception ex) { MessageBox.Show("Startup error: " + ex.Message, "RAGE 2 Toolkit"); return; }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HomeForm());
        }
    }
}





