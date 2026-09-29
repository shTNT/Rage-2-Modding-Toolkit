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
        RoundedButton btnExtract;

        RoundedButton btnClearSel;
        Panel overlay;
        string _prefsResolution = "1024";
        string _prefsFormat = "png";
        string _prefsAudio = "keep";



        List<TypeNode> _types = new List<TypeNode>();
        List<AssetNode> _typesV3 = new List<AssetNode>();
        Dictionary<ulong, string> _hashToSubdir = new Dictionary<ulong, string>();
        bool _useSchemaV3 = false;
int _dbgMapCount = 0;
int _dbgHits = 0;
int _dbgMiss = 0;
        List<HashEntry> _allHashes = new List<HashEntry>();
        HashSet<string> _checked = new HashSet<string>();

        bool _suppressAfterCheck;

        class HashEntry { public string Hash; public string Path; public string Basename; }
        class ResourceNode { public string Name; public int Count; public List<HashEntry> Hashes; }
        class EntityNode { public string Name; public int Total; public List<ResourceNode> Resources; }
        class TypeNode { public string Name; public int Total; public List<EntityNode> Entities; }

class AssetNode
{
    public string Name;
    public int Count;
    public Dictionary<string, AssetNode> Children;
    public List<HashEntry> Assets;
    public bool IsLeaf { get { return Children == null || Children.Count == 0; } }
}

        public SingleExtractForm(string gamePath, string outputPath)
        {
            this.gamePath = gamePath;
            this.outputPath = outputPath;

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.Shown += (s, e) => { try { this.Invalidate(true); this.Refresh(); } catch { } };

            this.Text = "Browse & Extract single assets - RAGE 2 Modding Toolkit";
            this.ClientSize = new Size(1000, 740);
            this.MinimumSize = new Size(820, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            AppInfo.ApplyTo(this);
            LoadPrefs();

            // ===== FOOTER =====
            footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = FOOTER_H + 34;
            footer.BackColor = C_HEAD;
            this.Controls.Add(footer);
            var gear = new RoundedButton();
            gear.Text = "\u2699";
            gear.Font = new Font("Segoe UI", 15, FontStyle.Bold);
            gear.ForeColor = C_INFO;
            gear.BackColor = C_BG;
            gear.BorderColor = C_INFO;
            gear.BorderThickness = 2;
            gear.CornerRadius = 20;
            gear.HoverColor = Color.FromArgb(32, 60, 60);
            gear.Size = new Size(40, 40);
            gear.TextAlign = ContentAlignment.MiddleCenter;
            gear.Cursor = Cursors.Hand;
            gear.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            gear.Location = new Point(this.ClientSize.Width - 60, 25);
            gear.Click += (s, e) => OpenConversionPrefs();
            this.Controls.Add(gear);
            gear.BringToFront();
            try {
                Action _centerSel = null;
                _centerSel = () => {
                    try {
                        if (selectionLbl == null) return;
                        int cx = this.ClientSize.Width / 2;
                        int w = selectionLbl.Width;
                        int y = selectionLbl.Top;
                        selectionLbl.Location = new Point(cx - w / 2, y);
                    } catch { }
                };
                this.Load += (s, e) => {
                    try {
                        if (selectionLbl != null) selectionLbl.Anchor = AnchorStyles.Top;
                    } catch { }
                    _centerSel();
                };
                this.Resize += (s, e) => _centerSel();
            } catch { }






            btnAdd = MakeBtn("+ Add to Selection", C_OK, Color.Black);
            btnAdd.Location = new Point(SIDE_PAD, 48);
            btnAdd.Size = new Size(170, 36);
            btnAdd.Click += (s, e) => AddToSelection();
            footer.Controls.Add(btnAdd);



            btnExtract = MakeBtn("Extract Selected", C_INFO, Color.Black);
            btnExtract.Location = new Point(SIDE_PAD + 182, 48);
            btnExtract.Size = new Size(180, 36);
            btnExtract.Click += (s, e) => ExtractSelection();
            footer.Controls.Add(btnExtract);

            btnClearSel = MakeBtn("Clear Selection", C_BTN, Color.White);
            btnClearSel.Location = new Point(SIDE_PAD + 374, 48);
            btnClearSel.Size = new Size(140, 36);
            btnClearSel.Click += (s, e) => ClearSelection();
            footer.Controls.Add(btnClearSel);

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
            searchBox.PlaceholderText = "Search: keyword / hash / .ext (e.g. .ddsc, .atx1, *.ogg)";
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
            var headerBar = new AccentBar();
            headerBar.BackColor = C_INFO;
            headerBar.Location = new Point(0, 0);
            headerBar.Size = new Size(this.ClientSize.Width, 6);
            headerBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            header.Controls.Add(headerBar);
            headerBar.BringToFront();

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
            _typesV3.Clear();
            _allHashes.Clear();
            _useSchemaV3 = false;
            try {
                string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "type_map_v3.json");
                if (!File.Exists(jsonPath)) { statusLbl.Text = "type_map_v3.json not found (reinstall from .7z)"; return; }
                string json = File.ReadAllText(jsonPath);

                using (JsonDocument doc = JsonDocument.Parse(json)) {
                    int schemaV3 = 0;
                    if (doc.RootElement.TryGetProperty("schema", out JsonElement seV3)) schemaV3 = seV3.GetInt32();
                    if (schemaV3 == 3) {
                        JsonElement treeV3;
                        if (!doc.RootElement.TryGetProperty("tree", out treeV3)) return;
                        _useSchemaV3 = true;
                        foreach (JsonProperty tProp in treeV3.EnumerateObject()) {
                            var anRoot = new AssetNode { Name = tProp.Name };
                            ParseAssetNode(tProp.Value, anRoot);
                            _typesV3.Add(anRoot);
                        }
                        _typesV3.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    } else {
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
                }

                if (!_useSchemaV3) {
                    _types.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    foreach (var tn in _types) {
                        tn.Entities.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                        foreach (var en in tn.Entities)
                            en.Resources.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                    }
                }
            } catch (Exception ex) {
                statusLbl.Text = "Error: " + ex.Message;
            }
        }

        void ParseAssetNode(JsonElement el, AssetNode node)
        {
            if (el.TryGetProperty("count", out JsonElement ce)) node.Count = ce.GetInt32();
            if (el.TryGetProperty("children", out JsonElement ch) && ch.ValueKind == JsonValueKind.Object) {
                node.Children = new Dictionary<string, AssetNode>();
                foreach (JsonProperty p in ch.EnumerateObject()) {
                    var child = new AssetNode { Name = p.Name };
                    ParseAssetNode(p.Value, child);
                    node.Children[p.Name] = child;
                }
            }
            if (el.TryGetProperty("assets", out JsonElement asEl) && asEl.ValueKind == JsonValueKind.Array) {
                node.Assets = new List<HashEntry>();
                foreach (JsonElement entry in asEl.EnumerateArray()) {
                    string h = "", bn = "", p = "";
                    JsonElement _v;
                    if (entry.TryGetProperty("h", out _v)) h = _v.GetString() ?? "";
                    if (entry.TryGetProperty("b", out _v)) bn = _v.GetString() ?? "";
                    if (entry.TryGetProperty("p", out _v)) p = _v.GetString() ?? "";
                    if (string.IsNullOrEmpty(h)) continue;
                    var he = new HashEntry { Hash = h, Path = p, Basename = bn };
                    node.Assets.Add(he);
                    _allHashes.Add(new HashEntry { Hash = h, Path = p, Basename = bn });
                }
            }
        }

        void PopulateTopLevel()
        {
            tree.BeginUpdate();
            tree.Nodes.Clear();
            if (_useSchemaV3) {
                foreach (var an in _typesV3) {
                    var n = new TreeNode(an.Name + "   (" + an.Count + ")");
                    n.Tag = an;
                    if (!an.IsLeaf || (an.Assets != null && an.Assets.Count > 0))
                        n.Nodes.Add(new TreeNode(DUMMY));
                    tree.Nodes.Add(n);
                }
            } else {
                foreach (var tn in _types) {
                    var n = new TreeNode(tn.Name + "   (" + tn.Total + ")");
                    n.Tag = tn;
                    n.Nodes.Add(new TreeNode(DUMMY));
                    tree.Nodes.Add(n);
                }
            }
            tree.EndUpdate();
        }

        void Tree_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            var node = e.Node;
            if (node == null) return;
            if (node.Nodes.Count == 1 && node.Nodes[0].Text == DUMMY) {
                node.Nodes.Clear();
                var an = node.Tag as AssetNode;
                var tn = node.Tag as TypeNode;
                var en = node.Tag as EntityNode;
                var rn = node.Tag as ResourceNode;
                if (an != null) {
                    if (an.Children != null) {
                        foreach (var kv in an.Children.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)) {
                            var child = new TreeNode(kv.Value.Name + "   (" + kv.Value.Count + ")");
                            child.Tag = kv.Value;
                            if (!kv.Value.IsLeaf || (kv.Value.Assets != null && kv.Value.Assets.Count > 0))
                                child.Nodes.Add(new TreeNode(DUMMY));
                            if (node.Checked) child.Checked = true;
                            node.Nodes.Add(child);
                        }
                    }
                    if (an.Assets != null) {
                        foreach (var h in an.Assets.OrderBy(x => string.IsNullOrEmpty(x.Basename) ? (x.Path ?? "") : x.Basename, StringComparer.OrdinalIgnoreCase)) {
                            var child = new TreeNode(LabelForHash(h));
                            child.Tag = h;
                            child.ToolTipText = (string.IsNullOrEmpty(h.Path) ? h.Hash : h.Path);
                            child.Checked = _checked.Contains(h.Hash);
                            node.Nodes.Add(child);
                        }
                    }
                } else if (tn != null) {
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
                    foreach (var h in rn.Hashes.OrderBy(x => string.IsNullOrEmpty(x.Basename) ? (x.Path ?? "") : x.Basename, StringComparer.OrdinalIgnoreCase)) {
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

            if (e.Node.Tag is AssetNode) {
                var anTag = (AssetNode)e.Node.Tag;
                if (anTag.Children != null && anTag.Children.Count > 0) {
                    Color nodeColor;
                    if (e.Node.Level == 0) nodeColor = C_INFO;
                    else if (e.Node.Level == 1) nodeColor = Color.White;
                    else nodeColor = Color.FromArgb(170, 220, 220);
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), nodeColor);
                } else {
                    var m2 = System.Text.RegularExpressions.Regex.Match(e.Node.Text, @"^([^/]+)\s{3,}\((\d+)\)$");
                    if (m2.Success) {
                        x = DrawPart(e.Graphics, m2.Groups[1].Value, font, x, y, C_GAME);
                        x = DrawPart(e.Graphics, "   (", font, x, y, C_GAME);
                        x = DrawPart(e.Graphics, m2.Groups[2].Value, font, x, y, C_COUNT);
                        x = DrawPart(e.Graphics, ")", font, x, y, C_GAME);
                    } else {
                        TextRenderer.DrawText(e.Graphics, e.Node.Text, font, new Point(x, y), C_GAME);
                    }
                }
            } else if (e.Node.Tag is TypeNode) {
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

        void PersistAssetNode(AssetNode node, bool st)
        {
            if (node == null) return;
            if (node.Assets != null) {
                foreach (var h in node.Assets) {
                    if (st) _checked.Add(h.Hash); else _checked.Remove(h.Hash);
                }
            }
            if (node.Children != null) {
                foreach (var kv in node.Children) PersistAssetNode(kv.Value, st);
            }
        }

        void PersistCheck(TreeNode n, bool st)
        {
            // NO-OP: la seleccion se anade explicitamente con [+ Add to Selection].
            // Marcar checkboxes ya no auto-suma al carro. Solo AddToSelection escribe en _checked.
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



            return menu;
        }

        // ============================================================
        // SEARCH / SELECTION
        // ============================================================
        void DoSearch()
        {
            string q = searchBox.Text.Trim();
            if (string.IsNullOrEmpty(q)) { ShowTree(); return; }
            bool isExtSearch = q.StartsWith(".") || q.StartsWith("*.");
            string extQuery = isExtSearch ? (q.StartsWith("*.") ? q.Substring(1).ToLowerInvariant() : q.ToLowerInvariant()) : null;
            var matches = _allHashes.Where(x => {
                if (isExtSearch) {
                    if (string.IsNullOrEmpty(x.Path)) return false;
                    int d = x.Path.LastIndexOf('.');
                    if (d < 0) return false;
                    return x.Path.Substring(d).ToLowerInvariant() == extQuery;
                }
                return x.Path.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || x.Hash.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
            }).ToList();

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

        void CollectAssetHashes(AssetNode node, List<string> result)
        {
            if (node == null || result == null) return;
            if (node.Assets != null) {
                foreach (var a in node.Assets) {
                    if (!string.IsNullOrEmpty(a.Hash)) result.Add(a.Hash);
                }
            }
            if (node.Children != null) {
                foreach (var kv in node.Children) CollectAssetHashes(kv.Value, result);
            }
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
                            var an = n.Tag as AssetNode;
                            var tn = n.Tag as TypeNode;
                            var en = n.Tag as EntityNode;
                            var rn = n.Tag as ResourceNode;
                            if (an != null) CollectAssetHashes(an, result);
                            else if (tn != null) foreach (var e in tn.Entities) foreach (var r in e.Resources) foreach (var h in r.Hashes) result.Add(h.Hash);
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
        }



                void LoadPrefs()
        {
            try {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "conversion_prefs.json");
                if (!File.Exists(p)) return;
                string json = File.ReadAllText(p);
                using (var doc = System.Text.Json.JsonDocument.Parse(json)) {
                    var r = doc.RootElement;
                    System.Text.Json.JsonElement v;
                    if (r.TryGetProperty("resolution", out v)) _prefsResolution = v.GetString() ?? "1024";
                    if (r.TryGetProperty("format", out v)) _prefsFormat = v.GetString() ?? "png";
                    if (r.TryGetProperty("audio", out v)) _prefsAudio = v.GetString() ?? "keep";
                }
                if (_prefsResolution != "1024" && _prefsResolution != "2048") _prefsResolution = "1024";
                if (_prefsFormat != "png" && _prefsFormat != "dds") _prefsFormat = "png";
                if (_prefsAudio != "keep" && _prefsAudio != "wav" && _prefsAudio != "ogg") _prefsAudio = "keep";
            } catch { }
        }

        void SavePrefs()
        {
            try {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "conversion_prefs.json");
                string json = "{\"resolution\":\"" + _prefsResolution + "\",\"format\":\"" + _prefsFormat + "\",\"audio\":\"" + _prefsAudio + "\"}";
                File.WriteAllText(p, json);
            } catch { }
        }

        void OpenConversionPrefs()
        {
            using (var dlg = new ConversionPrefsForm(_prefsResolution, _prefsFormat, _prefsAudio)) {
                dlg.ShowDialog(this);
                if (dlg.Saved) {
                    _prefsResolution = dlg.Resolution;
                    _prefsFormat = dlg.Format;
                    _prefsAudio = dlg.Audio;
                    SavePrefs();
                }
            }
        }

        static bool IsColorSuffix(string stemNoExt)
        {
            string s = stemNoExt;
            int us = s.LastIndexOf((char)95);
            if (us > 0) {
                string tail = s.Substring(us + 1);
                if (tail.Length == 16) {
                    bool isHex = true;
                    foreach (char c in tail) if (!Uri.IsHexDigit(c)) { isHex = false; break; }
                    if (isHex) s = s.Substring(0, us);
                }
            }
            us = s.LastIndexOf((char)95);
            if (us < 0) return false;
            string suffix = s.Substring(us).ToLowerInvariant();
            return suffix == "_dif" || suffix == "_emc" || suffix == "_albedo"
                || suffix == "_color" || suffix == "_diffuse" || suffix == "_alpha_dif";
        }

        static bool TryConvertAtx1ToEditable(string atx1Path, string outDir, string toolkitRel, string target)
        {
            try {
                byte[] data = File.ReadAllBytes(atx1Path);
                int size = data.Length;
                int dim = 0;
                bool isBC1 = false;
                int pixelsBC1 = size * 2;
                int d1 = (int)Math.Sqrt(pixelsBC1);
                if (d1 > 0 && d1 * d1 == pixelsBC1 && (d1 & (d1 - 1)) == 0) { dim = d1; isBC1 = true; }
                else {
                    int d2 = (int)Math.Sqrt(size);
                    if (d2 > 0 && d2 * d2 == size && (d2 & (d2 - 1)) == 0) { dim = d2; isBC1 = false; }
                    else return false;
                }
                string stem = Path.GetFileNameWithoutExtension(atx1Path);
                string tmpDir = Path.Combine(outDir, "__atxtmp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                try { if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir); } catch { return false; }
                string tmpDds = Path.Combine(tmpDir, stem + ".dds");
                using (var fs = new FileStream(tmpDds, FileMode.Create, FileAccess.Write)) {
                    using (var bw = new BinaryWriter(fs, System.Text.Encoding.UTF8, true)) {
                        bw.Write(new byte[] { 0x44, 0x44, 0x53, 0x20 });
                        bw.Write((uint)124);
                        bw.Write((uint)(0x1 | 0x2 | 0x4 | 0x1000 | 0x80000));
                        bw.Write((uint)dim);
                        bw.Write((uint)dim);
                        bw.Write((uint)size);
                        bw.Write((uint)0);
                        bw.Write((uint)1);
                        for (int i = 0; i < 11; i++) bw.Write((uint)0);
                        bw.Write((uint)32);
                        bw.Write((uint)0x4);
                        if (isBC1) bw.Write(new byte[] { 0x44, 0x58, 0x54, 0x31 });
                        else       bw.Write(new byte[] { 0x44, 0x58, 0x54, 0x35 });
                        bw.Write((uint)0); bw.Write((uint)0); bw.Write((uint)0); bw.Write((uint)0); bw.Write((uint)0);
                        bw.Write((uint)0x1000); bw.Write((uint)0); bw.Write((uint)0); bw.Write((uint)0); bw.Write((uint)0);
                    }
                    fs.Write(data, 0, data.Length);
                }
                if (string.Equals(target, "dds", StringComparison.OrdinalIgnoreCase)) {
                    string finalDds = Path.Combine(outDir, stem + ".dds");
                    try { if (File.Exists(finalDds)) File.Delete(finalDds); File.Move(tmpDds, finalDds); } catch { }
                    try { Directory.Delete(tmpDir); } catch { }
                    return File.Exists(finalDds);
                }
                string texconv = Path.Combine(toolkitRel, "bin", "texconv.exe");
                if (!File.Exists(texconv)) { try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | texconv NOT FOUND: " + texconv + Environment.NewLine); } catch { } try { File.Delete(tmpDds); } catch { } return false; }
                var psi = new ProcessStartInfo(texconv);
                bool _isColor = IsColorSuffix(stem);
                string _fmt = _isColor ? "R8G8B8A8_UNORM_SRGB" : "R8G8B8A8_UNORM";
                string _srgbi = _isColor ? "-srgbi " : "";
                psi.Arguments = _srgbi + "-f " + _fmt + " -ft png -y -o \"" + tmpDir + "\" \"" + tmpDds + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                var p = Process.Start(psi);
                if (p == null) { try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | Process.Start null" + Environment.NewLine); } catch { } try { File.Delete(tmpDds); } catch { } return false; }
                p.WaitForExit(30000);
                if (!p.HasExited) { try { p.Kill(); } catch { } try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | texconv timeout" + Environment.NewLine); } catch { } }
                else if (p.ExitCode != 0) { try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | texconv exit=" + p.ExitCode + Environment.NewLine); } catch { } }
                string outPng = Path.Combine(tmpDir, stem + ".png");
                string finalPng = Path.Combine(outDir, stem + ".png");
                try { if (File.Exists(outPng)) { if (File.Exists(finalPng)) File.Delete(finalPng); File.Move(outPng, finalPng); } } catch (Exception exM) { try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | Move failed: " + exM.Message + Environment.NewLine); } catch { } }
                try { File.Delete(tmpDds); } catch { }
                try { Directory.Delete(tmpDir); } catch { }
                bool _ok = File.Exists(finalPng);
                if (!_ok) { try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | finalPng missing tmpDir=" + Directory.Exists(tmpDir) + " outPng=" + File.Exists(outPng) + Environment.NewLine); } catch { } }
                return _ok;
            } catch (Exception ex) { try { File.AppendAllText(Path.Combine(outDir, "_atx1_error.txt"), atx1Path + " | outer " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine); } catch { } return false; }
        }

        void BuildHashSubdirMap()
        {
            try { _hashToSubdir.Clear(); } catch { }
            if (_typesV3 == null) return;
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "enemies","npcs","creatures","player","shared","effects",
                "cars","bikes","leaders","helicopters","boats","shared_materials","other",
                "terrain","landscape","structures","props","locations","surfaces",
                "music","vocals","fmod_banks",
                "flow","mission_logic","ai","graphs","packages","global","settings","resourcesets",
                "weapons","characters","vehicles","gameplayprops","editor_misc","localization"
            };
            foreach (var root in _typesV3) {
                if (root.Children == null) continue;
                foreach (var kv in root.Children) {
                    string childName = kv.Key;
                    var childNode = kv.Value;
                    if (skip.Contains(childName) && childNode.Children != null && childNode.Children.Count > 0) {
                        foreach (var kv2 in childNode.Children) {
                            string f2 = SanitizeFolder(kv2.Key);
                            try { WalkAssetNode(kv2.Value, f2); } catch { }
                        }
                    } else {
                        string folder = SanitizeFolder(childName);
                        try { WalkAssetNode(childNode, folder); } catch { }
                    }
                }
            }
        }

        void WalkAssetNode(AssetNode node, string folder)
        {
            if (node == null) return;
            if (node.Assets != null) {
                foreach (var a in node.Assets) {
                    if (string.IsNullOrEmpty(a.Hash)) continue;
                    ulong h;
                    if (ulong.TryParse(a.Hash, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out h)) {
                        if (!_hashToSubdir.ContainsKey(h)) _hashToSubdir[h] = folder;
                    }
                }
            }
            if (node.Children != null) {
                foreach (var kv in node.Children) {
                    WalkAssetNode(kv.Value, folder);
                }
            }
        }

        static string SanitizeFolder(string s)
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

                void ClearSelection()
        {
            _checked.Clear();
            try { UncheckRecursive(tree.Nodes); } catch { }
            UpdateSelectionLabel();
        }

        void UncheckRecursive(TreeNodeCollection nodes)
        {
            _suppressAfterCheck = true;
            try {
                foreach (TreeNode n in nodes) {
                    if (n.Checked) n.Checked = false;
                    if (n.Nodes.Count > 0 && !(n.Nodes.Count == 1 && n.Nodes[0].Text == DUMMY))
                        UncheckRecursive(n.Nodes);
                }
            } finally { _suppressAfterCheck = false; }
        }

        void UpdateSelectionLabel() { selectionLbl.Text = (_checked.Count > 0 ? "\u2726  " : "") + "Total selected Asset(s): " + _checked.Count + (_checked.Count > 0 ? "  \u2726" : ""); }

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
                DarkDialog.Info("Nothing to extract. Mark items and use [+ Add to Selection].", "Empty selection");
                return;
            }
            if (!Directory.Exists(outputPath)) {
                DarkDialog.Warn("Output folder not set.", "Missing folder");
                return;
            }

            int nTex = 0, nAud = 0, nNative = 0, nManual = 0;
            foreach (var he in _allHashes) {
                if (!_checked.Contains(he.Hash)) continue;
                string p = he.Path ?? "";
                string e = "";
                int d = p.LastIndexOf('.');
                if (d >= 0) e = p.Substring(d).ToLowerInvariant();
                if (e == ".ddsc" || e == ".avtx" || e == ".atx1" || e == ".atx2" || e == ".atx3" || e == ".atx4" || e == ".atx5" || e == ".atx6" || e == ".atx7" || e == ".atx8" || e == ".atx9" || e == ".png" || e == ".dds" || e == ".jpg" || e == ".jpeg" || e == ".bmp" || e == ".tga") nTex++;
                else if (e == ".wav" || e == ".mp3" || e == ".flac" || e == ".ogg" || e == ".riff") nAud++;
                else if (e == ".mp4" || e == ".avi" || e == ".mov" || e == ".mkv" || e == ".swf") nManual++;
                else nNative++;
            }

            int choice = 0;
            using (var dlg = new ConvertPromptForm(nTex, nAud, nNative, nManual)) {
                dlg.ShowDialog(this);
                choice = dlg.Choice;
            }
            if (choice == 0) return;
            bool doConvert = (choice == 1);

            string _origTitle = this.Text;
            btnExtract.Enabled = false;
            btnAdd.Enabled = false;
            btnClearSel.Enabled = false;
            if (tree != null) tree.Enabled = false;
            this.Text = "Toolkit working - please wait";
            this.UseWaitCursor = true;
            statusLbl.Text = "Working ...";

            var hashes = _checked.ToList();

            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                int ok = 0, fail = 0;
                string errMsg = null;
                int _atxSkipped = 0;
                int _ddscConv = 0, _atxConv = 0, _audioConv = 0;
                try {
                    string toolkitRel = Path.GetDirectoryName(Application.ExecutablePath);
                    var _before = new HashSet<string>(Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories));

                    BuildHashSubdirMap();
                    ok = ExtractorOpt.ExtractMany(gamePath, outputPath, hashes, (n, total, tabPath) =>
                    {
                        try {
                            this.BeginInvoke((Action)(() => {
                                try { statusLbl.Text = "Extracting " + n + " / " + total + " ..."; } catch { }
                            }));
                        } catch { }
                    });
                    fail = hashes.Count - ok;

                    if (doConvert) {
                        try {
                            var _after = Directory.GetFiles(outputPath, "*.*", SearchOption.AllDirectories);
                            var _newFiles = new List<string>();
                            foreach (var nf in _after) {
                                if (_before.Contains(nf)) continue;
                                _before.Add(nf);
                                _newFiles.Add(nf);
                            }
                            int _nConv = 0, _nTotal = _newFiles.Count;
                            var _po = new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 };
                            System.Threading.Tasks.Parallel.ForEach(_newFiles, _po, nf =>
                            {
                                try {
                                    string ext = Path.GetExtension(nf).ToLowerInvariant();
                                    // NOTE: conversion is unconditional per extension.
                                    // Every supported extension converts to its natural resolution.
                                    AssetConverters.ConvertResult _cr = null;
                                    if (ext == ".ddsc" || ext == ".avtx") {
                                        if (_prefsFormat == "png") _cr = AssetConverters.Convert(nf, outputPath, toolkitRel, "png", m => { });
                                        else if (_prefsFormat == "dds") _cr = AssetConverters.Convert(nf, outputPath, toolkitRel, "dds", m => { });
                                        if (_cr != null && _cr.Status == "OK") System.Threading.Interlocked.Increment(ref _ddscConv);
                                        else {
                                            string _err = (_cr != null) ? string.Join("; ", _cr.Errors) : "no result";
                                            try { System.IO.File.AppendAllText(System.IO.Path.Combine(outputPath, "_conversion_log.txt"), nf + " | FAIL " + _err + Environment.NewLine); } catch { }
                                        }
                                    }
                                    else if (ext == ".atx1") {
                                        if (TryConvertAtx1ToEditable(nf, outputPath, toolkitRel, _prefsFormat)) System.Threading.Interlocked.Increment(ref _atxConv);
                                        else {
                                            try { System.IO.File.AppendAllText(System.IO.Path.Combine(outputPath, "_conversion_log.txt"), nf + " | FAIL atx1->" + _prefsFormat + Environment.NewLine); } catch { }
                                        }
                                    }
                                    else if (ext == ".wav" || ext == ".mp3" || ext == ".flac") {
                                        if (_prefsAudio == "wav") { _cr = AssetConverters.Convert(nf, outputPath, toolkitRel, "wav", m => { }); if (_cr != null && _cr.Status == "OK") System.Threading.Interlocked.Increment(ref _audioConv); }
                                        else if (_prefsAudio == "ogg") { _cr = AssetConverters.Convert(nf, outputPath, toolkitRel, "ogg", m => { }); if (_cr != null && _cr.Status == "OK") System.Threading.Interlocked.Increment(ref _audioConv); }
                                    }
                                } catch (Exception _ex) {
                                    try { System.IO.File.AppendAllText(System.IO.Path.Combine(outputPath, "_conversion_log.txt"), nf + " | EXCEPTION " + _ex.Message + Environment.NewLine); } catch { }
                                }
                                int _nNow = System.Threading.Interlocked.Increment(ref _nConv);
                                if ((_nNow % 3) == 0) {
                                    try {
                                        this.BeginInvoke((Action)(() => {
                                            try { statusLbl.Text = "Converting " + _nNow + " / " + _nTotal + " ..."; } catch { }
                                        }));
                                    } catch { }
                                }
                            });
                        } catch { }
                    }

                    if (doConvert) {
                        try {
                            // Cleanup CONDICIONAL: borrar original solo si existe editable hermano (.png / .dds).
                            // Si la conversion fallo -> preservar original + contar como preserved.
                            int _atxPreserved = 0;
                            Action<string> _tryCleanup = (f) => {
                                string _dir = Path.GetDirectoryName(f);
                                string _stem = Path.GetFileNameWithoutExtension(f);
                                if (File.Exists(Path.Combine(_dir, _stem + ".png")) || File.Exists(Path.Combine(_dir, _stem + ".dds"))) {
                                    try { File.Delete(f); _atxSkipped++; } catch { }
                                } else {
                                    _atxPreserved++;
                                }
                            };
                            foreach (var f in Directory.GetFiles(outputPath, "*.atx*", SearchOption.AllDirectories)) {
                                string _e = Path.GetExtension(f).ToLowerInvariant();
                                if (_e.Length == 5 && _e.StartsWith(".atx") && _e[4] >= (char)49 && _e[4] <= (char)57) {
                                    _tryCleanup(f);
                                }
                            }
                            foreach (var f in Directory.GetFiles(outputPath, "*.avtx", SearchOption.AllDirectories)) {
                                _tryCleanup(f);
                            }
                            foreach (var f in Directory.GetFiles(outputPath, "*.ddsc", SearchOption.AllDirectories)) {
                                _tryCleanup(f);
                            }
                            if (_atxPreserved > 0) {
                                try { File.AppendAllText(Path.Combine(outputPath, "_conversion_log.txt"), "[cleanup] " + _atxPreserved + " originales preservados (sin editable hermano)" + Environment.NewLine); } catch { }
                            }
                            foreach (var _td in Directory.GetDirectories(outputPath, "__atxtmp*")) {
                                try { Directory.Delete(_td, true); } catch { }
                            }
                            foreach (var _td in Directory.GetDirectories(outputPath, "__tmp_rev*")) {
                                try { Directory.Delete(_td, true); } catch { }
                            }
                            string _tmpDir = Path.Combine(outputPath, "__atxtmp");
                            try { if (Directory.Exists(_tmpDir)) Directory.Delete(_tmpDir, true); } catch { }
                        } catch { }
                    }
                    try {
                    var _rootFiles = new System.Collections.Generic.List<string>();
                    foreach (var _rf in Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories)) {
                        if (_rf.Contains("__atxtmp")) continue;
                        _rootFiles.Add(_rf);
                    }
                    int _movedToFolders = 0;
                    foreach (var _f in _rootFiles) {
                        string _fn = Path.GetFileName(_f);
                        string _hashHex = null;
                        var _m = System.Text.RegularExpressions.Regex.Match(_fn, @"_([0-9A-Fa-f]{16})\.[^.]*$");
                        if (_m.Success) _hashHex = _m.Groups[1].Value;
                        else {
                            _m = System.Text.RegularExpressions.Regex.Match(_fn, @"^([0-9A-Fa-f]{16})\.[^.]*$");
                            if (_m.Success) _hashHex = _m.Groups[1].Value;
                        }
                        if (string.IsNullOrEmpty(_hashHex)) continue;
                        ulong _hh;
                        if (!ulong.TryParse(_hashHex, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _hh)) continue;
                        string _folder;
                        if (!_hashToSubdir.TryGetValue(_hh, out _folder) || string.IsNullOrEmpty(_folder)) continue;
                        string _destDir = Path.Combine(outputPath, _folder);
                        try { if (!Directory.Exists(_destDir)) Directory.CreateDirectory(_destDir); } catch { continue; }
                        string _destFile = Path.Combine(_destDir, _fn);
                        try {
                            string _srcFull = Path.GetFullPath(_f);
                            string _dstFull = Path.GetFullPath(_destFile);
                            if (string.Equals(_srcFull, _dstFull, StringComparison.OrdinalIgnoreCase)) continue;
                            if (File.Exists(_destFile)) File.Delete(_destFile);
                            File.Move(_f, _destFile);
                            _movedToFolders++;
                        } catch { }
                    }
                } catch { }
                try { ExtractorOpt.SortOutputDirectory(outputPath); } catch { }
                } catch (Exception ex) {
                    errMsg = ex.Message;
                }

                int _okF = ok, _failF = fail, _atxSkippedF = _atxSkipped;
                string _errF = errMsg;
                try {
                    this.BeginInvoke((Action)(() =>
                    {
                        this.Text = _origTitle;
                        this.UseWaitCursor = false;
                        btnExtract.Enabled = true;
                        btnAdd.Enabled = true;
                                btnClearSel.Enabled = true;
                        if (tree != null) tree.Enabled = true;

                        if (_errF != null) {
                            statusLbl.Text = "Failed: " + _errF;
                            DarkDialog.Error("Error: " + _errF, "Failed");
                        } else {
                            string _atxNote = (_atxSkippedF > 0) ? (" (" + _atxSkippedF + " intermediate files skipped)") : "";
                            int _nPngOut = 0, _nTotalOut = 0;
                            try {
                                _nTotalOut = Directory.GetFiles(outputPath, "*", SearchOption.AllDirectories).Length;
                                _nPngOut = Directory.GetFiles(outputPath, "*.png", SearchOption.AllDirectories).Length;
                            } catch { }
                            statusLbl.Text = "Done. " + _okF + " extracted (" + _failF + " fail). Files: " + _nTotalOut + " total, " + _nPngOut + " png. Textures: " + _ddscConv + " ddsc / " + _atxConv + " atx. Audio: " + _audioConv + ".";
                            string _msgExtra = (_atxSkippedF > 0) ? (Environment.NewLine + Environment.NewLine + "Note: " + _atxSkippedF + " intermediate files skipped (.atx1..9, .avtx, .ddsc). They are not editable - use Extract raw only if you need them.") : "";
                            DarkDialog.Info("Extracted " + _okF + " / " + hashes.Count + Environment.NewLine + "Output: " + outputPath + _msgExtra, "Done");
                        }
                    }));
                } catch { }
            });
        }
    }

    public class ConversionPrefsForm : Form
    {
        public string Resolution;
        public string Format;
        public string Audio;
        public bool Saved = false;
        Panel optHost;

        public ConversionPrefsForm(string resolution, string format, string audio)
        {
            Resolution = resolution; Format = format; Audio = audio;
            this.Text = "Conversion preferences";
            this.ClientSize = new Size(560, 380);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false; this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            var banner = new AccentBar();
            banner.BackColor = Color.FromArgb(0, 200, 200);
            banner.Location = new Point(0, 0);
            banner.Size = new Size(this.ClientSize.Width, 6);
            banner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(banner);

            optHost = new Panel();
            optHost.BackColor = Color.FromArgb(24, 24, 30);
            optHost.Location = new Point(20, 20);
            optHost.Size = new Size(520, 280);
            optHost.AutoScroll = true;
            this.Controls.Add(optHost);

            var btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.BackColor = Color.FromArgb(50, 50, 60);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 110);
            btnCancel.Font = new Font("Segoe UI", 10);
            btnCancel.Size = new Size(120, 40);
            btnCancel.Location = new Point(this.ClientSize.Width - 270, this.ClientSize.Height - 60);
            btnCancel.Click += (s, e) => { Saved = false; this.Close(); };
            this.Controls.Add(btnCancel);

            var btnSave = new Button();
            btnSave.Text = "Save";
            btnSave.BackColor = Color.FromArgb(0, 200, 200);
            btnSave.ForeColor = Color.Black;
            btnSave.FlatStyle = FlatStyle.Flat;
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnSave.Size = new Size(120, 40);
            btnSave.Location = new Point(this.ClientSize.Width - 140, this.ClientSize.Height - 60);
            btnSave.Click += (s, e) => { Saved = true; this.Close(); };
            this.Controls.Add(btnSave);

            Rebuild();
        }

        void Rebuild()
        {
            optHost.Controls.Clear();
            int y = 0;
            // Texture resolution preference removed in v2.0.5.1.
            // Every extension now converts to its natural resolution, independently of any setting.
            AddSection("Texture output format", ref y);
            AddRadio("PNG (editable)", ref y, Format == "png", () => { Format = "png"; Rebuild(); });
            AddRadio("DDS (editable, keeps format)", ref y, Format == "dds", () => { Format = "dds"; Rebuild(); });
            y += 14;
            AddSection("Audio output format", ref y);
            AddRadio("Keep original format", ref y, Audio == "keep", () => { Audio = "keep"; Rebuild(); });
            AddRadio("Force WAV (PCM editable)", ref y, Audio == "wav", () => { Audio = "wav"; Rebuild(); });
            AddRadio("Force OGG (Vorbis game-native)", ref y, Audio == "ogg", () => { Audio = "ogg"; Rebuild(); });
        }

        void AddSection(string text, ref int y)
        {
            var lbl = new Label();
            lbl.Text = text;
            lbl.ForeColor = Color.FromArgb(0, 200, 200);
            lbl.BackColor = Color.FromArgb(24, 24, 30);
            lbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lbl.AutoSize = true;
            lbl.Location = new Point(0, y);
            optHost.Controls.Add(lbl);
            y += 28;
        }

        void AddRadio(string text, ref int y, bool selected, Action onClick)
        {
            var lbl = new Label();
            lbl.Text = (selected ? "\u25C9  " : "\u25CB  ") + text;
            lbl.ForeColor = selected ? Color.White : Color.FromArgb(180, 180, 190);
            lbl.BackColor = Color.FromArgb(24, 24, 30);
            lbl.Font = new Font("Segoe UI", 9);
            lbl.AutoSize = true;
            lbl.Cursor = Cursors.Hand;
            lbl.Location = new Point(20, y);
            lbl.Click += (s, e) => onClick();
            optHost.Controls.Add(lbl);
            y += 26;
        }
    }

    public enum DarkKind { Info, Warn, Error }

    public static class DarkDialog
    {
        public static DialogResult Info(string text, string title) { return Show(text, title, DarkKind.Info, Color.Empty); }
        public static DialogResult Info(string text, string title, Color accent) { return Show(text, title, DarkKind.Info, accent); }
        public static DialogResult Confirm(string text, string title) { return ShowConfirm(text, title, DarkKind.Info, Color.Empty); }
        public static DialogResult Confirm(string text, string title, Color accent) { return ShowConfirm(text, title, DarkKind.Info, accent); }
        public static DialogResult Warn(string text, string title) { return Show(text, title, DarkKind.Warn, Color.Empty); }
        public static DialogResult Warn(string text, string title, Color accent) { return Show(text, title, DarkKind.Warn, accent); }
        public static DialogResult Error(string text, string title) { return Show(text, title, DarkKind.Error, Color.Empty); }
        public static DialogResult Error(string text, string title, Color accent) { return Show(text, title, DarkKind.Error, accent); }

        public static DialogResult Show(string text, string title, DarkKind kind) { return Show(text, title, kind, Color.Empty); }
        public static DialogResult Show(string text, string title, DarkKind kind, Color accent)
        {
            using (var f = new DarkMessageForm(text, title, kind, accent, false))
            {
                return f.ShowDialog();
            }
        }
        static DialogResult ShowConfirm(string text, string title, DarkKind kind, Color accent)
        {
            using (var f = new DarkMessageForm(text, title, kind, accent, true))
            {
                return f.ShowDialog();
            }
        }
    }

    public class DarkMessageForm : Form
    {
        public DarkMessageForm(string text, string title, DarkKind kind) : this(text, title, kind, Color.Empty, false) { }

        public DarkMessageForm(string text, string title, DarkKind kind, Color accentOverride, bool confirm)
        {
            this.Text = title ?? "";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            int w = 480;
            int h = 220;

            Color accent;
            string iconGlyph = "i";
            if (accentOverride.IsEmpty)
            {
                accent = Color.FromArgb(0, 200, 200);
                if (kind == DarkKind.Warn) { accent = Color.FromArgb(230, 200, 80); iconGlyph = "!"; }
                if (kind == DarkKind.Error) { accent = Color.FromArgb(220, 90, 90); iconGlyph = "x"; }
            }
            else
            {
                accent = accentOverride;
                if (kind == DarkKind.Warn) iconGlyph = "!";
                if (kind == DarkKind.Error) iconGlyph = "x";
            }

            var banner = new AccentBar();
            banner.BackColor = accent;
            banner.Location = new Point(0, 0);
            banner.Size = new Size(w, 6);
            this.Controls.Add(banner);

            var iconLbl = new Label();
            iconLbl.Text = iconGlyph;
            iconLbl.ForeColor = accent;
            iconLbl.BackColor = Color.FromArgb(24, 24, 30);
            iconLbl.Font = new Font("Segoe UI", 20, FontStyle.Bold);
            iconLbl.Location = new Point(20, 24);
            iconLbl.Size = new Size(40, 40);
            iconLbl.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(iconLbl);

            var titleLbl = new Label();
            titleLbl.Text = title ?? "";
            titleLbl.ForeColor = Color.White;
            titleLbl.BackColor = Color.FromArgb(24, 24, 30);
            titleLbl.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            titleLbl.Location = new Point(70, 26);
            titleLbl.Size = new Size(w - 90, 24);
            this.Controls.Add(titleLbl);

            var bodyLbl = new Label();
            bodyLbl.Text = text ?? "";
            bodyLbl.ForeColor = Color.FromArgb(210, 210, 210);
            bodyLbl.BackColor = Color.FromArgb(24, 24, 30);
            bodyLbl.Font = new Font("Segoe UI", 9);
            bodyLbl.Location = new Point(70, 56);
            bodyLbl.Size = new Size(w - 90, 90);
            this.Controls.Add(bodyLbl);

            if (confirm)
            {
                h = 230;
                var noBtn = new Button();
                noBtn.Text = "No";
                noBtn.BackColor = Color.FromArgb(30, 30, 38);
                noBtn.ForeColor = Color.White;
                noBtn.FlatStyle = FlatStyle.Flat;
                noBtn.FlatAppearance.BorderColor = accent;
                noBtn.FlatAppearance.BorderSize = 1;
                noBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                noBtn.Size = new Size(100, 38);
                noBtn.Location = new Point(w - 240, h - 58);
                noBtn.Cursor = Cursors.Hand;
                noBtn.Click += (s, e) => { this.DialogResult = DialogResult.No; this.Close(); };
                this.Controls.Add(noBtn);

                var yesBtn = new Button();
                yesBtn.Text = "Yes";
                yesBtn.BackColor = accent;
                yesBtn.ForeColor = Color.Black;
                yesBtn.FlatStyle = FlatStyle.Flat;
                yesBtn.FlatAppearance.BorderSize = 0;
                yesBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                yesBtn.Size = new Size(100, 38);
                yesBtn.Location = new Point(w - 130, h - 58);
                yesBtn.Cursor = Cursors.Hand;
                yesBtn.Click += (s, e) => { this.DialogResult = DialogResult.Yes; this.Close(); };
                this.Controls.Add(yesBtn);

                this.ClientSize = new Size(w, h);
                this.AcceptButton = yesBtn;
            }
            else
            {
                var okBtn = new Button();
                okBtn.Text = "OK";
                okBtn.BackColor = accent;
                okBtn.ForeColor = Color.Black;
                okBtn.FlatStyle = FlatStyle.Flat;
                okBtn.FlatAppearance.BorderSize = 0;
                okBtn.Font = new Font("Segoe UI", 10, FontStyle.Bold);
                okBtn.Size = new Size(120, 40);
                okBtn.Location = new Point(w - 140, h - 60);
                okBtn.Cursor = Cursors.Hand;
                okBtn.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
                this.Controls.Add(okBtn);

                this.ClientSize = new Size(w, h);
                this.AcceptButton = okBtn;
            }
        }
    }

    public class ConvertPromptForm : Form
    {
        public int Choice = 0;
        public ConvertPromptForm(int nTex, int nAud, int nNative, int nManual)
        {
            this.Text = "Extract selection";
            this.ClientSize = new Size(530, 320);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false; this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(24, 24, 30);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);

            var banner = new AccentBar();
            banner.BackColor = Color.FromArgb(0, 200, 200);
            banner.Location = new Point(0, 0);
            banner.Size = new Size(this.ClientSize.Width, 6);
            banner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(banner);

            var title = new Label();
            title.Text = "You are about to extract " + (nTex + nAud + nNative + nManual) + " asset(s):";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI", 11, FontStyle.Bold);
            title.Location = new Point(20, 20);
            title.AutoSize = true;
            this.Controls.Add(title);

            var body = new Label();
            string msg = "";
            if (nTex > 0) msg += "  " + (char)0x2022 + "  " + nTex + " texture(s) -> PNG editable" + (char)10;
            if (nAud > 0) msg += "  " + (char)0x2022 + "  " + nAud + " audio(s) -> WAV editable" + (char)10;
            if (nNative > 0) msg += "  " + (char)0x2022 + "  " + nNative + " native asset(s) -> copied as-is (no conversion)" + (char)10;
            if (nManual > 0) msg += "  " + (char)0x2022 + "  " + nManual + " asset(s) need external tools (skipped)" + (char)10;
            body.Text = msg.TrimEnd();
            body.ForeColor = Color.FromArgb(210, 210, 210);
            body.Location = new Point(30, 60);
            body.AutoSize = true;
            this.Controls.Add(body);

            var tip = new Label();
            tip.Text = "Convert = run toolkit converters. Raw = extract native payload as-is.";
            tip.ForeColor = Color.FromArgb(160, 160, 160);
            tip.Font = new Font("Segoe UI", 8);
            tip.Location = new Point(30, 190);
            tip.AutoSize = true;
            this.Controls.Add(tip);

            var btnConvert = new Button();
            btnConvert.Text = "Convert && Extract";
            btnConvert.BackColor = Color.FromArgb(0, 200, 200);
            btnConvert.ForeColor = Color.Black;
            btnConvert.FlatStyle = FlatStyle.Flat;
            btnConvert.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnConvert.Location = new Point(30, 245);
            btnConvert.Size = new Size(210, 44);
            btnConvert.Click += (s, e) => { Choice = 1; this.Close(); };
            this.Controls.Add(btnConvert);

            var btnRaw = new Button();
            btnRaw.Text = "Extract raw only";
            btnRaw.BackColor = Color.FromArgb(50, 50, 60);
            btnRaw.ForeColor = Color.White;
            btnRaw.FlatStyle = FlatStyle.Flat;
            btnRaw.Font = new Font("Segoe UI", 10);
            btnRaw.Location = new Point(250, 245);
            btnRaw.Size = new Size(160, 44);
            btnRaw.Click += (s, e) => { Choice = 2; this.Close(); };
            this.Controls.Add(btnRaw);

            var btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.BackColor = Color.FromArgb(50, 50, 60);
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.Font = new Font("Segoe UI", 10);
            btnCancel.Location = new Point(420, 245);
            btnCancel.Size = new Size(90, 44);
            btnCancel.Click += (s, e) => { Choice = 0; this.Close(); };
            this.Controls.Add(btnCancel);
        }
    }}