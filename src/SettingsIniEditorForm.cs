using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class SettingsIniEditorForm : Form
    {
        static readonly Color BG      = Color.FromArgb(24, 24, 28);
        static readonly Color PANEL   = Color.FromArgb(32, 32, 38);
        static readonly Color FG      = Color.FromArgb(228, 228, 232);
        static readonly Color DIM     = Color.FromArgb(150, 150, 158);
        static readonly Color ACCENT = Color.FromArgb(210, 70, 190);
        static readonly Color WARN    = Color.FromArgb(240, 180, 60);
        static readonly Color EDIT_BG = Color.FromArgb(60, 50, 15);
        static readonly Color CTRL_BG = Color.FromArgb(40, 40, 48);

        static string DefaultIniPath() { return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Saved Games", "id Software", "Rage 2", "settings.ini"); }

        readonly string _iniPath;
        readonly Dictionary<string, Dictionary<string, string>> _ini
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, Dictionary<string, string>> _original
            = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        readonly List<string> _sectionOrder = new List<string>();

        TabControl _tabs;
        CheckBox _cbAdvanced;
        Label _status;
        bool _loading = false;
        readonly List<Row> _rows = new List<Row>();

        class Row
        {
            public string Section, Key, Kind, Original;
            public string[] Options;
            public bool Advanced;
            public Control Ctrl;
            public Row(string s, string k, string kind, string[] opts, bool adv, Control c, string orig)
            { Section=s; Key=k; Kind=kind; Options=opts; Advanced=adv; Ctrl=c; Original=orig; }
        }

        static readonly object[] Defs = new object[]
        {
            // ============================================================
            // GRAPHICS - VERIFIED (rango extraido estaticamente del exe)
            // ============================================================
            new object[]{"Graphics","AntiAliasing","tier4",new[]{"Off (0)","FXAA (1)","TAA (2)","TAA HQ (3)"},false,"Anti-aliasing method.","V"},
            new object[]{"Graphics","MotionBlur","float:0:8",null,false,"Camera motion blur intensity.","V"},
            new object[]{"Graphics","PostEffects","bool",null,false,"Master toggle for bloom/DOF/grain. Default fallback = 223 (bitmask).","I"},
            new object[]{"Graphics","Fullscreen","tier3",new[]{"Windowed (0)","Borderless (1)","Fullscreen (2)"},false,"Display mode.","I"},

            // ============================================================
            // GRAPHICS - INFERRED (id Tech convention + valores observados)
            // ============================================================
            new object[]{"Graphics","ShadowResolution","tier5",new[]{"Off (0)","Low (1)","Medium (2)","High (3)","Ultra (4)"},false,"Shadow map resolution.","I"},
            new object[]{"Graphics","ShadowedLights","tier5",new[]{"Off (0)","Low (1)","Medium (2)","High (3)","Ultra (4)"},false,"Lights that cast shadows.","I"},
            new object[]{"Graphics","TextureDetail","tier5",new[]{"Low (0)","Medium (1)","High (2)","Ultra (3)","Insane (4)"},false,"Texture streaming budget.","I"},
            new object[]{"Graphics","WaterDetail","tier5",new[]{"Off (0)","Low (1)","Medium (2)","High (3)","Ultra (4)"},false,"Water shader quality.","I"},
            new object[]{"Graphics","SSAOQuality","tier5",new[]{"Off (0)","Low (1)","Medium (2)","High (3)","Ultra (4)"},false,"Screen-space ambient occlusion.","I"},
            new object[]{"Graphics","LightShadingQuality","tier3",new[]{"Low (0)","Medium (1)","High (2)"},false,"Deferred light shading.","I"},
            new object[]{"Graphics","GeometryLodFactor","tier9",new[]{"Min (0)","1","2","3","4","5","6","7","Max (8)"},false,"LOD distance factor.","I"},
            new object[]{"Graphics","Aniso","tier5",new[]{"Off (0)","Low (1)","Medium (2)","High (3)","Ultra (4)"},false,"Anisotropic filtering tier.","I"},
            new object[]{"Graphics","GlobalIllumination","bool",null,false,"Real-time global illumination.","U"},
            new object[]{"Graphics","SSReflection","bool",null,false,"Screen-space reflections.","U"},
            new object[]{"Graphics","BokehDOF","bool",null,false,"Bokeh depth of field.","U"},
            new object[]{"Graphics","PlayerSelfShadow","bool",null,false,"Player casts shadows.","U"},
            new object[]{"Graphics","EdgeFade","bool",null,false,"Screen edge fade (vignette).","U"},
            new object[]{"Graphics","TerrainAnisotropic","bool",null,false,"Anisotropic on terrain.","U"},
            new object[]{"Graphics","ChromaticAberration","bool",null,true,"Chromatic aberration.","U"},
            new object[]{"Graphics","SoftParticles","bool",null,true,"Soft particle depth blending.","U"},
            new object[]{"Graphics","FidelityFXSharpeningUpsampling","bool",null,true,"AMD FidelityFX CAS sharpening.","U"},

            // ============================================================
            // GRAPHICS - FRAME SCALING (inferido)
            // ============================================================
            new object[]{"Graphics","VSync","tier3",new[]{"Off (0)","On (1)","Adaptive (2)"},true,"Vertical sync.","I"},
            new object[]{"Graphics","AsyncComputeDisable","bool",null,true,"Disable async compute (debug).","U"},
            new object[]{"Graphics","FrameScaleMode","tier3",new[]{"Off (0)","Target FPS (1)","Target Scale (2)"},true,"Dynamic resolution scaling mode.","I"},
            new object[]{"Graphics","FrameScaleMinimum","int:0:100",null,true,"Minimum dynamic resolution percent.","I"},
            new object[]{"Graphics","FrameScaleTargetFPS","int:30:240",null,true,"Target FPS for dynamic res.","I"},
            new object[]{"Graphics","MinScale","int:0:100",null,true,"Minimum render scale percent.","I"},
            new object[]{"Graphics","TargetFPS","int:0:240",null,true,"FPS cap. 0=unlimited.","I"},
            new object[]{"Graphics","Gamma","int:0:100",null,true,"Gamma correction.","I"},

            // ============================================================
            // DISPLAY
            // ============================================================
            new object[]{"Display","FullscreenWidth","int:640:7680",null,false,"Fullscreen width px.","I"},
            new object[]{"Display","FullscreenHeight","int:480:4320",null,false,"Fullscreen height px.","I"},
            new object[]{"Display","WindowedWidth","int:640:7680",null,false,"Windowed width px.","I"},
            new object[]{"Display","WindowedHeight","int:480:4320",null,false,"Windowed height px.","I"},
            new object[]{"Display","RefreshRate","int:0:360",null,false,"Monitor refresh rate Hz.","I"},

            // ============================================================
            // SOUND
            // ============================================================
            new object[]{"Sound","MasterVolume","int:0:100",null,false,"Master volume percent.","I"},
            new object[]{"Sound","GameplayVolume","int:0:100",null,false,"Gameplay volume percent.","I"},
            new object[]{"Sound","MusicVolume","int:0:100",null,false,"Music volume percent.","I"},
            new object[]{"Sound","VoiceVolume","int:0:100",null,false,"Voice volume percent.","I"},

            // ============================================================
            // CAMERA
            // ============================================================
            new object[]{"Camera","HFOV","int:60:120",null,false,"Horizontal FOV degrees.","I"},
        };

        public SettingsIniEditorForm()
        {
            _iniPath = DefaultIniPath();
            Text = "RAGE 2 - Game Settings (settings.ini)";
            ClientSize = new Size(1020, 640);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = BG;
            ForeColor = FG;
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(860, 500);

            BuildUi();
            LoadIni();
            RebuildRows();
        }

        void BuildUi()
        {
            _tabs = new TabControl();
            var header = new Panel { Dock = DockStyle.Top, Height = 96, BackColor = PANEL };

            var title = new Label {
                Text = "RAGE 2 - GAME SETTINGS",
                Font = new Font("Segoe UI Semibold", 11f),
                ForeColor = ACCENT,
                AutoSize = true,
                Location = new Point(18, 12),
            };
            header.Controls.Add(title);

            var sub = new Label {
                Text = "Edits %USERPROFILE%\\Saved Games\\id Software\\Rage 2\\settings.ini - the game reads this on next launch.",
                ForeColor = DIM,
                AutoSize = true,
                Location = new Point(18, 38),
            };
            header.Controls.Add(sub);

            var pathLbl = new Label {
                Text = _iniPath,
                ForeColor = DIM,
                AutoSize = false,
                AutoEllipsis = true,
                Location = new Point(18, 64),
                Size = new Size(760, 18),
            };
            header.Controls.Add(pathLbl);

            _cbAdvanced = new CheckBox {
                Text = "Show advanced graphics settings",
                ForeColor = ACCENT,
                AutoSize = false,
                Size = new Size(260, 24),
                Location = new Point(740, 60),
                FlatStyle = FlatStyle.Flat,
            };
            _cbAdvanced.CheckedChanged += (s, e) => ApplyAdvancedFilter();
            header.Controls.Add(_cbAdvanced);

            Controls.Add(header);

            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = new Font("Segoe UI", 9f);
            Controls.Add(_tabs);
            _tabs.BringToFront();

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = PANEL };

            _status = new Label {
                Text = "",
                ForeColor = DIM,
                AutoSize = false,
                Location = new Point(18, 20),
                Size = new Size(420, 20),
            };
            footer.Controls.Add(_status);

            var bRevert = MkBtn("REVERT", 660, 12, 90, 32, PANEL, FG);
            bRevert.Click += (s, e) => RevertAll();
            footer.Controls.Add(bRevert);

            var bSave = MkBtn("SAVE", 760, 12, 100, 32, ACCENT, Color.White);
            bSave.Click += (s, e) => SaveIni();
            footer.Controls.Add(bSave);

            var bOpen = MkBtn("OPEN FOLDER", 870, 12, 130, 32, PANEL, FG);
            bOpen.Click += (s, e) => {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = Path.GetDirectoryName(_iniPath), UseShellExecute = true }); } catch { }
            };
            footer.Controls.Add(bOpen);

            Controls.Add(footer);
            _tabs.BringToFront();
        }

        Button MkBtn(string text, int x, int y, int w, int h, Color bg, Color fg)
        {
            var b = new Button {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = fg,
                Font = new Font("Segoe UI Semibold", 9f),
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 70);
            return b;
        }

        void LoadIni()
        {
            _ini.Clear();
            _original.Clear();
            _sectionOrder.Clear();

            if (!File.Exists(_iniPath))
            {
                _ini["Graphics"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _sectionOrder.Add("Graphics");
                return;
            }

            string currentSection = "Global";
            _ini[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _sectionOrder.Add(currentSection);

            foreach (var raw in File.ReadAllLines(_iniPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(";") || line.StartsWith("#")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2);
                    if (!_ini.ContainsKey(currentSection))
                    {
                        _ini[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        _sectionOrder.Add(currentSection);
                    }
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string k = line.Substring(0, eq).Trim();
                string v = line.Substring(eq + 1).Trim();
                _ini[currentSection][k] = v;
            }

            foreach (var s in _ini)
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var kv in s.Value) d[kv.Key] = kv.Value;
                _original[s.Key] = d;
            }
        }

        void RebuildRows()
        {
            _loading = true;
            _tabs.TabPages.Clear();
            _rows.Clear();

            var bySection = new Dictionary<string, List<object[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in Defs)
            {
                var arr = (object[])d;
                string sec = (string)arr[0];
                if (!bySection.ContainsKey(sec)) bySection[sec] = new List<object[]>();
                bySection[sec].Add(arr);
            }

            foreach (var section in bySection.Keys)
            {
                var page = new TabPage(section) { BackColor = BG, ForeColor = FG };
                var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = BG };

                var table = new TableLayoutPanel {
                    Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3,
                    Padding = new Padding(20, 16, 20, 16), BackColor = BG,
                };
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                table.Controls.Add(new Label { Text = "Setting", ForeColor = DIM, Font = new Font("Segoe UI Semibold", 8.5f) }, 0, 0);
                table.Controls.Add(new Label { Text = "Value", ForeColor = DIM, Font = new Font("Segoe UI Semibold", 8.5f) }, 1, 0);
                table.Controls.Add(new Label { Text = "What it does", ForeColor = DIM, Font = new Font("Segoe UI Semibold", 8.5f) }, 2, 0);

                int row = 1;
                foreach (var def in bySection[section])
                {
                    string key = (string)def[1];
                    string kind = (string)def[2];
                    string[] opts = def[3] as string[];
                    bool adv = (bool)def[4];
                    string desc = (string)def[5];
                    string conf = def.Length > 6 ? (string)def[6] : "I";

                    // Color por confidence: V=verde, I=blanco, U=naranja
                    System.Drawing.Color nameColor;
                    if (conf == "V") nameColor = System.Drawing.Color.FromArgb(140, 220, 140);
                    else if (conf == "U") nameColor = System.Drawing.Color.FromArgb(240, 180, 90);
                    else nameColor = FG;

                    string prefix = conf == "V" ? "[V] " : conf == "U" ? "[U] " : "";
                    var lblName = new Label {
                        Text = prefix + key, ForeColor = nameColor,
                        AutoSize = false, Size = new Size(250, 22),
                        TextAlign = ContentAlignment.MiddleLeft,
                    };

                    Control ctrl = BuildControl(section, key, kind, opts);
                    ctrl.Tag = new Row(section, key, kind, opts, adv, ctrl, GetValue(section, key));
                    _rows.Add((Row)ctrl.Tag);

                    var lblDesc = new Label {
                        Text = desc, ForeColor = DIM,
                        AutoSize = false, Size = new Size(400, 22),
                        TextAlign = ContentAlignment.MiddleLeft,
                    };

                    table.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                    table.Controls.Add(lblName, 0, row);
                    table.Controls.Add(ctrl, 1, row);
                    table.Controls.Add(lblDesc, 2, row);
                    row++;
                }

                scroll.Controls.Add(table);
                page.Controls.Add(scroll);
                _tabs.TabPages.Add(page);
            }

            ApplyAdvancedFilter();
            UpdateStatus();
            _loading = false;
        }

        Control BuildControl(string section, string key, string kind, string[] opts)
        {
            string val = GetValue(section, key);

            if (kind == "bool")
            {
                var cb = new CheckBox {
                    Checked = val == "1", AutoSize = false,
                    Size = new Size(22, 22), Location = new Point(0, 1),
                    BackColor = CTRL_BG, ForeColor = FG,
                };
                cb.CheckedChanged += (s, e) => MarkDirty(cb);
                return cb;
            }

            if (kind.StartsWith("tier"))
            {
                var combo = new ComboBox {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = CTRL_BG, ForeColor = FG,
                    Size = new Size(220, 22),
                };
                combo.Items.AddRange(opts);

                int v = 0;
                int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);

                if (kind == "aniso")
                {
                    int[] map = { 0, 2, 4, 8, 16 };
                    int best = 0;
                    for (int i = 0; i < map.Length; i++) if (v >= map[i]) best = i;
                    combo.SelectedIndex = best;
                }
                else
                {
                    if (v >= 0 && v < opts.Length) combo.SelectedIndex = v;
                    else combo.SelectedIndex = 0;
                }

                combo.SelectedIndexChanged += (s, e) => MarkDirty(combo);
                return combo;
            }

            if (kind == "int" || kind == "float")
            {
                var tb = new TextBox {
                    Text = val, Size = new Size(140, 22),
                    BackColor = CTRL_BG, ForeColor = FG,
                    BorderStyle = BorderStyle.FixedSingle,
                };
                tb.TextChanged += (s, e) => MarkDirty(tb);
                return tb;
            }

            var fb = new TextBox {
                Text = val, Size = new Size(220, 22),
                BackColor = CTRL_BG, ForeColor = FG,
                BorderStyle = BorderStyle.FixedSingle,
            };
            fb.TextChanged += (s, e) => MarkDirty(fb);
            return fb;
        }

        string GetValue(string section, string key)
        {
            if (_ini.TryGetValue(section, out var s) && s.TryGetValue(key, out var v)) return v;
            return "";
        }

        void MarkDirty(Control c)
        {
            if (_loading) return;
            var r = _rows.FirstOrDefault(x => x.Ctrl == c);
            if (r == null) return;
            string cur = ReadCtrlValue(c);
            bool dirty = cur != r.Original;
            c.BackColor = dirty ? EDIT_BG : CTRL_BG;
            UpdateStatus();
        }

        string ReadCtrlValue(Control c)
        {
            if (c is CheckBox cb) return cb.Checked ? "1" : "0";
            if (c is ComboBox combo)
            {
                var r = _rows.FirstOrDefault(x => x.Ctrl == c);
                if (r == null) return "0";
                if (r.Kind == "aniso")
                {
                    int[] map = { 0, 2, 4, 8, 16 };
                    return map[Math.Max(0, Math.Min(4, combo.SelectedIndex))].ToString();
                }
                return combo.SelectedIndex.ToString();
            }
            if (c is TextBox tb) return tb.Text.Trim();
            return "";
        }

        void ApplyAdvancedFilter()
        {
            bool showAdv = _cbAdvanced.Checked;
            foreach (TabPage page in _tabs.TabPages)
            {
                foreach (Control c in page.Controls)
                {
                    if (c is Panel p)
                    {
                        foreach (Control t in p.Controls)
                        {
                            if (t is TableLayoutPanel tbl)
                            {
                                for (int row = 1; row < tbl.RowCount; row++)
                                {
                                    var ctrlCell = tbl.GetControlFromPosition(1, row);
                                    if (ctrlCell == null) continue;
                                    var rowInfo = _rows.FirstOrDefault(x => x.Ctrl == ctrlCell);
                                    if (rowInfo == null || !rowInfo.Advanced) continue;
                                    for (int col = 0; col < 3; col++)
                                    {
                                        var cell = tbl.GetControlFromPosition(col, row);
                                        if (cell != null) cell.Visible = showAdv;
                                    }
                                    tbl.RowStyles[row].Height = showAdv ? 30 : 0;
                                }
                            }
                        }
                    }
                }
            }
        }

        void UpdateStatus()
        {
            int dirty = _rows.Count(r => ReadCtrlValue(r.Ctrl) != r.Original);
            _status.Text = dirty == 0 ? "No unsaved changes." : dirty + " unsaved change(s).";
            _status.ForeColor = dirty == 0 ? DIM : WARN;
        }

        void RevertAll()
        {
            if (MessageBox.Show(this, "Discard all changes and reload from disk?", "Revert",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            LoadIni();
            RebuildRows();
        }

        void SaveIni()
        {
            // Validacion de rango VERIFICADA
            var invalid = new List<string>();
            foreach (var r in _rows)
            {
                string nv = ReadCtrlValue(r.Ctrl);
                if (nv == r.Original) continue;
                if (r.Kind.StartsWith("int:"))
                {
                    var parts = r.Kind.Split(':');
                    int min = int.Parse(parts[1]);
                    int max = int.Parse(parts[2]);
                    int val;
                    if (!int.TryParse(nv, out val))
                    {
                        invalid.Add(r.Section + "." + r.Key + " = '" + nv + "' no es un int");
                    }
                    else if (val < min || val > max)
                    {
                        invalid.Add(r.Section + "." + r.Key + " = " + val + " fuera de rango [" + min + ".." + max + "]");
                    }
                }
                else if (r.Kind.StartsWith("float:"))
                {
                    var parts = r.Kind.Split(':');
                    float min = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    float max = float.Parse(parts[2], CultureInfo.InvariantCulture);
                    float val;
                    if (!float.TryParse(nv, NumberStyles.Float, CultureInfo.InvariantCulture, out val))
                    {
                        invalid.Add(r.Section + "." + r.Key + " = '" + nv + "' no es un float");
                    }
                    else if (val < min || val > max)
                    {
                        invalid.Add(r.Section + "." + r.Key + " = " + val + " fuera de rango [" + min + ".." + max + "]");
                    }
                }
                else if (r.Kind == "bool")
                {
                    if (nv != "0" && nv != "1")
                        invalid.Add(r.Section + "." + r.Key + " = '" + nv + "' debe ser 0 o 1");
                }
            }
            if (invalid.Count > 0)
            {
                MessageBox.Show(this,
                    "Valores invalidos. Corrige antes de guardar:\n\n" + string.Join("\n", invalid),
                    "Validation error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }


            var changes = new List<Tuple<string, string, string, string>>();
            foreach (var r in _rows)
            {
                string nv = ReadCtrlValue(r.Ctrl);
                if (nv != r.Original) changes.Add(Tuple.Create(r.Section, r.Key, r.Original, nv));
            }

            if (changes.Count == 0)
            {
                MessageBox.Show(this, "No changes to save.", "Save",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Warning para cambios en settings con confianza UNKNOWN
            var unknownChanged = new System.Collections.Generic.List<string>();
            foreach (var r in _rows)
            {
                string nv = ReadCtrlValue(r.Ctrl);
                if (nv == r.Original) continue;
                foreach (var d in Defs)
                {
                    var arr = (object[])d;
                    if (arr.Length > 6 && (string)arr[0] == r.Section && (string)arr[1] == r.Key)
                    {
                        if ((string)arr[6] == "U") unknownChanged.Add(r.Section + "." + r.Key);
                        break;
                    }
                }
            }
            if (unknownChanged.Count > 0)
            {
                var warnMsg = "WARNING: " + unknownChanged.Count + " change(s) in UNKNOWN-range settings:\n\n"
                    + string.Join("\n", unknownChanged)
                    + "\n\nRange for these settings was NOT verified statically.\n"
                    + "Values may be ignored or clamped by the game.\n\n"
                    + "Backup will be created automatically. Continue?";
                if (MessageBox.Show(this, warnMsg, "Unverified settings",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(changes.Count + " change(s) to apply:");
            foreach (var c in changes)
                sb.AppendLine("  [" + c.Item1 + "] " + c.Item2 + ": " + c.Item3 + " -> " + c.Item4);
            sb.AppendLine();
            sb.AppendLine("Backup will be created automatically.");
            sb.AppendLine("Launch RAGE 2 to apply.");

            if (MessageBox.Show(this, sb.ToString(), "Confirm save",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;

            try
            {
                if (File.Exists(_iniPath))
                {
                    string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    File.Copy(_iniPath, _iniPath + ".bak_" + ts, true);
                }

                foreach (var c in changes)
                {
                    if (!_ini.ContainsKey(c.Item1))
                        _ini[c.Item1] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _ini[c.Item1][c.Item2] = c.Item4;
                }

                var lines = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var sec in _sectionOrder)
                {
                    if (!_ini.ContainsKey(sec) || sec == "Global") continue;
                    lines.Add("[" + sec + "]");
                    foreach (var kv in _ini[sec]) lines.Add(kv.Key + "=" + kv.Value);
                    lines.Add("");
                    seen.Add(sec);
                }
                foreach (var sec in _ini.Keys)
                {
                    if (seen.Contains(sec) || sec == "Global") continue;
                    lines.Add("[" + sec + "]");
                    foreach (var kv in _ini[sec]) lines.Add(kv.Key + "=" + kv.Value);
                    lines.Add("");
                }

                File.WriteAllLines(_iniPath, lines, new UTF8Encoding(false));
                MessageBox.Show(this, "Saved " + changes.Count + " change(s).\nBackup created.\n\nLaunch RAGE 2 to apply.",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadIni();
                RebuildRows();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Save failed:\n\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}