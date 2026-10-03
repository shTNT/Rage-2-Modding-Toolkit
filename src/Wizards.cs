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
        public string TitleText { get; set; }
        public Font TitleFont { get; set; }
        public Color TitleColor { get; set; }

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
            TitleText = "";
            TitleFont = null;
            TitleColor = Color.White;
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

        public void SimulateHover(bool on) { BeginHover(on); }

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

            if (!string.IsNullOrEmpty(TitleText) && TitleFont != null)
            {
                int W = this.ClientRectangle.Width;
                int H = this.ClientRectangle.Height;
                int padX = 24;
                int padTop = 34;
                Size titleSz = TextRenderer.MeasureText(g, TitleText, TitleFont, new Size(W - 2 * padX, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                int gap = 16;
                Rectangle titleRect = new Rectangle(padX, padTop, W - 2 * padX, titleSz.Height);
                Rectangle descRect = new Rectangle(padX, titleRect.Bottom + gap, W - 2 * padX, H - titleRect.Bottom - gap - 16);
                TextRenderer.DrawText(g, TitleText, TitleFont, titleRect, TitleColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(g, this.Text, this.Font, descRect, this.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else
            {
                TextRenderer.DrawText(
                    g, this.Text, this.Font, this.ClientRectangle, this.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                    | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
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
                "FILE BROWSER\n& EXTRACTOR",
                "Pull assets out of the game\ninto editable formats:\n\ntextures (DDS), audio (OGG),\nvideo (BK2), scripts, UI...",
                C_INFO);
            btnExtract.Click += (s, e) => { new WizardExtract(gamePath, outputPath).ShowDialog(this); };
            center.Controls.Add(btnExtract);
            btnExtractRef = (RoundedButton)btnExtract;
            AttachReflection(center, (RoundedButton)btnExtract, C_INFO);

            Button btnRepack = MakeBigButton(
                "MODDING",
                "Mod Manager (Install / Remove Mods)\n\n& Repacker for sharing your mods",
                C_MAGENTA);
            btnRepack.Click += (s, e) => { new WizardModding(gamePath, outputPath).ShowDialog(this); };
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
            linkOutput.AutoSize = false;
            linkOutput.Location = new Point(40, 128);
            linkOutput.Size = new Size(this.ClientSize.Width - 40 - 480, 22);
            linkOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            linkOutput.AutoEllipsis = true;
            linkOutput.Font = new Font("Segoe UI", 9);
            linkOutput.LinkColor = C_OK;
            linkOutput.ActiveLinkColor = Color.White;
            linkOutput.VisitedLinkColor = C_OK;
            linkOutput.Text = "";
            linkOutput.LinkClicked += (s, e) =>
            {
                if (outputPath != null && Directory.Exists(outputPath))
                {
                    using (var p = Process.Start("explorer.exe", outputPath)) { }
                }
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
            btnGame.Location = new Point(this.ClientSize.Width - 400, 55);
            btnGame.Size = new Size(180, 42);
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
            btnOutput.Location = new Point(this.ClientSize.Width - 400, 113);
            btnOutput.Size = new Size(180, 42);
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
            btnOpenOut.CornerRadius = 10;
            btnOpenOut.BorderColor = Color.FromArgb(90, 90, 110);
            btnOpenOut.BorderThickness = 1;
            btnOpenOut.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnOpenOut.Location = new Point(this.ClientSize.Width - 200, 113);
            btnOpenOut.Size = new Size(180, 42);
            btnOpenOut.FlatStyle = FlatStyle.Flat;
            btnOpenOut.FlatAppearance.BorderSize = 0;
            btnOpenOut.BackColor = C_BTN;
            btnOpenOut.ForeColor = Color.White;
            btnOpenOut.Font = new Font("Segoe UI", 10);
            btnOpenOut.Cursor = Cursors.Hand;
            btnOpenOut.Click += (s, e) =>
            {
                if (outputPath != null && Directory.Exists(outputPath))
                {
                    using (var p = Process.Start("explorer.exe", outputPath)) { }
                }
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
            btnUpdate.Location = new Point(this.ClientSize.Width - 200, 55);
            btnUpdate.Size = new Size(180, 42);
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
                if (manual) DarkDialog.Warn(lastUpdaterMsg, "Update check", Color.FromArgb(220, 80, 220));
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
                btnExtractRef.TitleColor = ready ? C_INFO : C_GRAY;
                btnExtractRef.Cursor = ready ? Cursors.Hand : Cursors.Default;
                btnExtractRef.Invalidate();
                var r = btnExtractRef.Tag as ReflectionPanel;
                if (r != null) { r.ReflectionColor = ready ? C_INFO : C_GRAY; r.Invalidate(); }
            }
            if (btnRepackRef != null)
            {
                btnRepackRef.Enabled = ready;
                btnRepackRef.BorderColor = ready ? C_MAGENTA : C_GRAY;
                btnRepackRef.TitleColor = ready ? C_MAGENTA : C_GRAY;
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
                    string abbr = Abbreviate.ShortPath(outputPath, 35);
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
            b.Font = new Font("Segoe UI", 10);
            b.Text = desc;
            b.TitleText = title;
            b.TitleFont = new Font("Segoe UI", 16, FontStyle.Bold);
            b.TitleColor = accent;
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
        protected Button btnBack, btnNext, btnCancel;
        protected int currentStep = 0;
        protected List<Panel> stepPanels = new List<Panel>();
        protected Color AccentColor = Color.FromArgb(0, 200, 200);
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
        }

        protected Panel headerBar;
        protected Label headerTitle;

        public WizardBase(string title, int totalSteps)
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.Shown += (s, e) => { try { this.Invalidate(true); this.Refresh(); } catch { } };
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

            headerBar = new AccentBar();
            headerBar.Dock = DockStyle.Top;
            headerBar.Height = 6;
            headerBar.BackColor = AccentColor;
            header.Controls.Add(headerBar);

            headerTitle = new Label();
            headerTitle.Text = title.ToUpper();
            headerTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            headerTitle.ForeColor = AccentColor;
            headerTitle.Location = new Point(30, 12);
            headerTitle.AutoSize = true;
            header.Controls.Add(headerTitle);

            stepLbl = new Label();
            stepLbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            stepLbl.ForeColor = C_GRAY;
            stepLbl.Location = new Point(32, 48);
            stepLbl.AutoSize = true;
            header.Controls.Add(stepLbl);


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

            btnBack = MakeBtn("Back", C_BTN);
            btnBack.Location = new Point(this.ClientSize.Width - 340, 18);
            btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnBack.Click += (s, e) =>
            {
                if (currentStep <= 0) { this.Close(); }
                else { GotoStep(currentStep - 1); }
            };
            bottom.Controls.Add(btnBack);

            btnNext = MakeBtn("Next >", AccentColor);
            btnNext.Location = new Point(this.ClientSize.Width - 200, 18);
            btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnNext.ForeColor = Color.Black;
            btnNext.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnNext.Click += (s, e) => OnNextClicked();
            bottom.Controls.Add(btnNext);
            
            btnCancel = MakeBtn("Cancel", C_BTN);
            btnCancel.Location = new Point(this.ClientSize.Width - 200, 18);
            btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnCancel.Visible = false;
            btnCancel.Click += (s, e) => OnCancelClicked();
            bottom.Controls.Add(btnCancel);

            this.Load += (s, e) => GotoStep(0);
        }

        protected virtual void OnCancelClicked() { this.Close(); }
        
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
            btnBack.Enabled = true;
            btnNext.Text = (n == stepPanels.Count - 1) ? "Finish" : "Next >";
            OnStepShown(n);
        }

        protected virtual void OnStepShown(int n) { }
        protected abstract void OnNextClicked();

        protected void SetAccent(Color c)
        {
            AccentColor = c;
            try { if (headerBar != null) headerBar.BackColor = c; } catch { }
            try { if (headerTitle != null) headerTitle.ForeColor = c; } catch { }
            try { if (btnNext != null) { btnNext.BackColor = c; btnNext.Invalidate(); } } catch { }
        }
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
        volatile bool _cancelRequested = false;
        System.Threading.CancellationTokenSource _cts = null;

        public WizardExtract(string gamePath, string outputPath)
            : base("Wizard: Extract assets", 4)
        {
            this.gamePath = gamePath;
            this.outputPath = outputPath;
            BuildStep1();
            BuildStep2();
            BuildStep3();
            BuildStep4();

            // Gear: conversion preferences (solo en Extract)
            var gearBtnWE = new RoundedButton();
            gearBtnWE.Text = "\u2699";
            gearBtnWE.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            gearBtnWE.ForeColor = C_INFO;
            gearBtnWE.BackColor = C_HEAD;
            gearBtnWE.BorderColor = C_INFO;
            gearBtnWE.BorderThickness = 2;
            gearBtnWE.CornerRadius = 20;
            gearBtnWE.HoverColor = Color.FromArgb(32, 60, 60);
            gearBtnWE.Size = new Size(40, 40);
            gearBtnWE.TextAlign = ContentAlignment.MiddleCenter;
            gearBtnWE.Cursor = Cursors.Hand;
            gearBtnWE.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gearBtnWE.Location = new Point(this.ClientSize.Width - 60, 20);
            gearBtnWE.Click += (s, e) => {
                try {
                    string _prefsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "conversion_prefs.json");
                    string _res = "1024", _fmt = "png", _aud = "keep";
                    if (File.Exists(_prefsPath)) {
                        using (var _d = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_prefsPath))) {
                            System.Text.Json.JsonElement _v;
                            if (_d.RootElement.TryGetProperty("resolution", out _v)) _res = _v.GetString() ?? "1024";
                            if (_d.RootElement.TryGetProperty("format", out _v)) _fmt = _v.GetString() ?? "png";
                            if (_d.RootElement.TryGetProperty("audio", out _v)) _aud = _v.GetString() ?? "keep";
                        }
                    }
                    using (var dlg = new ConversionPrefsForm(_res, _fmt, _aud)) {
                        dlg.ShowDialog(this);
                        if (dlg.Saved) {
                            string _json = "{\"resolution\":\"" + dlg.Resolution + "\",\"format\":\"" + dlg.Format + "\",\"audio\":\"" + dlg.Audio + "\"}";
                            File.WriteAllText(_prefsPath, _json);
                        }
                    }
                } catch { }
            };
            this.Controls.Add(gearBtnWE);
            gearBtnWE.BringToFront();
            if (btnNext != null)
            {
                btnNext.Location = new Point(this.ClientSize.Width - 200, 18);
                btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                btnNext.Text = "Extract all";
            }
            if (btnBack != null)
            {
                btnBack.Location = new Point(30, 18);
                btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }
        }

        protected override void OnCancelClicked()
        {
            if (currentStep == 2)
            {
                _cancelRequested = true;
                try { if (_cts != null) _cts.Cancel(); } catch { }
                if (btnCancel != null)
                {
                    btnCancel.Text = "Cancelling...";
                    btnCancel.Enabled = false;
                }
                return;
            }
            base.OnCancelClicked();
        }

        protected override void OnStepShown(int n)
        {
            base.OnStepShown(n);
            if (btnNext != null)
            {
                btnNext.Visible = (n != 0 && n != 2);
                btnNext.Enabled = true;
                btnNext.Location = new Point(this.ClientSize.Width - 200, 18);
                btnNext.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                if (n == 1) btnNext.Text = "Start extraction";
                else if (n == 2) btnNext.Text = "Wait...";
                else btnNext.Text = "Finish";
            }
            if (btnBack != null)
            {
                btnBack.Visible = (n != 2);
                btnBack.Enabled = true;
                btnBack.Location = new Point(30, 18);
                btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }
            if (btnCancel != null)
            {
                if (n == 2)
                {
                    btnCancel.Visible = true;
                    btnCancel.Enabled = true;
                    btnCancel.Text = "Cancel extraction";
                    btnCancel.Location = new Point(this.ClientSize.Width - 200, 18);
                    btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
                }
                else { btnCancel.Visible = false; }
            }
        }

                                void BuildStep1()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("What do you want to extract?", 0, 10));

            // Info tooltip con acento cyan
            var info = new InfoTooltipPanel();
            info.Size = new Size(640, 140);
            info.AccentColor = C_INFO;
            info.Reset();
            p.Controls.Add(info);

            // ---- Boton CORE grande: BROWSE & PICK MANUALLY ----
            btnSingle = new RoundedButton();
            btnSingle.Text = "BROWSE & PICK MANUALLY";
            btnSingle.CornerRadius = 14;
            btnSingle.BorderThickness = 3;
            btnSingle.BorderColor = C_INFO;
            btnSingle.BackColor = C_BTN;
            btnSingle.ForeColor = Color.White;
            btnSingle.Font = new Font("Segoe UI", 17, FontStyle.Bold);
            btnSingle.Cursor = Cursors.Hand;
            btnSingle.Size = new Size(640, 92);
            btnSingle.Click += (s, e) => { SetToggleAll(false); new SingleExtractForm(gamePath, outputPath).ShowDialog(this); };
            p.Controls.Add(btnSingle);

            // ---- Boton secundario: EXTRACT EVERYTHING ----
            btnAll = new RoundedButton();
            btnAll.Text = "EXTRACT EVERYTHING";
            btnAll.CornerRadius = 10;
            btnAll.BorderThickness = 2;
            btnAll.BorderColor = Color.FromArgb(120, 170, 180);
            btnAll.BackColor = C_BTN;
            btnAll.ForeColor = Color.White;
            btnAll.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnAll.Cursor = Cursors.Hand;
            btnAll.Size = new Size(640, 70);
            btnAll.Click += (s, e) => { SetToggleAll(true); GotoStep(1); };
            p.Controls.Add(btnAll);

            Action<RoundedButton, string, string> wireHover = (btn, title, desc) =>
            {
                btn.MouseEnter += (s, e) => { info.ShowInfo(title, desc); };
                btn.MouseLeave += (s, e) => { info.HideInfo(); };
            };

            wireHover(btnSingle, "\u25C6  BROWSE & PICK MANUALLY",
                "Open the asset browser and pick exactly what you need.\n\nSearch by keyword, extension or hash. The recommended way to extract assets for a specific mod.");

            wireHover(btnAll, "\u25B6  EXTRACT EVERYTHING",
                "Extract every asset from the game in one go: textures, audio, video, scripts and UI.\n\nBest if you don't know yet which assets you need, or want a complete reference dump of the game.\n\nRoughly 60 GB. Takes several minutes.");

            Action layout = () =>
            {
                int w = p.ClientSize.Width;
                if (w < 100) return;
                int cx = w / 2;

                btnSingle.Location = new Point(cx - btnSingle.Width / 2, 140);
                btnAll.Location = new Point(cx - btnAll.Width / 2, 270);

                int infoLeft = cx - info.Width / 2;
                if (infoLeft < 20) infoLeft = 20;
                int infoTop = 380;
                int maxTop = p.ClientSize.Height - info.Height - 40;
                if (maxTop < 280) maxTop = 280;
                if (infoTop > maxTop) infoTop = maxTop;
                info.Location = new Point(infoLeft, infoTop);
            };

            p.SizeChanged += (s, e) => layout();
            p.Layout += (s, e) => layout();
            p.VisibleChanged += (s, e) => { if (p.Visible) { try { p.BeginInvoke(new Action(layout)); } catch { } } };
        }

        void SetToggleAll(bool all)
        {
            _allSelected = all;
            if (all)
            {
                if (btnAll != null) { btnAll.BorderThickness = 2; btnAll.BorderColor = C_INFO; }
                if (btnSingle != null) { btnSingle.BorderThickness = 2; btnSingle.BorderColor = Color.FromArgb(120, 170, 180); }
                SetSubs(true);
            }
            else
            {
                if (btnAll != null) { btnAll.BorderThickness = 2; btnAll.BorderColor = Color.FromArgb(120, 170, 180); }
                if (btnSingle != null) { btnSingle.BorderThickness = 3; btnSingle.BorderColor = C_INFO; }
                SetSubs(false);
            }
            try { if (btnAll != null) { btnAll.Invalidate(); btnAll.Refresh(); } } catch { }
            try { if (btnSingle != null) { btnSingle.Invalidate(); btnSingle.Refresh(); } } catch { }
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
            // checkboxes retirados de la UI - ahora son no-op
            if (cbTex != null) { cbTex.Enabled = !enabled; cbTex.Checked = enabled; }
            if (cbAudio != null) { cbAudio.Enabled = !enabled; cbAudio.Checked = enabled; }
            if (cbVideo != null) { cbVideo.Enabled = !enabled; cbVideo.Checked = enabled; }
            if (cbScripts != null) { cbScripts.Enabled = !enabled; cbScripts.Checked = enabled; }
            if (cbUI != null) { cbUI.Enabled = !enabled; cbUI.Checked = enabled; }
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
                    OnNextClicked();
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
                    OnNextClicked();
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
            logBox.HideSelection = false;
            logBox.WordWrap = false;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.ScrollBars = RichTextBoxScrollBars.ForcedBoth;
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
                {
                    using (var p = Process.Start("explorer.exe", outputPath)) { }
                }
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
                    DarkDialog.Warn("Please choose an output folder first.", "Missing folder", Color.FromArgb(220, 80, 220));
                    return;
                }
                if (gamePath == null || !Directory.Exists(gamePath))
                {
                    DarkDialog.Warn("Please set the game folder first.", "Missing game folder", Color.FromArgb(220, 80, 220));
                    return;
                }
                if (!Oodle.IsLoaded)
                {
                    DarkDialog.Warn("Oodle runtime not loaded. Select RAGE2.exe in the main window first.", "Oodle missing", Color.FromArgb(220, 80, 220));
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
                    DarkDialog.Info("Please wait for the extraction to finish.", "In progress", Color.FromArgb(220, 80, 220));
                    return;
                }
                GotoStep(3);
                return;
            }
            this.Close();
        }

        void StartExtract()
        {
            try { if (_cts != null) { _cts.Dispose(); _cts = null; } } catch { }
            _cancelRequested = false;
            _cts = new System.Threading.CancellationTokenSource();
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
                            try {
                                var _subdirs = BuildHashSubdirMapFromTree();
                                if (_subdirs.Count > 0) {
                                    ExtractorOpt.PathResolver = (h) => { string v; return _subdirs.TryGetValue(h, out v) ? v : null; };
                                    Log("[ORG] PathResolver active (" + _subdirs.Count + " entries)");
                                }
                            } catch (Exception exOrg) { Log("[ORG] " + exOrg.Message); }
                            Log("[TECH-15] NameResolver active (" + _fl.Count + " entries)");
                        }
                    } catch (Exception ex) { Log("[TECH-15] " + ex.Message); }

                    var st = Extractor.ExtractAll(gamePath, outputPath, msg => Log(msg),
                        delegate(int i, int tot, string n, int c) {
                            if (_cancelRequested) throw new OperationCanceledException("cancelled");
                            this.Invoke(new Action(() => {

                                int pct = (int)((i - 1) * 100.0 / tot);
                                progress.Value = Math.Max(0, Math.Min(100, pct));
                                Log(string.Format("[{0:D2}/{1:D2}] {2,-14}  ({3}%)  {4} entries", i, tot, n, pct, c));
                            }));
                        },
                        delegate(int i, int tot, string n, int ok, int f) {
                            if (_cancelRequested) throw new OperationCanceledException("cancelled");
                            this.Invoke(new Action(() => {
                                progress.Value = i;
                                int pct = (int)(i * 100.0 / tot);
                                Log(string.Format("       {0,-14}  ok={1,6}  fail={2,4}   ({3}% total)", n, ok, f, pct));
                            }));
                        },
                        true, _cts.Token);
                    swTotal.Stop();
                    ExtractorOpt.NameResolver = null;
                      ExtractorOpt.PathResolver = null;
                      try { if (_cts != null) { _cts.Dispose(); _cts = null; } } catch { }
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
                    try
                    {
                        ConvertAvtxInPlace();
                    }
                    catch (Exception exCl)
                    {
                        try { File.WriteAllText(System.IO.Path.Combine(@"D:\RAGE2MODDING\_outputs", "_diag_classify_err.txt"), DateTime.Now.ToString("o") + "\r\n" + exCl.ToString()); } catch { }
                        throw;
                    }

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
                        GotoStep(3);
                    }));
                }
                catch (Exception ex)
                {
                    try { File.WriteAllText(System.IO.Path.Combine(@"D:\RAGE2MODDING\_outputs", "_diag_startextract_err.txt"), DateTime.Now.ToString("o") + "\r\n" + ex.ToString()); } catch { }
                    this.Invoke(new Action(() =>
                    {
                        if (_cancelRequested)
                        {
                            Log("");
                            Log("\u26A0 Extraction cancelled by user.");
                            if (btnCancel != null) { btnCancel.Visible = false; btnCancel.Enabled = true; btnCancel.Text = "Cancel extraction"; }
                            if (btnBack != null) { btnBack.Visible = true; btnBack.Enabled = true; }
                            if (btnNext != null) { btnNext.Visible = false; }
                        }
                        else
                        {
                            Log("\u2717 ERROR: " + ex.Message);
                            DarkDialog.Error("Error: " + ex.Message, "Failed", Color.FromArgb(220, 80, 220));
                        }
                    }));
                }
            });
        }
                Dictionary<ulong, string> BuildHashSubdirMapFromTree()
        {
            var map = new Dictionary<ulong, string>();
            try {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "type_map_v3.json");
                if (!File.Exists(jsonPath)) return map;
                string json = File.ReadAllText(jsonPath);
                using (var doc = System.Text.Json.JsonDocument.Parse(json)) {
                    var root = doc.RootElement;
                    int schemaV3 = 0;
                    System.Text.Json.JsonElement se;
                    if (root.TryGetProperty("schema", out se)) schemaV3 = se.GetInt32();
                    if (schemaV3 != 3) return map;
                    System.Text.Json.JsonElement tree;
                    if (!root.TryGetProperty("tree", out tree)) return map;
                    foreach (var catProp in tree.EnumerateObject()) {
                        string catName = SanitizeFolderP2(catProp.Name);
                        WalkTree(catProp.Value, catName, map);
                    }
                }
            } catch { }
            return map;
        }

        void WalkTree(System.Text.Json.JsonElement node, string path, Dictionary<ulong, string> map)
        {
            System.Text.Json.JsonElement assets;
            if (node.TryGetProperty("assets", out assets) && assets.ValueKind == System.Text.Json.JsonValueKind.Array) {
                foreach (var a in assets.EnumerateArray()) {
                    System.Text.Json.JsonElement hEl;
                    if (!a.TryGetProperty("h", out hEl)) continue;
                    string hStr = hEl.GetString() ?? "";
                    ulong hv;
                    if (!ulong.TryParse(hStr, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out hv)) continue;
                    if (!map.ContainsKey(hv)) map[hv] = path;
                }
            }
            System.Text.Json.JsonElement ch;
            if (node.TryGetProperty("children", out ch) && ch.ValueKind == System.Text.Json.JsonValueKind.Object) {
                foreach (var child in ch.EnumerateObject()) {
                    string childPath = path + "/" + SanitizeFolderP2(child.Name);
                    WalkTree(child.Value, childPath, map);
                }
            }
        }

        static string SanitizeFolderP2(string s)
        {
            if (string.IsNullOrEmpty(s)) return "_";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s) {
                if (c == (char)32) sb.Append((char)95);
                else if (char.IsLetterOrDigit(c) || c == (char)95 || c == (char)45 || c == (char)46) sb.Append(c);
                else sb.Append((char)95);
            }
            return sb.ToString();
        }

        void ConvertAvtxInPlace()
        {
            string ddsc = Path.Combine(Paths.BinDir, "ddscConvert.exe");
            if (!File.Exists(ddsc)) { Log("  ddscConvert.exe no encontrado en " + Paths.BinDir); return; }
            var avtxFiles = Directory.GetFiles(outputPath, "*.avtx", SearchOption.AllDirectories)
                .Where(f => !f.Contains("__tmp_rev") && !f.Contains("__atxtmp")).ToList();
            int total = avtxFiles.Count;
            int converted = 0;
            Log("  [avtx] encontrados: " + total);
            foreach (var a in avtxFiles) {
                if (_cancelRequested) throw new OperationCanceledException("cancelled by user");
                try {
                    string dir = Path.GetDirectoryName(a);
                    var psi = new ProcessStartInfo(ddsc, "\"" + a + "\"");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.WorkingDirectory = dir;
                    using (var p = Process.Start(psi)) { p.WaitForExit(30000); if (!p.HasExited) { try { p.Kill(); } catch { } } }
                    string dds = Path.ChangeExtension(a, ".dds");
                    if (File.Exists(dds)) {
                        converted++;
                        try { File.Delete(a); } catch { }
                        string stem = Path.GetFileNameWithoutExtension(a);
                        foreach (var stray in Directory.GetFiles(dir, stem + ".atx*")) {
                            try { File.Delete(stray); } catch { }
                        }
                    }
                } catch { }
                if (total > 0 && (converted % 500) == 0 && converted > 0) {
                    int c = converted;
                    try { this.Invoke(new Action(() => Log("    [avtx] converted: " + c + " / " + total))); } catch { }
                }
            }
            Log("  [avtx] converted: " + converted + " / " + total);
            ddsCount += converted;

            // ATX1 -> DDS/PNG (misma logica que el single)
            string _atxTarget = "dds";
            try {
                string _pp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "conversion_prefs.json");
                if (File.Exists(_pp)) {
                    using (var _d = System.Text.Json.JsonDocument.Parse(File.ReadAllText(_pp))) {
                        System.Text.Json.JsonElement _v;
                        if (_d.RootElement.TryGetProperty("format", out _v)) _atxTarget = _v.GetString() ?? "dds";
                    }
                }
            } catch { }
            string _tkRel = Path.GetDirectoryName(Application.ExecutablePath);
            var atx1Files = Directory.GetFiles(outputPath, "*.atx1", SearchOption.AllDirectories)
                .Where(f => !f.Contains("__tmp_rev") && !f.Contains("__atxtmp")).ToList();
            int atx1Total = atx1Files.Count;
            int atx1Conv = 0;
            Log("  [atx1] encontrados: " + atx1Total + " (target=" + _atxTarget + ")");
            var _po = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 };
            System.Threading.Tasks.Parallel.ForEach(atx1Files, _po, a => {
                if (_cancelRequested) throw new OperationCanceledException("cancelled by user");
                try {
                    if (SingleExtractForm.TryConvertAtx1ToEditable(a, outputPath, _tkRel, _atxTarget)) {
                        int c = System.Threading.Interlocked.Increment(ref atx1Conv);
                        try { File.Delete(a); } catch { }
                        if (c % 500 == 0) {
                            int cc = c;
                            try { this.Invoke(new Action(() => Log("    [atx1] converted: " + cc + " / " + atx1Total))); } catch { }
                        }
                    }
                } catch { }
            });
            Log("  [atx1] converted: " + atx1Conv + " / " + atx1Total);
            ddsCount += atx1Conv;

        }
void ClassifyAndConvert()
        {
            if (_cancelRequested) throw new OperationCanceledException("cancelled by user");
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
                        using (var p = Process.Start(psi))
                        {
                            p.WaitForExit();
                        }
                        foreach (var a in batch) {
                            string dds = Path.ChangeExtension(a, ".dds");
                            if (File.Exists(dds)) {
                                string target = Path.Combine(root, "TEXTURES", Path.GetFileName(dds));
                                try { if (File.Exists(target)) File.Delete(target); File.Move(dds, target); System.Threading.Interlocked.Increment(ref ddsLocal); } catch { }
                            }
                            try { File.Delete(a); } catch { }
                        }
                    } catch (Exception exBatch) {
                        try { File.AppendAllText(System.IO.Path.Combine(@"D:\RAGE2MODDING\_outputs", "_diag_avtx_batch_err.txt"), DateTime.Now.ToString("o") + " | " + exBatch.GetType().FullName + " | " + exBatch.Message + "\r\n" + exBatch.StackTrace + "\r\n\r\n"); } catch { }
                    }
                    int d = System.Threading.Interlocked.Increment(ref doneBatches);
                    if (d % 2 == 0 || d == totalBatches) {
                        int dd = d;
                        try
                        {
                            this.Invoke(new Action(() => {
                                Log("    avtx batches: " + dd + " / " + totalBatches);
                            }));
                        }
                        catch (Exception exInvoke)
                        {
                            try { File.AppendAllText(System.IO.Path.Combine(@"D:\RAGE2MODDING\_outputs", "_diag_avtx_invoke_err.txt"), DateTime.Now.ToString("o") + " | " + exInvoke.GetType().FullName + " | " + exInvoke.Message + "\r\n" + exInvoke.StackTrace + "\r\n\r\n"); } catch { }
                        }
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
                    try
                    {
                        this.Invoke(new Action(() => Log("    move: " + ddm + " / " + totalMove)));
                    }
                    catch (Exception exMoveInvoke)
                    {
                        try { File.AppendAllText(System.IO.Path.Combine(@"D:\RAGE2MODDING\_outputs", "_diag_move_invoke_err.txt"), DateTime.Now.ToString("o") + " | " + exMoveInvoke.GetType().FullName + " | " + exMoveInvoke.Message + "\r\n" + exMoveInvoke.StackTrace + "\r\n\r\n"); } catch { }
                    }
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
            sb.AppendLine("  " + outputPath + "\\  (organized by category / entity / resource)");
            sb.AppendLine("    TEXTURES\\   AUDIO\\   VIDEO\\   SCRIPTS\\   UI\\   OTHER\\");
            sb.AppendLine();
            sb.AppendLine("When done editing, go back to the main screen and use MOD & REPACK.");

            string summary = sb.ToString();
            bool shown = false;
            if (stepPanels.Count > 3)
            {
                try
                {
                    var found = stepPanels[3].Controls.Find("lblStats", true);
                    if (found != null && found.Length > 0 && found[0] is Label lbl)
                    {
                        lbl.Text = summary;
                        shown = true;
                    }
                }
                catch { }
            }
            if (!shown)
            {
                foreach (var line in summary.Split('\n')) Log(line.TrimEnd('\r'));
            }
        }

        protected bool logSilent = false;
        private readonly object logLock = new object();
        private System.Text.StringBuilder logBuf = new System.Text.StringBuilder();

        const int LOG_MAX_LINES = 5000;
        const int LOG_TRIM_LINES = 1000;
        void TrimLogBox()
        {
            try
            {
                if (logBox == null) return;
                int n = logBox.Lines.Length;
                if (n <= LOG_MAX_LINES) return;
                int firstLineStart = logBox.GetFirstCharIndexFromLine(0);
                int trimLineStart = logBox.GetFirstCharIndexFromLine(LOG_TRIM_LINES);
                if (firstLineStart < 0 || trimLineStart <= firstLineStart) return;
                logBox.Select(firstLineStart, trimLineStart - firstLineStart);
                logBox.SelectedText = "";
            }
            catch { }
        }

        protected void Log(string msg)
        {
            if (logSilent) { lock (logLock) { logBuf.Append(msg).Append(Environment.NewLine); } return; }
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg))); return; }
            TrimLogBox();
            logBox.AppendText(msg + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.ScrollToCaret();
        }

        protected void FlushLog()
        {
            string content;
            lock (logLock) { content = logBuf.ToString(); logBuf.Clear(); }
            if (string.IsNullOrEmpty(content)) return;
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(FlushLog)); return; }
            TrimLogBox();
            logBox.AppendText(content);
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
        enum PendingStatus
        {
            Unknown,
            Native,
            Convert,
            Manual,
            InvalidHash,
            HashNotFound,
            Shadowed,
        }

        class PendingFile
        {
            public string Path;
            public ulong Hash;
            public string HashHex;
            public string Ext;
            public string ArcName;
            public string Reason;
            public PendingStatus Status = PendingStatus.Unknown;
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
        // (cbInstall / cbPackage eliminados en rediseno)
        bool scanDone = false;
        string _convertedTempDir = null;
        Dictionary<ulong, string> origExtByHash = new Dictionary<ulong, string>();
        Dictionary<ulong, AssetConverters.AvtxMeta> origMetaByHash = new Dictionary<ulong, AssetConverters.AvtxMeta>();
        Dictionary<ulong, byte[]> origAvtxHeaderByHash = new Dictionary<ulong, byte[]>();

        public WizardRepack(string gamePath)
            : base("Wizard: Mod & Repack", 3)
        {
            this.gamePath = gamePath;
            LoadFilelist();
            BuildStep1();
            BuildStep2();
            BuildStep3();
            SetAccent(C_MAGENTA);
            this.FormClosed += (s, e) =>
            {
                try {
                    if (!string.IsNullOrEmpty(_convertedTempDir) && Directory.Exists(_convertedTempDir))
                        Directory.Delete(_convertedTempDir, true);
                } catch { }
            };
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
                    dropLbl.ForeColor = C_MAGENTA;
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
                else if (File.Exists(dir))
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "RAGE2Repack_single_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(tmp);
                    File.Copy(dir, Path.Combine(tmp, Path.GetFileName(dir)), true);
                    sourceDir = tmp;
                    dropLbl.Text = "\u2713 " + Path.GetFileName(dir) + " (single file)";
                    dropLbl.ForeColor = C_OK;
                }
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
                    dropLbl.ForeColor = C_MAGENTA;
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
                else if (File.Exists(dir))
                {
                    string tmp = Path.Combine(Path.GetTempPath(), "RAGE2Repack_single_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(tmp);
                    File.Copy(dir, Path.Combine(tmp, Path.GetFileName(dir)), true);
                    sourceDir = tmp;
                    dropLbl.Text = "\u2713 " + Path.GetFileName(dir) + " (single file)";
                    dropLbl.ForeColor = C_OK;
                }
            };
            dropZone.Click += (s, e) => PickFolder();

            Button btnPick = MakeBtn("Or browse manually...", C_BTN);
            btnPick.Location = new Point(20, 420);
            btnPick.Size = new Size(320, 36);
            btnPick.Click += (s, e) => PickFolder();
            p.Controls.Add(btnPick);

            // v2.0: boton Guidelines que abre la matriz de formatos soportados.
            Button btnGuide = MakeBtn("Format & Files Guidelines", C_MAGENTA);
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
            logBox.ScrollBars = RichTextBoxScrollBars.ForcedBoth;
            p.Controls.Add(logBox);
        }
        Label lblZipPath;
        Button btnOpenZip;

        void BuildStep3()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("Mod packaged", 0, 10));
            p.Controls.Add(MakeSub("The .zip is ready to share. Drag it into the Mod Manager to install.", 0, 50, 900));

            lblZipPath = new Label();
            lblZipPath.Font = new Font("Consolas", 10);
            lblZipPath.ForeColor = Color.FromArgb(100, 220, 100);
            lblZipPath.Location = new Point(20, 130);
            lblZipPath.Size = new Size(900, 26);
            lblZipPath.Text = "(pending)";
            p.Controls.Add(lblZipPath);

            Label lblHint = new Label();
            lblHint.Text = "Next: open Mod Manager, drag the .zip in. It will verify, order by priority and patch the game.";
            lblHint.ForeColor = Color.FromArgb(200, 200, 200);
            lblHint.Location = new Point(20, 165);
            lblHint.AutoSize = true;
            p.Controls.Add(lblHint);

            btnOpenZip = MakeBtn("Open folder", C_MAGENTA);
            btnOpenZip.ForeColor = Color.Black;
            btnOpenZip.Location = new Point(20, 210);
            btnOpenZip.Size = new Size(200, 40);
            btnOpenZip.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnOpenZip.Click += (s, e) =>
            {
                try
                {
                    string path = lblZipPath.Text;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        using (var p = Process.Start("explorer.exe", "/select,\"" + path + "\"")) { }
                    }
                }
                catch { }
            };
            p.Controls.Add(btnOpenZip);
        }




        protected override void OnNextClicked()
        {
            if (currentStep == 0)
            {
                if (sourceDir == null || !Directory.Exists(sourceDir))
                {
                    DarkDialog.Warn("Drop or choose a folder first.", "Missing folder", Color.FromArgb(220, 80, 220));
                    return;
                }

                int convertibleCount = 0;
                int fileCount = 0;
                int validHashCount = 0;
                try
                {
                    var filesAll = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                    var files = filesAll.Where(x => x.IndexOf("__converted__", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
                    fileCount = files.Length;
                    foreach (var f in files)
                    {
                        var cls = Rage2Toolkit.AssetConverters.Classify(f);
                        if (cls.Status == "convert") convertibleCount++;
                        string bn = Path.GetFileNameWithoutExtension(f);
                        ulong hh; string hhHex;
                        if (TryParseHashFromName(bn, out hh, out hhHex)) validHashCount++;
                    }
                }
                catch { }

                if (fileCount == 0)
                {
                    DarkDialog.Warn("No files in the folder.", "Empty", Color.FromArgb(220, 80, 220));
                    return;
                }
                if (validHashCount == 0)
                {
                    DarkDialog.Warn("None of the files have a valid 16-char hex hash in the filename.\r\n\r\nSee Format & Files Guidelines.", "No valid hashes", Color.FromArgb(220, 80, 220));
                    return;
                }

                int choice = 0;
                using (var dlg = new ConvertChoiceForm(fileCount, convertibleCount))
                {
                    dlg.ShowDialog(this);
                    choice = dlg.Choice;
                }
                if (choice == 0) return;
                if (choice == 2)
                {
                    GotoStep(1);
                    btnNext.Enabled = false;
                    btnBack.Enabled = false;
                    Log("");
                    Log("=== Converting files with toolkit ===");
                    Task.Run(() =>
                    {
                        try
                        {
                            string newDir = ConvertDropped(sourceDir);
                            if (newDir != null && Directory.Exists(newDir)) sourceDir = newDir;
                            this.Invoke(new Action(() =>
                            {
                                btnNext.Enabled = true;
                                btnBack.Enabled = true;
                                StartScan();
                            }));
                        }
                        catch (Exception ex)
                        {
                            this.Invoke(new Action(() =>
                            {
                                btnNext.Enabled = true;
                                btnBack.Enabled = true;
                                Log("\u2717 Convert exception: " + ex.Message);
                                DarkDialog.Error("Convert failed: " + ex.Message, "Error", Color.FromArgb(220, 80, 220));
                            }));
                        }
                    });
                    return;
                }

                GotoStep(1);
                StartScan();
                return;
            }

            if (currentStep == 1)
            {
                if (!scanDone)
                {
                    DarkDialog.Info("Please wait for the analysis to finish.", "In progress", Color.FromArgb(220, 80, 220));
                    return;
                }
                btnNext.Enabled = false;
                btnBack.Enabled = false;
                StartPack();
                return;
            }

            this.Close();
        }

        // v2.0: cruza los archivos droppeados con filelist + type_map + validadores + clasificador.
        // Escribe resumen en el log del wizard. No bloquea nada; solo informa.
        // Llamado desde StartScan() despues de llenar `pending`.
        // v2.0: recorre sourceDir, convierte los archivos que necesitan conversion
        // con los conversores del toolkit, y devuelve un dir nuevo que contiene
        // SOLO archivos nativos listos para repack. Los originales quedan intactos.
        // Si todo falla, retorna el sourceDir original (sin cambios).
        void EnsureOrigExts()
        {
            if (origExtByHash.Count > 0) return;
            try {
                string flPath = null;
                foreach (var cand in new[] { Path.Combine(Paths.DataDir, "filelist.txt"), Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "filelist.txt") }) {
                    if (File.Exists(cand)) { flPath = cand; break; }
                }
                if (flPath == null) return;
                int _n = 0;
                foreach (var line in File.ReadAllLines(flPath)) {
                    var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) continue;
                    ulong h;
                    if (!ulong.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out h)) continue;
                    string x = Path.GetExtension(parts[1].Trim()).ToLowerInvariant();
                    if (!string.IsNullOrEmpty(x)) { origExtByHash[h] = x; _n++; }
                }
            } catch { }
        }

        void EnsureOrigMeta(ulong hash, string origExt)
        {
            if (origMetaByHash.ContainsKey(hash)) return;
            if (origExt != ".ddsc" && origExt != ".avtx") return;
            string tmpDir = Path.Combine(Path.GetTempPath(), "RAGE2Toolkit_meta_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try {
                Directory.CreateDirectory(tmpDir);
                string hh = hash.ToString("X16");
                if (!Rage2Toolkit.ExtractorOpt.ExtractSingle(gamePath, tmpDir, hh)) return;
                var files = Directory.GetFiles(tmpDir, "*", SearchOption.AllDirectories);
                if (files.Length == 0) return;
                byte[] buf = File.ReadAllBytes(files[0]);
                AssetConverters.AvtxMeta meta;
                if (AssetConverters.TryReadAvtxHeader(buf, out meta)) {
                    origMetaByHash[hash] = meta;
                    if (buf.Length >= 128) {
                        byte[] hdr = new byte[128];
                        Array.Copy(buf, 0, hdr, 0, 128);
                        origAvtxHeaderByHash[hash] = hdr;
                    }
                }
            } catch { }
              finally { try { Directory.Delete(tmpDir, true); } catch { } }
        }

        string ConvertDropped(string originalDir)
        {
            EnsureOrigExts();
            try
            {
                // cleanup legacy por si existia de versiones anteriores
                try { var _legacy = Path.Combine(originalDir, "__converted__"); if (Directory.Exists(_legacy)) Directory.Delete(_legacy, true); } catch { }
                string convertedDir = Path.Combine(Path.GetTempPath(), "RAGE2Toolkit_conv_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                if (Directory.Exists(convertedDir))
                {
                    try { Directory.Delete(convertedDir, true); } catch { }
                }
                Directory.CreateDirectory(convertedDir);
                _convertedTempDir = convertedDir;

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
                        string _target = "auto";
                        string _origExt = null;
                        if (origExtByHash.TryGetValue(hh0, out _origExt) && _origExt.Length == 5 && _origExt.StartsWith(".atx"))
                            _target = "atx1";
                        int _oDxgi = 0, _oW = 0, _oH = 0; bool _oSrgb = false;
                        byte[] _oHdr = null;
                        if (_target == "auto" && _origExt != null) {
                            EnsureOrigMeta(hh0, _origExt);
                            AssetConverters.AvtxMeta _m;
                            if (origMetaByHash.TryGetValue(hh0, out _m)) { _oDxgi = _m.Dxgi; _oW = _m.Width; _oH = _m.Height; _oSrgb = _m.Srgb; }
                            origAvtxHeaderByHash.TryGetValue(hh0, out _oHdr);
                        }
                        var r = Rage2Toolkit.AssetConverters.Convert(
                            f,
                            convertedDir,
                            toolkitRel,
                            _target,
                            msg => Log("  " + msg),
                            _oDxgi, _oW, _oH, _oSrgb, _oHdr);

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
                            // FIX: borrar .dds intermedio + side files de ddscConvert (.atx1/.atx2/...).
                            // Solo se queda el archivo con la extension del OutputPath final.
                            try {
                                string _keepExt = Path.GetExtension(wantPath);
                                foreach (var _side in Directory.GetFiles(convertedDir)) {
                                    string _sName = Path.GetFileNameWithoutExtension(_side);
                                    string _sExt = Path.GetExtension(_side);
                                    if (string.Equals(_sName, bn, StringComparison.OrdinalIgnoreCase) &&
                                        !string.Equals(_sExt, _keepExt, StringComparison.OrdinalIgnoreCase)) {
                                        try { File.Delete(_side); } catch { }
                                    }
                                }
                            } catch { }
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
        static int ExtPrio(string ext)
        {
            switch (ext)
            {
                case ".ddsc": case ".avtx": return 100;
                case ".atx1": return 90;
                case ".atx2": case ".atx3": case ".atx4": case ".atx5":
                case ".atx6": case ".atx7": case ".atx8": case ".atx9": return 80;
                case ".dds": return 70;
                case ".png": case ".jpg": case ".jpeg": case ".bmp": case ".tga": return 60;
                default: return 10;
            }
        }

        void StartPack()
        {
            // 1) Pedir metadata en el UI thread (OnNextClicked ya esta en UI)
            string defaultName = Path.GetFileName(sourceDir);
            string modName = null, modAuthor = null;
            using (var metaDlg = new ModMetaDialog(defaultName, ""))
            {
                if (metaDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
                modName = metaDlg.ModName;
                modAuthor = metaDlg.ModAuthor;
            }

            // 2) Validacion + empaquetado en background
            btnNext.Enabled = false;
            btnBack.Enabled = false;
            Task.Run(() =>
            {
                try
                {
                    this.Invoke(new Action(() => Log("")));
                    this.Invoke(new Action(() => Log("=== Validating folder ===")));
                    var val = ModValidator.ValidateFolder(sourceDir);
                    this.Invoke(new Action(() => Log("  OK=" + val.OkCount + " WARN=" + val.WarnCount + " FAIL=" + val.FailCount + " SKIP=" + val.SkippedCount)));
                    if (!val.CanPackage)
                    {
                        this.Invoke(new Action(() =>
                        {
                            DarkDialog.Error("Cannot package: FAIL=" + val.FailCount + " OK=" + val.OkCount, "Validation failed", Color.FromArgb(220, 80, 220));
                            btnNext.Enabled = true;
                            btnBack.Enabled = true;
                        }));
                        return;
                    }

                    string slug = ModMetadata.Slugify(modName);
                    string version = "1.0.0";
                    string outDir = Path.Combine(Paths.DefaultOutputDir, "mods_export");
                    Directory.CreateDirectory(outDir);
                    string zipPath = Path.Combine(outDir, slug + "-v" + version + ".zip");

                    var meta = new ModMetadata
                    {
                        Schema = 1,
                        Id = slug,
                        Name = modName,
                        Version = version,
                        Author = modAuthor,
                        Game = "RAGE2",
                        CreatedWith = AppInfo.Display,
                        CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
                    };

                    this.Invoke(new Action(() => Log("")));
                    this.Invoke(new Action(() => Log("=== Packing .zip ===")));
                    var pack = ModPackager.Build(sourceDir, zipPath, meta, msg => this.Invoke(new Action(() => Log("  " + msg))));

                    if (!pack.Success)
                    {
                        this.Invoke(new Action(() =>
                        {
                            DarkDialog.Error("Pack failed: " + (pack.Error ?? "?"), "Error", Color.FromArgb(220, 80, 220));
                            btnNext.Enabled = true;
                            btnBack.Enabled = true;
                        }));
                        return;
                    }

                    long sizeKB = pack.ZipSizeBytes / 1024;
                    string sizeStr = sizeKB > 1024 ? (sizeKB / 1024) + " MB" : sizeKB + " KB";

                    this.Invoke(new Action(() =>
                    {
                        lblZipPath.Text = pack.ZipPath;
                        Log("");
                        Log("\u2713 ZIP ready: " + pack.ZipPath);
                        Log("  Size: " + sizeStr + "  |  Assets: " + pack.AssetsCount);
                        btnNext.Enabled = true;
                        btnBack.Enabled = true;
                        GotoStep(2);
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        Log("\u2717 Pack exception: " + ex.Message);
                        DarkDialog.Error("Pack exception: " + ex.Message, "Error", Color.FromArgb(220, 80, 220));
                        btnNext.Enabled = true;
                        btnBack.Enabled = true;
                    }));
                }
            });
        }
        void StartScan()
        {
            Task.Run(() =>
            {
                try
                {
                    this.BeginInvoke(new Action(() => { try { progress.Value = 5; } catch { } }));
                    Log("Loading .arc index...");
                    LoadArcIndex();
                    this.BeginInvoke(new Action(() => { try { progress.Value = 10; } catch { } }));
                    Log("  " + arcByHash.Count + " hashes indexed from " + tabByArc.Count + " .arc files\n");

                    var files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories);
                    Log("Analyzing " + files.Length + " files...\n");

                    pending.Clear();
                    int totalFiles = files.Length;
                    int idxF = 0;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    foreach (var f in files)
                    {
                        idxF++;
                        if ((idxF % 5) == 0 || idxF == totalFiles)
                        {
                            int pct = 10 + (int)((85.0 * idxF) / Math.Max(1, totalFiles));
                            int capIdx = idxF;
                            this.BeginInvoke(new Action(() =>
                            {
                                try { progress.Value = Math.Min(95, pct); } catch { }
                            }));
                        }
                        var pf = new PendingFile();
                        pf.Path = f;
                        pf.Ext = Path.GetExtension(f).ToLowerInvariant();
                        string bn = Path.GetFileNameWithoutExtension(f);

                        ulong hh;
                        string hhHex;
                        if (!TryParseHashFromName(bn, out hh, out hhHex))
                        {
                            pf.Status = PendingStatus.InvalidHash;
                            pf.Reason = "Filename is not a 16-char hex hash";
                            pending.Add(pf);
                            continue;
                        }

                        pf.HashHex = hhHex;
                        pf.Hash = hh;

                        if (!arcByHash.ContainsKey(pf.Hash))
                        {
                            pf.Status = PendingStatus.HashNotFound;
                            pf.Reason = "This hash does not exist in any .arc";
                            pending.Add(pf);
                            continue;
                        }

                        pf.ArcName = arcByHash[pf.Hash];

                        // Criterio alineado con el harness: solo MANUAL rechaza.
                        // NATIVE / CONVERT / UNKNOWN pasan. La conversion se hace al empaquetar
                        // (StartRebuild) o ya se ha hecho antes (ConvertDropped con opcion B).
                        var cls = Rage2Toolkit.AssetConverters.Classify(f);

                        if (cls.Status == "manual")
                        {
                            pf.Status = PendingStatus.Manual;
                            pf.Reason = "manual conversion required: " + cls.Tool;
                        }
                        else if (cls.Status == "convert")
                        {
                            pf.Status = PendingStatus.Convert;
                            pf.Reason = "[CONVERT] " + cls.Tool + " will convert on pack";
                        }
                        else if (cls.Status == "native")
                        {
                            pf.Status = PendingStatus.Native;
                            pf.Reason = "[NATIVE] " + cls.Reason;
                        }
                        else
                        {
                            pf.Status = PendingStatus.Unknown;
                            pf.Reason = "[UNKNOWN] passes as-is (harness rule)";
                        }

                        pending.Add(pf);
                        Log("  " + pf.HashHex + "  " + pf.Ext.PadRight(6) + "  " + pf.ArcName + "  " + pf.Reason);
                    }

                    // COLAPSO: agrupar pending por hash, quedarse con el ganador (mayor prioridad).
                    // Los perdedores se descartan (shadowed). El usuario vera solo el ganador.
                    var _grouped = new Dictionary<ulong, List<PendingFile>>();
                    var _newPending = new List<PendingFile>();
                    int _shadowedCount = 0;
                    foreach (var _pf in pending)
                    {
                        if (_pf.Hash == 0) { _newPending.Add(_pf); continue; }
                        if (!_grouped.ContainsKey(_pf.Hash)) _grouped[_pf.Hash] = new List<PendingFile>();
                        _grouped[_pf.Hash].Add(_pf);
                    }
                    foreach (var _kv in _grouped)
                    {
                        var _lst = _kv.Value;
                        if (_lst.Count == 1) { _newPending.Add(_lst[0]); continue; }
                        // FIX: si conocemos la extension original, esa gana. Si no, prioridad por extension.
                        string _origE = null;
                        if (_lst.Count > 0 && _lst[0].Hash != 0) origExtByHash.TryGetValue(_lst[0].Hash, out _origE);
                        if (!string.IsNullOrEmpty(_origE)) {
                            PendingFile _match = null;
                            foreach (var _p in _lst) { if (string.Equals(_p.Ext, _origE, StringComparison.OrdinalIgnoreCase)) { _match = _p; break; } }
                            if (_match != null) {
                                _lst.Remove(_match);
                                _lst.Insert(0, _match);
                            } else {
                                _lst.Sort((a, b) => ExtPrio(b.Ext).CompareTo(ExtPrio(a.Ext)));
                            }
                        } else {
                            _lst.Sort((a, b) => ExtPrio(b.Ext).CompareTo(ExtPrio(a.Ext)));
                        }
                        _newPending.Add(_lst[0]);
                        for (int _i = 1; _i < _lst.Count; _i++)
                        {
                            _lst[_i].Status = PendingStatus.Shadowed;
                            _lst[_i].Reason = "same hash as " + Path.GetFileName(_lst[0].Path);
                            _shadowedCount++;
                        }
                    }
                    pending.Clear();
                    pending.AddRange(_newPending);

                    this.Invoke(new Action(() =>
                    {
                        progress.Style = ProgressBarStyle.Continuous;
                        progress.Value = 100;
// (PopulatePreview eliminado)
                        scanDone = true;
// (AnalyzeDropped eliminado)

                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        Log("\u2717 " + ex.Message);
                        DarkDialog.Error(ex.Message, "Error", Color.FromArgb(220, 80, 220));
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
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
            }
            catch { }
        }


        bool logSilent = false;
        readonly object logLock = new object();
        System.Text.StringBuilder logBuf = new System.Text.StringBuilder();

        void Log(string msg)
        {
            if (logSilent) { lock (logLock) { logBuf.Append(msg).Append(Environment.NewLine); } return; }
            if (logBox == null) { System.Diagnostics.Debug.WriteLine("[wizard-pre-ui] " + msg); return; }
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg))); return; }
            logBox.AppendText(msg + Environment.NewLine);
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.ScrollToCaret();
        }

        void FlushLog()
        {
            string content;
            lock (logLock) { content = logBuf.ToString(); logBuf.Clear(); }
            if (string.IsNullOrEmpty(content)) return;
            if (logBox == null) { System.Diagnostics.Debug.WriteLine("[wizard-pre-ui flush] " + content); return; }
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(FlushLog)); return; }
            logBox.AppendText(content);
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.ScrollToCaret();
        }
    }
    // ============================================================
    // WizardModding
    // ============================================================
    public class WizardModding : WizardBase
    {
        string gamePath, outputPath;

        public WizardModding(string gamePath, string outputPath)
            : base("Wizard: Modding", 1)
        {
            this.gamePath = gamePath;
            this.outputPath = outputPath;
            BuildStep1();
            SetAccent(C_MAGENTA);
            if (btnNext != null) btnNext.Visible = false;
            if (btnBack != null)
            {
                btnBack.Location = new Point(30, 18);
                btnBack.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            }
        }

        void BuildStep1()
        {
            var p = MakeStepPanel();
            p.Controls.Add(MakeTitle("What do you want to do?", 0, 10));
            // Sub eliminada (feedback del user)


            var info = new InfoTooltipPanel();
            info.Size = new Size(620, 140);
            info.Reset();
            p.Controls.Add(info);

            var btnManager = new RoundedButton();
            btnManager.Text = "MOD MANAGER";
            btnManager.CornerRadius = 14;
            btnManager.BorderThickness = 3;
            btnManager.BorderColor = C_MAGENTA;
            btnManager.BackColor = C_BTN;
            btnManager.ForeColor = Color.White;
            btnManager.Font = new Font("Segoe UI", 17, FontStyle.Bold);
            btnManager.Cursor = Cursors.Hand;
            btnManager.Size = new Size(620, 88);
            btnManager.Click += (s, e) => { new ModManagerForm(gamePath).ShowDialog(this); };
            p.Controls.Add(btnManager);

            // Magenta apagado para las dos subherramientas (Mod Manager mantiene el magenta vivo)
            Color magDim = Color.FromArgb(140, 55, 130);

            var btnEditor = new RoundedButton();
            btnEditor.Text = "WORLD SETTINGS EDITOR";
            btnEditor.CornerRadius = 10;
            btnEditor.BorderThickness = 2;
            btnEditor.BorderColor = magDim;
            btnEditor.BackColor = C_BTN;
            btnEditor.ForeColor = Color.White;
            btnEditor.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnEditor.Cursor = Cursors.Hand;
            btnEditor.Size = new Size(300, 70);
            btnEditor.Click += (s, e) => { LaunchSettingsEditor(); };
            p.Controls.Add(btnEditor);

            var btnRepacker = new RoundedButton();
            btnRepacker.Text = "REPACKER";
            btnRepacker.CornerRadius = 10;
            btnRepacker.BorderThickness = 2;
            btnRepacker.BorderColor = magDim;
            btnRepacker.BackColor = C_BTN;
            btnRepacker.ForeColor = Color.White;
            btnRepacker.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnRepacker.Cursor = Cursors.Hand;
            btnRepacker.Size = new Size(300, 70);
            btnRepacker.Click += (s, e) => { new WizardRepack(gamePath).ShowDialog(this); };
            p.Controls.Add(btnRepacker);
                        Action<RoundedButton, string, string> wireHover = (btn, title, desc) =>
            {
                btn.MouseEnter += (s, e) => { info.ShowInfo(title, desc); };
                btn.MouseLeave += (s, e) => { info.HideInfo(); };
            };

            wireHover(btnManager, "\u2699  MOD MANAGER",
                "Install, uninstall and manage mods. Handles conflicts, load order, priorities and backups automatically.\n\nUse this both to install other people's mods and to test your own.");

            wireHover(btnEditor, "\u25C6  WORLD SETTINGS EDITOR",
                "Tune the world's balance: spawns, difficulty, player stats, damage types, vehicle handling, weather and lighting.\n\nProduces a .zip mod ready to install with the Mod Manager.");

            wireHover(btnRepacker, "\u25A3  REPACKER",
                "Turn an edited asset folder (textures, audio, meshes) into a distributable .zip mod.\n\nThe .zip can be installed with the Mod Manager or uploaded to Nexus / Discord.");

            Action layout = () =>
            {
                int w = p.ClientSize.Width;
                if (w < 100) return;   // aun no dimensionado

                int cx = w / 2;

                btnManager.Location = new Point(cx - btnManager.Width / 2, 140);

                int pairGap = 20;
                int pairWidth = btnEditor.Width + pairGap + btnRepacker.Width;
                int pairLeft = cx - pairWidth / 2;
                if (pairLeft < 20) pairLeft = 20;
                btnEditor.Location = new Point(pairLeft, 260);
                btnRepacker.Location = new Point(pairLeft + btnEditor.Width + pairGap, 260);

                int infoLeft = cx - info.Width / 2;
                if (infoLeft < 20) infoLeft = 20;
                info.Location = new Point(infoLeft, 360);
            };

            p.SizeChanged += (s, e) => layout();
            p.Layout += (s, e) => layout();
            p.VisibleChanged += (s, e) => { if (p.Visible) { try { p.BeginInvoke(new Action(layout)); } catch { } } };
        }



        void LaunchSettingsEditor()
        {
            try
            {
                string settingsRoot = System.IO.Path.Combine(Rage2Toolkit.Paths.ExeDir, "settings_editor");
                System.IO.Directory.CreateDirectory(settingsRoot);
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(settingsRoot, "input"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(settingsRoot, "_backups"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(settingsRoot, "_stage_mods"));

                // Auto-copiar .rtpc desde _outputs\settings_extract si input esta vacio
                string inputDir = System.IO.Path.Combine(settingsRoot, "input");
                if (System.IO.Directory.GetFiles(inputDir, "*.rtpc").Length == 0)
                {
                    string external = @"D:\RAGE2MODDING\_outputs\settings_extract";
                    if (System.IO.Directory.Exists(external))
                    {
                        foreach (var f in System.IO.Directory.GetFiles(external, "*.rtpc"))
                        {
                            try { System.IO.File.Copy(f, System.IO.Path.Combine(inputDir, System.IO.Path.GetFileName(f)), true); } catch { }
                        }
                    }
                }

                var form = new SettingsEditorForm();
                form.ShowDialog(this);
            }
            catch (System.Exception ex)
            {
                DarkDialog.Error("Failed to open Settings Editor:" + System.Environment.NewLine + ex.Message, "Settings Editor", C_MAGENTA);
            }
        }

        protected override void OnNextClicked() { }
    }

        public class InfoTooltipPanel : Panel
    {
        string _titleText = "";
        string _descText = "";
        Timer _timer;
        double _opacity = 0;
        double _target = 0;
        public Color AccentColor = Color.FromArgb(220, 90, 220);

        public InfoTooltipPanel()
        {
            BackColor = Color.Transparent;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.UserPaint
                   | ControlStyles.SupportsTransparentBackColor, true);

            _timer = new Timer { Interval = 20 };
            _timer.Tick += (s, e) => Tick();
            Disposed += (s, e) => { try { _timer.Stop(); _timer.Dispose(); } catch { } };
        }

        public void HideInfo()
        {
            _target = 0;
            if (!_timer.Enabled) _timer.Start();
        }

        public void Reset()
        {
            _opacity = 0;
            _target = 0;
            _titleText = "";
            _descText = "";
            this.Visible = false;
            Invalidate();
        }

        public void ShowInfo(string title, string desc)
        {
            _titleText = title;
            _descText = desc;
            this.Visible = true;
            this.BringToFront();
            _target = 1;
            if (!_timer.Enabled) _timer.Start();
        }

        void Tick()
        {
            const double step = 0.14;
            if (_opacity < _target) _opacity = Math.Min(_target, _opacity + step);
            else if (_opacity > _target) _opacity = Math.Max(_target, _opacity - step);
            else
            {
                _timer.Stop();
                if (_target == 0)
                {
                    this.Visible = false;
                    return;
                }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int aBg     = (int)(210 * _opacity);
            int aBorder = (int)(230 * _opacity);
            int aTitle  = (int)(255 * _opacity);
            int aDesc   = (int)(235 * _opacity);

            if (aBg > 4)
            {
                Rectangle r = new Rectangle(1, 1, Width - 3, Height - 3);
                int radius = 12;
                using (var path = new GraphicsPath())
                {
                    int d = radius * 2;
                    if (d <= 0 || r.Width <= d || r.Height <= d) path.AddRectangle(r);
                    else
                    {
                        path.AddArc(r.X, r.Y, d, d, 180, 90);
                        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                        path.CloseFigure();
                    }
                    using (var b = new SolidBrush(Color.FromArgb(aBg, 18, 18, 24)))
                        g.FillPath(b, path);
                    using (var pen = new Pen(Color.FromArgb(aBorder, AccentColor), 2))
                        g.DrawPath(pen, path);
                }
            }

            if (aTitle > 4 && !string.IsNullOrEmpty(_titleText))
            {
                using (var font = new Font("Segoe UI", 11, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.FromArgb(aTitle, AccentColor)))
                {
                    g.DrawString(_titleText, font, brush, new PointF(20, 14));
                }
            }
            if (aDesc > 4 && !string.IsNullOrEmpty(_descText))
            {
                using (var font = new Font("Segoe UI", 9))
                using (var brush = new SolidBrush(Color.FromArgb(aDesc, 225, 225, 235)))
                {
                    var rect = new RectangleF(20, 48, Width - 40, Height - 58);
                    g.DrawString(_descText, font, brush, rect);
                }
            }

            base.OnPaint(e);
        }
    }
}

