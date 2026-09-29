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
            ("9661597C2BA6B6EF", "Player Stats",         "Player health, armor, movement, abilities and combat tuning."),
            ("67B4C03CA7428D09", "Difficulty",           "Enemy scaling, cooldowns and damage tuning per difficulty tier."),
            ("2F6B4EC6033419CF", "Damage Types",         "Bitmask definitions of every damage type (Fire, Bullet, EMP, Corruption, ...)."),
            ("D779BE9109D9BB6F", "Vehicle Types",        "Physics and handling parameters for each vehicle class."),
            ("77637ABA456411A0", "Weather Settings",     "Weather presets, transitions and conditions."),
            ("2DBD1B76CC037780", "Sun / Lighting",       "Global sun, sky and lighting setup for the world."),
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

        public SettingsEditorForm()
        {
            BKUP = Path.Combine(OUTS, "_backups");
            STAGE = Path.Combine(OUTS, "_stage_mods");
            CLOG = Path.Combine(OUTS, "_changes_log.json");
            try { Directory.CreateDirectory(BKUP); } catch { }
            try { Directory.CreateDirectory(STAGE); } catch { }
            try { Directory.CreateDirectory(EXDIR); } catch { }

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
                Size = new Size(900, 22),
                ForeColor = C_TEXT,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Text = "No node selected"
            };
            footer.Controls.Add(lblCat);

            lblStatus = new Label
            {
                Location = new Point(20, 34),
                Size = new Size(900, 20),
                ForeColor = C_MUTED,
                Font = new Font("Segoe UI", 8)
            };
            footer.Controls.Add(lblStatus);

            lblWarn = new Label
            {
                Location = new Point(20, 58),
                Size = new Size(900, 20),
                ForeColor = C_WARN,
                Font = new Font("Segoe UI", 8, FontStyle.Italic)
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
        void LoadFile()
        {
            string sel = cboFile.SelectedItem as string;
            if (string.IsNullOrEmpty(sel)) return;
            var m = Regex.Match(sel, "([0-9A-Fa-f]{16})");
            if (!m.Success) return;
            string h = m.Groups[1].Value.ToUpperInvariant();
            string rtpc = Path.Combine(EXDIR, h + ".rtpc");

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
            if (rootNode != null) AddTreeNode(rootNode, null);
            tree.ExpandAll();

            lblCat.Text = FILES.FirstOrDefault(x => x.Hash == h).Name;
            lblStatus.Text = "File loaded.";
            UpdateFileDescription();
            lblWarn.Text = "";
            grid.Rows.Clear();
            currentNode = null;
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
                string icon = info.Conf == "C" ? "OK" : info.Conf == "P" ? "\u2248" : info.Conf == "D" ? "~" : info.Conf == "I" ? "i" : "?";

                int idx = grid.Rows.Add(icon, info.Name, val, val, info.Desc ?? "", p.Name);
                var row = grid.Rows[idx];

                if (info.Conf == "P") row.DefaultCellStyle.BackColor = Color.FromArgb(42, 36, 10);   // dark yellow
                if (info.Conf == "D") row.DefaultCellStyle.BackColor = Color.FromArgb(45, 25, 10);   // dark orange
                if (info.Conf == "E") row.DefaultCellStyle.BackColor = Color.FromArgb(45, 15, 15);   // dark red
                if (info.Conf == "I") row.DefaultCellStyle.BackColor = Color.FromArgb(28, 28, 34);   // dark gray

                row.Tag = p;
                // Columna Current (original, no editable) - gris apagado
                row.Cells[2].Style.BackColor = Color.FromArgb(18, 18, 24);
                row.Cells[2].Style.ForeColor = Color.FromArgb(160, 160, 170);
                row.Cells[2].Style.SelectionBackColor = Color.FromArgb(40, 40, 50);
                row.Cells[2].Style.SelectionForeColor = Color.FromArgb(210, 210, 220);
                row.Cells[2].Style.Font = new Font("Consolas", 9, FontStyle.Italic);
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
            lblStatus.Text = "Hover a row for the full explanation. Edit the 'New value' column, then click SAVE.";
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
            // Bitmask validation
            var prop = row.Tag as RTPCProp;
            bool bitmaskInvalid = false;
            if (_currentNodeIsBitmask && prop != null && prop.Type == 1)
            {
                string nv = Convert.ToString(row.Cells[3].Value);
                int iv;
                if (int.TryParse(nv, out iv))
                {
                    if (!IsValidBitmaskValue(iv))
                    {
                        bitmaskInvalid = true;
                        row.Cells[3].Style.BackColor = Color.FromArgb(80, 20, 20);
                        row.Cells[3].Style.ForeColor = Color.FromArgb(255, 180, 180);
                        row.Cells[3].ToolTipText = "INVALID BITMASK VALUE \u2014 must be 0 or a power of 2 (1, 2, 4, 8, 16, ...)";
                    }
                    else
                    {
                        if (Convert.ToString(row.Cells[2].Value) != Convert.ToString(row.Cells[3].Value))
                        {
                            // editado y valido -> magenta suave
                            row.Cells[3].Style.BackColor = Color.FromArgb(70, 30, 80);
                            row.Cells[3].Style.ForeColor = Color.FromArgb(240, 200, 255);
                            row.Cells[3].ToolTipText = "Edited value (valid).";
                        }
                        else
                        {
                            row.Cells[3].Style.BackColor = Color.Empty;
                            row.Cells[3].Style.ForeColor = Color.Empty;
                            row.Cells[3].ToolTipText = "";
                        }
                    }
                }
            }

            if (bitmaskInvalid)
                row.DefaultCellStyle.BackColor = C_ERROR;
            else if (Convert.ToString(row.Cells[2].Value) != Convert.ToString(row.Cells[3].Value))
                row.DefaultCellStyle.BackColor = C_DIFF;
            else
                row.DefaultCellStyle.BackColor = C_PANEL;

            int n = 0;
            foreach (DataGridViewRow r in grid.Rows)
                if (Convert.ToString(r.Cells[2].Value) != Convert.ToString(r.Cells[3].Value)) n++;
            lblStatus.Text = n > 0 ? (n + " unsaved change(s).") : "";
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
            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Cells[3].Value = row.Cells[2].Value;
                row.DefaultCellStyle.BackColor = C_PANEL;
            }
            lblStatus.Text = "Reverted (nothing saved).";
        }

        // =========================================================
        // Save (1:1)
        // =========================================================
        void Save()
        {
            if (string.IsNullOrEmpty(rtpcPath))
            {
                MessageBox.Show(this, "Load a file first.", "World Settings Editor");
                return;
            }
            var patches = new List<RTPCPatch>();
            var changeList = new List<Dictionary<string, object>>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                string o = Convert.ToString(row.Cells[2].Value);
                string nv = Convert.ToString(row.Cells[3].Value);
                if (o == nv) continue;
                var p = row.Tag as RTPCProp;
                if (p == null) continue;
                double v;
                if (!double.TryParse(nv, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
                {
                    MessageBox.Show(this, "Invalid numeric value for " + p.Name + ": " + nv, "Save");
                    return;
                }
                patches.Add(new RTPCPatch { Offset = p.ValueOffset, Type = p.Type, Value = v, Name = p.Name });
                changeList.Add(new Dictionary<string, object>
                {
                    { "name_tech", p.Name },
                    { "name_human", Convert.ToString(row.Cells[1].Value) },
                    { "old", o }, { "new", nv }, { "offset", p.ValueOffset }
                });
            }
            if (patches.Count == 0)
            {
                MessageBox.Show(this, "Nothing to save.", "World Settings Editor");
                return;
            }

            // Bitmask validation gate
            if (_currentNodeIsBitmask)
            {
                var bad = new List<string>();
                foreach (DataGridViewRow r in grid.Rows)
                {
                    var pp = r.Tag as RTPCProp;
                    if (pp == null || pp.Type != 1) continue;
                    string nv = Convert.ToString(r.Cells[3].Value);
                    int iv;
                    if (!int.TryParse(nv, out iv)) continue;
                    if (!IsValidBitmaskValue(iv)) bad.Add(pp.Name + " = " + nv);
                }
                if (bad.Count > 0)
                {
                    MessageBox.Show(this,
                        "Some values are invalid for this BITMASK node.\r\n\r\nValid values are 0 or a power of 2 (1, 2, 4, 8, 16, ...).\r\n\r\nOffending rows:\r\n  " +
                        string.Join("\r\n  ", bad),
                        "Invalid bitmask value", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            // Backup
            string hashDir = Path.Combine(BKUP, currentHash);
            Directory.CreateDirectory(hashDir);
            string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string bakPath = Path.Combine(hashDir, ts + ".rtpc");
            File.Copy(rtpcPath, bakPath, true);

            // Apply in-place
            int applied = RTPCApplier.Apply(rtpcPath, patches);

            // Log
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
                var nameHuman = "";
                foreach (var f in FILES) if (f.Hash == currentHash) nameHuman = f.Name;
                var entry = new Dictionary<string, object>
                {
                    { "timestamp", DateTime.Now.ToString("s") },
                    { "file_hash", currentHash },
                    { "file_name", nameHuman },
                    { "node", currentNode != null ? currentNode.Name : "" },
                    { "node_human", currentNode != null ? SemanticMap.GetInfo(currentNode.Name).Name : "" },
                    { "backup", bakPath },
                    { "changes", changeList }
                };
                logArray.Add(entry);
                File.WriteAllText(CLOG, JsonSerializer.Serialize(logArray, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }

            // Stage
            string dest = Path.Combine(STAGE, currentHash + ".rtpc");
            File.Copy(rtpcPath, dest, true);

            lblStatus.Text = "SAVED: " + applied + " change(s).";

            foreach (DataGridViewRow row in grid.Rows)
            {
                row.Cells[2].Value = row.Cells[3].Value;
                row.DefaultCellStyle.BackColor = C_PANEL;
            }

            // Build mod .zip
            try
            {
                if (!EnsureSessionMeta()) return;
                string zipPath = BuildModZip();
                if (!string.IsNullOrEmpty(zipPath))
                {
                    lblWarn.Text = "MOD BUILT: " + zipPath;
                    var r = MessageBox.Show(this,
                        "Mod built:\r\n\r\n" + zipPath + "\r\n\r\nOpen the folder?",
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
                    lblWarn.Text = "Backup saved   |   Stage: " + dest;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Mod zip build failed:\r\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblWarn.Text = "Backup saved   |   Stage: " + dest;
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
                CreatedWith = "RAGE2Toolkit v2.1.0",
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
            bool hasChanges = false;
            foreach (DataGridViewRow r in grid.Rows)
                if (Convert.ToString(r.Cells[2].Value) != Convert.ToString(r.Cells[3].Value)) { hasChanges = true; break; }
            if (hasChanges) return 3;
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
                if (bm.Restored) LoadFile();
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
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  WORKFLOW");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("  1. Extract RTPC files from the game first");
            sb.AppendLine("     (via the toolkit extractor). They land in:");
            sb.AppendLine("     D:\\RAGE2MODDING\\_outputs\\settings_extract\\");
            sb.AppendLine();
            sb.AppendLine("  2. Open this editor.");
            sb.AppendLine();
            sb.AppendLine("  3. Pick a file from the dropdown at the top.");
            sb.AppendLine();
            sb.AppendLine("  4. Browse the tree on the left.");
            sb.AppendLine("     Each [N] next to a node = number of editable");
            sb.AppendLine("     properties inside.");
            sb.AppendLine();
            sb.AppendLine("  5. Click any property and edit the \"New value\" column.");
            sb.AppendLine("     The row turns yellow to mark it as modified.");
            sb.AppendLine();
            sb.AppendLine("  6. Click SAVE. Three things happen:");
            sb.AppendLine("       - A backup is written");
            sb.AppendLine("       - The change is logged to _changes_log.json");
            sb.AppendLine("       - The modified .rtpc is copied to _stage_mods\\");
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine("===========================================================");
            sb.AppendLine("  WHERE THINGS GO");
            sb.AppendLine("===========================================================");
            sb.AppendLine();
            sb.AppendLine("D:\\RAGE2MODDING\\_outputs\\settings_editor\\");
            sb.AppendLine();
            sb.AppendLine("  _stage_mods\\<hash>.rtpc");
            sb.AppendLine("      The modified file. Drag it into the toolkit's repacker.");
            sb.AppendLine();
            sb.AppendLine("  _backups\\<hash>\\<timestamp>.rtpc");
            sb.AppendLine("      Every SAVE creates a NEW backup. Nothing is overwritten.");
            sb.AppendLine("      Manage them with the BACKUPS button in the footer:");
            sb.AppendLine("      restore an older version, delete unused ones, open folder.");
            sb.AppendLine();
            sb.AppendLine("  _changes_log.json");
            sb.AppendLine("      Full history of every change.");
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
            sb.AppendLine("  ≈   = Probable. High-confidence structural match. Meaning likely correct.");
            sb.AppendLine("  ?   = Experimental. Unknown or inferred meaning. Use with care.");
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
            sb.AppendLine("  - Backups are your safety net. Use them.");
            helpText.Text = sb.ToString();
            helpText.SelectionStart = 0;
            helpText.SelectionLength = 0;

            helpForm.ActiveControl = btnClose;
            helpForm.ShowDialog(this);
        }
    }

    // =============================================================
    // BACKUP MANAGER
    // =============================================================
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
}