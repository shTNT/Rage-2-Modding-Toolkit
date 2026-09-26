using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    // ============================================================
    // RoundedButton - boton con esquinas redondeadas + hover suave
    // ============================================================
    // ============================================================
    // RoundedButton - hover suave + pulse opcional
    // ============================================================
    public class RoundedButton : Button
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }
        public int BorderThickness { get; set; }
        public Color HoverColor { get; set; }

        bool _pulseEnabled;
        Color _pulseBase;
        Color _pulseTarget;
        int _pulsePeriodMs = 2400;
        DateTime _pulseStart = DateTime.Now;

        Color _normal;
        Color _current;
        Timer _timer;
        bool _hovering;
        bool _animInit;

        public RoundedButton()
        {
            CornerRadius = 10;
            BorderColor = Color.Transparent;
            BorderThickness = 0;
            HoverColor = Color.Empty;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);

            _timer = new Timer();
            _timer.Interval = 30;
            _timer.Tick += (s, e) => TickAnim();

            MouseEnter += (s, e) => BeginHover(true);
            MouseLeave += (s, e) => BeginHover(false);
            Disposed += (s, e) => { try { _timer.Stop(); _timer.Dispose(); } catch { } };
        }

        public bool PulseEnabled
        {
            get { return _pulseEnabled; }
            set
            {
                if (_pulseEnabled == value) return;
                _pulseEnabled = value;
                if (value)
                {
                    _pulseStart = DateTime.Now;
                    EnsureInit();
                    if (!_timer.Enabled) _timer.Start();
                }
                else
                {
                    if (!_timer.Enabled) _timer.Start();
                }
            }
        }

        public Color PulseBase { get { return _pulseBase; } set { _pulseBase = value; } }
        public Color PulseTarget { get { return _pulseTarget; } set { _pulseTarget = value; } }
        public int PulsePeriodMs
        {
            get { return _pulsePeriodMs; }
            set { if (value >= 400) _pulsePeriodMs = value; }
        }

        void EnsureInit()
        {
            if (_animInit) return;
            _animInit = true;
            _normal = this.BackColor;
            _current = _normal;
        }

        void BeginHover(bool on)
        {
            EnsureInit();
            _hovering = on;
            if (!_timer.Enabled) _timer.Start();
        }

        void TickAnim()
        {
            EnsureInit();
            Color desired;
            if (_hovering)
            {
                desired = HoverColor.IsEmpty ? Lighten(_normal, 24) : HoverColor;
            }
            else if (_pulseEnabled)
            {
                double elapsed = (DateTime.Now - _pulseStart).TotalMilliseconds;
                double phase = (elapsed % _pulsePeriodMs) / (double)_pulsePeriodMs;
                double k = 0.5 - 0.5 * Math.Cos(phase * 2 * Math.PI);
                Color pb = _pulseBase.IsEmpty ? _normal : _pulseBase;
                Color pt = _pulseTarget.IsEmpty ? _normal : _pulseTarget;
                desired = Lerp(pb, pt, k);
            }
            else
            {
                desired = _normal;
            }

            _current = Lerp(_current, desired, 0.20);
            try { this.BackColor = _current; } catch { }

            int diff = Math.Abs(_current.R - desired.R)
                     + Math.Abs(_current.G - desired.G)
                     + Math.Abs(_current.B - desired.B);
            if (!_pulseEnabled && !_hovering && diff < 4)
            {
                _current = desired;
                try { this.BackColor = _current; } catch { }
                _timer.Stop();
            }
            Invalidate();
        }

        static Color Lerp(Color a, Color b, double k)
        {
            int r  = (int)(a.R + (b.R - a.R) * k);
            int g  = (int)(a.G + (b.G - a.G) * k);
            int bl = (int)(a.B + (b.B - a.B) * k);
            if (r  < 0) r  = 0; if (r  > 255) r  = 255;
            if (g  < 0) g  = 0; if (g  > 255) g  = 255;
            if (bl < 0) bl = 0; if (bl > 255) bl = 255;
            return Color.FromArgb(r, g, bl);
        }

        static Color Lighten(Color c, int amount)
        {
            int r = c.R + amount; if (r > 255) r = 255;
            int g = c.G + amount; if (g > 255) g = 255;
            int b = c.B + amount; if (b > 255) b = 255;
            return Color.FromArgb(r, g, b);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color parentBg = (this.Parent != null) ? this.Parent.BackColor : this.BackColor;
            using (var bg = new SolidBrush(parentBg))
                g.FillRectangle(bg, this.ClientRectangle);

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
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        GraphicsPath GetRoundedPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
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
    // ReflectionPanel - reflejo sutil bajo un boton grande
    // ============================================================
    public class ReflectionPanel : Control
    {
        public int CornerRadius { get; set; }
        public Color ReflectionColor { get; set; }

        double _intensity;
        double _target;
        Timer _timer;

        public ReflectionPanel()
        {
            CornerRadius = 20;
            ReflectionColor = Color.Cyan;
            BackColor = Color.FromArgb(24, 24, 30);
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.ResizeRedraw, true);
            _timer = new Timer();
            _timer.Interval = 30;
            _timer.Tick += (s, e) => TickFade();
            Disposed += (s, e) => { try { _timer.Stop(); _timer.Dispose(); } catch { } };
        }

        public double Intensity
        {
            get { return _intensity; }
            set { _intensity = value; _target = value; Invalidate(); }
        }

        public void FadeTo(double target)
        {
            _target = target;
            if (!_timer.Enabled) _timer.Start();
        }

        void TickFade()
        {
            _intensity += (_target - _intensity) * 0.22;
            if (Math.Abs(_target - _intensity) < 0.005)
            {
                _intensity = _target;
                _timer.Stop();
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var bg = new SolidBrush(this.BackColor))
                g.FillRectangle(bg, this.ClientRectangle);

            if (_intensity <= 0.01) return;

            Rectangle r = ClientRectangle;
            r = new Rectangle(r.X + 1, r.Y, r.Width - 2, r.Height - 1);

            int topAlpha    = (int)(48 * _intensity);
            int midAlpha    = (int)(20 * _intensity);
            int bottomAlpha = 0;

            Color c1 = Color.FromArgb(topAlpha,    ReflectionColor);
            Color c2 = Color.FromArgb(midAlpha,    ReflectionColor);
            Color c3 = Color.FromArgb(bottomAlpha, ReflectionColor);

            using (var path = GetRoundedPath(r, CornerRadius))
            {
                using (var brush = new LinearGradientBrush(
                    new Rectangle(r.X, r.Y, r.Width, r.Height + 1),
                    c1, c3, LinearGradientMode.Vertical))
                {
                    brush.InterpolationColors = new ColorBlend
                    {
                        Colors    = new[] { c1, c2, c3 },
                        Positions = new[] { 0.0f, 0.5f, 1.0f }
                    };
                    g.FillPath(brush, path);
                }
            }
        }

        GraphicsPath GetRoundedPath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
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
    // HomeForm
    // ============================================================
    public class HomeForm : Form
    {
        readonly Color C_BG      = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL   = Color.FromArgb(30, 30, 38);
        readonly Color C_HEAD    = Color.FromArgb(15, 15, 20);
        readonly Color C_INFO    = Color.FromArgb(0, 200, 200);
        readonly Color C_OK      = Color.FromArgb(100, 220, 100);
        readonly Color C_ERR     = Color.FromArgb(240, 100, 100);
        readonly Color C_WARN    = Color.FromArgb(230, 180, 74);
        readonly Color C_GRAY    = Color.FromArgb(160, 160, 160);
        readonly Color C_BTN     = Color.FromArgb(50, 50, 60);
        readonly Color C_MAGENTA = Color.FromArgb(220, 80, 220);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

        string gamePath, outputPath;
        Label statusGame, statusOodle, statusArcs, statusTools;
        RoundedButton btnGameRef;
        RoundedButton btnExtractRef;
        RoundedButton btnRepackRef;
        LinkLabel linkOutput;
        Panel center;
        Timer autoTimer;
        Timer autoupdateCooldown;
        string lastUpdaterMsg = "Autoupdate: not checked yet";

        public HomeForm()
        {
            this.Text = "RAGE 2 Modding Toolkit " + AppInfo.Display;
            this.ClientSize = new Size(1100, 720);
            this.MinimumSize = new Size(1000, 720);
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
            EnsureDefaultOutput();
            BuildUI();

            autoTimer = new Timer();
            autoTimer.Interval = 2000;
            autoTimer.Tick += (s, e) => { try { RefreshStatus(); } catch { } };
            autoTimer.Start();

            this.FormClosing += (s, e) =>
            {
                try { autoTimer.Stop(); autoTimer.Dispose(); } catch { }
                try { if (autoupdateCooldown != null) { autoupdateCooldown.Stop(); autoupdateCooldown.Dispose(); } } catch { }
                // v2.0: borrar oodle al cerrar (nunca queda en disco).
                try { DeleteOodle(); } catch { }
            };

            // Autoupdate: primer check a los 3 segundos
            autoupdateCooldown = new Timer();
            autoupdateCooldown.Interval = 3000;
            autoupdateCooldown.Tick += (s, e) =>
            {
                autoupdateCooldown.Stop();
                RunAutoupdate(false);
            };
            autoupdateCooldown.Start();
        }

        void LoadConfig()
        {
            // v2.0: paths reset cada arranque. NO se persiste config entre sesiones.
            // El user vuelve a seleccionar RAGE2.exe + output en cada ejecucion.
            gamePath = "";
            outputPath = "";
        }

        void SaveConfig()
        {
            try { /* v2.0: no persistir paths */ } catch { }
        }

        void EnsureDefaultOutput()
        {
            // Primer arranque: output = <ExeDir>\RAGE2Toolkit_Output
            if (outputPath != null && outputPath.Length > 0 && Directory.Exists(outputPath)) return;
            try
            {
                Directory.CreateDirectory(Paths.DefaultOutputDir);
                outputPath = Paths.DefaultOutputDir;
                SaveConfig();
            }
            catch { }
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
            verLbl.Text = AppInfo.Display + "  \u00b7  by Kry0genik";
            verLbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            verLbl.ForeColor = Color.FromArgb(220, 200, 100);
            verLbl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            verLbl.Location = new Point(this.ClientSize.Width - 220, 24);
            verLbl.Size = new Size(180, 24);
            verLbl.TextAlign = ContentAlignment.MiddleRight;
            verLbl.AutoSize = false;
            header.Controls.Add(verLbl);

            RoundedButton btnAdv = new RoundedButton();
            btnAdv.Text = "Advanced Tools (Legacy)";
            btnAdv.CornerRadius = 10;
            btnAdv.BorderColor = Color.FromArgb(90, 90, 110);
            btnAdv.BorderThickness = 1;
            btnAdv.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnAdv.Location = new Point(this.ClientSize.Width - 220, 56);
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
            btnExtractRef = (RoundedButton)btnExtract;
            AttachReflection(center, (RoundedButton)btnExtract, C_INFO);

            Button btnRepack = MakeBigButton(
                "MOD & REPACK",
                "Push your edited files\nback into the game,\nor build a mod package\nto share on Nexus / ModDB",
                C_MAGENTA);
            btnRepack.Click += (s, e) => { new WizardRepack(gamePath).ShowDialog(this); };
            center.Controls.Add(btnRepack);
                        btnRepackRef = (RoundedButton)btnRepack;
            AttachReflection(center, (RoundedButton)btnRepack, C_MAGENTA);

            center.Resize += (s, e) => LayoutButtons();
            center.PerformLayout();

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 210;
            bottom.BackColor = C_HEAD;
            this.Controls.Add(bottom);

            Label sysCheck = new Label();
            sysCheck.Text = "TOOL CHECK";
            sysCheck.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            sysCheck.ForeColor = C_MAGENTA;
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

            statusArcs = new Label();
            statusArcs.AutoSize = true;
            statusArcs.Location = new Point(40, 80);
            statusArcs.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusArcs);

            statusTools = new Label();
            statusTools.AutoSize = true;
            statusTools.Location = new Point(40, 104);
            statusTools.Font = new Font("Segoe UI", 10);
            bottom.Controls.Add(statusTools);

            // LinkLabel del output - clickable, abreviado
            linkOutput = new LinkLabel();
            linkOutput.AutoSize = true;
            linkOutput.Location = new Point(40, 128);
            linkOutput.Font = new Font("Segoe UI", 9);
            linkOutput.LinkColor = C_OK;
            linkOutput.ActiveLinkColor = Color.White;
            linkOutput.VisitedLinkColor = C_OK;
            linkOutput.Text = "";
            linkOutput.LinkClicked += (s, e) =>
            {
                if (outputPath != null && Directory.Exists(outputPath))
                    Process.Start("explorer.exe", outputPath);
                else
                    BrowseOutputFolder();
            };
            bottom.Controls.Add(linkOutput);

            // Botones derecha
            RoundedButton btnGame = new RoundedButton();
            btnGame.Text = "Browse RAGE2.exe";
            btnGame.CornerRadius = 10;
            btnGame.BorderColor = Color.FromArgb(90, 90, 110);
            btnGame.BorderThickness = 1;
            btnGame.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGame.Location = new Point(this.ClientSize.Width - 420, 22);
            btnGame.Size = new Size(220, 42);
            btnGame.FlatStyle = FlatStyle.Flat;
            btnGame.FlatAppearance.BorderSize = 0;
            btnGame.BackColor = C_BTN;
            btnGame.ForeColor = Color.White;
            btnGame.Font = new Font("Segoe UI", 10);
            btnGame.Cursor = Cursors.Hand;
            btnGame.Click += (s, e) => BrowseGameExe();
            bottom.Controls.Add(btnGame);
            btnGameRef = btnGame;

            RoundedButton btnOutput = new RoundedButton();
            btnOutput.Text = "Select output folder";
            btnOutput.CornerRadius = 10;
            btnOutput.BorderColor = Color.FromArgb(90, 90, 110);
            btnOutput.BorderThickness = 1;
            btnOutput.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOutput.Location = new Point(this.ClientSize.Width - 420, 78);
            btnOutput.Size = new Size(220, 42);
            btnOutput.FlatStyle = FlatStyle.Flat;
            btnOutput.FlatAppearance.BorderSize = 0;
            btnOutput.BackColor = C_BTN;
            btnOutput.ForeColor = Color.White;
            btnOutput.Font = new Font("Segoe UI", 10);
            btnOutput.Cursor = Cursors.Hand;
            btnOutput.Click += (s, e) => BrowseOutputFolder();
            bottom.Controls.Add(btnOutput);

            RoundedButton btnOpenOut = new RoundedButton();
            btnOpenOut.Text = "Output Folder";
            btnOpenOut.CornerRadius = 10;
            btnOpenOut.BorderColor = Color.FromArgb(90, 90, 110);
            btnOpenOut.BorderThickness = 1;
            btnOpenOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenOut.Location = new Point(this.ClientSize.Width - 180, 22);
            btnOpenOut.Size = new Size(140, 42);
            btnOpenOut.FlatStyle = FlatStyle.Flat;
            btnOpenOut.FlatAppearance.BorderSize = 0;
            btnOpenOut.BackColor = C_BTN;
            btnOpenOut.ForeColor = Color.White;
            btnOpenOut.Font = new Font("Segoe UI", 10);
            btnOpenOut.Cursor = Cursors.Hand;
            btnOpenOut.Click += (s, e) =>
            {
                if (outputPath != null && Directory.Exists(outputPath))
                    Process.Start("explorer.exe", outputPath);
                else
                    BrowseOutputFolder();
            };
            bottom.Controls.Add(btnOpenOut);

            RoundedButton btnUpdate = new RoundedButton();
            btnUpdate.Text = "Check update";
            btnUpdate.CornerRadius = 10;
            btnUpdate.BorderColor = Color.FromArgb(90, 90, 110);
            btnUpdate.BorderThickness = 1;
            btnUpdate.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnUpdate.Location = new Point(this.ClientSize.Width - 180, 78);
            btnUpdate.Size = new Size(140, 42);
            btnUpdate.FlatStyle = FlatStyle.Flat;
            btnUpdate.FlatAppearance.BorderSize = 0;
            btnUpdate.BackColor = C_BTN;
            btnUpdate.ForeColor = Color.White;
            btnUpdate.Font = new Font("Segoe UI", 10);
            btnUpdate.Cursor = Cursors.Hand;
            btnUpdate.Click += (s, e) => RunAutoupdate(true);
            bottom.Controls.Add(btnUpdate);

            RefreshStatus();
        }

        void RunAutoupdate(bool manual)
        {
            try
            {
                Updater.CheckAsync(msg =>
                {
                    lastUpdaterMsg = msg;
                    try
                    {
                        if (IsHandleCreated && !IsDisposed)
                            BeginInvoke(new Action(() => { try { RefreshStatus(); } catch { } }));
                    }
                    catch { }
                }, manual);
            }
            catch (Exception ex)
            {
                lastUpdaterMsg = "Autoupdate failed: " + ex.Message;
                if (manual) MessageBox.Show(lastUpdaterMsg, "Update check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void BrowseGameExe()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = "Select RAGE2.exe (the game executable)";
                dlg.Filter = "RAGE 2 executable|RAGE2.exe|Executables|*.exe|All files|*.*";
                dlg.CheckFileExists = true;
                dlg.CheckPathExists = true;
                if (gamePath != null && Directory.Exists(gamePath))
                {
                    string cand = Path.Combine(gamePath, "RAGE2.exe");
                    if (File.Exists(cand)) dlg.InitialDirectory = gamePath;
                }
                if (dlg.ShowDialog() != DialogResult.OK) return;

                if (!string.Equals(Path.GetFileName(dlg.FileName), "RAGE2.exe", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show("Selected file is not RAGE2.exe.\n\nPick the game executable.", "Not RAGE2.exe",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                gamePath = Path.GetDirectoryName(dlg.FileName);
                string target = Paths.OodleDllPath;
                if (!File.Exists(target))
                {
                    string direct = Path.Combine(gamePath, "oo2core_7_win64.dll");
                    if (File.Exists(direct))
                    {
                        try { File.Copy(direct, target, true); } catch { }
                    }
                    else
                    {
                        try
                        {
                            var r = Directory.GetFiles(gamePath, "oo2core_7_win64.dll", SearchOption.AllDirectories);
                            if (r.Length > 0) File.Copy(r[0], target, true);
                        }
                        catch { }
                    }
                }

                try { if (File.Exists(target)) Oodle.TryLoad(target); } catch { }

                SaveConfig();
                RefreshStatus();
            }
        }

        void BrowseOutputFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select the folder where extracted assets will be stored (~60 GB free)";
                if (outputPath != null && Directory.Exists(outputPath)) dlg.SelectedPath = outputPath;
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (Cfa.WarnIfProtected(this, dlg.SelectedPath)) outputPath = dlg.SelectedPath;
                SaveConfig();
                RefreshStatus();
            }
        }

        void LayoutButtons()
        {
            if (center == null) return;
            var bigs = new System.Collections.Generic.List<RoundedButton>();
            foreach (Control c in center.Controls)
            {
                var rb = c as RoundedButton;
                if (rb != null && rb.Tag is ReflectionPanel) bigs.Add(rb);
            }
            if (bigs.Count < 2) return;

            var b1 = bigs[0];
            var b2 = bigs[1];

            int gap = 40;
            int reflH = 70;
            int reflGap = 6;
            int maxBw = 460;
            int bw = Math.Min(maxBw, (center.Width - 3 * gap) / 2);
            int bh = Math.Min(240, center.Height - 200);
            if (bh < 140) bh = 140;
            int totalW = bw * 2 + gap;
            int totalH = bh + reflGap + reflH;
            int x = (center.Width - totalW) / 2;
            int topMargin = 75; // ~2cm de separacion extra respecto a la barra superior
            int y = Math.Max(35, (center.Height - totalH) / 2) + topMargin;
            int maxY = center.Height - totalH - 20;
            if (maxY > 35 && y > maxY) y = maxY;

            b1.Size = new Size(bw, bh);
            b1.Location = new Point(x, y);
            b2.Size = new Size(bw, bh);
            b2.Location = new Point(x + bw + gap, y);

            var r1 = b1.Tag as ReflectionPanel;
            var r2 = b2.Tag as ReflectionPanel;
            if (r1 != null) { r1.Size = new Size(bw, reflH); r1.Location = new Point(x, y + bh + reflGap); }
            if (r2 != null) { r2.Size = new Size(bw, reflH); r2.Location = new Point(x + bw + gap, y + bh + reflGap); }
        }

        // v2.0: borra oodle del disco. Best-effort; si esta en uso por otro proceso, no peta.
        void DeleteOodle()
        {
            try
            {
                string p = Paths.OodleDllPath;
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        // v2.0: habilita/deshabilita EXTRACT y MOD & REPACK segun estado del gamePath.
        // Cuando no esta configurado, los perimetros y reflejos van en GRIS.
        void ApplyReadyState(bool ready)
        {
            if (btnExtractRef != null)
            {
                btnExtractRef.Enabled = ready;
                btnExtractRef.BorderColor = ready ? C_INFO : C_GRAY;
                btnExtractRef.Cursor = ready ? Cursors.Hand : Cursors.Default;
                btnExtractRef.Invalidate();
                var r = btnExtractRef.Tag as ReflectionPanel;
                if (r != null) { r.ReflectionColor = ready ? C_INFO : C_GRAY; r.Invalidate(); }
            }
            if (btnRepackRef != null)
            {
                btnRepackRef.Enabled = ready;
                btnRepackRef.BorderColor = ready ? C_MAGENTA : C_GRAY;
                btnRepackRef.Cursor = ready ? Cursors.Hand : Cursors.Default;
                btnRepackRef.Invalidate();
                var r = btnRepackRef.Tag as ReflectionPanel;
                if (r != null) { r.ReflectionColor = ready ? C_MAGENTA : C_GRAY; r.Invalidate(); }
            }
        }

        void RefreshStatus()
        {
            if (statusGame != null)
            {
                if (gamePath != null && gamePath.Length > 0 && Directory.Exists(gamePath))
                {
                    if (File.Exists(Path.Combine(gamePath, "RAGE2.exe")))
                    {
                        statusGame.Text = "\u2713  Game folder: " + Abbreviate.ShortPath(gamePath, 70);
                        statusGame.ForeColor = C_OK;
                    }
                    else
                    {
                        statusGame.Text = "\u26a0  Game folder set, but RAGE2.exe not found";
                        statusGame.ForeColor = C_ERR;
                    }
                }
                else
                {
                    statusGame.Text = "\u2717  Game folder not set";
                    statusGame.ForeColor = C_ERR;
                }
            }

            if (statusOodle != null)
            {
                string dll = Paths.OodleDllPath;
                if (Oodle.IsLoaded)
                {
                    long sz = 0;
                    try { sz = new FileInfo(dll).Length; } catch { }
                    statusOodle.Text = "\u2713  Oodle runtime: loaded  (" + (sz / 1024) + " KB)";
                    statusOodle.ForeColor = C_OK;
                }
                else if (File.Exists(dll))
                {
                    statusOodle.Text = "\u26a0  Oodle DLL present but not loaded: " + Oodle.LastError;
                    statusOodle.ForeColor = C_ERR;
                }
                else
                {
                    statusOodle.Text = "\u2717  Oodle runtime MISSING  -  select RAGE2.exe and it will be copied automatically";
                    statusOodle.ForeColor = C_ERR;
                }
            }

            if (linkOutput != null)
            {
                if (outputPath != null && outputPath.Length > 0 && Directory.Exists(outputPath))
                {
                    string free = "";
                    bool low = false;
                    try
                    {
                        var drive = new DriveInfo(Path.GetPathRoot(outputPath));
                        free = "  (" + (drive.AvailableFreeSpace / 1073741824L) + " GB free)";
                        if (drive.AvailableFreeSpace < 25L * 1073741824L) low = true;
                    }
                    catch { }
                    string abbr = Abbreviate.ShortPath(outputPath, 45);
                    string check = low ? "\u26a0" : "\u2713";
                    string prefix = check + "   Output Folder: ";
                    string suffix = free + (low ? "  -  less than 25 GB" : "");
                    linkOutput.Text = prefix + abbr + suffix;
                    Color green = low ? C_WARN : C_OK;
                    linkOutput.ForeColor = green;
                    linkOutput.LinkColor = green;
                    linkOutput.ActiveLinkColor = Color.White;
                    linkOutput.VisitedLinkColor = green;
                    linkOutput.LinkArea = new LinkArea(prefix.Length, abbr.Length);
                    linkOutput.Visible = true;
                }
                else
                {
                    linkOutput.Text = "\u2717   Output Folder not set  -  click to configure";
                    linkOutput.ForeColor = C_ERR;
                    linkOutput.LinkColor = C_ERR;
                    linkOutput.VisitedLinkColor = C_ERR;
                    linkOutput.LinkArea = new LinkArea(0, 0);
                    linkOutput.Visible = true;
                }
            }

            if (statusArcs != null)
            {
                if (gamePath != null && Directory.Exists(gamePath))
                {
                    string dirI = Path.Combine(gamePath, "archives_win64", "initial");
                    string dirS = Path.Combine(gamePath, "archives_win64", "supplemental");
                    if (Directory.Exists(dirI) || Directory.Exists(dirS))
                    {
                        int arcCount = 0, tabCount = 0;
                        try
                        {
                            if (Directory.Exists(dirI))
                            {
                                arcCount += Directory.GetFiles(dirI, "game*.arc").Length;
                                tabCount += Directory.GetFiles(dirI, "game*.tab").Length;
                            }
                            if (Directory.Exists(dirS))
                            {
                                arcCount += Directory.GetFiles(dirS, "game*.arc").Length;
                                tabCount += Directory.GetFiles(dirS, "game*.tab").Length;
                            }
                        }
                        catch { }
                        if (arcCount > 0 && arcCount == tabCount)
                        {
                            statusArcs.Text = "\u2713  Game archives: " + arcCount + " .arc files detected";
                            statusArcs.ForeColor = C_OK;
                        }
                        else if (arcCount > 0)
                        {
                            statusArcs.Text = "\u26a0  Game archives: " + arcCount + " .arc but " + tabCount + " .tab";
                            statusArcs.ForeColor = C_WARN;
                        }
                        else
                        {
                            statusArcs.Text = "\u2717  Game archives: no .arc files found";
                            statusArcs.ForeColor = C_ERR;
                        }
                    }
                    else
                    {
                        statusArcs.Text = "\u2717  Game archives: archives_win64 not found";
                        statusArcs.ForeColor = C_ERR;
                    }
                }
                else
                {
                    statusArcs.Text = "\u2717  Game archives: unknown (game folder not set)";
                    statusArcs.ForeColor = C_ERR;
                }
            }

            if (statusTools != null)
            {
                var missing = new List<string>();
                string[] tools = { "ddscConvert.exe", "ddscConvert.exe.config", "texconv.exe", "R2SmallArchive.exe" };
                foreach (var t in tools)
                    if (!File.Exists(Path.Combine(Paths.BinDir, t))) missing.Add(t);

                if (missing.Count == 0)
                {
                    statusTools.Text = "\u2713  Bundled tools: ddscConvert, texconv, R2SmallArchive  -  all present";
                    statusTools.ForeColor = C_OK;
                }
                else
                {
                    statusTools.Text = "\u2717  Bundled tools missing: " + string.Join(", ", missing.ToArray());
                    statusTools.ForeColor = C_ERR;
                }
            }

            // Mostrar tambien el mensaje del updater en la barra de titulo (sin UI nueva)
            try
            {
                string baseTitle = "RAGE 2 Modding Toolkit " + AppInfo.Display;
                if (!string.IsNullOrEmpty(lastUpdaterMsg) && lastUpdaterMsg.StartsWith("Autoupdate: new"))
                    this.Text = baseTitle + "  -  UPDATE AVAILABLE";
                else
                    this.Text = baseTitle;
            }
            catch { }

            if (btnGameRef != null)
            {
                bool missing = !(gamePath != null && gamePath.Length > 0 && Directory.Exists(gamePath) &&
                                 File.Exists(Path.Combine(gamePath, "RAGE2.exe")));
                btnGameRef.PulseBase = C_BTN;
                btnGameRef.PulseTarget = C_INFO;
                btnGameRef.PulsePeriodMs = 2400;
                btnGameRef.PulseEnabled = missing;

                // v2.0: enable/disable EXTRACT y MOD & REPACK segun estado del gamePath.
                bool ready = !missing && gamePath != null && gamePath.Length > 0 && Directory.Exists(gamePath);
                ApplyReadyState(ready);
            }
        }

        void AttachReflection(Panel parent, RoundedButton btn, Color accent)
        {
            ReflectionPanel refl = new ReflectionPanel();
            refl.CornerRadius = btn.CornerRadius;
            refl.ReflectionColor = accent;
            refl.BackColor = parent.BackColor;
            refl.Intensity = 0;
            btn.Tag = refl;
            btn.MouseEnter += (s, e) => refl.FadeTo(1.0);
            btn.MouseLeave += (s, e) => refl.FadeTo(0.0);
            parent.Controls.Add(refl);
            LayoutButtons();
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
            return b;
        }
    }

    // ============================================================
    // WizardBase
    // ============================================================
    public abstract class WizardBase : Form
    {
        protected readonly Color C_BG      = Color.FromArgb(24, 24, 30);
        protected readonly Color C_PANEL   = Color.FromArgb(30, 30, 38);
        protected readonly Color C_HEAD    = Color.FromArgb(15, 15, 20);
        protected readonly Color C_INFO    = Color.FromArgb(0, 200, 200);
        protected readonly Color C_OK      = Color.FromArgb(100, 220, 100);
        protected readonly Color C_WARN    = Color.FromArgb(230, 180, 74);
        protected readonly Color C_ERR     = Color.FromArgb(240, 100, 100);
        protected readonly Color C_GRAY    = Color.FromArgb(160, 160, 160);
        protected readonly Color C_BTN     = Color.FromArgb(50, 50, 60);
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
            this.Text = title + "  -  RAGE 2 Modding Toolkit " + AppInfo.Display;
            this.ClientSize = new Size(1000, 700);
            this.MinimumSize = new Size(820, 600);
            this.StartPosition = FormStartPosition.CenterParent;
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
            btnCancel.Click += (s, e) =>
            {
                this.Close();

            };
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
    // WizardExtract
    // ============================================================
    public class WizardExtract : WizardBase
    {
        string gamePath, outputPath;
        RoundedButton btnAll, btnSingle;
        CheckBox cbTex, cbAudio, cbVideo, cbScripts, cbUI;
        bool _allSelected = true;
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
            p.Controls.Add(MakeSub("Pick what interests you.", 0, 50, 900));

            btnAll = new RoundedButton();
            btnAll.Text = "Extract everything";
            btnAll.CornerRadius = 10;
            btnAll.BorderThickness = 2;
            btnAll.BorderColor = C_INFO;
            btnAll.BackColor = C_BTN;
            btnAll.ForeColor = Color.White;
            btnAll.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnAll.Cursor = Cursors.Hand;
            btnAll.Size = new Size(400, 44);
            btnAll.Click += (s, e) => SetToggleAll(true);
            p.Controls.Add(btnAll);

            btnSingle = new RoundedButton();
            btnSingle.Text = "Browse && pick manually";
            btnSingle.CornerRadius = 10;
            btnSingle.BorderThickness = 1;
            btnSingle.BorderColor = Color.FromArgb(90, 90, 110);
            btnSingle.BackColor = C_BTN;
            btnSingle.ForeColor = Color.White;
            btnSingle.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            btnSingle.Cursor = Cursors.Hand;
            btnSingle.Size = new Size(400, 44);
            btnSingle.Click += (s, e) => SetToggleAll(false);
            p.Controls.Add(btnSingle);

            Action centerToggles = () => {
                int cx = p.ClientSize.Width / 2;
                if (cx < 400) cx = 400;
                btnAll.Location = new Point(cx - btnAll.Width / 2, 110);
                btnSingle.Location = new Point(cx - btnSingle.Width / 2, 168);
            };
            p.Resize += (s, e) => centerToggles();
            p.HandleCreated += (s, e) => centerToggles();

            cbTex = MakeSubCheck("Textures (editable DDS)", 60, 240);
            cbAudio = MakeSubCheck("Audio / SFX (OGG, RIFF)", 60, 275);
            cbVideo = MakeSubCheck("Video (BK2, BIK)", 60, 310);
            cbScripts = MakeSubCheck("Scripts and data (ADF, BL, EE...)", 60, 345);
            cbUI = MakeSubCheck("UI (GFX, CFX)", 60, 380);

            Label warn = new Label();
            warn.Text = "\u2139  Extracting everything uses ~60 GB. Time depends on hardware: ~5 min on modern NVMe, up to 30 min on older hardware.";
            warn.ForeColor = C_WARN;
            warn.Location = new Point(20, 430);
            warn.AutoSize = true;
            p.Controls.Add(warn);
        }

        void SetToggleAll(bool all)
        {
            _allSelected = all;
            if (all)
            {
                btnAll.BorderThickness = 2;
                btnAll.BorderColor = C_INFO;
                btnSingle.BorderThickness = 1;
                btnSingle.BorderColor = Color.FromArgb(90, 90, 110);
                SetSubs(true);
            }
            else
            {
                btnAll.BorderThickness = 1;
                btnAll.BorderColor = Color.FromArgb(90, 90, 110);
                btnSingle.BorderThickness = 2;
                btnSingle.BorderColor = C_INFO;
                SetSubs(false);
            }
            try { btnAll.Invalidate(); btnAll.Refresh(); } catch { }
            try { btnSingle.Invalidate(); btnSingle.Refresh(); } catch { }
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
            p.Controls.Add(MakeSub("You need about 60 GB free if extracting everything.", 0, 50, 900));

            lblOutput = new Label();
            lblOutput.Font = new Font("Consolas", 10);
            lblOutput.ForeColor = C_INFO;
            lblOutput.Location = new Point(20, 120);
            lblOutput.Size = new Size(900, 30);
            lblOutput.Text = outputPath != null && outputPath.Length > 0 ? outputPath : "(not set)";
            p.Controls.Add(lblOutput);

            Button btnPick = MakeBtn("Choose folder...", C_BTN);
            btnPick.Location = new Point(20, 160);
            btnPick.Size = new Size(200, 36);
            btnPick.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Folder where the extracted assets will be saved";
                    if (outputPath != null && Directory.Exists(outputPath)) dlg.SelectedPath = outputPath;
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    if (Cfa.WarnIfProtected(this, dlg.SelectedPath)) outputPath = dlg.SelectedPath;
                    lblOutput.Text = outputPath;
                }
            };
            p.Controls.Add(btnPick);

            Button btnUsePrev = MakeBtn("Use toolkit default", C_BTN);
            btnUsePrev.Location = new Point(230, 160);
            btnUsePrev.Size = new Size(200, 36);
            btnUsePrev.Click += (s, e) =>
            {
                try
                {
                    if (File.Exists(Paths.ConfigFile))
                    {
                        var lines = File.ReadAllLines(Paths.ConfigFile);
                        if (lines.Length > 1 && lines[1].Length > 0 && Directory.Exists(lines[1]))
                        {
                            outputPath = lines[1];
                            lblOutput.Text = outputPath;
                        }
                    }
                    else
                    {
                        Directory.CreateDirectory(Paths.DefaultOutputDir);
                        outputPath = Paths.DefaultOutputDir;
                        lblOutput.Text = outputPath;
                    }
                }
                catch { }
            };
            p.Controls.Add(btnUsePrev);
        }

        void BuildStep3()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Extracting...", 0, 10));
            p.Controls.Add(MakeSub("Do not close this window. Time depends on hardware: ~5 min on modern NVMe, up to 30 min on older hardware.", 0, 50, 900));

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
            logBox.ShortcutsEnabled = true;
            logBox.WordWrap = false;
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
            btnOpen.Click += (s, e) =>
            {
                if (outputPath != null && Directory.Exists(outputPath))
                    Process.Start("explorer.exe", outputPath);
            };
            p.Controls.Add(btnOpen);
        }

        protected override void OnNextClicked()
        {
            if (currentStep == 0) {
                    if (!_allSelected) {
                        new SingleExtractForm(gamePath, outputPath).ShowDialog(this);
                        return;
                    }
                    GotoStep(1); return;
                }
            if (currentStep == 1)
            {
                if (outputPath == null || outputPath.Length == 0 || !Directory.Exists(outputPath))
                {
                    MessageBox.Show("Please choose an output folder first.", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (gamePath == null || !Directory.Exists(gamePath))
                {
                    MessageBox.Show("Please set the game folder first.", "Missing game folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!Oodle.IsLoaded)
                {
                    MessageBox.Show("Oodle runtime not loaded. Select RAGE2.exe in the main window first.", "Oodle missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GotoStep(2);
                StartExtract();
                return;
            }
            if (currentStep == 2)
            {
                if (!completed)
                {
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
                    this.Invoke(new Action(() => {
                        Log("Starting extraction...");
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Minimum = 0;
                        progress.Maximum = 100;
                        progress.Value = 0;
                    }));
                    Directory.CreateDirectory(outputPath);

                    var swTotal = System.Diagnostics.Stopwatch.StartNew();
                    try {
                        string _dd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                        if (!Directory.Exists(_dd)) _dd = AppDomain.CurrentDomain.BaseDirectory;
                        var _fl = ExtractorOpt.LoadFilelistCache(_dd);
                        if (_fl.Count > 0) {
                            ExtractorOpt.NameResolver = (h) => { string p; return _fl.TryGetValue(h, out p) ? ExtractorOpt.SanitizeBasename(p) : null; };
                            Log("[TECH-15] NameResolver active (" + _fl.Count + " entries)");
                        }
                    } catch (Exception ex) { Log("[TECH-15] " + ex.Message); }

                    var st = Extractor.ExtractAll(gamePath, outputPath, msg => Log(msg),
                        delegate(int i, int tot, string n, int c) {
                            this.Invoke(new Action(() => {

                                int pct = (int)((i - 1) * 100.0 / tot);
                                progress.Value = Math.Max(0, Math.Min(100, pct));
                                Log(string.Format("[{0:D2}/{1:D2}] {2,-14}  ({3}%)  {4} entries", i, tot, n, pct, c));
                            }));
                        },
                        delegate(int i, int tot, string n, int ok, int f) {
                            this.Invoke(new Action(() => {
                                progress.Value = i;
                                int pct = (int)(i * 100.0 / tot);
                                Log(string.Format("       {0,-14}  ok={1,6}  fail={2,4}   ({3}% total)", n, ok, f, pct));
                            }));
                        },
                        true);
                    swTotal.Stop();
                    ExtractorOpt.NameResolver = null;
                    try { ExtractorOpt.SortOutputDirectory(outputPath); } catch { }

                    long ms = swTotal.ElapsedMilliseconds;
                    long hh = ms / 3600000;
                    long mm = (ms % 3600000) / 60000;
                    long ss = (ms % 60000) / 1000;
                    string tiempo = string.Format("{0}h {1:D2}m {2:D2}s", hh, mm, ss);

                    Log("");
                    Log("=== Extract summary ===");
                    Log("  Total: " + st.EntriesOk + " extracted, " + st.EntriesFail + " missing, in " + tiempo);
                    Log("  RAM peak: " + st.PeakRamMB + " MB");
                    Log("  Bytes: " + (st.BytesWritten / 1024 / 1024) + " MB");

                    Log("");
                    Log("Classifying and converting textures...");
                    ClassifyAndConvert();

                    this.Invoke(new Action(() =>
                    {
                        try {
                            progress.Minimum = 0;
                            progress.Maximum = 100;
                            progress.Value = 100;
                        } catch { }
                        Log("");
                        Log("\u2713 Extraction complete.");
                        completed = true;
                        ShowStats();
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        Log("\u2717 ERROR: " + ex.Message);
                        MessageBox.Show("Error: " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }
        void ClassifyAndConvert()
        {
            var swTotal = System.Diagnostics.Stopwatch.StartNew();
            string[] sub = { "TEXTURES", "AUDIO", "VIDEO", "SCRIPTS", "UI", "OTHER" };
            string root = Path.Combine(outputPath, "_EDITABLE");
            foreach (var s in sub) Directory.CreateDirectory(Path.Combine(root, s));

            string ddsc = Path.Combine(Paths.BinDir, "ddscConvert.exe");

            // Recopilar archivos
            var swScan = System.Diagnostics.Stopwatch.StartNew();
            var all = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("_EDITABLE")).ToList();
                swScan.Stop();
                this.Invoke(new Action(() => Log("  [scan] " + swScan.ElapsedMilliseconds + " ms")));
            int total = all.Count;
            this.Invoke(new Action(() => Log("  classify: " + total + " archivos")));

            // Separar avtx del resto
            var avtxList = new List<string>();
            var restList = new List<string>();
            foreach (var f in all) {
                if (Path.GetExtension(f).ToLowerInvariant() == ".avtx") avtxList.Add(f);
                else restList.Add(f);
            }
            this.Invoke(new Action(() => Log("  avtx: " + avtxList.Count + "  resto: " + restList.Count)));

            // ============ Sub-fase 1: avtx en lotes paralelos ============
            var swAvtx = System.Diagnostics.Stopwatch.StartNew();
            if (avtxList.Count > 0 && File.Exists(ddsc)) {
                int batchSize = 100;
                var batches = new List<List<string>>();
                for (int k = 0; k < avtxList.Count; k += batchSize) {
                    int end = Math.Min(k + batchSize - 1, avtxList.Count - 1);
                    var b = new List<string>();
                    for (int m = k; m <= end; m++) b.Add(avtxList[m]);
                    batches.Add(b);
                }
                int totalBatches = batches.Count;
                int doneBatches = 0;
                int ddsLocal = 0;
                Parallel.ForEach(batches, new ParallelOptions { MaxDegreeOfParallelism = 4 }, batch => {
                    try {
                        string argsList = string.Join(" ", batch.Select(x => "\"" + x + "\"").ToArray());
                        var psi = new ProcessStartInfo(ddsc, argsList);
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        psi.WorkingDirectory = Paths.BinDir;
                        var p = Process.Start(psi);
                        p.WaitForExit();
                        foreach (var a in batch) {
                            string dds = Path.ChangeExtension(a, ".dds");
                            if (File.Exists(dds)) {
                                string target = Path.Combine(root, "TEXTURES", Path.GetFileName(dds));
                                try { if (File.Exists(target)) File.Delete(target); File.Move(dds, target); System.Threading.Interlocked.Increment(ref ddsLocal); } catch { }
                            }
                            try { File.Delete(a); } catch { }
                        }
                    } catch { }
                    int d = System.Threading.Interlocked.Increment(ref doneBatches);
                    if (d % 2 == 0 || d == totalBatches) {
                        int dd = d;
                        this.Invoke(new Action(() => {
                            Log("    avtx batches: " + dd + " / " + totalBatches);
                        }));
                    }
                });
                ddsCount += ddsLocal;
            }
            swAvtx.Stop();
            this.Invoke(new Action(() => Log("  [avtx] " + swAvtx.ElapsedMilliseconds + " ms")));

            // ============ Sub-fase 2: resto en paralelo ============
            var swMove = System.Diagnostics.Stopwatch.StartNew();
            int doneMove = 0;
            int totalMove = restList.Count;
            Parallel.ForEach(restList, new ParallelOptions { MaxDegreeOfParallelism = 8 }, f => {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                string bn = Path.GetFileName(f);
                string target = null;
                if (ext == ".dds") { target = Path.Combine(root, "TEXTURES", bn); System.Threading.Interlocked.Increment(ref ddsCount); }
                else if (ext == ".ogg" || ext == ".riff" || ext == ".rtpc" || ext == ".fsb") { target = Path.Combine(root, "AUDIO", bn); System.Threading.Interlocked.Increment(ref audioCount); }
                else if (ext == ".bik" || ext == ".bk2") { target = Path.Combine(root, "VIDEO", bn); System.Threading.Interlocked.Increment(ref videoCount); }
                else if (ext == ".gfx" || ext == ".cfx") { target = Path.Combine(root, "UI", bn); System.Threading.Interlocked.Increment(ref otherCount); }
                else if (ext == ".adf" || ext == ".bl" || ext == ".ee" || ext == ".nl" || ext == ".fl" || ext == ".tag") { target = Path.Combine(root, "SCRIPTS", bn); System.Threading.Interlocked.Increment(ref otherCount); }
                else { target = Path.Combine(root, "OTHER", bn); System.Threading.Interlocked.Increment(ref otherCount); }
                if (target != null && !File.Exists(target)) {
                    try { File.Move(f, target); } catch { }
                }
                int dm = System.Threading.Interlocked.Increment(ref doneMove);
                if (dm % 2000 == 0) {
                    int ddm = dm;
                    this.Invoke(new Action(() => Log("    move: " + ddm + " / " + totalMove)));
                }
            });
            swMove.Stop();
            this.Invoke(new Action(() => Log("  [move] " + swMove.ElapsedMilliseconds + " ms")));

            extractedCount = total;
            swTotal.Stop();
            long ms = swTotal.ElapsedMilliseconds;
            long hh = ms / 3600000;
            long mm = (ms % 3600000) / 60000;
            long ss = (ms % 60000) / 1000;
            string tiempo = string.Format("{0}h {1:D2}m {2:D2}s", hh, mm, ss);
            this.Invoke(new Action(() => Log("  [classify TOTAL] " + tiempo)));
        }
        void ShowStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Processed " + extractedCount.ToString("N0") + " files.\n");
            sb.AppendLine("\u2713 " + ddsCount.ToString("N0") + "  textures in .dds format");
            sb.AppendLine("\u2713 " + audioCount.ToString("N0") + "  audio files in .ogg / .riff");
            sb.AppendLine("\u2713 " + videoCount.ToString("N0") + "  videos in .bik / .bk2");
            sb.AppendLine("\u2713 " + otherCount.ToString("N0") + "  scripts, UI and other data");
            sb.AppendLine();
            sb.AppendLine("Organized in:");
            sb.AppendLine("  " + Path.Combine(outputPath, "_EDITABLE") + "\\");
            sb.AppendLine("    TEXTURES\\   AUDIO\\   VIDEO\\   SCRIPTS\\   UI\\   OTHER\\");
            sb.AppendLine();
            sb.AppendLine("When done editing, go back to the main screen and use MOD & REPACK.");

            var lbl = (Label)stepPanels[3].Controls.Find("lblStats", true)[0];
            lbl.Text = sb.ToString();
        }

        void Log(string msg)
        {
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg))); return; }
            logBox.AppendText(msg + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.ScrollToCaret();
        }
    }

    // ============================================================
    // WizardRepack
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

        // v2.0: preset del sourceDir desde drag&drop sobre el boton MOD & REPACK
        // del HomeForm. Se llama ANTES de ShowDialog.
        public void PresetSource(string dir)
        {
            try
            {
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    sourceDir = dir;
                }
            }
            catch { }
        }

                // TECH-15: parsea hash de <basename>_<HASH16> o de <HASH16> (fallback legacy)
        static bool TryParseHashFromName(string basename, out ulong hash, out string hashHex)
        {
            hash = 0; hashHex = null;
            if (string.IsNullOrEmpty(basename)) return false;
            var m = System.Text.RegularExpressions.Regex.Match(basename, @"_([0-9A-Fa-f]{16})$");
            if (m.Success)
            {
                hashHex = m.Groups[1].Value.ToUpperInvariant();
            }
            else if (basename.Length == 16 && System.Text.RegularExpressions.Regex.IsMatch(basename, "^[0-9A-Fa-f]{16}$"))
            {
                hashHex = basename.ToUpperInvariant();
            }
            else
            {
                return false;
            }
            return ulong.TryParse(hashHex, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out hash);
        }
void LoadFilelist()
        {
            try
            {
                string[] candidates = new string[]
                {
                    Path.Combine(Paths.DataDir, "filelist.txt"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "filelist.txt")
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
                        if (!ulong.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture, out h)) continue;
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
            var allTabs = new List<string>();
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
            dropZone.Height = 280;
            // v2.0-fix: ancho = ancho del padre - 20 izq - 20 der. Ya no hay 900 fijo
            // que deje descolgado el borde derecho cuando el wizard es mas ancho.
            dropZone.Width = p.ClientSize.Width > 40 ? p.ClientSize.Width - 40 : 900;
            dropZone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            dropZone.BackColor = C_PANEL;
            dropZone.BorderStyle = BorderStyle.FixedSingle;
            dropZone.AllowDrop = true;
            p.Controls.Add(dropZone);
            // Recalcular cuando el step panel reciba su ancho final
            p.Resize += (s, e) => { dropZone.Width = p.ClientSize.Width - 40; };

            dropLbl = new Label();
            dropLbl.Dock = DockStyle.Fill;
            dropLbl.TextAlign = ContentAlignment.MiddleCenter;
            dropLbl.Font = new Font("Segoe UI", 13);
            dropLbl.ForeColor = C_GRAY;
            dropLbl.Text = "Drop your folder here\n\n(or click to browse)";
            dropZone.Controls.Add(dropLbl);
            // v2.0-fix: el Label hijo (Dock=Fill) se come los eventos drag del Panel
            // padre. Propagamos manualmente para que el drop funcione en toda el area.
            dropLbl.AllowDrop = true;
            dropLbl.Cursor = Cursors.Hand;
            dropLbl.DragEnter += (s, e) => {
                // v2.0-fix: al arrastrar desde Explorer, Windows manda la ventana
                // atras. Forzamos bring-to-front para ver el feedback del drop.
                try {
                    if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
                    this.Activate();
                    this.BringToFront();
                } catch { }
                if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                    e.Effect = DragDropEffects.Copy;
                    dropZone.BackColor = Color.FromArgb(50, 50, 65);
                    dropLbl.ForeColor = C_INFO;
                } else {
                    e.Effect = DragDropEffects.None;
                }
            };
            dropLbl.DragLeave += (s, e) => {
                dropZone.BackColor = C_PANEL;
                dropLbl.ForeColor = C_GRAY;
            };
            dropLbl.DragDrop += (s, e) => {
                dropZone.BackColor = C_PANEL;
                dropLbl.ForeColor = C_GRAY;
                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0) return;
                string dir = files[0];
                if (Directory.Exists(dir)) { sourceDir = dir; dropLbl.Text = "\u2713 " + dir; dropLbl.ForeColor = C_OK; }
                else if (File.Exists(dir)) { sourceDir = Path.GetDirectoryName(dir); dropLbl.Text = "\u2713 " + sourceDir; dropLbl.ForeColor = C_OK; }
            };
            dropLbl.Click += (s, e) => PickFolder();

            dropZone.DragEnter += (s, e) =>
            {
                try {
                    if (this.WindowState == FormWindowState.Minimized) this.WindowState = FormWindowState.Normal;
                    this.Activate();
                    this.BringToFront();
                } catch { }
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effect = DragDropEffects.Copy;
                    dropZone.BackColor = Color.FromArgb(50, 50, 65);
                    dropLbl.ForeColor = C_INFO;
                }
            };
            dropZone.DragLeave += (s, e) =>
            {
                dropZone.BackColor = C_PANEL;
                dropLbl.ForeColor = C_GRAY;
            };
            dropZone.DragDrop += (s, e) =>
            {
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

            // v2.0: boton Guidelines que abre la matriz de formatos soportados.
            Button btnGuide = MakeBtn("Format && Files Guidelines", C_INFO);
            btnGuide.Location = new Point(360, 420);
            btnGuide.Size = new Size(280, 36);
            btnGuide.Click += (s, e) => { using (var g = new GuidelinesForm()) g.ShowDialog(this); };
            p.Controls.Add(btnGuide);
        }

        void PickFolder()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Folder with your edited files";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                if (Cfa.WarnIfProtected(this, dlg.SelectedPath)) sourceDir = dlg.SelectedPath;
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
            logBox.WordWrap = false;
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
            p.Controls.Add(MakeSub("Do not close this window. Rebuild may take a while depending on hardware.", 0, 50, 900));

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
            logBox.WordWrap = false;
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
            if (currentStep == 0)
            {
                if (sourceDir == null || !Directory.Exists(sourceDir))
                {
                    MessageBox.Show("Drop or choose a folder first.", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // v2.0: preguntar si los archivos ya estan preparados o si hay
                // que convertirlos con los conversores del toolkit.
                int convertibleCount = 0;
                int fileCount = 0;
                int validHashCount = 0;
                try
                {
                    var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                    fileCount = files.Length;
                    foreach (var f in files)
                    {
                        var cls = Rage2Toolkit.AssetConverters.Classify(f);
                        if (cls.Status == "convert") convertibleCount++;

                        // v2.0: hash-based name detection
                        string bn = Path.GetFileNameWithoutExtension(f);
                        ulong hh;
                        string hhHex;
                        if (TryParseHashFromName(bn, out hh, out hhHex))
                            validHashCount++;
                    }
                }
                catch { }

                // v2.0: si NINGUN archivo tiene nombre-hash, parar con explicacion clara.
                if (fileCount > 0 && validHashCount == 0)
                {
                    string warnMsg =
                        "None of the files in this folder have a valid hash-based name.\r\n\r\n" +
                        "RAGE 2 identifies every asset by a 64-bit hash, and the toolkit uses\r\n" +
                        "the FILENAME to know WHICH asset you want to replace.\r\n\r\n" +
                        "  VALID example:    4E37BD8EAD14BEA2.png\r\n" +
                        "  INVALID example:  wallpaper.png\r\n\r\n" +
                        "If the filename is not a 16-char hex hash, the toolkit cannot guess\r\n" +
                        "what the file is supposed to replace. The repack would do nothing.\r\n\r\n" +
                        "Correct workflow:\r\n" +
                        "  1. EXTRACT the game (or use the 'Browse & pick manually' option\r\n" +
                        "     in the Extract wizard to grab a single asset).\r\n" +
                        "  2. You get files like 4E37BD8EAD14BEA2.dds.\r\n" +
                        "  3. Convert to PNG if you want to edit in Photoshop/GIMP:\r\n" +
                        "     4E37BD8EAD14BEA2.dds -> 4E37BD8EAD14BEA2.png\r\n" +
                        "  4. Edit the PNG. Save it.\r\n" +
                        "  5. KEEP THE SAME FILENAME. Only the extension may change.\r\n" +
                        "  6. Drop the folder here and press Next again.\r\n\r\n" +
                        "See 'Format & Files Guidelines' for the full explanation.\r\n\r\n" +
                        "Continue anyway? (nothing will be repackable)";
                    var r = MessageBox.Show(this, warnMsg, "No valid hashes found",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                    if (r != DialogResult.OK) return;
                }

                int choice = 0;
                using (var dlg = new ConvertChoiceForm(fileCount, convertibleCount))
                {
                    dlg.ShowDialog(this);
                    choice = dlg.Choice;
                }

                if (choice == 0) return;  // cancel

                if (choice == 2)
                {
                    // Convertir primero
                    string newDir = ConvertDropped(sourceDir);
                    if (newDir != null && Directory.Exists(newDir)) sourceDir = newDir;
                }

                GotoStep(1);
                StartScan();
                return;
            }
            if (currentStep == 1)
            {
                if (!scanDone)
                {
                    MessageBox.Show("Please wait for the analysis to finish.", "In progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GotoStep(2);
                return;
            }
            if (currentStep == 2)
            {
                var okFiles = pending.Where(f => f.Ok).ToList();
                if (okFiles.Count == 0)
                {
                    MessageBox.Show("No valid files to apply.", "Nothing to do", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                GotoStep(3);
                StartRebuild();
                return;
            }
            if (currentStep == 3)
            {
                if (!rebuildDone)
                {
                    MessageBox.Show("Please wait for the rebuild to finish.", "In progress", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                GotoStep(4);
                return;
            }
            ApplyFinalActions();
            this.Close();
        }

        // v2.0: cruza los archivos droppeados con filelist + type_map + validadores + clasificador.
        // Escribe resumen en el log del wizard. No bloquea nada; solo informa.
        // Llamado desde StartScan() despues de llenar `pending`.
        void AnalyzeDropped()
        {
            try
            {
                if (sourceDir == null || !Directory.Exists(sourceDir)) return;

                var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                Log("");
                Log("--- File analysis (" + files.Length + " files) ---");

                int nativeCount = 0, convertCount = 0, manualCount = 0, unknownCount = 0;
                int validCount = 0, warnCount = 0, invalidCount = 0, valUnknownCount = 0;
                int hashFound = 0, hashMissing = 0, invalidHashCount = 0;
                var issues = new System.Collections.Generic.List<string>();

                foreach (var f in files)
                {
                    string bn = Path.GetFileNameWithoutExtension(f);
                    string ext = Path.GetExtension(f).ToLowerInvariant();

                    // Hash lookup contra filelist
                    string fam = "(unknown family)";
                    ulong hh;
                    string hhHex;
                    if (TryParseHashFromName(bn, out hh, out hhHex))
                    {
                        string path;
                        if (namesByHash.TryGetValue(hh, out path))
                        {
                            hashFound++;
                            int slash = path.IndexOf('/');
                            if (slash > 0) fam = path.Substring(0, slash);
                            else fam = "(root)";
                        }
                        else hashMissing++;
                    }
                    else
                    {
                        // v2.0: filename no es un hash de 16 hex chars.
                        // El writer NO podra repackearlo. Avisamos claramente.
                        invalidHashCount++;
                    }

                    // Clasificacion de conversion
                    var cls = Rage2Toolkit.AssetConverters.Classify(f);
                    switch (cls.Status)
                    {
                        case "native":   nativeCount++; break;
                        case "convert":  convertCount++; break;
                        case "manual":   manualCount++; break;
                        default:         unknownCount++; break;
                    }

                    // Validacion
                    var val = Rage2Toolkit.AssetValidators.ValidateFile(f);
                    switch (val.Status)
                    {
                        case Rage2Toolkit.ValidationStatus.Valid:   validCount++; break;
                        case Rage2Toolkit.ValidationStatus.Warn:    warnCount++; break;
                        case Rage2Toolkit.ValidationStatus.Invalid: invalidCount++; break;
                        default:                                     valUnknownCount++; break;
                    }

                    // Guardar issues relevantes
                    if (cls.Status == "convert")
                        issues.Add("  CONVERT: " + Path.GetFileName(f) + " -> " + cls.Reason + " (tool: " + cls.Tool + ")");
                    else if (cls.Status == "manual")
                        issues.Add("  MANUAL:  " + Path.GetFileName(f) + " -> " + cls.Reason + " (tool: " + cls.Tool + ")");
                    else if (cls.Status == "unknown")
                        issues.Add("  UNKNOWN: " + Path.GetFileName(f) + " (extension " + ext + " sin converter; el writer lo aceptara igual)");

                    if (val.Status == Rage2Toolkit.ValidationStatus.Invalid)
                        issues.Add("  INVALID: " + Path.GetFileName(f) + " -> " + string.Join("; ", val.Errors));
                    else if (val.Status == Rage2Toolkit.ValidationStatus.Warn)
                        issues.Add("  WARN:    " + Path.GetFileName(f) + " -> " + string.Join("; ", val.Warnings));
                }

                Log("  Hash lookup: found=" + hashFound + " missing=" + hashMissing + " invalid-name=" + invalidHashCount);
                if (invalidHashCount > 0)
                {
                    Log("  NOTE: " + invalidHashCount + " file(s) have a filename that is NOT a 16-char hex hash.");
                    Log("        These will be IGNORED at repack. RAGE 2 identifies every asset by its hash,");
                    Log("        and the toolkit uses the FILENAME to know which asset you are replacing.");
                    Log("        Rename them to <16-hex>.ext (see Format & Files Guidelines).");
                }
                Log("  Clasificacion: native=" + nativeCount + " convert=" + convertCount + " manual=" + manualCount + " unknown=" + unknownCount);
                Log("  Validacion: Valid=" + validCount + " Warn=" + warnCount + " Invalid=" + invalidCount + " Unknown=" + valUnknownCount);
                Log("  Issues (" + issues.Count + "):");
                int shown = Math.Min(issues.Count, 60);
                for (int i = 0; i < shown; i++) Log(issues[i]);
                if (issues.Count > shown) Log("  ... (" + (issues.Count - shown) + " more)");
                Log("--- End analysis ---");
                Log("");
            }
            catch (Exception ex)
            {
                Log("AnalyzeDropped error: " + ex.Message);
            }
        }
        // v2.0: recorre sourceDir, convierte los archivos que necesitan conversion
        // con los conversores del toolkit, y devuelve un dir nuevo que contiene
        // SOLO archivos nativos listos para repack. Los originales quedan intactos.
        // Si todo falla, retorna el sourceDir original (sin cambios).
        string ConvertDropped(string originalDir)
        {
            try
            {
                string convertedDir = Path.Combine(originalDir, "__converted__");
                if (Directory.Exists(convertedDir))
                {
                    try { Directory.Delete(convertedDir, true); } catch { }
                }
                Directory.CreateDirectory(convertedDir);

                string toolkitRel = Path.GetDirectoryName(Application.ExecutablePath);
                if (string.IsNullOrEmpty(toolkitRel))
                {
                    Log("Cannot determine toolkit release dir");
                    return originalDir;
                }

                var files = Directory.GetFiles(originalDir, "*.*", SearchOption.AllDirectories);
                int okConv = 0, okCopy = 0, skipped = 0, failed = 0;

                Log("");
                Log("--- Converting files ---");

                foreach (var f in files)
                {
                    // Ignorar lo que ya este en __converted__
                    if (f.StartsWith(convertedDir, StringComparison.OrdinalIgnoreCase)) continue;

                    // v2.0: skip archivos sin nombre-hash. No son repackeables y no
                    // tiene sentido gastar tiempo convirtiendolos o copiandolos.
                    string bn0 = Path.GetFileNameWithoutExtension(f);
                    ulong hh0;
                    string hh0Hex;
                    if (!TryParseHashFromName(bn0, out hh0, out hh0Hex))
                    {
                        skipped++;
                        Log("  SKIP " + Path.GetFileName(f) + " (filename is not a 16-char hex hash)");
                        continue;
                    }

                    string rel = f.Substring(originalDir.Length).TrimStart('\\');
                    string clsStatus = Rage2Toolkit.AssetConverters.Classify(f).Status;

                    if (clsStatus == "convert")
                    {
                        var r = Rage2Toolkit.AssetConverters.Convert(
                            f,
                            convertedDir,
                            toolkitRel,
                            "auto",
                            msg => Log("  " + msg));

                        if (r.Status == "OK" && !string.IsNullOrEmpty(r.OutputPath))
                        {
                            // El nombre final mantiene el hash original
                            string bn = Path.GetFileNameWithoutExtension(f);
                            string wantName = bn + Path.GetExtension(r.OutputPath);
                            string wantPath = Path.Combine(convertedDir, wantName);
                            // Si el output real esta en subdir, moverlo a la raiz del convertedDir
                            if (!string.Equals(r.OutputPath, wantPath, StringComparison.OrdinalIgnoreCase))
                            {
                                try { if (File.Exists(wantPath)) File.Delete(wantPath); File.Move(r.OutputPath, wantPath); }
                                catch { }
                            }
                            okConv++;
                            Log("  OK  " + Path.GetFileName(f) + " -> " + Path.GetFileName(r.OutputPath));
                        }
                        else
                        {
                            failed++;
                            Log("  FAIL " + Path.GetFileName(f) + ": " + string.Join("; ", r.Errors));
                        }
                    }
                    else if (clsStatus == "native" || clsStatus == "unknown")
                    {
                        // Copia tal cual manteniendo estructura
                        string dest = Path.Combine(convertedDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        try { File.Copy(f, dest, true); okCopy++; }
                        catch (Exception ex) { failed++; Log("  FAIL copy " + Path.GetFileName(f) + ": " + ex.Message); }
                    }
                    else // manual
                    {
                        skipped++;
                        Log("  SKIP " + Path.GetFileName(f) + " (manual conversion required: " + Rage2Toolkit.AssetConverters.Classify(f).Tool + ")");
                    }
                }

                Log("");
                Log("--- Conversion summary ---");
                Log("  converted: " + okConv + "  copied: " + okCopy + "  skipped: " + skipped + "  failed: " + failed);
                Log("  output: " + convertedDir);
                Log("");

                if (okConv == 0 && okCopy == 0)
                {
                    Log("  Nothing to use. Keeping original folder.");
                    return originalDir;
                }

                return convertedDir;
            }
            catch (Exception ex)
            {
                Log("ConvertDropped error: " + ex.Message);
                return originalDir;
            }
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

                        ulong hh;
                        string hhHex;
                        if (!TryParseHashFromName(bn, out hh, out hhHex))
                        {
                            pf.Ok = false;
                            pf.Reason = "Filename is not a 16-char hex hash";
                            pending.Add(pf);
                            continue;
                        }

                        pf.HashHex = hhHex;
                        pf.Hash = hh;

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
                            if (pf.Ext == ".dds") pf.Reason = "DDS -> AVTX (auto-convert)";
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

                    this.Invoke(new Action(() =>
                    {
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Value = 100;
                        PopulatePreview();
                        scanDone = true;
                AnalyzeDropped();
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
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

                        var repl = new Dictionary<ulong, byte[]>();
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
                                    if (File.Exists(mip))
                                    {
                                        RunTool(ddsc, "\"" + mip + "\"");
                                        var avtx = Path.ChangeExtension(mip, ".avtx");
                                        if (File.Exists(avtx)) payload = File.ReadAllBytes(avtx);
                                        else { Log("    \u2717 ddsc failed"); continue; }
                                    }
                                    else { Log("    \u2717 texconv failed"); continue; }
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

                            repl[f.Hash] = payload;
                            Log("  " + f.HashHex + " ready (" + payload.Length + " bytes)");
                        }

                        string tabPath = tabByArc[arcName];
                        string arcPath = Path.ChangeExtension(tabPath, ".arc");
                        string outDir = Path.Combine(Path.GetDirectoryName(arcPath), "MOD_" + arcName);
                        Directory.CreateDirectory(outDir);
                        string outTab = Path.Combine(outDir, arcName + "_mod.tab");
                        string outArc = Path.Combine(outDir, arcName + "_mod.arc");

                        Log("  Rebuilding...");
                        RepackerCore.Rebuild(tabPath, arcPath, repl, outTab, outArc, m => Log("    " + m));
                        Log("  \u2713 " + outArc);
                        Log("");
                    }

                    this.Invoke(new Action(() =>
                    {
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Value = 100;
                        rebuildDone = true;
                        Log("\u2713 Rebuild complete.");
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        Log("\u2717 " + ex.Message);
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        void RunTool(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                var p = Process.Start(psi);
                p.WaitForExit();
            }
            catch { }
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
                            "RAGE 2 Mod\r\n\r\nContains:\r\n  " + arcName + "_mod.arc\r\n  " + arcName + "_mod.tab\r\n\r\nHow to install:\r\n  1. Copy both files to the SAME folder the original .arc was extracted from.\r\n  2. Rename them to remove the '_mod' suffix so they replace the originals\r\n  3. Back up the originals first\r\n\r\nMade with RAGE 2 Modding Toolkit " + AppInfo.Display + " by Kry0genik\r\n");
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
            if (logBox == null) { System.Diagnostics.Debug.WriteLine("[wizard-pre-ui] " + msg); return; }
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg))); return; }
            logBox.AppendText(msg + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.ScrollToCaret();
        }
    }
}

