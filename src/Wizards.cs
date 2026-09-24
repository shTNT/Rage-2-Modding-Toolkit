using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    // ============================================================
    // RoundedButton
    // ============================================================
    public class RoundedButton : Button
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }
        public int BorderThickness { get; set; }

        public RoundedButton()
        {
            CornerRadius = 10;
            BorderColor = Color.Transparent;
            BorderThickness = 0;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 1) Rellenar TODO el fondo con el color del padre (elimina las esquinas negras)
            Color parentBg = (this.Parent != null) ? this.Parent.BackColor : this.BackColor;
            using (var bg = new SolidBrush(parentBg))
                g.FillRectangle(bg, this.ClientRectangle);

            // 2) Dibujar la forma redondeada con el BackColor propio
            Rectangle r = this.ClientRectangle;
            int t = BorderThickness > 0 ? BorderThickness : 0;
            r = new Rectangle(r.X + t, r.Y + t, r.Width - 2 * t - 1, r.Height - 2 * t - 1);

            using (var path = GetRoundedPath(r, CornerRadius))
            {
                using (var b = new SolidBrush(this.BackColor))
                    g.FillPath(b, path);

                if (BorderThickness > 0 && BorderColor != Color.Transparent)
                {
                    using (var p = new Pen(BorderColor, BorderThickness))
                        g.DrawPath(p, path);
                }
            }

            TextRenderer.DrawText(
                g, this.Text, this.Font, this.ClientRectangle, this.ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        private System.Drawing.Drawing2D.GraphicsPath GetRoundedPath(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || r.Width <= d || r.Height <= d) { path.AddRectangle(r); return path; }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
    // ============================================================
    // HOME
    // ============================================================
    public class HomeForm : Form
    {
        readonly Color C_BG = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL = Color.FromArgb(30, 30, 38);
        readonly Color C_HEAD = Color.FromArgb(15, 15, 20);
        readonly Color C_INFO = Color.FromArgb(0, 200, 200);
        readonly Color C_OK = Color.FromArgb(100, 220, 100);
        readonly Color C_ERR = Color.FromArgb(240, 100, 100);
        readonly Color C_WARN = Color.FromArgb(230, 180, 74);
        readonly Color C_GRAY = Color.FromArgb(160, 160, 160);
        readonly Color C_BTN = Color.FromArgb(50, 50, 60);
        readonly Color C_MAGENTA = Color.FromArgb(220, 80, 220);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

        string gamePath, outputPath;
        Label statusGame, statusOutput, statusOodle, statusArcs, statusTools;
        Panel center;
        Timer autoTimer;

        public HomeForm()
        {
            this.Text = "RAGE 2 Modding Toolkit v1.1";
            this.ClientSize = new Size(1100, 720);
            this.MinimumSize = new Size(960, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.Icon = SystemIcons.Application;
            this.HandleCreated += (s, e) => {
                try { int v = 1; DwmSetWindowAttribute(this.Handle, 20, ref v, 4); DwmSetWindowAttribute(this.Handle, 19, ref v, 4); } catch { }
            };

            LoadConfig();
            BuildUI();

            autoTimer = new Timer();
            autoTimer.Interval = 2000;
            autoTimer.Tick += (s, e) => { try { RefreshStatus(); } catch { } };
            autoTimer.Start();
            this.FormClosing += (s, e) => { try { autoTimer.Stop(); autoTimer.Dispose(); } catch { } };
        }

        void LoadConfig()
        {
            try {
                if (File.Exists(Paths.ConfigFile)) {
                    var lines = File.ReadAllLines(Paths.ConfigFile);
                    if (lines.Length > 0) gamePath = lines[0];
                    if (lines.Length > 1) outputPath = lines[1];
                }
            } catch { }
        }

        void SaveConfig()
        {
            try { File.WriteAllLines(Paths.ConfigFile, new[] { gamePath ?? "", outputPath ?? "" }); } catch { }
        }

        void BuildUI()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 110;
            header.BackColor = C_HEAD;
            this.Controls.Add(header);

            Label title = new Label();
            title.Text = "RAGE 2 MODDING TOOLKIT";
            title.Font = new Font("Segoe UI", 22, FontStyle.Bold);
            title.ForeColor = C_INFO;
            title.Location = new Point(40, 22);
            title.AutoSize = true;
            header.Controls.Add(title);

            Label sub = new Label();
            sub.Text = "Extract  \u00b7  Edit  \u00b7  Repack  \u00b7  Share";
            sub.Font = new Font("Segoe UI", 11);
            sub.ForeColor = C_GRAY;
            sub.Location = new Point(42, 66);
            sub.AutoSize = true;
            header.Controls.Add(sub);

            Label verLbl = new Label();
            verLbl.Text = "v1.1  \u00b7  by Kry0genik";
            verLbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            verLbl.ForeColor = Color.FromArgb(220, 200, 100);
            verLbl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            verLbl.Location = new Point(this.ClientSize.Width - 220, 28);
            verLbl.AutoSize = true;
            header.Controls.Add(verLbl);

            RoundedButton btnAdv = new RoundedButton();
            btnAdv.Text = "Advanced tools";
            btnAdv.CornerRadius = 10; btnAdv.BorderColor = Color.FromArgb(90, 90, 110); btnAdv.BorderThickness = 1;
            btnAdv.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdv.Location = new Point(this.ClientSize.Width - 220, 55);
            btnAdv.Size = new Size(180, 36);
            btnAdv.FlatStyle = FlatStyle.Flat;
            btnAdv.FlatAppearance.BorderSize = 0;
            btnAdv.BackColor = C_BTN;
            btnAdv.ForeColor = Color.White;
            btnAdv.Cursor = Cursors.Hand;
            btnAdv.Click += (s, e) => { new MainForm().ShowDialog(this); };
            header.Controls.Add(btnAdv);

            center = new Panel();
            center.Dock = DockStyle.Fill;
            center.BackColor = C_BG;
            this.Controls.Add(center);

            Button btnExtract = MakeBigButton(
                "EXTRACT",
                "Pull assets out of the game\ninto editable formats:\n\ntextures (DDS), audio (OGG),\nvideo (BK2), scripts, UI...",
                C_INFO);
            btnExtract.Click += (s, e) => { new WizardExtract(gamePath, outputPath).ShowDialog(this); };
            center.Controls.Add(btnExtract);

            Button btnRepack = MakeBigButton(
                "MOD & REPACK",
                "Push your edited files\nback into the game,\nor build a mod package\nto share on Nexus / ModDB",
                C_MAGENTA);
            btnRepack.Click += (s, e) => { new WizardRepack(gamePath).ShowDialog(this); };
            center.Controls.Add(btnRepack);

            center.Resize += (s, e) => LayoutButtons();
            center.PerformLayout();

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 190;
            bottom.BackColor = C_HEAD;
            this.Controls.Add(bottom);

            Label sysCheck = new Label();
            sysCheck.Text = "TOOL CHECK";
            sysCheck.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            sysCheck.ForeColor = Color.FromArgb(220, 80, 220);
            sysCheck.Location = new Point(40, 8);
            sysCheck.AutoSize = true;
            bottom.Controls.Add(sysCheck);

            statusGame = new Label();
            statusGame.AutoSize = true;
            statusGame.Location = new Point(40, 32);
            statusGame.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusGame);

            statusOodle = new Label();
            statusOodle.AutoSize = true;
            statusOodle.Location = new Point(40, 56);
            statusOodle.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusOodle);

            statusOutput = new Label();
            statusOutput.AutoSize = true;
            statusOutput.Location = new Point(40, 80);
            statusOutput.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusOutput);

            statusArcs = new Label();
            statusArcs.AutoSize = true;
            statusArcs.Location = new Point(40, 104);
            statusArcs.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusArcs);

            statusTools = new Label();
            statusTools.AutoSize = true;
            statusTools.Location = new Point(40, 128);
            statusTools.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusTools);

            RoundedButton btnGame = new RoundedButton();
            btnGame.Text = "Browse RAGE2.exe location";
            btnGame.CornerRadius = 10; btnGame.BorderColor = Color.FromArgb(90, 90, 110); btnGame.BorderThickness = 1;
            btnGame.Location = new Point(600, 30);
            btnGame.Size = new Size(220, 38);
            btnGame.FlatStyle = FlatStyle.Flat;
            btnGame.FlatAppearance.BorderSize = 0;
            btnGame.BackColor = C_BTN;
            btnGame.ForeColor = Color.White;
            btnGame.Font = new Font("Segoe UI", 10);
            btnGame.Cursor = Cursors.Hand;
            btnGame.Click += (s, e) => BrowseGameFolder();
            bottom.Controls.Add(btnGame);

            RoundedButton btnOutput = new RoundedButton();
            btnOutput.Text = "Select output folder";
            btnOutput.CornerRadius = 10; btnOutput.BorderColor = Color.FromArgb(90, 90, 110); btnOutput.BorderThickness = 1;
            btnOutput.Location = new Point(600, 76);
            btnOutput.Size = new Size(220, 38);
            btnOutput.FlatStyle = FlatStyle.Flat;
            btnOutput.FlatAppearance.BorderSize = 0;
            btnOutput.BackColor = C_BTN;
            btnOutput.ForeColor = Color.White;
            btnOutput.Font = new Font("Segoe UI", 10);
            btnOutput.Cursor = Cursors.Hand;
            btnOutput.Click += (s, e) => BrowseOutputFolder();
            bottom.Controls.Add(btnOutput);


            RoundedButton btnOpenOut = new RoundedButton();
            btnOpenOut.Text = "Open output folder";
            btnOpenOut.CornerRadius = 10; btnOpenOut.BorderColor = Color.FromArgb(90, 90, 110); btnOpenOut.BorderThickness = 1;
            btnOpenOut.Location = new Point(840, 30);
            btnOpenOut.Size = new Size(140, 38);
            btnOpenOut.FlatStyle = FlatStyle.Flat;
            btnOpenOut.FlatAppearance.BorderSize = 0;
            btnOpenOut.BackColor = C_BTN;
            btnOpenOut.ForeColor = Color.White;
            btnOpenOut.Font = new Font("Segoe UI", 10);
            btnOpenOut.Cursor = Cursors.Hand;
            btnOpenOut.Click += (s, e) => {
                if (outputPath != null && Directory.Exists(outputPath)) Process.Start("explorer.exe", outputPath);
                else MessageBox.Show("Output folder not set yet.", "Nothing to open", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            bottom.Controls.Add(btnOpenOut);

            RefreshStatus();
        }

        void BrowseGameFolder()
        {
            using (var dlg = new FolderBrowserDialog()) {
                dlg.Description = "Select the folder where RAGE2.exe is located";
                if (gamePath != null && Directory.Exists(gamePath)) dlg.SelectedPath = gamePath;
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (!File.Exists(Path.Combine(dlg.SelectedPath, "RAGE2.exe"))) {
                    MessageBox.Show("RAGE2.exe was not found in that folder.\n\nPlease select the game's root folder.", "Not a RAGE 2 folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                gamePath = dlg.SelectedPath;
                string target = Path.Combine(Paths.BinDir, "oo2core_7_win64.dll");
                if (!File.Exists(target)) {
                    string direct = Path.Combine(gamePath, "oo2core_7_win64.dll");
                    if (File.Exists(direct)) File.Copy(direct, target, true);
                    else {
                        try {
                            var r = Directory.GetFiles(gamePath, "oo2core_7_win64.dll", SearchOption.AllDirectories);
                            if (r.Length > 0) File.Copy(r[0], target, true);
                        } catch { }
                    }
                }
                SaveConfig();
                RefreshStatus();
            }
        }

        void BrowseOutputFolder()
        {
            using (var dlg = new FolderBrowserDialog()) {
                dlg.Description = "Select the folder where extracted assets will be stored (~25 GB free)";
                if (outputPath != null && Directory.Exists(outputPath)) dlg.SelectedPath = outputPath;
                if (dlg.ShowDialog() != DialogResult.OK) return;
                outputPath = dlg.SelectedPath;
                SaveConfig();
                RefreshStatus();
            }
        }

        void LayoutButtons()
        {
            if (center == null || center.Controls.Count < 2) return;
            var b1 = center.Controls[0] as Button;
            var b2 = center.Controls[1] as Button;
            if (b1 == null || b2 == null) return;

            int gap = 40;
            int maxBw = 460;
            int bw = Math.Min(maxBw, (center.Width - 3 * gap) / 2);
            int bh = Math.Min(280, center.Height - 80);
            int total = bw * 2 + gap;
            int x = (center.Width - total) / 2;
            int y = Math.Max(20, (center.Height - bh) / 2 - 10);

            b1.Size = new Size(bw, bh);
            b1.Location = new Point(x, y);
            b2.Size = new Size(bw, bh);
            b2.Location = new Point(x + bw + gap, y);
        }

        void RefreshStatus()
        {
            if (statusGame != null) {
                if (gamePath != null && gamePath.Length > 0 && Directory.Exists(gamePath)) {
                    if (File.Exists(Path.Combine(gamePath, "RAGE2.exe"))) {
                        statusGame.Text = "\u2713  Game folder: " + gamePath;
                        statusGame.ForeColor = C_OK;
                    } else {
                        statusGame.Text = "\u26a0  Game folder set, but RAGE2.exe not found inside";
                        statusGame.ForeColor = C_ERR;
                    }
                } else {
                    statusGame.Text = "\u2717  Game folder not set";
                    statusGame.ForeColor = C_ERR;
                }
            }

            if (statusOodle != null) {
                string dll = Path.Combine(Paths.BinDir, "oo2core_7_win64.dll");
                if (File.Exists(dll)) {
                    long sz = 0;
                    try { sz = new FileInfo(dll).Length; } catch { }
                    if (sz > 900 * 1024) {
                        statusOodle.Text = "\u2713  Oodle runtime (oo2core_7_win64.dll): OK  (" + (sz / 1024) + " KB)";
                        statusOodle.ForeColor = C_OK;
                    } else {
                        statusOodle.Text = "\u26a0  Oodle DLL found but suspiciously small (" + sz + " bytes)";
                        statusOodle.ForeColor = C_ERR;
                    }
                } else {
                    statusOodle.Text = "\u2717  Oodle runtime MISSING  -  set the game folder so it can be copied automatically";
                    statusOodle.ForeColor = C_ERR;
                }
            }

            if (statusOutput != null) {
                if (outputPath != null && outputPath.Length > 0 && Directory.Exists(outputPath)) {
                    string free = "";
                    try {
                        var drive = new DriveInfo(Path.GetPathRoot(outputPath));
                        free = "  (" + (drive.AvailableFreeSpace / 1073741824L) + " GB free)";
                        if (drive.AvailableFreeSpace < 25L * 1073741824L) {
                            statusOutput.Text = "\u26a0  Output: " + outputPath + free + "  -  less than 25 GB, extraction may fail";
                            statusOutput.ForeColor = C_WARN;
                            goto afterOutput;
                        }
                    } catch { }
                    statusOutput.Text = "\u2713  Output folder: " + outputPath + free;
                    statusOutput.ForeColor = C_OK;
                } else {
                    statusOutput.Text = "\u2717  Output folder not set";
                    statusOutput.ForeColor = C_ERR;
                }
            }
        afterOutput:

            if (statusArcs != null) {
                if (gamePath != null && Directory.Exists(gamePath)) {
                    string dirI = Path.Combine(gamePath, "archives_win64", "initial");
                    string dirS = Path.Combine(gamePath, "archives_win64", "supplemental");
                    if (Directory.Exists(dirI) || Directory.Exists(dirS)) {
                        int arcCount = 0, tabCount = 0;
                        try {
                            if (Directory.Exists(dirI)) { arcCount += Directory.GetFiles(dirI, "game*.arc").Length; tabCount += Directory.GetFiles(dirI, "game*.tab").Length; }
                            if (Directory.Exists(dirS)) { arcCount += Directory.GetFiles(dirS, "game*.arc").Length; tabCount += Directory.GetFiles(dirS, "game*.tab").Length; }
                        } catch { }
                        if (arcCount > 0 && arcCount == tabCount) {
                            statusArcs.Text = "\u2713  Game archives: " + arcCount + " .arc files detected";
                            statusArcs.ForeColor = C_OK;
                        } else if (arcCount > 0) {
                            statusArcs.Text = "\u26a0  Game archives: " + arcCount + " .arc but " + tabCount + " .tab  -  mismatched";
                            statusArcs.ForeColor = C_WARN;
                        } else {
                            statusArcs.Text = "\u2717  Game archives: no .arc files found";
                            statusArcs.ForeColor = C_ERR;
                        }
                    } else {
                        statusArcs.Text = "\u2717  Game archives: archives_win64\\initial nor supplemental not found";
                        statusArcs.ForeColor = C_ERR;
                    }
                } else {
                    statusArcs.Text = "\u2717  Game archives: unknown (game folder not set)";
                    statusArcs.ForeColor = C_ERR;
                }
            }

            if (statusTools != null) {
                var missing = new System.Collections.Generic.List<string>();
                string[] tools = { "ddscConvert.exe", "ddscConvert.exe.config", "texconv.exe", "R2SmallArchive.exe" };
                foreach (var t in tools) {
                    if (!File.Exists(Path.Combine(Paths.BinDir, t))) missing.Add(t);
                }
                if (missing.Count == 0) {
                    statusTools.Text = "\u2713  Bundled tools: ddscConvert, texconv, R2SmallArchive  -  all present";
                    statusTools.ForeColor = C_OK;
                } else {
                    statusTools.Text = "\u2717  Bundled tools missing: " + string.Join(", ", missing.ToArray());
                    statusTools.ForeColor = C_ERR;
                }
            }
        }

        Button MakeBigButton(string title, string desc, Color accent)
        {
            RoundedButton b = new RoundedButton();
            b.CornerRadius = 20;
            b.BorderColor = accent;
            b.BorderThickness = 2;
            b.BackColor = C_PANEL;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.TextAlign = ContentAlignment.MiddleCenter;
            b.Font = new Font("Segoe UI", 12);
            b.Text = title + "\n\n" + desc;

            Color normal = C_PANEL;
            Color hover = Color.FromArgb(45, 45, 55);
            b.MouseEnter += (s, e) => { b.BackColor = hover; b.Invalidate(); };
            b.MouseLeave += (s, e) => { b.BackColor = normal; b.Invalidate(); };
            return b;
        }
    }

    // ============================================================
    // WIZARD BASE
    // ============================================================
    public abstract class WizardBase : Form
    {
        protected readonly Color C_BG = Color.FromArgb(24, 24, 30);
        protected readonly Color C_PANEL = Color.FromArgb(30, 30, 38);
        protected readonly Color C_HEAD = Color.FromArgb(15, 15, 20);
        protected readonly Color C_INFO = Color.FromArgb(0, 200, 200);
        protected readonly Color C_OK = Color.FromArgb(100, 220, 100);
        protected readonly Color C_WARN = Color.FromArgb(230, 180, 74);
        protected readonly Color C_ERR = Color.FromArgb(240, 100, 100);
        protected readonly Color C_GRAY = Color.FromArgb(160, 160, 160);
        protected readonly Color C_BTN = Color.FromArgb(50, 50, 60);
        protected readonly Color C_MAGENTA = Color.FromArgb(220, 80, 220);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

        protected Panel contentHost;
        protected Label stepLbl;
        protected ProgressBar stepBar;
        protected Button btnBack, btnNext, btnCancel;
        protected int currentStep = 0;
        protected List<Panel> stepPanels = new List<Panel>();

        public WizardBase(string title, int totalSteps)
        {
            this.Text = title + "  -  RAGE 2 Modding Toolkit v1.1";
            this.ClientSize = new Size(1000, 700);
            this.MinimumSize = new Size(820, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.Icon = SystemIcons.Application;
            this.HandleCreated += (s, e) => {
                try { int v = 1; DwmSetWindowAttribute(this.Handle, 20, ref v, 4); DwmSetWindowAttribute(this.Handle, 19, ref v, 4); } catch { }
            };

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 80;
            header.BackColor = C_HEAD;
            this.Controls.Add(header);

            Label t = new Label();
            t.Text = title.ToUpper();
            t.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            t.ForeColor = C_INFO;
            t.Location = new Point(30, 12);
            t.AutoSize = true;
            header.Controls.Add(t);

            stepLbl = new Label();
            stepLbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            stepLbl.ForeColor = C_GRAY;
            stepLbl.Location = new Point(32, 48);
            stepLbl.AutoSize = true;
            header.Controls.Add(stepLbl);

            stepBar = new ProgressBar();
            stepBar.Location = new Point(this.ClientSize.Width - 260, 40);
            stepBar.Size = new Size(220, 12);
            stepBar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            stepBar.Maximum = totalSteps;
            stepBar.Value = 1;
            stepBar.Style = ProgressBarStyle.Continuous;
            header.Controls.Add(stepBar);

            contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.BackColor = C_BG;
            contentHost.Padding = new Padding(30);
            this.Controls.Add(contentHost);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 70;
            bottom.BackColor = C_HEAD;
            this.Controls.Add(bottom);

            btnCancel = MakeBtn("Cancel", C_BTN);
            btnCancel.Location = new Point(30, 18);
            btnCancel.Click += (s, e) => { if (MessageBox.Show("Exit the wizard?", "Cancel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) this.Close(); };
            bottom.Controls.Add(btnCancel);

            btnBack = MakeBtn("< Back", C_BTN);
            btnBack.Location = new Point(this.ClientSize.Width - 340, 18);
            btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBack.Click += (s, e) => GotoStep(currentStep - 1);
            bottom.Controls.Add(btnBack);

            btnNext = MakeBtn("Next >", C_INFO);
            btnNext.Location = new Point(this.ClientSize.Width - 200, 18);
            btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNext.ForeColor = Color.Black;
            btnNext.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnNext.Click += (s, e) => OnNextClicked();
            bottom.Controls.Add(btnNext);

            this.Load += (s, e) => GotoStep(0);
        }

        protected Button MakeBtn(string text, Color color)
        {
            RoundedButton b = new RoundedButton();
            b.Text = text;
            b.Size = new Size(140, 34);
            b.CornerRadius = 10;
            b.BorderColor = Color.FromArgb(90, 90, 110);
            b.BorderThickness = 1;
            b.BackColor = color;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 10);
            b.Cursor = Cursors.Hand;
            return b;
        }

        protected Panel MakeStepPanel()
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Fill;
            p.BackColor = C_BG;
            p.Visible = false;
            contentHost.Controls.Add(p);
            stepPanels.Add(p);
            return p;
        }

        protected Label MakeTitle(string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            l.ForeColor = Color.White;
            l.Location = new Point(x, y);
            l.AutoSize = true;
            return l;
        }

        protected Label MakeSub(string text, int x, int y, int w = -1)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = new Font("Segoe UI", 10);
            l.ForeColor = C_GRAY;
            l.Location = new Point(x, y);
            if (w > 0) l.Size = new Size(w, 60);
            else l.AutoSize = true;
            return l;
        }

        protected void GotoStep(int n)
        {
            if (n < 0 || n >= stepPanels.Count) return;
            foreach (var p in stepPanels) p.Visible = false;
            stepPanels[n].Visible = true;
            stepPanels[n].BringToFront();
            currentStep = n;
            stepLbl.Text = "Step " + (n + 1) + " of " + stepPanels.Count;
            stepBar.Value = n + 1;
            btnBack.Enabled = n > 0;
            btnNext.Text = (n == stepPanels.Count - 1) ? "Finish" : "Next >";
            OnStepShown(n);
        }

        protected virtual void OnStepShown(int n) { }
        protected abstract void OnNextClicked();
    }

    // ============================================================
    // WIZARD EXTRACT
    // ============================================================
    public class WizardExtract : WizardBase
    {
        string gamePath, outputPath;
        CheckBox cbAll, cbTex, cbAudio, cbVideo, cbScripts, cbUI;
        Label lblOutput;
        RichTextBox logBox;
        ProgressBar progress;
        bool completed = false;
        int extractedCount = 0, ddsCount = 0, audioCount = 0, videoCount = 0, otherCount = 0;

        public WizardExtract(string gamePath, string outputPath)
            : base("Wizard: Extract assets", 4)
        {
            this.gamePath = gamePath;
            this.outputPath = outputPath;
            BuildStep1();
            BuildStep2();
            BuildStep3();
            BuildStep4();
        }

        void BuildStep1()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("What do you want to extract?", 0, 10));
            p.Controls.Add(MakeSub("Pick what interests you. If unsure, leave \"Everything\" checked.", 0, 50, 900));

            cbAll = new CheckBox();
            cbAll.Text = "Everything (recommended)";
            cbAll.ForeColor = Color.White;
            cbAll.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            cbAll.Location = new Point(20, 110);
            cbAll.AutoSize = true;
            cbAll.Checked = true;
            cbAll.CheckedChanged += (s, e) => { if (cbAll.Checked) SetSubs(true); else SetSubs(false); };
            p.Controls.Add(cbAll);

            cbTex = MakeSubCheck("Textures (editable DDS)", 60, 160);
            cbAudio = MakeSubCheck("Audio / SFX (OGG, RIFF)", 60, 195);
            cbVideo = MakeSubCheck("Video (BK2, BIK)", 60, 230);
            cbScripts = MakeSubCheck("Scripts and data (ADF, BL, EE...)", 60, 265);
            cbUI = MakeSubCheck("UI (GFX, CFX)", 60, 300);

            Label warn = new Label();
            warn.Text = "\u2139  Extracting everything uses about 25 GB and takes roughly 30 seconds.";
            warn.ForeColor = C_WARN;
            warn.Location = new Point(20, 360);
            warn.AutoSize = true;
            p.Controls.Add(warn);
        }

        CheckBox MakeSubCheck(string text, int x, int y)
        {
            CheckBox cb = new CheckBox();
            cb.Text = text;
            cb.ForeColor = Color.White;
            cb.Location = new Point(x, y);
            cb.AutoSize = true;
            cb.Checked = true;
            cb.Enabled = false;
            return cb;
        }

        void SetSubs(bool enabled)
        {
            cbTex.Enabled = !enabled; cbTex.Checked = enabled;
            cbAudio.Enabled = !enabled; cbAudio.Checked = enabled;
            cbVideo.Enabled = !enabled; cbVideo.Checked = enabled;
            cbScripts.Enabled = !enabled; cbScripts.Checked = enabled;
            cbUI.Enabled = !enabled; cbUI.Checked = enabled;
        }

        void BuildStep2()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Where should we save it?", 0, 10));
            p.Controls.Add(MakeSub("You need about 25 GB free if extracting everything.", 0, 50, 900));

            lblOutput = new Label();
            lblOutput.Font = new Font("Consolas", 10);
            lblOutput.ForeColor = C_INFO;
            lblOutput.Location = new Point(20, 120);
            lblOutput.Size = new Size(700, 30);
            lblOutput.Text = outputPath != null && outputPath.Length > 0 ? outputPath : "(not set)";
            p.Controls.Add(lblOutput);

            Button btnPick = MakeBtn("Choose folder...", C_BTN);
            btnPick.Location = new Point(20, 160);
            btnPick.Size = new Size(200, 36);
            btnPick.Click += (s, e) => {
                using (var dlg = new FolderBrowserDialog()) {
                    dlg.Description = "Folder where the extracted assets will be saved";
                    if (outputPath != null && Directory.Exists(outputPath)) dlg.SelectedPath = outputPath;
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    outputPath = dlg.SelectedPath;
                    lblOutput.Text = outputPath;
                }
            };
            p.Controls.Add(btnPick);

            Button btnUsePrev = MakeBtn("Use toolkit default", C_BTN);
            btnUsePrev.Location = new Point(230, 160);
            btnUsePrev.Size = new Size(200, 36);
            btnUsePrev.Click += (s, e) => {
                try {
                    if (File.Exists(Paths.ConfigFile)) {
                        var lines = File.ReadAllLines(Paths.ConfigFile);
                        if (lines.Length > 1 && lines[1].Length > 0 && Directory.Exists(lines[1])) {
                            outputPath = lines[1];
                            lblOutput.Text = outputPath;
                        }
                    }
                } catch { }
            };
            p.Controls.Add(btnUsePrev);
        }

        void BuildStep3()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Extracting...", 0, 10));
            p.Controls.Add(MakeSub("Do not close this window. This can take 30-60 seconds.", 0, 50, 900));

            progress = new ProgressBar();
            progress.Location = new Point(20, 120);
            progress.Size = new Size(900, 24);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Style = ProgressBarStyle.Marquee;
            p.Controls.Add(progress);

            logBox = new RichTextBox();
            logBox.Location = new Point(20, 165);
            logBox.Size = new Size(900, 380);
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            logBox.BackColor = C_HEAD;
            logBox.ForeColor = Color.White;
            logBox.Font = new Font("Consolas", 9);
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            p.Controls.Add(logBox);
        }

        void BuildStep4()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Done!", 0, 10));
            var lblStats = new Label();
            lblStats.Name = "lblStats";
            lblStats.Font = new Font("Segoe UI", 11);
            lblStats.ForeColor = Color.White;
            lblStats.Location = new Point(20, 60);
            lblStats.Size = new Size(900, 400);
            p.Controls.Add(lblStats);

            Button btnOpen = MakeBtn("Open folder", C_INFO);
            btnOpen.ForeColor = Color.Black;
            btnOpen.Location = new Point(20, 500);
            btnOpen.Size = new Size(200, 44);
            btnOpen.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnOpen.Click += (s, e) => {
                if (outputPath != null && Directory.Exists(outputPath))
                    Process.Start("explorer.exe", outputPath);
            };
            p.Controls.Add(btnOpen);
        }

        protected override void OnNextClicked()
        {
            if (currentStep == 0) { GotoStep(1); return; }
            if (currentStep == 1) {
                if (outputPath == null || outputPath.Length == 0 || !Directory.Exists(outputPath)) {
                    MessageBox.Show("Please choose an output folder first.", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (gamePath == null || !Directory.Exists(gamePath)) {
                    MessageBox.Show("Please set the game folder first.", "Missing game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GotoStep(2);
                StartExtract();
                return;
            }
            if (currentStep == 2) {
                if (!completed) {
                    MessageBox.Show("Please wait for the extraction to finish.", "In progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GotoStep(3);
                return;
            }
            this.Close();
        }

        void StartExtract()
        {
            Task.Run(() =>
            {
                try
                {
                    Log("Starting extraction...");
                    Directory.CreateDirectory(outputPath);
                    Extractor.ExtractAll(gamePath, outputPath, msg => Log(msg));

                    Log("");
                    Log("Classifying and converting textures...");
                    ClassifyAndConvert();

                    this.Invoke(new Action(() => {
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Value = 100;
                        Log("");
                        Log("\u2713 Extraction complete.");
                        completed = true;
                        ShowStats();
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => {
                        Log("\u2717 ERROR: " + ex.Message);
                        MessageBox.Show("Error: " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        void ClassifyAndConvert()
        {
            string[] sub = { "TEXTURES", "AUDIO", "VIDEO", "SCRIPTS", "UI", "OTHER" };
            string root = Path.Combine(outputPath, "_EDITABLE");
            foreach (var s in sub) Directory.CreateDirectory(Path.Combine(root, s));

            string ddsc = Path.Combine(Paths.BinDir, "ddscConvert.exe");

            var all = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("_EDITABLE")).ToList();

            int total = all.Count;
            int i = 0;
            foreach (var f in all)
            {
                i++;
                string ext = Path.GetExtension(f).ToLowerInvariant();
                string bn = Path.GetFileName(f);
                string target = null;

                try
                {
                    if (ext == ".avtx" && File.Exists(ddsc))
                    {
                        try {
                            var psi = new ProcessStartInfo(ddsc, "\"" + f + "\"");
                            psi.UseShellExecute = false;
                            psi.CreateNoWindow = true;
                            psi.WorkingDirectory = Paths.BinDir;
                            var p = Process.Start(psi);
                            p.WaitForExit();
                            string dds = Path.ChangeExtension(f, ".dds");
                            if (File.Exists(dds)) {
                                target = Path.Combine(root, "TEXTURES", Path.GetFileName(dds));
                                File.Copy(dds, target, true);
                                File.Delete(dds);
                                ddsCount++;
                            }
                        } catch { }
                        File.Delete(f);
                        continue;
                    }

                    if (ext == ".dds") {
                        target = Path.Combine(root, "TEXTURES", bn); ddsCount++;
                    }
                    else if (ext == ".ogg" || ext == ".riff" || ext == ".rtpc" || ext == ".fsb") {
                        target = Path.Combine(root, "AUDIO", bn); audioCount++;
                    }
                    else if (ext == ".bik" || ext == ".bk2") {
                        target = Path.Combine(root, "VIDEO", bn); videoCount++;
                    }
                    else if (ext == ".gfx" || ext == ".cfx") {
                        target = Path.Combine(root, "UI", bn); otherCount++;
                    }
                    else if (ext == ".adf" || ext == ".bl" || ext == ".ee" || ext == ".nl" || ext == ".fl" || ext == ".tag") {
                        target = Path.Combine(root, "SCRIPTS", bn); otherCount++;
                    }
                    else {
                        target = Path.Combine(root, "OTHER", bn); otherCount++;
                    }

                    if (target != null && !File.Exists(target))
                        File.Move(f, target);
                }
                catch { }

                if (i % 200 == 0) {
                    int idx = i;
                    this.Invoke(new Action(() => Log("  ... " + idx + " / " + total)));
                }
            }

            extractedCount = total;
        }

        void ShowStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Processed " + extractedCount.ToString("N0") + " files.\n");
            sb.AppendLine("\u2713 " + ddsCount.ToString("N0") + "  textures in .dds format (editable with Paint.NET / Photoshop / GIMP)");
            sb.AppendLine("\u2713 " + audioCount.ToString("N0") + "  audio files in .ogg / .riff");
            sb.AppendLine("\u2713 " + videoCount.ToString("N0") + "  videos in .bik / .bk2 (open with RAD Video Tools)");
            sb.AppendLine("\u2713 " + otherCount.ToString("N0") + "  scripts, UI and other data");
            sb.AppendLine();
            sb.AppendLine("Organized in:");
            sb.AppendLine("  " + Path.Combine(outputPath, "_EDITABLE") + "\\");
            sb.AppendLine("    TEXTURES\\   AUDIO\\   VIDEO\\   SCRIPTS\\   UI\\   OTHER\\");
            sb.AppendLine();
            sb.AppendLine("When done editing, go back to the main screen and use MOD & REPACK");
            sb.AppendLine("to push your changes into the game.");

            var lbl = (Label)stepPanels[3].Controls.Find("lblStats", true)[0];
            lbl.Text = sb.ToString();
        }

        void Log(string msg)
        {
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg))); return; }
            logBox.AppendText(msg + Environment.NewLine);
            logBox.ScrollToCaret();
        }
    }

    // ============================================================
    // WIZARD REPACK
    // ============================================================
    public class WizardRepack : WizardBase
    {
        class PendingFile
        {
            public string Path;
            public ulong Hash;
            public string HashHex;
            public string Ext;
            public string ArcName;
            public string Reason;
            public bool Ok;
        }

        string gamePath;
        string sourceDir;
        List<PendingFile> pending = new List<PendingFile>();
        Dictionary<ulong, string> namesByHash = new Dictionary<ulong, string>();
        Dictionary<ulong, string> arcByHash = new Dictionary<ulong, string>();
        Dictionary<string, string> tabByArc = new Dictionary<string, string>();

        Panel dropZone;
        Label dropLbl;
        ListView previewList;
        RichTextBox logBox;
        ProgressBar progress;
        Label summaryLbl;
        CheckBox cbInstall, cbPackage;
        bool scanDone = false, rebuildDone = false;

        public WizardRepack(string gamePath)
            : base("Wizard: Mod & Repack", 5)
        {
            this.gamePath = gamePath;
            LoadFilelist();
            BuildStep1();
            BuildStep2();
            BuildStep3();
            BuildStep4();
            BuildStep5();
        }

        void LoadFilelist()
        {
            try
            {
                string[] candidates = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "filelist.txt"),
                    Path.Combine(Paths.DataDir, "filelist.txt")
                };
                bool loaded = false;
                foreach (string p in candidates)
                {
                    if (!File.Exists(p)) continue;
                    foreach (string line in File.ReadAllLines(p))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        string[] parts = line.Split(new char[] { '\t' }, 2);
                        if (parts.Length < 2) continue;
                        ulong h;
                        if (!ulong.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out h)) continue;
                        namesByHash[h] = parts[1].Trim();
                    }
                    Log("Filelist: " + namesByHash.Count + " entries loaded from " + p);
                    loaded = true;
                    break;
                }
                if (!loaded) Log("Filelist not found - hashes will be shown without names");
            }
            catch (Exception ex) { Log("Filelist error: " + ex.Message); }
        }

        void LoadArcIndex()
        {
            arcByHash.Clear();
            tabByArc.Clear();
            if (gamePath == null || !Directory.Exists(gamePath)) return;
            string initArcDir = Path.Combine(gamePath, "archives_win64", "initial");
            string suppArcDir = Path.Combine(gamePath, "archives_win64", "supplemental");
            if (!Directory.Exists(initArcDir) && !Directory.Exists(suppArcDir)) return;
            var allTabs = new System.Collections.Generic.List<string>();
            if (Directory.Exists(initArcDir)) allTabs.AddRange(Directory.GetFiles(initArcDir, "game*.tab"));
            if (Directory.Exists(suppArcDir)) allTabs.AddRange(Directory.GetFiles(suppArcDir, "game*.tab"));

            foreach (var tab in allTabs)
            {
                try
                {
                    string arcName = Path.GetFileNameWithoutExtension(tab);
                    tabByArc[arcName] = tab;
                    var t = TabFormat.Tab.Parse(tab);
                    foreach (var e in t.Entries)
                    {
                        if (!arcByHash.ContainsKey(e.Hash))
                            arcByHash[e.Hash] = arcName;
                    }
                }
                catch { }
            }
        }

        void BuildStep1()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Where are your edited files?", 0, 10));
            p.Controls.Add(MakeSub("Drop a folder here (or click to browse). Files must keep their original hash name (e.g. 4E37BD8EAD14BEA2.dds).", 0, 50, 900));

            dropZone = new Panel();
            dropZone.Location = new Point(20, 120);
            dropZone.Size = new Size(900, 280);
            dropZone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dropZone.BackColor = C_PANEL;
            dropZone.BorderStyle = BorderStyle.FixedSingle;
            dropZone.AllowDrop = true;
            p.Controls.Add(dropZone);

            dropLbl = new Label();
            dropLbl.Dock = DockStyle.Fill;
            dropLbl.TextAlign = ContentAlignment.MiddleCenter;
            dropLbl.Font = new Font("Segoe UI", 13);
            dropLbl.ForeColor = C_GRAY;
            dropLbl.Text = "Drop your folder here\n\n(or click to browse)";
            dropZone.Controls.Add(dropLbl);

            dropZone.DragEnter += (s, e) => {
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                    e.Effect = DragDropEffects.Copy;
                    dropZone.BackColor = Color.FromArgb(50, 50, 65);
                    dropLbl.ForeColor = C_INFO;
                }
            };
            dropZone.DragLeave += (s, e) => {
                dropZone.BackColor = C_PANEL;
                dropLbl.ForeColor = C_GRAY;
            };
            dropZone.DragDrop += (s, e) => {
                dropZone.BackColor = C_PANEL;
                dropLbl.ForeColor = C_GRAY;
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0) return;
                string dir = files[0];
                if (Directory.Exists(dir)) { sourceDir = dir; dropLbl.Text = "\u2713 " + dir; dropLbl.ForeColor = C_OK; }
                else if (File.Exists(dir)) { sourceDir = Path.GetDirectoryName(dir); dropLbl.Text = "\u2713 " + sourceDir; dropLbl.ForeColor = C_OK; }
            };
            dropZone.Click += (s, e) => PickFolder();

            Button btnPick = MakeBtn("Or browse manually...", C_BTN);
            btnPick.Location = new Point(20, 420);
            btnPick.Size = new Size(320, 36);
            btnPick.Click += (s, e) => PickFolder();
            p.Controls.Add(btnPick);
        }

        void PickFolder()
        {
            using (var dlg = new FolderBrowserDialog()) {
                dlg.Description = "Folder with your edited files";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                sourceDir = dlg.SelectedPath;
                dropLbl.Text = "\u2713 " + sourceDir;
                dropLbl.ForeColor = C_OK;
            }
        }

        void BuildStep2()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Analyzing files...", 0, 10));
            p.Controls.Add(MakeSub("Checking each file against the game and validating its format.", 0, 50, 900));

            progress = new ProgressBar();
            progress.Location = new Point(20, 120);
            progress.Size = new Size(900, 22);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Style = ProgressBarStyle.Marquee;
            p.Controls.Add(progress);

            logBox = new RichTextBox();
            logBox.Location = new Point(20, 165);
            logBox.Size = new Size(900, 380);
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            logBox.BackColor = C_HEAD;
            logBox.ForeColor = Color.White;
            logBox.Font = new Font("Consolas", 9);
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            p.Controls.Add(logBox);
        }

        void BuildStep3()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Review", 0, 10));
            p.Controls.Add(MakeSub("Green: will be applied. Red: cannot be applied.", 0, 50, 900));

            previewList = new ListView();
            previewList.Location = new Point(20, 100);
            previewList.Size = new Size(900, 400);
            previewList.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            previewList.View = View.Details;
            previewList.FullRowSelect = true;
            previewList.BackColor = Color.FromArgb(20, 20, 26);
            previewList.ForeColor = Color.White;
            previewList.BorderStyle = BorderStyle.FixedSingle;
            previewList.Font = new Font("Consolas", 9);
            previewList.Columns.Add("File", 260);
            previewList.Columns.Add("Hash", 180);
            previewList.Columns.Add("Arc", 80);
            previewList.Columns.Add("Status", 380);
            p.Controls.Add(previewList);

            summaryLbl = new Label();
            summaryLbl.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            summaryLbl.ForeColor = C_OK;
            summaryLbl.Location = new Point(20, 510);
            summaryLbl.AutoSize = true;
            p.Controls.Add(summaryLbl);
        }

        void BuildStep4()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Rebuilding .arc...", 0, 10));
            p.Controls.Add(MakeSub("Do not close this window.", 0, 50, 900));

            progress = new ProgressBar();
            progress.Location = new Point(20, 120);
            progress.Size = new Size(900, 22);
            progress.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            progress.Style = ProgressBarStyle.Marquee;
            p.Controls.Add(progress);

            logBox = new RichTextBox();
            logBox.Location = new Point(20, 165);
            logBox.Size = new Size(900, 380);
            logBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            logBox.BackColor = C_HEAD;
            logBox.ForeColor = Color.White;
            logBox.Font = new Font("Consolas", 9);
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.ScrollBars = RichTextBoxScrollBars.Vertical;
            p.Controls.Add(logBox);
        }

        void BuildStep5()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Done!", 0, 10));
            p.Controls.Add(MakeSub("What should we do with the modified .arc files?", 0, 50, 900));

            cbInstall = new CheckBox();
            cbInstall.Text = "Install into the game (auto-backup .original the first time)";
            cbInstall.ForeColor = Color.White;
            cbInstall.Font = new Font("Segoe UI", 11);
            cbInstall.Location = new Point(30, 120);
            cbInstall.AutoSize = true;
            cbInstall.Checked = true;
            p.Controls.Add(cbInstall);

            cbPackage = new CheckBox();
            cbPackage.Text = "Package for sharing (Nexus / ModDB)";
            cbPackage.ForeColor = Color.White;
            cbPackage.Font = new Font("Segoe UI", 11);
            cbPackage.Location = new Point(30, 165);
            cbPackage.AutoSize = true;
            cbPackage.Checked = false;
            p.Controls.Add(cbPackage);

            Label note = new Label();
            note.Text = "\u2139 The package includes the .arc, the .tab, a README and an install.bat.";
            note.ForeColor = C_WARN;
            note.Location = new Point(30, 210);
            note.AutoSize = true;
            p.Controls.Add(note);
        }

        protected override void OnNextClicked()
        {
            if (currentStep == 0) {
                if (sourceDir == null || !Directory.Exists(sourceDir)) {
                    MessageBox.Show("Drop or choose a folder first.", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GotoStep(1);
                StartScan();
                return;
            }
            if (currentStep == 1) {
                if (!scanDone) {
                    MessageBox.Show("Please wait for the analysis to finish.", "In progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GotoStep(2);
                return;
            }
            if (currentStep == 2) {
                var okFiles = pending.Where(f => f.Ok).ToList();
                if (okFiles.Count == 0) {
                    MessageBox.Show("No valid files to apply.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GotoStep(3);
                StartRebuild();
                return;
            }
            if (currentStep == 3) {
                if (!rebuildDone) {
                    MessageBox.Show("Please wait for the rebuild to finish.", "In progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GotoStep(4);
                return;
            }
            ApplyFinalActions();
            this.Close();
        }

        void StartScan()
        {
            Task.Run(() =>
            {
                try
                {
                    Log("Loading .arc index...");
                    LoadArcIndex();
                    Log("  " + arcByHash.Count + " hashes indexed from " + tabByArc.Count + " .arc files\n");

                    var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                    Log("Analyzing " + files.Length + " files...\n");

                    pending.Clear();
                    foreach (var f in files)
                    {
                        var pf = new PendingFile();
                        pf.Path = f;
                        pf.Ext = Path.GetExtension(f).ToLowerInvariant();
                        string bn = Path.GetFileNameWithoutExtension(f);

                        if (bn.Length != 16 || !System.Text.RegularExpressions.Regex.IsMatch(bn, "^[0-9A-Fa-f]{16}$"))
                        {
                            pf.Ok = false;
                            pf.Reason = "Filename is not a 16-char hash";
                            pending.Add(pf);
                            continue;
                        }

                        pf.HashHex = bn.ToUpperInvariant();
                        pf.Hash = ulong.Parse(bn, System.Globalization.NumberStyles.HexNumber);

                        if (!arcByHash.ContainsKey(pf.Hash))
                        {
                            pf.Ok = false;
                            pf.Reason = "This hash does not exist in any .arc";
                            pending.Add(pf);
                            continue;
                        }

                        pf.ArcName = arcByHash[pf.Hash];

                        if (pf.Ext == ".dds" || pf.Ext == ".avtx" || pf.Ext == ".ogg" || pf.Ext == ".riff"
                            || pf.Ext == ".bik" || pf.Ext == ".bk2" || pf.Ext == ".gfx" || pf.Ext == ".adf")
                        {
                            pf.Ok = true;
                            pf.Reason = "OK";
                            if (pf.Ext == ".dds") pf.Reason = "DDS \u2192 AVTX (auto-convert)";
                        }
                        else if (pf.Ext == ".png" || pf.Ext == ".jpg" || pf.Ext == ".jpeg" || pf.Ext == ".tga" || pf.Ext == ".bmp")
                        {
                            pf.Ok = false;
                            pf.Reason = pf.Ext + " is not a valid format. Save as DDS first";
                        }
                        else if (pf.Ext == ".wav")
                        {
                            pf.Ok = false;
                            pf.Reason = "The game uses OGG, not WAV. Convert first";
                        }
                        else
                        {
                            pf.Ok = true;
                            pf.Reason = "OK (no conversion)";
                        }

                        pending.Add(pf);
                        Log("  " + pf.HashHex + "  " + pf.Ext.PadRight(6) + "  " + pf.ArcName + "  " + pf.Reason);
                    }

                    this.Invoke(new Action(() => {
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Value = 100;
                        PopulatePreview();
                        scanDone = true;
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => {
                        Log("\u2717 " + ex.Message);
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        void PopulatePreview()
        {
            previewList.Items.Clear();
            int ok = 0, bad = 0;
            foreach (var f in pending)
            {
                var item = new ListViewItem(new[] {
                    Path.GetFileName(f.Path),
                    f.HashHex ?? "-",
                    f.ArcName ?? "-",
                    f.Reason ?? ""
                });
                if (f.Ok) { item.ForeColor = C_OK; ok++; }
                else { item.ForeColor = C_ERR; bad++; }
                previewList.Items.Add(item);
            }
            summaryLbl.Text = "\u2713 " + ok + " will be applied    \u2717 " + bad + " rejected";
            if (bad > 0)
                summaryLbl.Text += "   (check that red files are in the correct format)";
        }

        void StartRebuild()
        {
            Task.Run(() =>
            {
                try
                {
                    Log("Preparing files...\n");
                    var ddsFiles = pending.Where(f => f.Ok).ToList();
                    var byArc = ddsFiles.GroupBy(f => f.ArcName);

                    string ddsc = Path.Combine(Paths.BinDir, "ddscConvert.exe");
                    string texconv = Path.Combine(Paths.BinDir, "texconv.exe");
                    string workDir = Path.Combine(Path.GetTempPath(), "RAGE2Repack");
                    Directory.CreateDirectory(workDir);

                    foreach (var grp in byArc)
                    {
                        string arcName = grp.Key;
                        Log("=== " + arcName + " ===");

                        var repl = new Dictionary<ulong, Repacker.Replacement>();
                        foreach (var f in grp)
                        {
                            byte[] payload;
                            if (f.Ext == ".dds" && File.Exists(ddsc))
                            {
                                Log("  Converting " + Path.GetFileName(f.Path) + " to AVTX...");
                                if (File.Exists(texconv))
                                {
                                    var mipDir = Path.Combine(workDir, "mip_" + f.HashHex);
                                    Directory.CreateDirectory(mipDir);
                                    RunTool(texconv, "-m 0 -y -o \"" + mipDir + "\" \"" + f.Path + "\"");
                                    var mip = Path.Combine(mipDir, Path.GetFileName(f.Path));
                                    if (File.Exists(mip)) {
                                        RunTool(ddsc, "\"" + mip + "\"");
                                        var avtx = Path.ChangeExtension(mip, ".avtx");
                                        if (File.Exists(avtx)) payload = File.ReadAllBytes(avtx);
                                        else { Log("    \u2717 ddsc failed"); continue; }
                                    } else { Log("    \u2717 texconv failed"); continue; }
                                }
                                else
                                {
                                    RunTool(ddsc, "\"" + f.Path + "\"");
                                    var avtx = Path.ChangeExtension(f.Path, ".avtx");
                                    if (File.Exists(avtx)) payload = File.ReadAllBytes(avtx);
                                    else { Log("    \u2717 ddsc failed"); continue; }
                                }
                            }
                            else
                            {
                                payload = File.ReadAllBytes(f.Path);
                            }

                            repl[f.Hash] = new Repacker.Replacement { Data = payload };
                            Log("  " + f.HashHex + " ready (" + payload.Length + " bytes)");
                        }

                        string tabPath = tabByArc[arcName];
                        string arcPath = Path.ChangeExtension(tabPath, ".arc");
                        string outDir = Path.Combine(Path.GetDirectoryName(arcPath), "MOD_" + arcName);
                        Directory.CreateDirectory(outDir);
                        string outTab = Path.Combine(outDir, arcName + "_mod.tab");
                        string outArc = Path.Combine(outDir, arcName + "_mod.arc");

                        Log("  Rebuilding...");
                        Repacker.Rebuild(tabPath, arcPath, repl, outTab, outArc, m => Log("    " + m));
                        Log("  \u2713 " + outArc);
                        Log("");
                    }

                    this.Invoke(new Action(() => {
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Value = 100;
                        rebuildDone = true;
                        Log("\u2713 Rebuild complete.");
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => {
                        Log("\u2717 " + ex.Message);
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        void RunTool(string exe, string args)
        {
            try {
                var psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                var p = Process.Start(psi);
                p.WaitForExit();
            } catch { }
        }

        void ApplyFinalActions()
        {
            try
            {
                var byArc = pending.Where(f => f.Ok).GroupBy(f => f.ArcName);
                foreach (var grp in byArc)
                {
                    string arcName = grp.Key;
                    string origTab = tabByArc[arcName];
                    string origArc = Path.ChangeExtension(origTab, ".arc");
                    string dir = Path.GetDirectoryName(origArc);
                    string modDir = Path.Combine(dir, "MOD_" + arcName);
                    string modTab = Path.Combine(modDir, arcName + "_mod.tab");
                    string modArc = Path.Combine(modDir, arcName + "_mod.arc");

                    if (cbInstall.Checked)
                    {
                        string bkTab = origTab + ".original";
                        string bkArc = origArc + ".original";
                        if (!File.Exists(bkTab)) File.Copy(origTab, bkTab, true);
                        if (!File.Exists(bkArc)) File.Copy(origArc, bkArc, true);
                        File.Copy(modTab, origTab, true);
                        File.Copy(modArc, origArc, true);
                    }

                    if (cbPackage.Checked)
                    {
                        File.WriteAllText(Path.Combine(modDir, "README.txt"),
                            "RAGE 2 Mod\r\n\r\nContains:\r\n  " + arcName + "_mod.arc\r\n  " + arcName + "_mod.tab\r\n\r\nHow to install:\r\n  1. Copy both files to the SAME folder the original .arc was extracted from:\r\n     <RAGE 2>\\archives_win64\\initial\\     (initial universe)\r\n     <RAGE 2>\\archives_win64\\supplemental\\ (supplemental universe)\r\n  2. Rename them to remove the '_mod' suffix so they replace the originals\r\n  3. Back up the originals first\r\n\r\nMade with RAGE 2 Modding Toolkit v1.1 by Kry0genik\r\n");
                        File.WriteAllText(Path.Combine(modDir, "install.bat"),
                            "@echo off\r\necho Manually copy the .arc and .tab files into your RAGE 2 game folder.\r\npause\r\n");
                    }
                }

                MessageBox.Show("Done. Launch RAGE 2 to see your changes.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void Log(string msg)
        {
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg))); return; }
            logBox.AppendText(msg + Environment.NewLine);
            logBox.ScrollToCaret();
        }
    }
}




