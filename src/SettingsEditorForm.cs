using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class SettingsEditorForm : Form
    {
        // === Paths EXACTOS como el PS ===
        readonly string EXDIR = @"D:\RAGE2MODDING\_outputs\world_editor\work";
        readonly string OUTS  = @"D:\RAGE2MODDING\_outputs\world_editor";
        // Bundled originals shipped with the toolkit (release\data\settings)
        readonly string BUNDLED_SETTINGS;
        readonly string BKUP;
        readonly string STAGE;
        readonly string CLOG;

        // === FILES (7) ===
        // Orden: de mas util a menos. Settlements descartado (405 entries sin valores editables).
        public static readonly (string Hash, string Name, string Desc)[] FILES = new[] {
            ("FC0BD72039E0B01D", "Spawn Budget Pools",   "Control how many civilians, vehicles, combatants, animals and encounters live in the open world."),
            ("33FB199032F32E37", "Asset Memory Budgets",  "12 ordinal budget slots (0..11) x 5 resource categories x (memory_max + soft_limit). Engine indexes slots by ordinal; role of each slot is not documented. Test one slot before scaling all 12."),
            ("9661597C2BA6B6EF", "Player Stats",         "Player health, armor, movement, abilities and combat tuning."),
            ("67B4C03CA7428D09", "Difficulty",           "Enemy scaling, cooldowns and damage tuning per difficulty tier."),
            ("2F6B4EC6033419CF", "Damage Types",         "Bitmask definitions of every damage type (Fire, Bullet, EMP, Corruption, ...)."),
            ("D779BE9109D9BB6F", "Vehicle Types",        "Physics and handling parameters for each vehicle class."),
            ("77637ABA456411A0", "Weather Settings",     "Weather presets, transitions and conditions."),
            ("2DBD1B76CC037780", "Sun / Lighting",       "Global sun, sky and lighting setup for the world."),
            ("1991948CE05363E4", "Post Effects",           "Bloom, tonemap, exposure, color grading, DOF, motion blur, chromatic aberration, film grain, SSAO, HDR."),
            // ===== v1 spawn_defs (linear entry tables) =====
            ("FF2002F3B254A8D6", "Spawn Vehicle Defs",    "Vehicle prefabs + spawn weight (v1). 119 entries. 114 weight=50 (cars/convoy), 5 weight=100 (bikes)."),
            ("A6D67A0901ABC820", "Spawn Combatant Defs",  "Combatant prefabs + float tiers (v1). 235 entries, 56 with numeric props (float_a 0.6-0.7, float_b 0.3-0.4, is_heavy 0/1)."),
            ("487726AB8BF3CF86", "Spawn Driver Defs",     "Driver prefabs for traffic (v1). 37 entries, catalog only."),
            ("6905DA0E9FB1BCF7", "Spawn Civilian Defs",   "Civilian / NPC prefabs (v1). 381 entries, catalog only."),
            ("96A9FA2210273CBE", "Spawn Encounter Defs",  "Encounter prefabs for open world events (v1). 270 entries, catalog only."),
            ("DCE68976CF8E7BD4", "Spawn Weapon Defs",     "Weapon catalog used by enemies (v1). 204 entries, catalog only."),
            ("635E003ABC37D457", "Enemy Type Spawn Settings", "Per-enemy-type spawn knobs (v3). Not a catalog - actual spawn tuning."),
        };

        // === Colors ===
        static readonly Color C_BG     = Color.FromArgb(16,16,20);
        static readonly Color C_PANEL  = Color.FromArgb(24,24,30);
        static readonly Color C_PANEL2 = Color.FromArgb(30,30,38);
        static readonly Color C_TEXT   = Color.FromArgb(235,235,240);
        static readonly Color C_MUTED  = Color.FromArgb(150,150,160);
        static readonly Color C_ACCENT = Color.FromArgb(210,70,190);
        static readonly Color C_OK     = Color.FromArgb(70,190,100);
        static readonly Color C_WARN   = Color.FromArgb(210,170,60);
        static readonly Color C_DANGER = Color.FromArgb(200,90,90);
        static readonly Color C_BORDER = Color.FromArgb(50,50,60);
        static readonly Color C_DIFF   = Color.FromArgb(55,25,60);   // magenta apagado = editado
        static readonly Color C_ERROR  = Color.FromArgb(95,25,25);   // rojo apagado = error
        static readonly Color C_ACTIVE = Color.FromArgb(60,30,80);

        // === UI ===
        ComboBox cboFile;
        TreeView tree;
        DataGridView grid;
        CheckBox chkShowAll;
        Label lblDesc;
        Panel warningBanner;
        Label lblCat, lblStatus, lblWarn;
        SplitContainer split;
        Panel breadcrumb;
        Label[] bcSteps;
        Label[] bcSeps;

        // === State ===
        string currentHash;
        string rtpcPath;
        RTPCNode rootNode;
        RTPCNode currentNode;

        // Session metadata (una vez por sesion)
        string _sessionModName = null;
        string _sessionModAuthor = null;

        // Bitmask detection del nodo actual
        bool _currentNodeIsBitmask = false;

        // === Session cache: cambios pendientes por (fileHash, ValueOffset) ===
        readonly Dictionary<string, Dictionary<int, PendingChange>> _pending =
            new Dictionary<string, Dictionary<int, PendingChange>>(StringComparer.OrdinalIgnoreCase);

        int PendingCount()
        {
            int n = 0;
            foreach (var kv in _pending) n += kv.Value.Count;
            return n;
        }

        void SetPendingEntry(string hash, RTPCProp p, string nameHuman, string newDisplay, double newValue, bool isBitmask)
        {
            Dictionary<int, PendingChange> byOffset;
            if (!_pending.TryGetValue(hash, out byOffset))
            {
                byOffset = new Dictionary<int, PendingChange>();
                _pending[hash] = byOffset;
            }
            byOffset[p.ValueOffset] = new PendingChange
            {
                FileHash = hash,
                ValueOffset = p.ValueOffset,
                Type = p.Type,
                Value = newValue,
                NameTech = p.Name,
                NameHuman = nameHuman,
                OldDisplay = p.DisplayValue(),
                NewDisplay = newDisplay,
                IsBitmask = isBitmask
            };
        }

        void RemovePendingEntry(string hash, int valueOffset)
        {
            Dictionary<int, PendingChange> byOffset;
            if (_pending.TryGetValue(hash, out byOffset))
            {
                byOffset.Remove(valueOffset);
                if (byOffset.Count == 0) _pending.Remove(hash);
            }
        }

        bool TryGetPending(string hash, int valueOffset, out PendingChange pc)
        {
            pc = null;
            Dictionary<int, PendingChange> byOffset;
            if (_pending.TryGetValue(hash, out byOffset))
            {
                return byOffset.TryGetValue(valueOffset, out pc);
            }
            return false;
        }

        string FormatPendingStatus()
        {
            int total = PendingCount();
            if (total == 0) return "Hover a row for the full explanation. Edit the 'New value' column, then click SAVE.";
            return total + " unsaved change(s) in " + _pending.Count + " file(s) \u2014 click here to view, or click SAVE to build the mod.";
        }

        public SettingsEditorForm()
        {
            BKUP = Path.Combine(OUTS, "_backups");
            STAGE = Path.Combine(OUTS, "_stage_mods");
            CLOG = Path.Combine(OUTS, "_changes_log.json");
            try { Directory.CreateDirectory(BKUP); } catch { }
            try { Directory.CreateDirectory(STAGE); } catch { }
            // Sesion fresca: limpiar STAGE + WORK de sesiones previas.
            // El toolkit es one-shot por sesion. Los .rtpc de WORK se recargan
            // desde release\data\settings\ (bundled = original del juego) mas abajo.
            try
            {
                if (Directory.Exists(STAGE))
                {
                    foreach (var f in Directory.GetFiles(STAGE, "*.rtpc"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
            try { Directory.CreateDirectory(EXDIR); } catch { }

            try
            {
                if (Directory.Exists(EXDIR))
                {
                    foreach (var f in Directory.GetFiles(EXDIR, "*.rtpc"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }

            // Bundled settings: release\data\settings\ - ANTES de tocar la UI,
            // para que LoadFile() encuentre los .rtpc ya disponibles.
            try
            {
                string bundled = Path.Combine(Rage2Toolkit.Paths.DataDir, "settings");
                if (Directory.Exists(bundled)) EnsureWorkFiles(bundled);
            }
            catch { }

            try { SemanticMap.LoadNames(); } catch { }

            Text = "RAGE 2 - World Settings Editor";
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(1000, 620);
            BackColor = C_BG;
            ForeColor = C_TEXT;
            Font = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterParent;

            BuildUI();
            this.Load += WrapIntoTabs_Loaded;
            PopulateFileCombo();

            cboFile.SelectedIndexChanged += (s, e) => LoadFile();
            tree.AfterSelect += (s, e) => RefreshGrid();
            grid.CellValueChanged += OnGridCellChanged;
            grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            chkShowAll.CheckedChanged += OnShowAllToggle;

            if (cboFile.Items.Count > 0) cboFile.SelectedIndex = 0;
            UpdateFileDescription();

            this.Shown += (s, e) =>
            {
                try { if (split.Width > 200) split.SplitterDistance = (int)(split.Width * 0.22); }
                catch { }
            };

            // Bundled settings: release\data\settings\
            try
            {
                string bundled = Path.Combine(Rage2Toolkit.Paths.DataDir, "settings");
                if (Directory.Exists(bundled)) EnsureWorkFiles(bundled);
            }
            catch { }
        }

        void EnsureWorkFiles(string bundledDir)
        {
            foreach (var f in FILES)
            {
                string work = Path.Combine(EXDIR, f.Hash + ".rtpc");
                if (File.Exists(work)) continue;
                string bundle = Path.Combine(bundledDir, f.Hash + ".rtpc");
                if (File.Exists(bundle))
                {
                    try { File.Copy(bundle, work, false); } catch { }
                }
            }
        }

        // =========================================================
        // BUILD UI (1:1 con PS)
        // =========================================================
        void BuildUI()
        {
            // HEADER
            var header = new Panel { Dock = DockStyle.Top, Height = 134, BackColor = C_PANEL };

            // ----- Fila 1: titulo (izq) + checkbox (der) -----
            var title = new Label
            {
                Text = "World Settings Editor",
                Location = new Point(20, 12),
                Size = new Size(360, 30),
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = C_ACCENT,
                BackColor = C_PANEL
            };
            header.Controls.Add(title);

            var chkExperimentalPanel = new Panel { Dock = DockStyle.Right, Width = 360, BackColor = C_PANEL };
            header.Controls.Add(chkExperimentalPanel);

            chkShowAll = new CheckBox
            {
                Text = "  Enable experimental settings",
                Location = new Point(30, 16),
                Size = new Size(320, 26),
                ForeColor = Color.FromArgb(235, 200, 60),
                BackColor = C_PANEL,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            chkExperimentalPanel.Controls.Add(chkShowAll);

            var lblWarnIcon = new Label
            {
                Text = "\u26A0",
                Location = new Point(0, 14),
                Size = new Size(28, 28),
                ForeColor = Color.FromArgb(235, 200, 60),
                BackColor = C_PANEL,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleCenter
            };
            lblWarnIcon.Click += (s, e) => { chkShowAll.Checked = !chkShowAll.Checked; };
            chkExperimentalPanel.Controls.Add(lblWarnIcon);
            lblWarnIcon.BringToFront();

            // ----- Fila 2: file selector (centrado) -----
            var lblFile = new Label
            {
                Text = "File:",
                Location = new Point(0, 76),
                Size = new Size(50, 26),
                ForeColor = C_MUTED,
                BackColor = C_PANEL,
                Font = new Font("Segoe UI", 10),
                TextAlign = ContentAlignment.MiddleRight
            };
            header.Controls.Add(lblFile);

            cboFile = new ComboBox
            {
                Location = new Point(0, 72),
                Size = new Size(900, 30),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = C_PANEL2,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 11),
                IntegralHeight = false,
                MaxDropDownItems = 12
            };
            header.Controls.Add(cboFile);

            // Descripcion del fichero seleccionado (auto-update)
            lblDesc = new Label
            {
                Location = new Point(0, 40),
                Size = new Size(900, 20),
                ForeColor = Color.FromArgb(180, 180, 195),
                BackColor = C_PANEL,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = ""
            };
            header.Controls.Add(lblDesc);

            // ----- Fila 3: leyenda (centrada) -----
            var legPanel = new FlowLayoutPanel
            {
                Location = new Point(0, 108),
                Size = new Size(800, 22),
                BackColor = C_PANEL,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            Action<string, string, Color> addLeg = (symbol, label, color) =>
            {
                legPanel.Controls.Add(new Label
                {
                    Text = symbol,
                    AutoSize = true,
                    ForeColor = color,
                    BackColor = C_PANEL,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Margin = new Padding(0, 2, 2, 0)
                });
                legPanel.Controls.Add(new Label
                {
                    Text = label + "     ",
                    AutoSize = true,
                    ForeColor = C_MUTED,
                    BackColor = C_PANEL,
                    Font = new Font("Segoe UI", 8),
                    Margin = new Padding(0, 4, 0, 0)
                });
            };
            addLeg("OK", "Confirmed",        Color.FromArgb(80, 180, 100));
            addLeg("\u2248", "Probable",     Color.FromArgb(235, 200, 60));
            addLeg("~",  "Deduced",          Color.FromArgb(255, 140, 50));
            addLeg("?",  "Experimental",     Color.FromArgb(230, 70, 70));
            addLeg("i",  "Info (read-only)", Color.FromArgb(130, 130, 145));
            header.Controls.Add(legPanel);

            // Reposicion centrado al resize / first show
            Action centerHeader = () =>
            {
                int w = header.ClientSize.Width;
                if (w < 400) return;
                // Combo centrado (con su label a la izquierda)
                int comboTotal = lblFile.Width + 6 + cboFile.Width;
                if (comboTotal > w - 40)
                {
                    // encoger combo si falta espacio
                    int newCombo = w - 40 - lblFile.Width - 6;
                    if (newCombo > 200) cboFile.Width = newCombo;
                    comboTotal = lblFile.Width + 6 + cboFile.Width;
                }
                int comboLeft = (w - comboTotal) / 2;
                if (comboLeft < 20) comboLeft = 20;
                lblFile.Left = comboLeft;
                cboFile.Left = comboLeft + lblFile.Width + 6;
                // Descripcion alineada con el combo (mismo ancho + centro)
                if (lblDesc != null)
                {
                    lblDesc.Left = lblFile.Left;
                    lblDesc.Top = 40;
                    lblDesc.Width = lblFile.Width + 6 + cboFile.Width;
                }
                // Leyenda centrada
                int legW = legPanel.PreferredSize.Width;
                if (legW > w - 40) legW = w - 40;
                legPanel.Width = legW;
                legPanel.Left = (w - legW) / 2;
                if (legPanel.Left < 20) legPanel.Left = 20;
            };
            header.Resize += (s, e) => centerHeader();
            this.Shown += (s, e) => centerHeader();

            // accent bar movida al Form como primer control Dock=Top

            // WARNING BANNER
            warningBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(80, 50, 15),
                Visible = false
            };
            var warningText = new Label
            {
                Dock = DockStyle.Fill,
                Text = "  EXPERIMENTAL SETTINGS ENABLED: yellow (probable), orange (deduced) and red (experimental) rows are visible. Verify in-game after each change.",
                ForeColor = Color.FromArgb(255, 210, 100),
                BackColor = Color.FromArgb(80, 50, 15),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 0)
            };
            warningBanner.Controls.Add(warningText);

            // SPLIT
            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterDistance = 300,
                SplitterWidth = 3,
                BackColor = C_BORDER
            };

            tree = new TreeView
            {
                Dock = DockStyle.Fill,
                BackColor = C_PANEL,
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                ItemHeight = 26,
                HideSelection = false,
                ShowNodeToolTips = true
            };
            split.Panel1.Controls.Add(tree);

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = C_PANEL,
                GridColor = C_BORDER,
                BorderStyle = BorderStyle.None,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Vertical,
                EnableHeadersVisualStyles = false
            };
            grid.ColumnHeadersDefaultCellStyle.BackColor = C_PANEL2;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = C_TEXT;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 6, 6, 6);
            grid.ColumnHeadersHeight = 40;
            grid.DefaultCellStyle.BackColor = C_PANEL;
            grid.DefaultCellStyle.ForeColor = C_TEXT;
            grid.DefaultCellStyle.SelectionBackColor = C_ACTIVE;
            grid.DefaultCellStyle.SelectionForeColor = C_TEXT;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9);
            grid.DefaultCellStyle.Padding = new Padding(6, 4, 6, 4);
            grid.RowTemplate.Height = 38;
            grid.AlternatingRowsDefaultCellStyle.BackColor = C_PANEL2;

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Conf", HeaderText = "", Width = 46, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Setting", HeaderText = "Setting", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 120, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Current", Width = 100, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "New", HeaderText = "New value", Width = 130, ReadOnly = false });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Desc", HeaderText = "What it does", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 220, ReadOnly = true });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tech", HeaderText = "Internal name", Width = 220, ReadOnly = true });
            split.Panel2.Controls.Add(grid);

            // FOOTER
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 100, BackColor = C_PANEL };

            lblCat = new Label
            {
                Location = new Point(20, 10),
                Size = new Size(920, 22),
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Text = "No node selected",
                AutoEllipsis = true
            };
            footer.Controls.Add(lblCat);

            lblStatus = new Label
            {
                Location = new Point(20, 34),
                Size = new Size(920, 20),
                ForeColor = C_MUTED,
                Font = new Font("Segoe UI", 8, FontStyle.Underline),
                Cursor = Cursors.Hand,
                AutoEllipsis = true
            };
            lblStatus.Click += (s, e) => ShowPendingChanges();
            var tipStatus = new ToolTip();
            tipStatus.SetToolTip(lblStatus, "Click to view and navigate pending changes.");
            footer.Controls.Add(lblStatus);

            lblWarn = new Label
            {
                Location = new Point(20, 58),
                Size = new Size(920, 20),
                ForeColor = C_WARN,
                Font = new Font("Segoe UI", 8, FontStyle.Italic),
                AutoEllipsis = true
            };
            footer.Controls.Add(lblWarn);

            var btnPanel = new Panel { Dock = DockStyle.Right, Width = 700, BackColor = C_PANEL };
            footer.Controls.Add(btnPanel);

            var btnReset = new Button
            {
                Text = "REVERT",
                Location = new Point(20, 24),
                Size = new Size(140, 50),
                BackColor = C_PANEL2,
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnReset.FlatAppearance.BorderColor = C_BORDER;
            btnReset.FlatAppearance.BorderSize = 1;
            btnReset.Click += (s, e) => Revert();
            btnPanel.Controls.Add(btnReset);

            var btnSave = new Button
            {
                Text = "SAVE",
                Location = new Point(170, 24),
                Size = new Size(150, 50),
                BackColor = C_OK,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += (s, e) => Save();
            btnPanel.Controls.Add(btnSave);

            var btnBackups = new Button
            {
                Text = "BACKUPS",
                Location = new Point(330, 24),
                Size = new Size(150, 50),
                BackColor = C_PANEL2,
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnBackups.FlatAppearance.BorderColor = C_BORDER;
            btnBackups.FlatAppearance.BorderSize = 1;
            btnBackups.Click += (s, e) => ShowBackupManager();
            btnPanel.Controls.Add(btnBackups);

            var btnHelp = new Button
            {
                Text = "?",
                Location = new Point(500, 24),
                Size = new Size(50, 50),
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnHelp.FlatAppearance.BorderSize = 0;
            btnHelp.Click += (s, e) => ShowHelp();
            btnPanel.Controls.Add(btnHelp);

            // BREADCRUMB (5 pasos)
            breadcrumb = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = C_PANEL2 };
            bcSteps = new Label[5];
            bcSeps = new Label[4];
            string[] bcLabels = new string[] { "Choose file", "Edit values", "SAVE", "Repack", "Install" };
            int[] bcWidths = new int[] { 200, 200, 140, 140, 140 };
            int bx = 24;
            for (int i = 0; i < 5; i++)
            {
                var lbl = new Label
                {
                    Text = "\u25CF  " + (i + 1) + ". " + bcLabels[i],
                    Location = new Point(bx, 13),
                    Size = new Size(bcWidths[i], 22),
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    ForeColor = C_MUTED,
                    BackColor = C_PANEL2,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                breadcrumb.Controls.Add(lbl);
                bcSteps[i] = lbl;
                bx += bcWidths[i];
                if (i < 4)
                {
                    var sep = new Label
                    {
                        Text = "\u203A",
                        Location = new Point(bx, 13),
                        Size = new Size(24, 22),
                        Font = new Font("Segoe UI", 12, FontStyle.Bold),
                        ForeColor = C_BORDER,
                        BackColor = C_PANEL2,
                        TextAlign = ContentAlignment.MiddleCenter
                    };
                    breadcrumb.Controls.Add(sep);
                    bcSeps[i] = sep;
                    bx += 24;
                }
            }

            // Z-order para Dock=Top (el ultimo añadido queda arriba)
            // Queremos, de arriba a abajo: accentBar(6px) > header(92) > warningBanner(48)
            var accentBar = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = C_ACCENT };
            Controls.Add(footer);          // Bottom
            Controls.Add(split);           // Fill
            Controls.Add(breadcrumb);      // Top - mas abajo
            Controls.Add(warningBanner);   // Top - encima de breadcrumb
            Controls.Add(header);          // Top - encima de warning
            Controls.Add(accentBar);       // Top - arriba del todo
            split.BringToFront();
            footer.BringToFront();
        }

        // =========================================================
        // PopulateFileCombo (1:1 con PS - itera $FILES.Keys)
        // =========================================================
        void PopulateFileCombo()
        {
            cboFile.Items.Clear();
            foreach (var f in FILES)
            {
                string path = Path.Combine(EXDIR, f.Hash + ".rtpc");
                string mark = File.Exists(path) ? "" : "  (not extracted)";
                cboFile.Items.Add(f.Name + mark + "  [" + f.Hash + "]");
            }
        }

        void UpdateFileDescription()
        {
            try
            {
                var sel = cboFile.SelectedItem as string;
                if (string.IsNullOrEmpty(sel)) return;
                var m = Regex.Match(sel, "([0-9A-Fa-f]{16})");
                if (!m.Success) return;
                string h = m.Groups[1].Value.ToUpperInvariant();
                foreach (var f in FILES)
                {
                    if (f.Hash == h)
                    {
                        if (lblDesc != null) lblDesc.Text = f.Desc;
                        break;
                    }
                }
            }
            catch { }
        }

        // =========================================================
        // LoadFile (1:1: extrae hash con regex del texto del combo)
        // =========================================================
        void LoadFile() { LoadFile(false); }

        void LoadFile(bool clearCurrentHashPending)
        {
            string sel = cboFile.SelectedItem as string;
            if (string.IsNullOrEmpty(sel)) return;
            var m = Regex.Match(sel, "([0-9A-Fa-f]{16})");
            if (!m.Success) return;
            string h = m.Groups[1].Value.ToUpperInvariant();
            string rtpc = Path.Combine(EXDIR, h + ".rtpc");

            // Si venimos de un RESTORE, los cambios pendientes de ESTE hash ya no aplican.
            if (clearCurrentHashPending && _pending.ContainsKey(h))
                _pending.Remove(h);

            if (!File.Exists(rtpc))
            {
                MessageBox.Show(this, "File not extracted: " + h + "\n\nExtract it from the game first.", "World Settings Editor");
                return;
            }

            try
            {
                var parsed = RTPCParser.Parse(rtpc);
                rootNode = parsed.Root;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Parse error: " + ex.Message, "World Settings Editor");
                return;
            }

            currentHash = h;
            rtpcPath = rtpc;

            tree.Nodes.Clear();
            if (rootNode != null)
            {
                if (h == "33FB199032F32E37")
                    BuildAmbTree(rootNode);
                else
                    AddTreeNode(rootNode, null);
            }
            tree.ExpandAll();

            lblCat.Text = FILES.FirstOrDefault(x => x.Hash == h).Name;
            UpdateFileDescription();
            lblWarn.Text = "";
            grid.Rows.Clear();
            currentNode = null;

            // Auto-seleccionar el primer nodo con contenido editable -> el grid muestra algo al abrir.
            SelectFirstEditableNode();

            // Refrescar el status bar para reflejar pending changes reales (multi-file).
            lblStatus.Text = FormatPendingStatus();
        }

        // Recorre el arbol buscando el primer nodo con props editables (type 1 o 2).
        // Si encuentra uno, lo selecciona -> dispara RefreshGrid -> grid con contenido.
        void SelectFirstEditableNode()
        {
            if (tree == null || tree.Nodes.Count == 0) return;
            TreeNode found = FindFirstEditable(tree.Nodes[0]);
            if (found != null)
            {
                tree.SelectedNode = found;
                found.EnsureVisible();
            }
        }

        TreeNode FindFirstEditable(TreeNode node)
        {
            if (node == null) return null;
            var n = node.Tag as RTPCNode;
            if (n != null && n.Props != null)
            {
                foreach (var p in n.Props)
                    if (p.Type == 1 || p.Type == 2) return node;
            }
            foreach (TreeNode child in node.Nodes)
            {
                var r = FindFirstEditable(child);
                if (r != null) return r;
            }
            return null;
        }

        // =========================================================
        // AddTreeNode (1:1 con PS)
        // =========================================================
        public class GroupTag
        {
            public List<RTPCNode> Children = new List<RTPCNode>();
            public string Prefix = "";
        }

        static readonly HashSet<string> GenericTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "unk", "def", "grp", "cc", "L", "v", "wp", "b", "i", "f", "n", "s", "u", "d",
            "a", "e", "w", "r", "t", "p", "m", "k", "c", "g", "x", "y", "z"
        };

        static bool IsGenericToken(string t)
        {
            if (string.IsNullOrEmpty(t)) return true;
            if (t.Length <= 2) return true;
            if (GenericTokens.Contains(t)) return true;
            if (t.StartsWith("unk", StringComparison.OrdinalIgnoreCase)) return true;
            if (t.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) return true;
            // hex puro (>= 6 chars) -> hash, no semantico
            if (t.Length >= 6)
            {
                bool allHex = true;
                foreach (var ch in t)
                {
                    if (!((ch >= '0' && ch <= '9') || (ch >= 'a' && ch <= 'f') || (ch >= 'A' && ch <= 'F')))
                    { allHex = false; break; }
                }
                if (allHex) return true;
            }
            return false;
        }

        // Tokeniza usando el DISPLAY name (no el raw "unk_XXX")
        static string[] TokensForGrouping(RTPCNode c)
        {
            var disp = SemanticMap.GetInfo(c.Name).Name;
            if (string.IsNullOrEmpty(disp)) disp = c.Name;
            return disp.Split(new[] { '_', ' ', '-', '.', '(', ')', '[', ']', ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        static string TitleToken(string t)
        {
            if (string.IsNullOrEmpty(t)) return t;
            return char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant();
        }

        // Encuentra el proximo token NO generico a partir de startIdx.
        static int FindNextMeaningfulToken(string[] tokens, int startIdx)
        {
            for (int i = startIdx; i < tokens.Length; i++)
            {
                if (!IsGenericToken(tokens[i])) return i;
            }
            return -1;
        }

        // =========================================================
        // BuildAmbTree: render custom para Asset Memory Budgets
        // (estructura 3 niveles: subtrees -> 12 slots -> 5 categorias -> 2 valores)
        // =========================================================
        const uint AMB_SUBTREE_CATEGORIES = 0x7469A239;
        const uint AMB_SUBTREE_BUDGETS    = 0xA56F07E7;
        const uint PROP_RESOURCE_TYPE     = 0x5ED906DD;

        static string AmbResourceType(RTPCNode n)
        {
            if (n == null) return "?";
            // 1. Prop resource_type (nodos de la subtree categories)
            foreach (var p in n.Props)
            {
                if (p.Hash == PROP_RESOURCE_TYPE && p.Value is string s && !string.IsNullOrEmpty(s))
                    return s;
            }
            // 2. Resolver el hash del nodo via SemanticMap (names.json / probable / experimental)
            var info = SemanticMap.GetInfo(n.Name);
            if (info != null && !string.IsNullOrEmpty(info.Name)
                && !info.Name.StartsWith("unk_") && !info.Name.StartsWith("Unknown (")
                && info.Name != "?")
                return info.Name;
            return n.Name;
        }

        // Ordinal real de cada slot (REDxEYE: lookup3("0")..lookup3("11"))
        static readonly Dictionary<uint,int> AmbSlotOrdinal = new Dictionary<uint,int>
        {
            { 0x00D7146E, 0 }, { 0x9A92A17C, 1 }, { 0xB5805128, 2 }, { 0xB94F5D01, 3 },
            { 0x2415FFE6, 4 }, { 0x14DDA5C9, 5 }, { 0xE10DCE2E, 6 }, { 0xF7CA9C57, 7 },
            { 0x2E3582F7, 8 }, { 0x43409C73, 9 }, { 0xCD97B55C, 10 }, { 0x1C66272F, 11 },
        };

        void BuildAmbTree(RTPCNode root)
        {
            var rootT = tree.Nodes.Add("Asset Memory Budgets");
            rootT.Tag = root;
            rootT.ForeColor = C_TEXT;
            rootT.NodeFont = new Font(tree.Font, FontStyle.Bold);
            rootT.ToolTipText = "12 ordinal slots x 5 resource categories x (memory_max + soft_limit). Total: 120 editable values.";

            RTPCNode catSub = null, budgetSub = null;
            foreach (var c in root.Children)
            {
                if (c.Hash == AMB_SUBTREE_CATEGORIES) catSub = c;
                else if (c.Hash == AMB_SUBTREE_BUDGETS) budgetSub = c;
            }

            // --- CATEGORIES ---
            if (catSub != null)
            {
                var catT = rootT.Nodes.Add("Categories   (" + catSub.Children.Count + ")");
                catT.Tag = catSub;
                catT.ForeColor = Color.FromArgb(200, 200, 220);
                catT.NodeFont = new Font(tree.Font, FontStyle.Bold);
                catT.ToolTipText = "Resource type identifiers used by the budget slots.";
                foreach (var cat in catSub.Children)
                {
                    string label = AmbResourceType(cat);
                    var catNode = catT.Nodes.Add(label);
                    catNode.Tag = cat;
                    catNode.ForeColor = C_TEXT;
                    catNode.ToolTipText = "resource_type = " + label;
                }
            }

            // --- BUDGET SLOTS ---
            if (budgetSub != null)
            {
                var bT = rootT.Nodes.Add("Budget Slots   (" + budgetSub.Children.Count + ")");
                bT.Tag = budgetSub;
                bT.ForeColor = Color.FromArgb(200, 200, 220);
                bT.NodeFont = new Font(tree.Font, FontStyle.Bold);
                bT.ToolTipText = "12 ordinal memory budget slots. Engine indexes them 0..11. Role of each slot is NOT documented in the file.";

                // Ordenar slots por ordinal real (decodificado del hash)
                var slotList = new List<RTPCNode>(budgetSub.Children);
                slotList.Sort((a, b) =>
                {
                    int oa = AmbSlotOrdinal.ContainsKey(a.Hash) ? AmbSlotOrdinal[a.Hash] : 999;
                    int ob = AmbSlotOrdinal.ContainsKey(b.Hash) ? AmbSlotOrdinal[b.Hash] : 999;
                    return oa.CompareTo(ob);
                });

                for (int i = 0; i < slotList.Count; i++)
                {
                    var slot = slotList[i];
                    int ordinal = AmbSlotOrdinal.ContainsKey(slot.Hash) ? AmbSlotOrdinal[slot.Hash] : i;
                    var slotT = bT.Nodes.Add("Slot " + ordinal.ToString("D2"));
                    slotT.Tag = slot;
                    slotT.ForeColor = C_TEXT;
                    slotT.NodeFont = new Font(tree.Font, FontStyle.Bold);
                    slotT.ToolTipText = "Ordinal slot " + ordinal + " of 12. Structurally identical to the others. Test one slot at a time in-game before scaling all 12.";

                    // Ordenar hijos por nombre de recurso para presentacion consistente
                    var ordered = new List<RTPCNode>();
                    var byName = new Dictionary<string, RTPCNode>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in slot.Children)
                    {
                        string nm = AmbResourceType(c);
                        if (!byName.ContainsKey(nm)) byName[nm] = c;
                    }
                    foreach (var kv in byName.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
                        ordered.Add(kv.Value);

                    foreach (var cat in ordered)
                    {
                        string label = AmbResourceType(cat);
                        int editables = 0;
                        foreach (var p in cat.Props) if (p.Type == 1 || p.Type == 2) editables++;
                        string display = label + (editables > 0 ? "   [" + editables + "]" : "");
                        var catNode = slotT.Nodes.Add(display);
                        catNode.Tag = cat;
                        catNode.ForeColor = C_TEXT;
                        catNode.ToolTipText = "Memory budget for '" + label + "' in Slot " + i;
                    }
                }
            }
        }

        void AddTreeNode(RTPCNode n, TreeNode parent)
        {
            if (n == null) return;
            if (n.Name == "zero") return;

            var info = SemanticMap.GetInfo(n.Name);
            int editables = 0;
            foreach (var p in n.Props) if (p.Type == 1 || p.Type == 2) editables++;

            string label;
            if (editables > 0) label = info.Name + "   [" + editables + "]";
            else if (n.Children.Count > 0) label = info.Name + "   (" + n.Children.Count + " children)";
            else label = info.Name;

            TreeNode node;
            if (parent != null) node = parent.Nodes.Add(label);
            else node = tree.Nodes.Add(label);

            node.Tag = n;
            node.ForeColor = info.Conf == "I" ? C_MUTED : C_TEXT;
            if (!string.IsNullOrEmpty(info.Desc))
                node.ToolTipText = info.Name + "\r\n\r\n" + info.Desc;
            else
                node.ToolTipText = info.Name;

            AddChildrenSmart(n.Children, node, 0, 0);
        }

        void AddChildrenSmart(List<RTPCNode> children, TreeNode parent, int tokenStart, int depth)
        {
            if (children == null || children.Count == 0) return;

            // Nodos pequenos: orden natural
            if (children.Count < 8 || depth >= 3)
            {
                var flat = children
                    .Select(c => new { Node = c, Editables = c.Props.Count(p => p.Type == 1 || p.Type == 2), Label = SemanticMap.GetInfo(c.Name).Name })
                    .OrderByDescending(x => x.Editables)
                    .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                foreach (var c in flat) AddTreeNode(c.Node, parent);
                return;
            }

            // Agrupar por proximo token no generico
            var buckets = new Dictionary<string, List<RTPCNode>>(StringComparer.OrdinalIgnoreCase);
            var singles = new List<RTPCNode>();
            foreach (var c in children)
            {
                var toks = TokensForGrouping(c);
                int idx = FindNextMeaningfulToken(toks, tokenStart);
                if (idx < 0) { singles.Add(c); continue; }
                string key = toks[idx].ToLowerInvariant();
                if (!buckets.ContainsKey(key)) buckets[key] = new List<RTPCNode>();
                buckets[key].Add(c);
            }

            // Si solo hay un bucket y NO hay singles -> ese token es redundante, saltarlo
            if (buckets.Count == 1 && singles.Count == 0)
            {
                var only = buckets.First().Value;
                AddChildrenSmart(only, parent, tokenStart + 1, depth);
                return;
            }

            // Grupos con >= 3 hijos -> carpeta
            var toFolder = buckets.Where(kv => kv.Value.Count >= 3).OrderBy(kv => kv.Key).ToList();
            var toFlat = buckets.Where(kv => kv.Value.Count < 3).SelectMany(kv => kv.Value).ToList();
            toFlat.AddRange(singles);

            foreach (var kv in toFolder)
            {
                var folderLabel = TitleToken(kv.Key) + "   (" + kv.Value.Count + ")";
                var folderNode = parent.Nodes.Add(folderLabel);
                var gt = new GroupTag { Children = kv.Value, Prefix = kv.Key };
                folderNode.Tag = gt;
                folderNode.ForeColor = Color.FromArgb(200, 200, 220);
                folderNode.NodeFont = new Font(tree.Font, FontStyle.Bold);

                // Sub-agrupar
                AddChildrenSmart(kv.Value, folderNode, tokenStart + 1, depth + 1);
            }

            // Los planos, orden natural
            var flatOrdered = toFlat
                .Select(c => new { Node = c, Editables = c.Props.Count(p => p.Type == 1 || p.Type == 2), Label = SemanticMap.GetInfo(c.Name).Name })
                .OrderByDescending(x => x.Editables)
                .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var c in flatOrdered) AddTreeNode(c.Node, parent);
        }

        // =========================================================
        // RefreshGrid (1:1 con PS)
        // =========================================================
        void RefreshGrid()
        {
            var sel = tree.SelectedNode;
            if (sel == null) return;

            List<RTPCProp> sourceProps = null;
            RTPCNode n = sel.Tag as RTPCNode;
            GroupTag gt = sel.Tag as GroupTag;
            if (n != null)
            {
                currentNode = n;
                _currentNodeIsBitmask = DetectBitmask(n);
                sourceProps = n.Props;
            }
            else if (gt != null)
            {
                // Carpeta: agregar props de todos los hijos
                currentNode = null;
                _currentNodeIsBitmask = false;
                sourceProps = new List<RTPCProp>();
                foreach (var c in gt.Children) sourceProps.AddRange(c.Props);
            }
            else
            {
                return;
            }

            grid.Rows.Clear();

            var items = new List<Tuple<int, RTPCProp, PropInfo>>();
            foreach (var p in sourceProps)
            {
                if (p.Type != 1 && p.Type != 2) continue;
                var infoTmp = SemanticMap.GetInfo(p.Name);
                if (!chkShowAll.Checked && (infoTmp.Conf == "P" || infoTmp.Conf == "D" || infoTmp.Conf == "E")) continue;
                var info = SemanticMap.GetInfo(p.Name);
                int rank = info.Conf == "C" ? 0 : info.Conf == "P" ? 1 : info.Conf == "D" ? 2 : info.Conf == "I" ? 3 : 4;
                items.Add(Tuple.Create(rank, p, info));
            }
            items.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            int cntC = 0, cntP = 0, cntD = 0, cntI = 0, cntE = 0;
            foreach (var it in items)
            {
                var p = it.Item2;
                var info = it.Item3;
                switch (info.Conf) { case "C": cntC++; break; case "P": cntP++; break; case "D": cntD++; break; case "I": cntI++; break; default: cntE++; break; }

                string val = p.DisplayValue();
                PendingChange pendingHit = null;
                TryGetPending(currentHash, p.ValueOffset, out pendingHit);
                string newVal = pendingHit != null ? pendingHit.NewDisplay : val;
                string icon = info.Conf == "C" ? "OK" : info.Conf == "P" ? "\u2248" : info.Conf == "D" ? "~" : info.Conf == "I" ? "i" : "?";

                int idx = grid.Rows.Add(icon, info.Name, val, newVal, info.Desc ?? "", p.Name);
                var row = grid.Rows[idx];

                if (info.Conf == "P") row.DefaultCellStyle.BackColor = Color.FromArgb(42, 36, 10);   // dark yellow
                if (info.Conf == "D") row.DefaultCellStyle.BackColor = Color.FromArgb(45, 25, 10);   // dark orange
                if (info.Conf == "E") row.DefaultCellStyle.BackColor = Color.FromArgb(45, 15, 15);   // dark red
                if (info.Conf == "I") row.DefaultCellStyle.BackColor = Color.FromArgb(28, 28, 34);   // dark gray

                if (pendingHit != null)
                    row.DefaultCellStyle.BackColor = C_DIFF;

                row.Tag = p;
                // Columna Current (original, no editable) - gris apagado
                row.Cells[2].Style.BackColor = Color.FromArgb(18, 18, 24);
                row.Cells[2].Style.ForeColor = Color.FromArgb(160, 160, 170);
                row.Cells[2].Style.SelectionBackColor = Color.FromArgb(40, 40, 50);
                row.Cells[2].Style.SelectionForeColor = Color.FromArgb(210, 210, 220);
                row.Cells[2].Style.Font = new Font("Consolas", 9, FontStyle.Italic);
                if (pendingHit != null)
                {
                    row.Cells[3].Style.BackColor = Color.FromArgb(70, 30, 80);
                    row.Cells[3].Style.ForeColor = Color.FromArgb(240, 200, 255);
                    row.Cells[3].ToolTipText = "Edited value (valid).";
                }
                row.Cells[0].Style.ForeColor = ConfColor(info.Conf);
                row.Cells[0].Style.Font = new Font("Segoe UI", 11, FontStyle.Bold);
                row.Cells[0].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;

                // Bitmask: solo tooltip, sin prefijo visual en la descripcion
                bool rowIsBitmask = _currentNodeIsBitmask && p.Type == 1;

                string tip = info.Name + "\r\n\r\n" + (info.Desc ?? "") + "\r\n\r\nInternal name: " + p.Name + "\r\nType: " + TypeLabel(p.Type);
                if (rowIsBitmask)
                    tip += "\r\n\r\n\u2691 BITMASK property. Valid values: 0 or a power of 2 (1, 2, 4, 8, 16, ...).";
                for (int i = 0; i < row.Cells.Count; i++) row.Cells[i].ToolTipText = tip;

                if (info.Conf == "C" || info.Conf == "I") row.Cells[5].Value = "";
            }

            var ni = n != null ? SemanticMap.GetInfo(n.Name) : new PropInfo { Name = (gt != null ? gt.Prefix + " (group)" : "Group") };
            string summary = "OK: " + cntC + "   ≈: " + cntP + "   ~: " + cntD + "   i: " + cntI + "   ?: " + cntE;

            // Breadcrumb: File > Root > ... > nodo actual
            var bc = new List<string>();
            var cur = tree.SelectedNode;
            while (cur != null)
            {
                var rn = cur.Tag as RTPCNode;
                if (rn != null) bc.Add(SemanticMap.GetInfo(rn.Name).Name);
                cur = cur.Parent;
            }
            bc.Reverse();
            string nodeTitle = string.Join("  \u203A  ", bc) + "   \u2014   " + summary;
            if (_currentNodeIsBitmask) nodeTitle = "\u2691 BITMASK NODE   \u2014   " + nodeTitle;
            lblCat.Text = nodeTitle;

            // Header de la columna New value cambia si es bitmask
            var newCol = grid.Columns["New"];
            if (newCol != null)
            {
                if (_currentNodeIsBitmask)
                {
                    newCol.HeaderText = "\u2691 New value (power of 2)";
                    newCol.HeaderCell.Style.BackColor = Color.FromArgb(70, 55, 15);
                    newCol.HeaderCell.Style.ForeColor = Color.FromArgb(245, 210, 80);
                    newCol.HeaderCell.ToolTipText = "Bitmask node: enter 0 or a power of 2 (1, 2, 4, 8, 16, ...).";
                }
                else
                {
                    newCol.HeaderText = "New value";
                    newCol.HeaderCell.Style.BackColor = Color.Empty;
                    newCol.HeaderCell.Style.ForeColor = Color.Empty;
                    newCol.HeaderCell.ToolTipText = "";
                }
            }
            lblStatus.Text = FormatPendingStatus();
            var warnParts = new List<string>();
            if (_currentNodeIsBitmask) warnParts.Add("\u2691 BITMASK: valid = 0 or powers of 2");
            if (cntP > 0) warnParts.Add(cntP + " probable");
            if (cntD > 0) warnParts.Add(cntD + " deduced");
            if (cntE > 0) warnParts.Add(cntE + " experimental");
            lblWarn.Text = warnParts.Count > 0 ? string.Join("   ", warnParts) : "";
        }

        // =========================================================
        // Bitmask detection
        // =========================================================
        bool DetectBitmask(RTPCNode n)
        {
            if (n == null) return false;
            // (nunca sera null en la practica, grupo no pasa por aqui)
            var vals = new List<int>();
            foreach (var p in n.Props)
            {
                if (p.Type != 1) continue;
                try
                {
                    int v = Convert.ToInt32(p.Value);
                    vals.Add(v);
                }
                catch { }
            }
            if (vals.Count < 4) return false;
            var nonZero = vals.FindAll(v => v != 0);
            if (nonZero.Count < 4) return false;
            foreach (var v in nonZero)
            {
                if (v <= 0) return false;
                if ((v & (v - 1)) != 0) return false; // no potencia de 2
            }
            // Todos distintos?
            var uniq = new HashSet<int>(nonZero);
            if (uniq.Count != nonZero.Count) return false;
            return true;
        }

        static bool IsValidBitmaskValue(int v)
        {
            if (v == 0) return true;
            if (v < 0) return false;
            return (v & (v - 1)) == 0;
        }

        static Color ConfColor(string c)
        {
            switch (c)
            {
                case "C": return Color.FromArgb(80, 180, 100);   // VERIFIED - green
                case "P": return Color.FromArgb(235, 200, 60);   // PROBABLE - yellow
                case "D": return Color.FromArgb(255, 140, 50);   // DEDUCED  - orange
                case "E": return Color.FromArgb(230, 70, 70);    // EXPERIM. - red
                case "I": return Color.FromArgb(130, 130, 145);  // INFO     - gray
                default: return Color.Gray;
            }
        }

        static string TypeLabel(byte t)
        {
            switch (t)
            {
                case 1: return "int";
                case 2: return "float";
                case 3: return "text";
                case 4: return "vec2";
                case 5: return "vec3";
                case 6: return "vec4";
                default: return "t" + t;
            }
        }

        // =========================================================
        // Cell changed
        // =========================================================
        void OnGridCellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (grid.Columns[e.ColumnIndex].Name != "New") return;
            var row = grid.Rows[e.RowIndex];
            var prop = row.Tag as RTPCProp;
            bool bitmaskInvalid = false;

            if (_currentNodeIsBitmask && prop != null && prop.Type == 1)
            {
                string nv0 = Convert.ToString(row.Cells[3].Value);
                int iv0;
                if (int.TryParse(nv0, out iv0) && !IsValidBitmaskValue(iv0))
                    bitmaskInvalid = true;
            }

            string oldVal = Convert.ToString(row.Cells[2].Value);
            string newVal = Convert.ToString(row.Cells[3].Value);
            bool edited = oldVal != newVal;
            double parsedValue = 0;
            bool numericOk = !edited || double.TryParse(newVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsedValue);

            if (bitmaskInvalid)
            {
                row.Cells[3].Style.BackColor = Color.FromArgb(80, 20, 20);
                row.Cells[3].Style.ForeColor = Color.FromArgb(255, 180, 180);
                row.Cells[3].ToolTipText = "INVALID BITMASK VALUE \u2014 must be 0 or a power of 2 (1, 2, 4, 8, 16, ...)";
                row.DefaultCellStyle.BackColor = C_ERROR;
            }
            else if (edited && numericOk)
            {
                row.Cells[3].Style.BackColor = Color.FromArgb(70, 30, 80);
                row.Cells[3].Style.ForeColor = Color.FromArgb(240, 200, 255);
                row.Cells[3].ToolTipText = "Edited value (valid).";
                row.DefaultCellStyle.BackColor = C_DIFF;
            }
            else if (edited && !numericOk)
            {
                row.Cells[3].Style.BackColor = Color.FromArgb(80, 20, 20);
                row.Cells[3].Style.ForeColor = Color.FromArgb(255, 180, 180);
                row.Cells[3].ToolTipText = "Invalid numeric value.";
                row.DefaultCellStyle.BackColor = C_ERROR;
            }
            else
            {
                row.Cells[3].Style.BackColor = Color.Empty;
                row.Cells[3].Style.ForeColor = Color.Empty;
                row.Cells[3].ToolTipText = "";
                row.DefaultCellStyle.BackColor = C_PANEL;
            }

            if (prop != null && !string.IsNullOrEmpty(currentHash))
            {
                if (bitmaskInvalid || (edited && !numericOk))
                {
                    // Valores invalidos no se cachean.
                }
                else if (!edited)
                {
                    RemovePendingEntry(currentHash, prop.ValueOffset);
                }
                else
                {
                    string nameHuman = Convert.ToString(row.Cells[1].Value);
                    SetPendingEntry(currentHash, prop, nameHuman, newVal, parsedValue, _currentNodeIsBitmask);
                }
            }

            lblStatus.Text = FormatPendingStatus();
            UpdateBreadcrumb();
        }

        // =========================================================
        // Experimental toggle
        // =========================================================
        void OnShowAllToggle(object sender, EventArgs e)
        {
            if (chkShowAll.Checked)
            {
                var lines = new[] {
                    "ENABLE EXPERIMENTAL SETTINGS", "",
                    "You are about to reveal entries beyond the fully verified ones.",
                    "Three tiers will appear, in order of confidence:", "",
                    "  \u2248 PROBABLE  (yellow rows)",
                    "      High-confidence structural matches. Names reconstructed from",
                    "      hungarian prefix + camelCase + engine suffix, cross-referenced",
                    "      against the game exe string table and RED_EYE asset DB.", "",
                    "  ~ DEDUCED  (orange rows)",
                    "      Names resolved by hash pattern matching. The hash is verified",
                    "      mathematically, but the human-readable name is inferred.", "",
                    "  ? EXPERIMENTAL  (red rows)",
                    "      Below 75% structural confidence. May be hash collisions.", "",
                    "Modifying DEDUCED or EXPERIMENTAL entries may:",
                    "  - Break the RTPC file (game will not load save)",
                    "  - Crash the game in unexpected ways",
                    "  - Corrupt unrelated systems", "",
                    "Always change ONE parameter at a time and test in-game.", "",
                    "Backups are automatic on SAVE. Use the BACKUPS button to restore.", "",
                    "Continue?"
                };
                var r = MessageBox.Show(this, string.Join("\n", lines), "Enable Experimental Settings", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) { chkShowAll.Checked = false; return; }
            }
            UpdateWarningBanner();
            RefreshGrid();
        }

        void UpdateWarningBanner()
        {
            bool showAll = chkShowAll != null && chkShowAll.Checked;
            warningBanner.Visible = showAll;
            if (!warningBanner.Visible) return;
            if (warningBanner.Controls.Count > 0 && warningBanner.Controls[0] is Label lbl)
                lbl.Text = "  EXPERIMENTAL SETTINGS ENABLED: yellow (\u2248 probable), orange (~ deduced) and red (? experimental) rows are visible. Verify in-game after each change. Backups are automatic.";
        }

        // =========================================================
        // Revert
        // =========================================================
        void Revert()
        {
            int total = PendingCount();
            if (total == 0)
            {
                lblStatus.Text = "Nothing to revert.";
                return;
            }
            var r = MessageBox.Show(this,
                "Discard ALL unsaved changes across all files?\r\n\r\n" +
                total + " change(s) in " + _pending.Count + " file(s) will be lost.\r\n\r\n" +
                "This does not touch the .rtpc files on disk.",
                "Discard all changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            _pending.Clear();
            if (tree.SelectedNode != null) RefreshGrid();
            lblStatus.Text = "All unsaved changes discarded.";
            UpdateBreadcrumb();
        }

        // =========================================================
        // Save (1:1)
        // =========================================================
        void Save()
        {
            if (string.IsNullOrEmpty(currentHash))
            {
                MessageBox.Show(this, "Load a file first.", "World Settings Editor");
                return;
            }

            int totalPending = PendingCount();
            if (totalPending == 0)
            {
                MessageBox.Show(this, "Nothing to save.", "World Settings Editor");
                return;
            }

            var bad = new List<string>();
            foreach (var fkv in _pending)
            {
                foreach (var ckv in fkv.Value)
                {
                    var pc = ckv.Value;
                    if (pc.IsBitmask && pc.Type == 1)
                    {
                        int iv = (int)pc.Value;
                        if (!IsValidBitmaskValue(iv))
                            bad.Add(fkv.Key + " / " + pc.NameTech + " = " + pc.NewDisplay);
                    }
                }
            }
            foreach (DataGridViewRow r in grid.Rows)
            {
                string o = Convert.ToString(r.Cells[2].Value);
                string nv = Convert.ToString(r.Cells[3].Value);
                if (o == nv) continue;
                var pp = r.Tag as RTPCProp;
                if (pp == null) continue;
                double vtmp;
                if (!double.TryParse(nv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out vtmp))
                    bad.Add("Visible / " + pp.Name + " = " + nv + " (not a number)");
            }
            if (bad.Count > 0)
            {
                MessageBox.Show(this,
                    "Some values are invalid:" + "\r\n\r\n  " + string.Join("\r\n  ", bad),
                    "Save blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var fileList = new System.Text.StringBuilder();
            foreach (var fkv in _pending)
            {
                string friendly = fkv.Key;
                foreach (var f in FILES) if (f.Hash == fkv.Key) friendly = f.Name + " (" + fkv.Key + ")";
                fileList.AppendLine("  - " + friendly + "  [" + fkv.Value.Count + " change(s)]");
            }

            var confirm = MessageBox.Show(this,
                "Apply " + totalPending + " change(s) across " + _pending.Count + " file(s)?" + "\r\n\r\n" +
                "Files that will be modified and packed:" + "\r\n" +
                fileList.ToString() + "\r\n" +
                "This will:" + "\r\n" +
                "  - Write a backup of each affected .rtpc" + "\r\n" +
                "  - Modify each .rtpc in-place" + "\r\n" +
                "  - Stage all of them" + "\r\n" +
                "  - Build a single mod .zip containing all affected files" + "\r\n\r\n" +
                "Continue?",
                "Confirm SAVE", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            if (!EnsureSessionMeta()) return;

            var snapshot = new Dictionary<string, Dictionary<int, PendingChange>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _pending)
            {
                var inner = new Dictionary<int, PendingChange>();
                foreach (var ikv in kv.Value) inner[ikv.Key] = ikv.Value;
                snapshot[kv.Key] = inner;
            }

            int filesWritten = 0;
            int changesApplied = 0;
            var logEntries = new List<object>();
            var writtenHashes = new List<string>();

            foreach (var fileKv in snapshot)
            {
                string hash = fileKv.Key;
                string fileRtpc = Path.Combine(EXDIR, hash + ".rtpc");
                if (!File.Exists(fileRtpc))
                {
                    MessageBox.Show(this, "File missing: " + fileRtpc, "Save", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var patches = new List<RTPCPatch>();
                var changeList = new List<Dictionary<string, object>>();
                foreach (var changeKv in fileKv.Value)
                {
                    var pc = changeKv.Value;
                    patches.Add(new RTPCPatch { Offset = pc.ValueOffset, Type = pc.Type, Value = pc.Value, Name = pc.NameTech });
                    changeList.Add(new Dictionary<string, object>
                    {
                        { "name_tech", pc.NameTech },
                        { "name_human", pc.NameHuman },
                        { "old", pc.OldDisplay },
                        { "new", pc.NewDisplay },
                        { "offset", pc.ValueOffset }
                    });
                }

                string bakPath = "";
                try
                {
                    string hashDir = Path.Combine(BKUP, hash);
                    Directory.CreateDirectory(hashDir);
                    string tsBak = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    bakPath = Path.Combine(hashDir, tsBak + ".rtpc");
                    File.Copy(fileRtpc, bakPath, true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Backup failed for " + hash + ":" + "\r\n" + ex.Message, "Save", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                int applied = RTPCApplier.Apply(fileRtpc, patches);
                changesApplied += applied;

                string nameHuman = hash;
                foreach (var f in FILES) if (f.Hash == hash) nameHuman = f.Name;
                logEntries.Add(new Dictionary<string, object>
                {
                    { "timestamp", DateTime.Now.ToString("s") },
                    { "file_hash", hash },
                    { "file_name", nameHuman },
                    { "node", "" },
                    { "node_human", "" },
                    { "backup", bakPath },
                    { "changes", changeList }
                });

                string dest = Path.Combine(STAGE, hash + ".rtpc");
                File.Copy(fileRtpc, dest, true);
                filesWritten++;
                writtenHashes.Add(hash);
            }

            try
            {
                var logArray = new List<object>();
                if (File.Exists(CLOG))
                {
                    try
                    {
                        var ex = JsonSerializer.Deserialize<List<object>>(File.ReadAllText(CLOG));
                        if (ex != null) logArray.AddRange(ex);
                    }
                    catch { }
                }
                logArray.AddRange(logEntries);
                File.WriteAllText(CLOG, JsonSerializer.Serialize(logArray, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            _pending.Clear();
            LoadFile();
            lblStatus.Text = "SAVED: " + changesApplied + " change(s) in " + filesWritten + " file(s).";

            try
            {
                string zipPath = BuildModZip();
                if (!string.IsNullOrEmpty(zipPath))
                {
                    lblWarn.Text = "MOD BUILT: " + zipPath;
                    var r = MessageBox.Show(this,
                        "Mod built:" + "\r\n\r\n" + zipPath + "\r\n\r\n" +
                        "Files: " + filesWritten + "   Changes: " + changesApplied + "\r\n" +
                        "Contents: " + string.Join(", ", writtenHashes) + "\r\n\r\n" +
                        "Open the folder?",
                        "Mod built", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (r == DialogResult.Yes)
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = Path.GetDirectoryName(zipPath),
                                UseShellExecute = true
                            });
                        }
                        catch { }
                    }
                }
                else
                {
                    lblWarn.Text = "Stage ready but no .rtpc found.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Mod zip build failed:" + "\r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            UpdateBreadcrumb();
        }

        // =========================================================
        // Mod zip building
        // =========================================================
        bool EnsureSessionMeta()
        {
            if (!string.IsNullOrEmpty(_sessionModName) && !string.IsNullOrEmpty(_sessionModAuthor)) return true;
            string def = "World Settings " + DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            using (var dlg = new ModMetaDialog(def, ""))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                _sessionModName = dlg.ModName;
                _sessionModAuthor = dlg.ModAuthor;
            }
            return !string.IsNullOrEmpty(_sessionModName) && !string.IsNullOrEmpty(_sessionModAuthor);
        }

        string BuildModZip()
        {
            // Recopilar los .rtpc que esten en stage
            if (!Directory.Exists(STAGE)) return null;
            var rtpcs = Directory.GetFiles(STAGE, "*.rtpc");
            if (rtpcs.Length == 0) return null;

            string slug = ModMetadata.Slugify(_sessionModName);
            string outDir = Path.Combine(Rage2Toolkit.Paths.ExeDir, "RAGE2Toolkit_Output", "world_editor");
            Directory.CreateDirectory(outDir);
            string zipPath = Path.Combine(outDir, slug + ".zip");
            if (File.Exists(zipPath)) File.Delete(zipPath);

            var meta = new ModMetadata
            {
                Schema = 1,
                Id = slug,
                Name = _sessionModName,
                Version = "1.0.0",
                Author = _sessionModAuthor,
                Game = "RAGE2",
                CreatedWith = "RAGE2Toolkit v2.1.1",
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Description = "World settings adjustments built with the World Settings Editor."
            };

            var res = ModPackager.Build(STAGE, zipPath, meta, null);
            if (!res.Success) throw new Exception(res.Error ?? "ModPackager returned no error but Success=false");
            return res.ZipPath;
        }

        // =========================================================
        // Breadcrumb
        // =========================================================
        int ComputeStep()
        {
            bool hasFile = !string.IsNullOrEmpty(rtpcPath);
            if (!hasFile) return 1;
            if (PendingCount() > 0) return 3;
            if (!string.IsNullOrEmpty(currentHash))
            {
                string stg = Path.Combine(STAGE, currentHash + ".rtpc");
                if (File.Exists(stg)) return 4;
            }
            return 2;
        }

        void UpdateBreadcrumb()
        {
            if (bcSteps == null) return;
            int current = ComputeStep();
            for (int i = 0; i < 5; i++)
            {
                int stepNum = i + 1;
                Color c;
                string bullet;
                if (stepNum < current) { c = C_OK;    bullet = "\u2713"; }   // hecho
                else if (stepNum == current) { c = C_ACCENT; bullet = "\u25CF"; } // actual
                else { c = C_MUTED;  bullet = "\u25CB"; }                    // pendiente
                string[] names = new string[] { "Choose file", "Edit values", "SAVE", "Repack", "Install" };
                bcSteps[i].Text = bullet + "  " + stepNum + ". " + names[i];
                bcSteps[i].ForeColor = (stepNum == current) ? C_TEXT : (stepNum < current ? C_OK : C_MUTED);
                if (bcSeps != null && i < 4)
                    bcSeps[i].ForeColor = (stepNum < current) ? C_OK : C_BORDER;
            }
            // Paso 5 (Install) siempre gris - es informativo
            if (bcSteps.Length >= 5)
                bcSteps[4].ForeColor = C_MUTED;
        }

        // =========================================================
        // Bitmask scan (todos los ficheros)
        // =========================================================
        void ScanBitmasks()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("BITMASK SCAN - " + FILES.Length + " files");
            report.AppendLine(new string('=', 60));
            report.AppendLine();
            int totalBitmaskNodes = 0;
            foreach (var f in FILES)
            {
                string path = Path.Combine(EXDIR, f.Hash + ".rtpc");
                report.AppendLine("[" + f.Hash + "]  " + f.Name);
                if (!File.Exists(path))
                {
                    report.AppendLine("    (file not found in work dir)");
                    report.AppendLine();
                    continue;
                }
                RTPCNode root = null;
                try { root = RTPCParser.Parse(path).Root; }
                catch (Exception ex) { report.AppendLine("    parse error: " + ex.Message); report.AppendLine(); continue; }
                int hits = 0;
                WalkBitmasks(root, "", report, ref hits);
                totalBitmaskNodes += hits;
                if (hits == 0) report.AppendLine("    (no bitmask nodes)");
                report.AppendLine();
            }
            report.AppendLine(new string('=', 60));
            report.AppendLine("Total bitmask nodes: " + totalBitmaskNodes);

            // Dialog
            var dlg = new Form
            {
                Text = "Bitmask scan",
                Size = new Size(820, 620),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = C_BG,
                ForeColor = C_TEXT,
                Font = new Font("Consolas", 9),
                MinimizeBox = false,
                MaximizeBox = false
            };
            var box = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BackColor = C_PANEL,
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9),
                Text = report.ToString()
            };
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = C_PANEL };
            var btnCopy = new Button
            {
                Text = "COPY",
                Location = new Point(20, 12),
                Size = new Size(120, 36),
                BackColor = C_PANEL2,
                ForeColor = C_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9)
            };
            btnCopy.FlatAppearance.BorderColor = C_BORDER;
            btnCopy.FlatAppearance.BorderSize = 1;
            btnCopy.Click += (s, e) => { try { Clipboard.SetText(box.Text); MessageBox.Show(dlg, "Copied.", "OK", MessageBoxButtons.OK, MessageBoxIcon.Information); } catch { } };
            footer.Controls.Add(btnCopy);
            var btnClose = new Button
            {
                Text = "CLOSE",
                Location = new Point(660, 12),
                Size = new Size(140, 36),
                BackColor = C_ACCENT,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => dlg.Close();
            footer.Controls.Add(btnClose);
            dlg.Controls.Add(box);
            dlg.Controls.Add(footer);
            box.BringToFront();
            dlg.AcceptButton = btnClose;
            dlg.ShowDialog(this);
        }

        void WalkBitmasks(RTPCNode n, string path, System.Text.StringBuilder sb, ref int hits)
        {
            if (n == null) return;
            string cur = string.IsNullOrEmpty(path) ? n.Name : path + " > " + n.Name;
            if (DetectBitmask(n))
            {
                hits++;
                int cnt = 0;
                foreach (var p in n.Props) if (p.Type == 1) cnt++;
                sb.AppendLine("    [BITMASK] " + cur + "   (" + cnt + " int props)");
                foreach (var p in n.Props)
                {
                    if (p.Type != 1) continue;
                    try
                    {
                        int v = Convert.ToInt32(p.Value);
                        sb.AppendLine("        " + p.Name.PadRight(28) + " = " + v);
                    }
                    catch { }
                }
            }
            foreach (var c in n.Children) WalkBitmasks(c, cur, sb, ref hits);
        }

        // =========================================================
        // Backup Manager
        // =========================================================
        // ============================================================
        // PENDING CHANGES VIEWER
        // ============================================================
        void ShowPendingChanges()
        {
            int total = PendingCount();
            if (total == 0)
            {
                MessageBox.Show(this,
                    "No pending changes.\r\n\r\nEdit a value in the 'New value' column and it will appear here.",
                    "Pending changes", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var pf = new PendingChangesForm(_pending, currentHash))
            {
                pf.ShowDialog(this);
                if (pf.NavigateTo.HasValue)
                {
                    var target = pf.NavigateTo.Value;
                    NavigateToChange(target.fileHash, target.valueOffset);
                }
                if (pf.ClearedAll)
                {
                    _pending.Clear();
                    lblStatus.Text = FormatPendingStatus();
                    RefreshGrid();
                }
            }
        }

        // Navega al nodo que contiene una prop con el ValueOffset indicado y refresca el grid.
        void NavigateToChange(string fileHash, int valueOffset)
        {
            if (string.IsNullOrEmpty(fileHash)) return;

            // Si es otro fichero, primero cambiar el combo.
            if (!string.Equals(fileHash, currentHash, StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < cboFile.Items.Count; i++)
                {
                    var s = cboFile.Items[i] as string;
                    if (s != null && s.IndexOf(fileHash, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        cboFile.SelectedIndex = i;
                        break;
                    }
                }
                if (!string.Equals(fileHash, currentHash, StringComparison.OrdinalIgnoreCase))
                    return; // no se pudo cambiar de fichero
            }

            // Buscar el nodo cuyo props contengan ese ValueOffset.
            TreeNode found = null;
            foreach (TreeNode root in tree.Nodes)
            {
                found = FindNodeByValueOffset(root, valueOffset);
                if (found != null) break;
            }
            if (found != null)
            {
                tree.SelectedNode = found;
                found.EnsureVisible();
                tree.Focus();
            }
        }

        TreeNode FindNodeByValueOffset(TreeNode node, int valueOffset)
        {
            if (node == null) return null;
            var n = node.Tag as RTPCNode;
            if (n != null && n.Props != null)
            {
                foreach (var p in n.Props)
                {
                    if (p.ValueOffset == valueOffset) return node;
                }
            }
            foreach (TreeNode child in node.Nodes)
            {
                var r = FindNodeByValueOffset(child, valueOffset);
                if (r != null) return r;
            }
            return null;
        }

        void ShowBackupManager()
        {
            if (string.IsNullOrEmpty(rtpcPath))
            {
                MessageBox.Show(this, "Load a file first.", "Backups", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (var bm = new BackupManagerForm(BKUP, currentHash, rtpcPath))
            {
                bm.ShowDialog(this);
                if (bm.Restored)
                {
                    LoadFile(true);  // limpiar pending del hash restaurado
                }
            }
        }

        // =========================================================
        // Help (1:1)
        // =========================================================
        void ShowHelp()
        {
            var helpForm = new Form
            {
                Text = "World Settings Editor - Help",
                Size = new Size(860, 780),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = C_BG,
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 9),
                MinimizeBox = false,
                MaximizeBox = false
            };

            var helpHeader = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = C_PANEL };
            helpForm.Controls.Add(helpHeader);
            var helpTitle = new Label
            {
                Text = "How to use the World Settings Editor",
                Location = new Point(20, 12),
                Size = new Size(600, 28),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = C_ACCENT
            };
            helpHeader.Controls.Add(helpTitle);
            var accentBar = new Panel { Location = new Point(0, 48), Size = new Size(860, 2), BackColor = C_ACCENT };
            helpHeader.Controls.Add(accentBar);

            var helpText = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                WordWrap = true,
                Dock = DockStyle.Fill,
                BackColor = C_PANEL,
                ForeColor = C_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10)
            };
            helpForm.Controls.Add(helpText);
            helpText.BringToFront();

            var helpFooter = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = C_PANEL };
            helpForm.Controls.Add(helpFooter);
            var btnClose = new Button
            {
                Text = "GOT IT",
                Size = new Size(140, 40),
                Location = new Point(690, 10),
                BackColor = C_OK,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, e) => helpForm.Close();
            helpFooter.Controls.Add(btnClose);

            var sb = new StringBuilder();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  WHAT IS THIS EDITOR?");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("This editor lets you modify the RTPC settings files");
            sb.AppendLine("extracted from RAGE 2. These files control the spawn");
            sb.AppendLine("system: how many civilians, vehicles, combatants,");
            sb.AppendLine("encounters, and other entities exist in the open world.");
            sb.AppendLine();
            sb.AppendLine("The real power lives in \"Spawn Budget Pools\". Changing");
            sb.AppendLine("\"Amount in World\" for a category is the main way to");
            sb.AppendLine("make the world feel alive (or empty).");
            sb.AppendLine();
            sb.AppendLine("You can edit as many files and as many nodes as you want");
            sb.AppendLine("in a single session. Every change is tracked in memory");
            sb.AppendLine("and shown with a magenta tint. Nothing is written to disk");
            sb.AppendLine("until you click SAVE.");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  WORKFLOW");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("  1. Pick a file from the dropdown at the top.");
            sb.AppendLine("     The files are already extracted and ready to edit.");
            sb.AppendLine();
            sb.AppendLine("  2. Browse the tree on the left.");
            sb.AppendLine("     Each [N] next to a node = number of editable");
            sb.AppendLine("     properties inside.");
            sb.AppendLine();
            sb.AppendLine("  3. Click any property and edit the \"New value\" column.");
            sb.AppendLine("     The row turns magenta to mark it as modified.");
            sb.AppendLine("     Navigate freely between nodes and files: your");
            sb.AppendLine("     changes stay in memory until SAVE.");
            sb.AppendLine();
            sb.AppendLine("  4. Watch the status bar at the bottom: it shows the");
            sb.AppendLine("     total number of unsaved changes across all files.");
            sb.AppendLine();
            sb.AppendLine("  5. Click SAVE when you are done.");
            sb.AppendLine("     A confirmation dialog lists every file that will be");
            sb.AppendLine("     modified. On confirm:");
            sb.AppendLine("       - A backup is written for each affected .rtpc");
            sb.AppendLine("       - Each .rtpc is modified in-place");
            sb.AppendLine("       - All of them are staged");
            sb.AppendLine("       - A single mod .zip is built with every affected file");
            sb.AppendLine("     The .zip is ready to install with the Mod Manager.");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  WHERE THINGS GO");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("D:\\RAGE2MODDING\\_outputs\\world_editor\\");
            sb.AppendLine();
            sb.AppendLine("  _backups\\<hash>\\<timestamp>.rtpc");
            sb.AppendLine("      Every SAVE creates a NEW backup. Nothing is overwritten.");
            sb.AppendLine("      Manage them with the BACKUPS button in the footer:");
            sb.AppendLine("      restore an older version, delete unused ones, open folder.");
            sb.AppendLine();
            sb.AppendLine("  _stage_mods\\<hash>.rtpc");
            sb.AppendLine("      Internal staging area. Cleared automatically when the");
            sb.AppendLine("      editor opens, so each session starts fresh. Populated");
            sb.AppendLine("      when you click SAVE.");
            sb.AppendLine();
            sb.AppendLine("  _changes_log.json");
            sb.AppendLine("      Full history of every change.");
            sb.AppendLine();
            sb.AppendLine("The final .zip lands in:");
            sb.AppendLine("  <toolkit>\\RAGE2Toolkit_Output\\world_editor\\<slug>.zip");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  BACKUP MANAGER");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("Click BACKUPS in the footer to open it. Shows every backup");
            sb.AppendLine("for the current file, newest first.");
            sb.AppendLine();
            sb.AppendLine("  - [original] = the very first snapshot. Cannot be deleted.");
            sb.AppendLine("  - [snapshot] = created before each SAVE. Can be deleted.");
            sb.AppendLine();
            sb.AppendLine("  RESTORE SELECTED copies a chosen backup over the current");
            sb.AppendLine("  .rtpc and reloads the editor. The change is NOT staged");
            sb.AppendLine("  until you click SAVE again.");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  CONFIDENCE ICONS");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("  OK  = Confirmed. Verified meaning. Safe to edit.");
            sb.AppendLine("  ~   = Deduced. High confidence, but not 100% verified.");
            sb.AppendLine("  \u2248   = Probable. High-confidence structural match.");
            sb.AppendLine("  ?   = Experimental. Unknown or inferred meaning.");
            sb.AppendLine("  i   = Info. Read-only, structural. Do not edit.");
            sb.AppendLine();
            sb.AppendLine("Parameters are sorted top-to-bottom by how well we");
            sb.AppendLine("understand them: the confirmed ones are at the top,");
            sb.AppendLine("the experimental ones at the bottom.");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  SAFETY RULES");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("  - Change ONE parameter at a time.");
            sb.AppendLine("  - Test in-game between changes.");
            sb.AppendLine("  - If the game crashes, use the BACKUPS button to roll back.");
            sb.AppendLine("  - Never edit \"?\" parameters first - start with OK.");
            sb.AppendLine("  - Never set \"Amount in World\" to extreme values");
            sb.AppendLine("    (stick to the range 0.1 .. 20.0).");
            sb.AppendLine("  - The REVERT button discards ALL unsaved changes across");
            sb.AppendLine("    every file in the session (after confirmation).");
            sb.AppendLine("  - Backups are your safety net. Use them.");
helpText.Text = sb.ToString();
            helpText.SelectionStart = 0;
            helpText.SelectionLength = 0;

            helpForm.ActiveControl = btnClose;
            helpForm.ShowDialog(this);
        }
        private bool _tabsWrapped = false;

        private void WrapIntoTabs_Loaded(object sender, EventArgs e)
        {
            if (_tabsWrapped) return;
            if (this.Controls.Count == 0) return;
            if (this.Controls[0] is TabControl) { _tabsWrapped = true; return; }

            _tabsWrapped = true;

            var tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9f) };
            var tab1 = new TabPage("RTPC Files") { BackColor = this.BackColor, ForeColor = this.ForeColor, UseVisualStyleBackColor = false };
            var tab2 = new TabPage("Game Settings (ini)") { BackColor = this.BackColor, ForeColor = this.ForeColor, UseVisualStyleBackColor = false };
            tabs.TabPages.Add(tab1);

            this.Controls.Add(tabs);
            tabs.BringToFront();

            var existing = new Control[this.Controls.Count];
            this.Controls.CopyTo(existing, 0);
            foreach (var c in existing)
            {
                if (c == tabs) continue;
                c.Parent = tab1;
            }
            tab1.PerformLayout();
            tab1.ResumeLayout(true);

            try
            {
                var iniForm = new SettingsIniEditorForm
                {
                    TopLevel = false,
                    FormBorderStyle = FormBorderStyle.None,
                    Dock = DockStyle.Fill
                };
                tab2.Controls.Add(iniForm);
                iniForm.Show();
            }
            catch (Exception exIni)
            {
                try
                {
                    string logPath = Path.Combine(Path.GetTempPath(), "settingsini_load_error.txt");
                    File.WriteAllText(logPath, DateTime.Now.ToString("s") + "\n" + exIni.ToString());
                }
                catch { }

                var lbl = new Label
                {
                    Text = "Failed to load settings.ini editor:\n\n" +
                           exIni.GetType().Name + ": " + exIni.Message +
                           "\n\nLog: %TEMP%\\settingsini_load_error.txt",
                    ForeColor = Color.Salmon,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 10f)
                };
                tab2.Controls.Add(lbl);
            }

            // Wire "Enable experimental settings" checkbox to toggle tab2 visibility
            CheckBox expCb = FindCheckBoxByText(tab1, "experimental");
            if (expCb != null)
            {
                if (expCb.Checked) tabs.TabPages.Add(tab2);
                expCb.CheckedChanged += (s2, e2) =>
                {
                    if (expCb.Checked)
                    {
                        if (!tabs.TabPages.Contains(tab2)) tabs.TabPages.Add(tab2);
                    }
                    else
                    {
                        tabs.TabPages.Remove(tab2);
                    }
                };
            }
        }

        private static CheckBox FindCheckBoxByText(Control parent, string textContains)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is CheckBox cb && cb.Text != null &&
                    cb.Text.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return cb;
                }
                var nested = FindCheckBoxByText(c, textContains);
                if (nested != null) return nested;
            }
            return null;
        }
    }

    // =============================================================
    // BACKUP MANAGER
    // =============================================================
    // ============================================================
    // PendingChange (top-level - compartido entre forms)
    // ============================================================
    public class PendingChange
    {
        public string FileHash;
        public int    ValueOffset;
        public byte   Type;          // 1=uint, 2=float
        public double Value;
        public string NameTech;
        public string NameHuman;
        public string OldDisplay;
        public string NewDisplay;
        public bool   IsBitmask;
    }

    public class BackupManagerForm : Form
    {
        readonly string hashDir;
        readonly string currentHash;
        readonly string rtpcPath;
        ListView lv;
        Label lblInfo;
        string selectedPath;

        public bool Restored { get; private set; }

        static readonly Color BM_BG     = Color.FromArgb(16, 16, 20);
        static readonly Color BM_PANEL  = Color.FromArgb(24, 24, 30);
        static readonly Color BM_PANEL2 = Color.FromArgb(30, 30, 38);
        static readonly Color BM_TEXT   = Color.FromArgb(235, 235, 240);
        static readonly Color BM_MUTED  = Color.FromArgb(150, 150, 160);
        static readonly Color BM_ACCENT = Color.FromArgb(210, 70, 190);
        static readonly Color BM_OK     = Color.FromArgb(80, 180, 100);
        static readonly Color BM_WARN   = Color.FromArgb(255, 180, 40);
        static readonly Color BM_BORDER = Color.FromArgb(60, 60, 72);

        public BackupManagerForm(string bkupRoot, string hash, string originalRtpc)
        {
            hashDir = Path.Combine(bkupRoot, hash);
            currentHash = hash;
            rtpcPath = originalRtpc;

            Text = "Backup Manager - " + hash;
            Size = new Size(880, 560);
            MinimumSize = new Size(720, 420);
            BackColor = BM_BG;
            ForeColor = BM_TEXT;
            Font = new Font("Segoe UI", 9);
            StartPosition = FormStartPosition.CenterParent;

            var header = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = BM_PANEL };
            var accent = new Panel { Dock = DockStyle.Top, Height = 3, BackColor = BM_ACCENT };
            header.Controls.Add(accent);
            accent.BringToFront();

            var title = new Label
            {
                Text = "Backup Manager",
                Location = new Point(20, 10),
                Size = new Size(400, 28),
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = BM_ACCENT
            };
            header.Controls.Add(title);

            var subtitle = new Label
            {
                Text = "Every SAVE creates a new backup. Newest first. [original] cannot be deleted.",
                Location = new Point(20, 42),
                Size = new Size(800, 20),
                Font = new Font("Segoe UI", 9),
                ForeColor = BM_MUTED
            };
            header.Controls.Add(subtitle);

            lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                BackColor = BM_PANEL,
                ForeColor = BM_TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10)
            };
            lv.Columns.Add("Timestamp", 200);
            lv.Columns.Add("Size", 100);
            lv.Columns.Add("Tag", 120);
            lv.Columns.Add("Path", 420);
            lv.SelectedIndexChanged += (s, e) => UpdateInfo();
            lv.DoubleClick += (s, e) => RestoreSelected();

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 110, BackColor = BM_PANEL };

            lblInfo = new Label
            {
                Location = new Point(20, 10),
                Size = new Size(840, 40),
                ForeColor = BM_MUTED,
                Font = new Font("Segoe UI", 9),
                Text = "No backup selected."
            };
            footer.Controls.Add(lblInfo);

            var btnPanel = new Panel { Dock = DockStyle.Right, Width = 700, BackColor = BM_PANEL };

            var btnRestore = new Button
            {
                Text = "RESTORE SELECTED",
                Location = new Point(20, 50),
                Size = new Size(180, 44),
                BackColor = BM_OK,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            btnRestore.FlatAppearance.BorderSize = 0;
            btnRestore.Click += (s, e) => RestoreSelected();
            btnPanel.Controls.Add(btnRestore);

            var btnDelete = new Button
            {
                Text = "DELETE",
                Location = new Point(210, 50),
                Size = new Size(120, 44),
                BackColor = BM_PANEL2,
                ForeColor = BM_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnDelete.FlatAppearance.BorderColor = BM_BORDER;
            btnDelete.FlatAppearance.BorderSize = 1;
            btnDelete.Click += (s, e) => DeleteSelected();
            btnPanel.Controls.Add(btnDelete);

            var btnFolder = new Button
            {
                Text = "OPEN FOLDER",
                Location = new Point(340, 50),
                Size = new Size(150, 44),
                BackColor = BM_PANEL2,
                ForeColor = BM_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnFolder.FlatAppearance.BorderColor = BM_BORDER;
            btnFolder.FlatAppearance.BorderSize = 1;
            btnFolder.Click += (s, e) => OpenFolder();
            btnPanel.Controls.Add(btnFolder);

            var btnClose = new Button
            {
                Text = "CLOSE",
                Location = new Point(500, 50),
                Size = new Size(140, 44),
                BackColor = BM_PANEL2,
                ForeColor = BM_TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10)
            };
            btnClose.FlatAppearance.BorderColor = BM_BORDER;
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.Click += (s, e) => this.Close();
            btnPanel.Controls.Add(btnClose);

            footer.Controls.Add(btnPanel);

            Controls.Add(lv);
            Controls.Add(footer);
            Controls.Add(header);
            lv.BringToFront();

            LoadList();
        }

        void LoadList()
        {
            lv.Items.Clear();
            selectedPath = null;
            if (!Directory.Exists(hashDir))
            {
                lblInfo.Text = "No backups yet for this file. First SAVE will create one.";
                return;
            }
            var files = new DirectoryInfo(hashDir).GetFiles("*.rtpc")
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
            if (files.Count == 0)
            {
                lblInfo.Text = "No backups yet for this file. First SAVE will create one.";
                return;
            }
            int lastIdx = files.Count - 1;
            for (int i = 0; i < files.Count; i++)
            {
                var f = files[i];
                bool isOriginal = (i == lastIdx);
                var item = new ListViewItem(f.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add((f.Length / 1024.0).ToString("0.0") + " KB");
                item.SubItems.Add(isOriginal ? "[original]" : "[snapshot]");
                item.SubItems.Add(f.FullName);
                item.Tag = f.FullName;
                item.ForeColor = isOriginal ? BM_WARN : BM_TEXT;
                lv.Items.Add(item);
            }
            if (lv.Items.Count > 0)
            {
                lv.Items[0].Selected = true;
                lv.Select();
            }
        }

        void UpdateInfo()
        {
            if (lv.SelectedItems.Count == 0) { selectedPath = null; lblInfo.Text = "No backup selected."; return; }
            var it = lv.SelectedItems[0];
            selectedPath = (string)it.Tag;
            string tag = it.SubItems[2].Text;
            lblInfo.Text = "Selected: " + it.SubItems[0].Text + "  (" + it.SubItems[1].Text + ")  " + tag + "\r\n" + selectedPath;
        }

        void RestoreSelected()
        {
            if (string.IsNullOrEmpty(selectedPath)) return;
            if (!File.Exists(selectedPath)) { MessageBox.Show(this, "Backup file not found:\r\n" + selectedPath, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Warning); LoadList(); return; }

            var r = MessageBox.Show(this,
                "Restore this backup over the current .rtpc?\r\n\r\n" +
                "Backup: " + selectedPath + "\r\n" +
                "Target: " + rtpcPath + "\r\n\r\n" +
                "The current file will be overwritten. This is NOT staged until you SAVE.\r\n" +
                "Continue?",
                "Confirm Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;

            try
            {
                File.Copy(selectedPath, rtpcPath, true);
                Restored = true;
                MessageBox.Show(this, "Restored.\r\n\r\nThe editor will reload the file now.", "Restore", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Restore failed:\r\n" + ex.Message, "Restore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void DeleteSelected()
        {
            if (string.IsNullOrEmpty(selectedPath)) return;
            var it = lv.SelectedItems[0];
            if (it.SubItems[2].Text == "[original]")
            {
                MessageBox.Show(this, "[original] cannot be deleted. It is the safety net.", "Delete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            var r = MessageBox.Show(this, "Delete this backup?\r\n\r\n" + selectedPath, "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            try { File.Delete(selectedPath); LoadList(); }
            catch (Exception ex) { MessageBox.Show(this, "Delete failed:\r\n" + ex.Message, "Delete", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        void OpenFolder()
        {
            try
            {
                if (!Directory.Exists(hashDir)) Directory.CreateDirectory(hashDir);
                Process.Start(new ProcessStartInfo { FileName = hashDir, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, "Cannot open folder:\r\n" + ex.Message, "Open", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }

    // ============================================================
    // PENDING CHANGES VIEWER
    // ============================================================
    public class PendingChangesForm : Form
    {
        public struct NavTarget
        {
            public string fileHash;
            public int valueOffset;
        }

        public NavTarget? NavigateTo { get; private set; }
        public bool ClearedAll { get; private set; }

        static readonly Color BG     = Color.FromArgb(16, 16, 20);
        static readonly Color PANEL  = Color.FromArgb(24, 24, 30);
        static readonly Color TEXT   = Color.FromArgb(235, 235, 240);
        static readonly Color MUTED  = Color.FromArgb(150, 150, 160);
        static readonly Color ACCENT = Color.FromArgb(210, 70, 190);
        static readonly Color OK     = Color.FromArgb(80, 180, 100);
        static readonly Color WARN   = Color.FromArgb(255, 180, 40);

        readonly Dictionary<string, Dictionary<int, PendingChange>> _pending;
        readonly string _currentHash;
        ListView _lv;

        public PendingChangesForm(Dictionary<string, Dictionary<int, PendingChange>> pending, string currentHash)
        {
            _pending = pending;
            _currentHash = currentHash;

            Text = "Pending changes";
            Size = new Size(960, 560);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BG;
            ForeColor = TEXT;
            Font = new Font("Segoe UI", 9);

            var header = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = PANEL };
            Controls.Add(header);
            var title = new Label
            {
                Text = "PENDING CHANGES",
                Location = new Point(20, 14),
                Size = new Size(400, 26),
                Font = new Font("Segoe UI", 13, FontStyle.Bold),
                ForeColor = ACCENT
            };
            header.Controls.Add(title);
            var subtitle = new Label
            {
                Text = "Double-click a row to jump to that setting in the editor. All changes are saved together when you click SAVE.",
                Location = new Point(20, 34),
                Size = new Size(900, 18),
                ForeColor = MUTED,
                Font = new Font("Segoe UI", 8.5f)
            };
            header.Controls.Add(subtitle);

            _lv = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                BackColor = PANEL,
                ForeColor = TEXT,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 9),
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            _lv.Columns.Add("File", 200);
            _lv.Columns.Add("Setting", 320);
            _lv.Columns.Add("Current", 140);
            _lv.Columns.Add("New value", 140);
            _lv.Columns.Add("Offset", 100);
            _lv.DoubleClick += (s, e) => FireNavigate();
            Controls.Add(_lv);
            _lv.BringToFront();

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 60, BackColor = PANEL };
            Controls.Add(footer);
            footer.BringToFront();

            var bGo = new Button
            {
                Text = "GO TO SELECTED",
                Size = new Size(170, 36),
                Location = new Point(20, 12),
                BackColor = ACCENT,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            bGo.FlatAppearance.BorderSize = 0;
            bGo.Click += (s, e) => FireNavigate();
            footer.Controls.Add(bGo);

            var bClear = new Button
            {
                Text = "DISCARD ALL CHANGES",
                Size = new Size(210, 36),
                Location = new Point(200, 12),
                BackColor = PANEL,
                ForeColor = WARN,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            bClear.FlatAppearance.BorderColor = WARN;
            bClear.FlatAppearance.BorderSize = 1;
            bClear.Click += (s, e) =>
            {
                var r = MessageBox.Show(this,
                    "Discard ALL pending changes across ALL files?\r\n\r\nThis cannot be undone.",
                    "Discard all", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r == DialogResult.Yes)
                {
                    ClearedAll = true;
                    Close();
                }
            };
            footer.Controls.Add(bClear);

            var bClose = new Button
            {
                Text = "CLOSE",
                Size = new Size(120, 36),
                Location = new Point(810, 12),
                BackColor = PANEL,
                ForeColor = TEXT,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            bClose.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 72);
            bClose.FlatAppearance.BorderSize = 1;
            bClose.Click += (s, e) => Close();
            footer.Controls.Add(bClose);

            PopulateList();
        }

        void PopulateList()
        {
            _lv.Items.Clear();
            foreach (var fkv in _pending)
            {
                bool isCurrent = string.Equals(fkv.Key, _currentHash, StringComparison.OrdinalIgnoreCase);
                foreach (var okv in fkv.Value)
                {
                    var pc = okv.Value;
                    var item = new ListViewItem(fkv.Key + (isCurrent ? "  (current)" : ""));
                    item.SubItems.Add(pc.NameHuman ?? pc.NameTech ?? "?");
                    item.SubItems.Add(pc.OldDisplay ?? "");
                    item.SubItems.Add(pc.NewDisplay ?? "");
                    item.SubItems.Add("0x" + pc.ValueOffset.ToString("X"));
                    item.Tag = new NavTarget { fileHash = fkv.Key, valueOffset = pc.ValueOffset };
                    if (!isCurrent) item.ForeColor = MUTED;
                    _lv.Items.Add(item);
                }
            }
            if (_lv.Items.Count > 0) _lv.Items[0].Selected = true;
        }

        void FireNavigate()
        {
            if (_lv.SelectedItems.Count == 0) return;
            var t = (NavTarget)_lv.SelectedItems[0].Tag;
            NavigateTo = t;
            Close();
        }
    }

}