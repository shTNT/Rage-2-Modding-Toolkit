using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class SingleExtractForm : Form
    {
        readonly Color C_BG     = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL  = Color.FromArgb(30, 30, 38);
        readonly Color C_HEAD   = Color.FromArgb(15, 15, 20);
        readonly Color C_INFO   = Color.FromArgb(0, 200, 200);
        readonly Color C_GRAY   = Color.FromArgb(160, 160, 160);
        readonly Color C_BTN    = Color.FromArgb(50, 50, 60);
        readonly Color C_BORDER = Color.FromArgb(90, 90, 110);
        readonly Color C_OK     = Color.FromArgb(100, 220, 100);
        readonly Color C_UNIV   = Color.FromArgb(95, 95, 110);
        readonly Color C_GAME   = Color.FromArgb(150, 150, 165);
        readonly Color C_COUNT  = Color.White;
        readonly Color C_HASH   = Color.FromArgb(120, 120, 140);

        const int HEADER_H = 90;
        const int SEARCH_H = 50;
        const int FOOTER_H = 76;
        const int SIDE_PAD = 20;
        const int GAP = 12;
        const string DUMMY = "\u0000dummy";

        string gamePath;
        string outputPath;

        Panel header;
        Label statusLbl;
        Label selectionLbl;
        Panel searchBar;
        TextBox searchBox;
        RoundedButton btnSearch;
        RoundedButton btnBackToTree;
        TreeView tree;
        TreeView searchResults;
        Panel footer;
        RoundedButton btnAdd;
        RoundedButton btnRemove;
        RoundedButton btnExtract;
        CheckBox cbConvertToEditable;
        ComboBox cbConvertFormat;
        RoundedButton btnClearSel;
        RoundedButton btnClose;
        Panel overlay;

        List<TypeNode> _types = new List<TypeNode>();
        List<HashEntry> _allHashes = new List<HashEntry>();
        HashSet<string> _checked = new HashSet<string>();

        bool _suppressAfterCheck;

        class HashEntry { public string Hash; public string Path; public string Basename; }
        class ResourceNode { public string Name; public int Count; public List<HashEntry> Hashes; }
        class EntityNode { public string Name; public int Total; public List<ResourceNode> Resources; }
        class TypeNode { public string Name; public int Total; public List<EntityNode> Entities; }

        public SingleExtractForm(string gamePath, string outputPath)
        {
            this.gamePath = gamePath;
            this.outputPath = outputPath;

            this.Text = "Browse & Extract single assets - RAGE 2 Modding Toolkit";
            this.ClientSize = new Size(1000, 740);
            this.MinimumSize = new Size(820, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            AppInfo.ApplyTo(this);

            // ===== FOOTER =====
            footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = FOOTER_H + 34;
            footer.BackColor = C_HEAD;
            this.Controls.Add(footer);

            cbConvertToEditable = new CheckBox();
            cbConvertToEditable.Text = "Convert to editable (PNG/DDSC/OGG...)";
            cbConvertToEditable.ForeColor = Color.White;
            cbConvertToEditable.BackColor = C_HEAD;
            cbConvertToEditable.FlatStyle = FlatStyle.Flat;
            cbConvertToEditable.Font = new Font("Segoe UI", 9);
            cbConvertToEditable.Location = new Point(SIDE_PAD, 10);
            cbConvertToEditable.Size = new Size(240, 22);
            cbConvertFormat = new ComboBox();
            cbConvertFormat.DropDownStyle = ComboBoxStyle.DropDownList;
            cbConvertFormat.Items.AddRange(new object[] { "PNG", "DDS", "OGG", "WAV" });
            cbConvertFormat.SelectedIndex = 0;
            cbConvertFormat.Location = new Point(SIDE_PAD + 245, 9);
            cbConvertFormat.Size = new Size(60, 24);
            cbConvertFormat.Enabled = false;
            cbConvertToEditable.CheckedChanged += (s, e) => { cbConvertFormat.Enabled = cbConvertToEditable.Checked; };
            footer.Controls.Add(cbConvertFormat);
            footer.Controls.Add(cbConvertToEditable);


            btnAdd = MakeBtn("+ Add to Selection", C_OK, Color.Black);
            btnAdd.Location = new Point(SIDE_PAD, 48);
            btnAdd.Size = new Size(170, 36);
            btnAdd.Click += (s, e) => AddToSelection();
            footer.Controls.Add(btnAdd);

            btnRemove = MakeBtn("- Remove", C_BTN, Color.White);
            btnRemove.Location = new Point(SIDE_PAD + 182, 48);
            btnRemove.Size = new Size(120, 36);
            btnRemove.Click += (s, e) => RemoveFromSelection();
            footer.Controls.Add(btnRemove);

            btnExtract = MakeBtn("Extract Selected", C_INFO, Color.Black);
            btnExtract.Location = new Point(SIDE_PAD + 314, 48);
            btnExtract.Size = new Size(180, 36);
            btnExtract.Click += (s, e) => ExtractSelection();
            footer.Controls.Add(btnExtract);

            btnClearSel = MakeBtn("Clear Selection", C_BTN, Color.White);
            btnClearSel.Location = new Point(SIDE_PAD + 506, 48);
            btnClearSel.Size = new Size(140, 36);
            btnClearSel.Click += (s, e) => { _checked.Clear(); UpdateSelectionLabel(); SyncVisibleChecks(); };
            footer.Controls.Add(btnClearSel);

            btnClose = MakeBtn("Close", C_BTN, Color.White);
            btnClose.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnClose.Size = new Size(120, 36);
            btnClose.Location = new Point(this.ClientSize.Width - 142, 48);
            btnClose.Click += (s, e) => this.Close();
            footer.Controls.Add(btnClose);

            // ===== SEARCH =====
            searchBar = new Panel();
            searchBar.BackColor = C_BG;
            searchBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(searchBar);

            searchBox = new TextBox();
            searchBox.BackColor = C_PANEL;
            searchBox.ForeColor = Color.White;
            searchBox.Font = new Font("Segoe UI", 10);
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.Location = new Point(SIDE_PAD, 10);
            searchBox.Size = new Size(400, 28);
            searchBox.PlaceholderText = "Search by keyword (vehicle, weapon, character, hkcc...)";
            searchBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; DoSearch(); } };
            searchBar.Controls.Add(searchBox);

            btnSearch = MakeBtn("Search", C_INFO, Color.Black);
            btnSearch.Location = new Point(SIDE_PAD + 412, 10);
            btnSearch.Size = new Size(100, 28);
            btnSearch.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnSearch.Click += (s, e) => DoSearch();
            searchBar.Controls.Add(btnSearch);

            btnBackToTree = MakeBtn("Back to tree", C_BTN, Color.White);
            btnBackToTree.Location = new Point(SIDE_PAD + 524, 10);
            btnBackToTree.Size = new Size(120, 28);
            btnBackToTree.Font = new Font("Segoe UI", 9);
            btnBackToTree.Visible = false;
            btnBackToTree.Click += (s, e) => ShowTree();
            searchBar.Controls.Add(btnBackToTree);

            // ===== TREE =====
            tree = new TreeView();
            tree.BackColor = C_PANEL;
            tree.ForeColor = Color.White;
            tree.Font = new Font("Consolas", 9);
            tree.CheckBoxes = true;
            tree.HideSelection = false;
            tree.ShowNodeToolTips = true;
            tree.BorderStyle = BorderStyle.FixedSingle;
            tree.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tree.AfterCheck += Tree_AfterCheck;
            tree.BeforeExpand += Tree_BeforeExpand;
            tree.AfterSelect += (s, e) => { };
            tree.AfterSelect += (s, e) => {
                if (e.Node == null || e.Node.Tag == null) return;
                if (cbConvertFormat == null) return;
                string _selExt = "";
                var _he = e.Node.Tag as HashEntry;
                if (_he != null && _he.Path != null) {
                    int _d = _he.Path.LastIndexOf('.');
                    if (_d >= 0) _selExt = _he.Path.Substring(_d).ToLowerInvariant();
                } else if (e.Node.Text != null && e.Node.Text.Contains(".")) {
                    int _d = e.Node.Text.LastIndexOf('.');
                    if (_d >= 0) _selExt = e.Node.Text.Substring(_d).ToLowerInvariant();
                }
                if (_selExt == ".ddsc" || _selExt == ".avtx" || _selExt == ".atx1" || _selExt == ".atx2" || _selExt == ".dds" || _selExt == ".png") { cbConvertFormat.SelectedItem = "PNG"; }
                else if (_selExt == ".ogg" || _selExt == ".wav" || _selExt == ".mp3" || _selExt == ".flac" || _selExt == ".riff") { cbConvertFormat.SelectedItem = "OGG"; }
            };
            tree.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
            tree.KeyDown += Tree_KeyDown;
            tree.ContextMenuStrip = BuildContextMenu(tree);
            tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
            tree.DrawNode += Tree_DrawNode;
            tree.Visible = false;
            this.Controls.Add(tree);

            searchResults = new TreeView();
            searchResults.BackColor = C_PANEL;
            searchResults.ForeColor = Color.White;
            searchResults.Font = new Font("Consolas", 9);
            searchResults.CheckBoxes = true;
            searchResults.HideSelection = false;
            searchResults.ShowNodeToolTips = true;
            searchResults.BorderStyle = BorderStyle.FixedSingle;
            searchResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            searchResults.AfterCheck += Tree_AfterCheck;
            searchResults.NodeMouseDoubleClick += Tree_NodeMouseDoubleClick;
            searchResults.KeyDown += Tree_KeyDown;
            searchResults.ContextMenuStrip = BuildContextMenu(searchResults);
            searchResults.DrawMode = TreeViewDrawMode.OwnerDrawText;
            searchResults.DrawNode += Tree_DrawNode;
            searchResults.Visible = false;
            this.Controls.Add(searchResults);

            // ===== HEADER =====
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = HEADER_H;
            header.BackColor = C_HEAD;
            this.Controls.Add(header);

            Label title = new Label();
            title.Text = "BROWSE && EXTRACT SINGLE ASSETS";
            title.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            title.ForeColor = C_INFO;
            title.Location = new Point(20, 15);
            title.AutoSize = true;
            header.Controls.Add(title);

            statusLbl = new Label();
            statusLbl.Text = "Loading...";
            statusLbl.Font = new Font("Segoe UI", 9);
            statusLbl.ForeColor = C_GRAY;
            statusLbl.Location = new Point(22, 55);
            statusLbl.AutoSize = true;
            header.Controls.Add(statusLbl);

            selectionLbl = new Label();
            selectionLbl.Text = "Total selected Asset(s): 0";
            selectionLbl.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            selectionLbl.ForeColor = C_OK;
            selectionLbl.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            selectionLbl.TextAlign = ContentAlignment.MiddleRight;
            selectionLbl.AutoSize = false;
            selectionLbl.Size = new Size(280, 30);
            selectionLbl.Location = new Point(this.ClientSize.Width - 300, 20);
            header.Controls.Add(selectionLbl);

            this.Load += (s, e) => LayoutControls();
            this.Shown += (s, e) => BeginInvoke(new Action(RunInitialLoad));
            this.Resize += (s, e) => LayoutControls();
        }

        RoundedButton MakeBtn(string text, Color bg, Color fg)
        {
            RoundedButton b = new RoundedButton();
            b.Text = text;
            b.CornerRadius = 10;
            b.BorderColor = C_BORDER;
            b.BorderThickness = 1;
            b.BackColor = bg;
            b.ForeColor = fg;
            b.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        void LayoutControls()
        {
            int w = this.ClientSize.Width;
            searchBar.Location = new Point(0, HEADER_H);
            searchBar.Size = new Size(w, SEARCH_H);

            int contentY = HEADER_H + SEARCH_H + GAP / 2;
            int contentH = this.ClientSize.Height - HEADER_H - SEARCH_H - FOOTER_H - GAP;
            if (contentH < 100) contentH = 100;
            int contentX = SIDE_PAD;
            int contentW = w - SIDE_PAD * 2;
            if (contentW < 100) contentW = 100;

            tree.Location = new Point(contentX, contentY);
            tree.Size = new Size(contentW, contentH);
            searchResults.Location = new Point(contentX, contentY);
            searchResults.Size = new Size(contentW, contentH);
            if (overlay != null && overlay.Visible)
                overlay.Bounds = new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height);
        }

        void RunInitialLoad()
        {
            overlay = new Panel();
            overlay.BackColor = Color.FromArgb(15, 15, 20);
            overlay.Bounds = new Rectangle(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            overlay.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Panel box = new Panel();
            box.Size = new Size(540, 110);
            box.BackColor = Color.FromArgb(15, 15, 20);
            box.Anchor = AnchorStyles.None;
            int cx = this.ClientSize.Width / 2;
            int cy = this.ClientSize.Height / 2;
            box.Location = new Point(cx - 270, cy - 55);
            overlay.Controls.Add(box);

            Label l1 = new Label();
            l1.Text = "Building asset index";
            l1.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            l1.ForeColor = Color.FromArgb(0, 200, 200);
            l1.TextAlign = ContentAlignment.MiddleCenter;
            l1.Dock = DockStyle.Top;
            l1.Height = 48;
            box.Controls.Add(l1);

            Label l2 = new Label();
            l2.Text = "Parsing 46 .tab files  \u00b7  building lazy tree  \u00b7  ready to browse";
            l2.Font = new Font("Segoe UI", 9);
            l2.ForeColor = Color.FromArgb(150, 150, 165);
            l2.TextAlign = ContentAlignment.MiddleCenter;
            l2.Dock = DockStyle.Top;
            l2.Height = 30;
            l2.Top = 48;
            box.Controls.Add(l2);

            this.Controls.Add(overlay);
            overlay.BringToFront();
            try { overlay.Refresh(); } catch { }
            Application.DoEvents();

            var sw1 = Stopwatch.StartNew();
            LoadData();
            sw1.Stop();

            var sw2 = Stopwatch.StartNew();
            PopulateTopLevel();
            sw2.Stop();

            this.Controls.Remove(overlay);
            overlay.Dispose();
            overlay = null;
            tree.Visible = true;
            tree.Refresh();

            statusLbl.Text = _allHashes.Count + " hashes  \u00b7  parse " + sw1.ElapsedMilliseconds + " ms  \u00b7  tree " + sw2.ElapsedMilliseconds + " ms";
            UpdateSelectionLabel();
        }

        void LoadData()
        {
            _types.Clear();
            _allHashes.Clear();
            try {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "type_map_v2.json");
                if (!File.Exists(jsonPath)) jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "type_map.json");
                if (!File.Exists(jsonPath)) { statusLbl.Text = "type_map not found"; return; }
                string json = File.ReadAllText(jsonPath);

                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    JsonElement types;
                    if (!doc.RootElement.TryGetProperty("types", out types)) { if (!doc.RootElement.TryGetProperty("tree", out types)) return; }
                    foreach (JsonProperty tProp in types.EnumerateObject()) {
                        var tn = new TypeNode { Name = tProp.Name, Entities = new List<EntityNode>() };
                        JsonElement tObj = tProp.Value;
                        if (tObj.TryGetProperty("total", out JsonElement tel)) tn.Total = tel.GetInt32();
                        JsonElement entities;
                        if (tObj.TryGetProperty("entities", out entities)) {
                            foreach (JsonProperty eProp in entities.EnumerateObject()) {
                                var en = new EntityNode { Name = eProp.Name, Resources = new List<ResourceNode>() };
                                JsonElement eObj = eProp.Value;
                                if (eObj.TryGetProperty("total", out JsonElement eel)) en.Total = eel.GetInt32();
                                JsonElement resources;
                                if (eObj.TryGetProperty("resources", out resources)) {
                                    foreach (JsonProperty rProp in resources.EnumerateObject()) {
                                        var rn = new ResourceNode { Name = rProp.Name, Hashes = new List<HashEntry>() };
                                        int cnt = 0;
                                        foreach (JsonElement entry in rProp.Value.EnumerateArray()) {
                                            string h = ""; string bn = ""; string p = "";
                                            JsonElement _v;
                                            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("h", out _v)) {
                                                h = _v.GetString() ?? "";
                                                if (entry.TryGetProperty("b", out _v)) bn = _v.GetString() ?? "";
                                                if (entry.TryGetProperty("p", out _v)) p = _v.GetString() ?? "";
                                            } else if (entry.ValueKind == JsonValueKind.Array) {
                                                if (entry.GetArrayLength() > 0) h = entry[0].GetString() ?? "";
                                                if (entry.GetArrayLength() > 1) bn = entry[1].GetString() ?? "";
                                                if (entry.GetArrayLength() > 2) p = entry[2].GetString() ?? "";
                                            }
                                            if (string.IsNullOrEmpty(h)) continue;
                                            rn.Hashes.Add(new HashEntry { Hash = h, Path = p, Basename = bn });
                                            _allHashes.Add(new HashEntry { Hash = h, Path = p, Basename = bn });
                                            cnt++;
                                        }
                                        rn.Count = cnt;
                                        en.Resources.Add(rn);
                                    }
                                }
                                tn.Entities.Add(en);
                            }
                        }
                        _types.Add(tn);
                    }
                }

                _types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                foreach (var tn in _types) {
                    tn.Entities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    foreach (var en in tn.Entities)
                        en.Resources.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                }
            } catch (Exception ex) {
                statusLbl.Text = "Error: " + ex.Message;
            }
        }

        void PopulateTopLevel()
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            foreach (var tn in _types) {
                var n = new TreeNode(tn.Name + "   (" + tn.Total + ")");
                n.Tag = tn;
                n.Nodes.Add(new TreeNode(DUMMY));
                tree.Nodes.Add(n);
            }
            tree.EndUpdate();
        }

        void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node == null) return;
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == DUMMY) {
                node.Nodes.Clear();
                var tn = node.Tag as TypeNode;
                var en = node.Tag as EntityNode;
                var rn = node.Tag as ResourceNode;
                if (tn != null) {
                    foreach (var ent in tn.Entities) {
                        var child = new TreeNode(ent.Name + "   (" + ent.Total + ")");
                        child.Tag = ent;
                        child.Nodes.Add(new TreeNode(DUMMY));
                        if (node.Checked) child.Checked = true;
                        node.Nodes.Add(child);
                    }
                } else if (en != null) {
                    foreach (var r in en.Resources) {
                        var child = new TreeNode(r.Name + "   (" + r.Count + ")");
                        child.Tag = r;
                        child.Nodes.Add(new TreeNode(DUMMY));
                        if (node.Checked) child.Checked = true;
                        node.Nodes.Add(child);
                    }
                } else if (rn != null) {
                    foreach (var h in rn.Hashes) {
                        var child = new TreeNode(LabelForHash(h));
                        child.Tag = h;
                        child.ToolTipText = (string.IsNullOrEmpty(h.Path) ? h.Hash : h.Path);
                        child.Checked = _checked.Contains(h.Hash);
                        node.Nodes.Add(child);
                    }
                }
            }
        }

        static string LabelForHash(HashEntry h) {
            string bn = h.Path ?? "";
            int slash = bn.LastIndexOf('/');
            if (slash >= 0 && slash < bn.Length - 1) bn = bn.Substring(slash + 1);
            string shortHash = h.Hash.Length >= 8 ? h.Hash.Substring(0, 8) : h.Hash;
            return shortHash + "   " + bn;
        }

        // ============================================================
        // DRAW (multi-segmento)
        // ============================================================
        void Tree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            if (e.Node == null) { e.DrawDefault = true; return; }
            var tv = (TreeView)sender;

            using (var b = new SolidBrush(tv.BackColor))
                e.Graphics.FillRectangle(b, e.Bounds);

            bool selected = (e.State & TreeNodeStates.Selected) != 0;
            if (selected) {
                using (var b = new SolidBrush(Color.FromArgb(40, 60, 80)))
                    e.Graphics.FillRectangle(b, e.Bounds);
            }

            Font font = e.Node.NodeFont ?? tv.Font;
            int x = e.Bounds.Left;
            int y = e.Bounds.Top + 1;

            if (e.Node.Tag is TypeNode) {
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), C_INFO);
            } else if (e.Node.Tag is EntityNode) {
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), C_INFO);
            } else if (e.Node.Tag is ResourceNode) {
                var m = System.Text.RegularExpressions.Regex.Match(e.Node.Text, @"^([^/]+)/(\S+)\s{3,}\((\d+)\)$");
                if (m.Success) {
                    x = DrawPart(e.Graphics, m.Groups[1].Value + "/", font, x, y, C_UNIV);
                    x = DrawPart(e.Graphics, m.Groups[2].Value, font, x, y, C_GAME);
                    x = DrawPart(e.Graphics, "   (", font, x, y, C_GAME);
                    x = DrawPart(e.Graphics, m.Groups[3].Value, font, x, y, C_COUNT);
                    x = DrawPart(e.Graphics, ")", font, x, y, C_GAME);
                } else {
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), C_GAME);
                }
            } else if (e.Node.Tag is HashEntry) {
                var m = System.Text.RegularExpressions.Regex.Match(e.Node.Text, @"^([0-9A-F]{8})\s{3,}(.+)$");
                if (m.Success) {
                    x = DrawPart(e.Graphics, m.Groups[1].Value, font, x, y, C_HASH);
                    x = DrawPart(e.Graphics, "   ", font, x, y, C_HASH);
                    x = DrawPart(e.Graphics, m.Groups[2].Value, font, x, y, Color.White);
                } else {
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), Color.White);
                }
            } else {
                TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), Color.White);
            }
            e.DrawDefault = false;
        }

        int DrawPart(Graphics g, string s, Font f, int x, int y, Color c)
        {
            if (string.IsNullOrEmpty(s)) return x;
            TextRenderer.DrawText(g, s, f, new Point(x, y), c);
            return x + TextRenderer.MeasureText(g, s, f).Width;
        }

        // ============================================================
        // INTERACCION ESTANDAR
        // ============================================================

        // Checkbox -> propagar a hijos + persistir en modelo
        void Tree_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (_suppressAfterCheck) return;
            if (e.Action == TreeViewAction.Unknown) return;
            _suppressAfterCheck = true;
            try {
                bool st = e.Node.Checked;
                PropagateDown(e.Node, st);
                PersistCheck(e.Node, st);
            } finally { _suppressAfterCheck = false; }
            UpdateSelectionLabel();
        }

        void PropagateDown(TreeNode n, bool st)
        {
            if (n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY) return;
            foreach (TreeNode c in n.Nodes) {
                if (c.Text == DUMMY) continue;
                c.Checked = st;
                PropagateDown(c, st);
            }
        }

        void PersistCheck(TreeNode n, bool st)
        {
            if (n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY) {
                var tn = n.Tag as TypeNode;
                var en = n.Tag as EntityNode;
                var rn = n.Tag as ResourceNode;
                if (tn != null) {
                    foreach (var e in tn.Entities)
                        foreach (var r in e.Resources)
                            foreach (var h in r.Hashes)
                                if (st) _checked.Add(h.Hash); else _checked.Remove(h.Hash);
                } else if (en != null) {
                    foreach (var r in en.Resources)
                        foreach (var h in r.Hashes)
                            if (st) _checked.Add(h.Hash); else _checked.Remove(h.Hash);
                } else if (rn != null) {
                    foreach (var h in rn.Hashes)
                        if (st) _checked.Add(h.Hash); else _checked.Remove(h.Hash);
                }
                return;
            }
            var he = n.Tag as HashEntry;
            if (he != null) { if (st) _checked.Add(he.Hash); else _checked.Remove(he.Hash); }
            foreach (TreeNode c in n.Nodes) if (c.Text != DUMMY) PersistCheck(c, st);
        }

        // Doble click: padre -> expand/collapse; hoja -> toggle check
        void Tree_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            var n = e.Node;
            if (n == null) return;
            if (n.Nodes.Count > 0 && !(n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY)) {
                n.Toggle();
            } else if (n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY) {
                n.Toggle();
            } else {
                // Hoja: toggle check
                n.Checked = !n.Checked;
            }
        }

        // Ctrl+A: marcar subárbol completo desde el nodo seleccionado
        // Ctrl+D: desmarcar subárbol
        // Space: toggle del nodo seleccionado
        void Tree_KeyDown(object sender, KeyEventArgs e)
        {
            var tv = (TreeView)sender;
            var n = tv.SelectedNode;
            if (e.Control && e.KeyCode == Keys.A) {
                if (n != null) {
                    _suppressAfterCheck = true;
                    try { n.Checked = true; PropagateDown(n, true); PersistCheck(n, true); }
                    finally { _suppressAfterCheck = false; }
                    UpdateSelectionLabel();
                }
                e.SuppressKeyPress = true;
            } else if (e.Control && e.KeyCode == Keys.D) {
                if (n != null) {
                    _suppressAfterCheck = true;
                    try { n.Checked = false; PropagateDown(n, false); PersistCheck(n, false); }
                    finally { _suppressAfterCheck = false; }
                    UpdateSelectionLabel();
                }
                e.SuppressKeyPress = true;
            } else if (e.KeyCode == Keys.Space) {
                if (n != null) {
                    _suppressAfterCheck = true;
                    try { n.Checked = !n.Checked; PropagateDown(n, n.Checked); PersistCheck(n, n.Checked); }
                    finally { _suppressAfterCheck = false; }
                    UpdateSelectionLabel();
                }
                e.SuppressKeyPress = true;
            }
        }

        // Menu contextual reutilizable
        ContextMenuStrip BuildContextMenu(TreeView tv)
        {
            var menu = new ContextMenuStrip();
            menu.BackColor = C_PANEL;
            menu.ForeColor = Color.White;

            var miCheck = new ToolStripMenuItem("Check subtree (Ctrl+A)");
            miCheck.Click += (s, e) => {
                var n = tv.SelectedNode;
                if (n == null) return;
                _suppressAfterCheck = true;
                try { n.Checked = true; PropagateDown(n, true); PersistCheck(n, true); }
                finally { _suppressAfterCheck = false; }
                UpdateSelectionLabel();
            };
            menu.Items.Add(miCheck);

            var miUncheck = new ToolStripMenuItem("Uncheck subtree (Ctrl+D)");
            miUncheck.Click += (s, e) => {
                var n = tv.SelectedNode;
                if (n == null) return;
                _suppressAfterCheck = true;
                try { n.Checked = false; PropagateDown(n, false); PersistCheck(n, false); }
                finally { _suppressAfterCheck = false; }
                UpdateSelectionLabel();
            };
            menu.Items.Add(miUncheck);

            menu.Items.Add(new ToolStripSeparator());

            var miExpand = new ToolStripMenuItem("Expand");
            miExpand.Click += (s, e) => { if (tv.SelectedNode != null) tv.SelectedNode.Expand(); };
            menu.Items.Add(miExpand);

            var miCollapse = new ToolStripMenuItem("Collapse");
            miCollapse.Click += (s, e) => { if (tv.SelectedNode != null) tv.SelectedNode.Collapse(); };
            menu.Items.Add(miCollapse);

            var miExpandAll = new ToolStripMenuItem("Expand all");
            miExpandAll.Click += (s, e) => { tv.ExpandAll(); };
            menu.Items.Add(miExpandAll);

            var miCollapseAll = new ToolStripMenuItem("Collapse all");
            miCollapseAll.Click += (s, e) => { if (tv == tree) tv.CollapseAll(); };
            menu.Items.Add(miCollapseAll);

            menu.Items.Add(new ToolStripSeparator());

            var miAdd = new ToolStripMenuItem("Add checked to selection");
            miAdd.Click += (s, e) => AddToSelection();
            menu.Items.Add(miAdd);

            var miRemove = new ToolStripMenuItem("Remove checked from selection");
            miRemove.Click += (s, e) => RemoveFromSelection();
            menu.Items.Add(miRemove);

            return menu;
        }

        // ============================================================
        // SEARCH / SELECTION
        // ============================================================
        void DoSearch()
        {
            string q = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(q)) { ShowTree(); return; }
            var matches = _allHashes.Where(x => x.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || x.Hash.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            searchResults.BeginUpdate();
            searchResults.Nodes.Clear();
            foreach (var m in matches) {
                var n = new TreeNode(LabelForHash(m));
                n.Tag = m;
                n.Checked = _checked.Contains(m.Hash);
                n.ToolTipText = m.Hash + "\n" + m.Path;
                searchResults.Nodes.Add(n);
            }
            searchResults.EndUpdate();

            tree.Visible = false;
            searchResults.Visible = true;
            btnBackToTree.Visible = true;
            statusLbl.Text = "Search '" + q + "': " + matches.Count + " results";
        }

        void ShowTree()
        {
            searchResults.Visible = false;
            tree.Visible = true;
            btnBackToTree.Visible = false;
            statusLbl.Text = _allHashes.Count + " hashes indexed";
        }

        List<string> CollectCheckedFromVisibleTree()
        {
            var result = new List<string>();
            if (searchResults.Visible) {
                foreach (TreeNode n in searchResults.Nodes) if (n.Checked) result.Add(((HashEntry)n.Tag).Hash);
            } else {
                Action<TreeNode> visit = null;
                visit = (n) => {
                    var he = n.Tag as HashEntry;
                    if (he != null) { if (n.Checked) result.Add(he.Hash); return; }
                    if (n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY) {
                        if (n.Checked) {
                            var tn = n.Tag as TypeNode;
                            var en = n.Tag as EntityNode;
                            var rn = n.Tag as ResourceNode;
                            if (tn != null) foreach (var e in tn.Entities) foreach (var r in e.Resources) foreach (var h in r.Hashes) result.Add(h.Hash);
                            else if (en != null) foreach (var r in en.Resources) foreach (var h in r.Hashes) result.Add(h.Hash);
                            else if (rn != null) foreach (var h in rn.Hashes) result.Add(h.Hash);
                        }
                        return;
                    }
                    foreach (TreeNode c in n.Nodes) if (c.Text != DUMMY) visit(c);
                };
                foreach (TreeNode root in tree.Nodes) visit(root);
            }
            return result;
        }

        void AddToSelection()
        {
            var hashes = CollectCheckedFromVisibleTree();
            foreach (var h in hashes) _checked.Add(h);
            UpdateSelectionLabel();
            statusLbl.Text = "Total selected Asset(s): " + _checked.Count;
        }

        void RemoveFromSelection()
        {
            var hashes = CollectCheckedFromVisibleTree();
            int removed = 0;
            foreach (var h in hashes) if (_checked.Remove(h)) removed++;
            UpdateSelectionLabel();
            SyncVisibleChecks();
            statusLbl.Text = "Removed " + removed + ". Total selected Asset(s): " + _checked.Count;
        }

        void UpdateSelectionLabel() { selectionLbl.Text = "Total selected Asset(s): " + _checked.Count; }

        void SyncVisibleChecks()
        {
            Action<TreeNodeCollection> walk = null;
            walk = (nodes) => {
                foreach (TreeNode n in nodes) {
                    var he = n.Tag as HashEntry;
                    if (he != null) n.Checked = _checked.Contains(he.Hash);
                    if (n.Nodes.Count > 0 && !(n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY)) walk(n.Nodes);
                }
            };
            _suppressAfterCheck = true;
            try { walk(tree.Nodes); } finally { _suppressAfterCheck = false; }
        }

        void ExtractSelection()
        {
            if (_checked.Count == 0) {
                MessageBox.Show("Nothing to extract. Mark items and use [+ Add to Selection].", "Empty selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!Directory.Exists(outputPath)) {
                MessageBox.Show("Output folder not set.", "Missing folder", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            btnExtract.Enabled = false;
            int ok = 0, fail = 0;
            var hashes = _checked.ToList();
            try {
                bool doConvert = cbConvertToEditable != null && cbConvertToEditable.Checked;
                string toolkitRel = Path.GetDirectoryName(Application.ExecutablePath);
                var _before = new HashSet<string>(Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories));
                foreach (var h in hashes) {
                    statusLbl.Text = "Extracting " + (ok + fail + 1) + " / " + hashes.Count + " ...";
                    Application.DoEvents();
                    if (ExtractorOpt.ExtractSingle(gamePath, outputPath, h)) {
                        ok++;
                        if (doConvert) {
                            try {
                                var _after = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories);
                                foreach (var nf in _after) {
                                    if (_before.Contains(nf)) continue;
                                    _before.Add(nf);
                                    try {
                                        var cls = AssetConverters.Classify(nf);
                                        string _ext2 = Path.GetExtension(nf).ToLowerInvariant();
                                        string _extA = Path.GetExtension(nf).ToLowerInvariant();
                                        if ((_extA == ".mp3" || _extA == ".wav" || _extA == ".flac") && cbConvertFormat != null && cbConvertFormat.SelectedItem != null && cbConvertFormat.SelectedItem.ToString() == "OGG") {
                                            AssetConverters.Convert(nf, outputPath, toolkitRel, "ogg", m => { });
                                        } else if (cls.Status == "convert") {
                                            AssetConverters.Convert(nf, outputPath, toolkitRel, "auto", m => { });
                                        } else if (_ext2 == ".ddsc" || _ext2 == ".avtx") {
                                            string _tgt = (cbConvertFormat != null && cbConvertFormat.SelectedItem != null) ? cbConvertFormat.SelectedItem.ToString().ToLower() : "png";
                                            var _cr = AssetConverters.Convert(nf, outputPath, toolkitRel, _tgt, m => { });
                                            if (_cr.Status == "FAIL") {
                                                string _allErr = string.Join("; ", _cr.Errors);
                                                if (_allErr.IndexOf("ffmpeg", StringComparison.OrdinalIgnoreCase) >= 0) {
                                                    if (MessageBox.Show("This asset needs ffmpeg (not bundled)." + Environment.NewLine + Environment.NewLine + "Download from: https://www.gyan.dev/ffmpeg/builds/" + Environment.NewLine + "Place ffmpeg.exe in <toolkit>\\bin\\" + Environment.NewLine + Environment.NewLine + "Open download page now?", "Optional tool required", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                                                        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://www.gyan.dev/ffmpeg/builds/") { UseShellExecute = true }); } catch { }
                                                }
                                            }
                                        }
                                    } catch { }
                                }
                            } catch { }
                        }
                    } else fail++;
                }

                try { ExtractorOpt.SortOutputDirectory(outputPath); } catch { }
                statusLbl.Text = "Done. " + ok + " extracted, " + fail + " failed. Output: " + outputPath;
                MessageBox.Show("Extracted " + ok + " / " + hashes.Count + "\nOutput: " + outputPath, "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("Error: " + ex.Message, "Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            } finally { btnExtract.Enabled = true; }
        }
    }
}