using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class ModManagerForm : Form
    {
        // === WM_DROPFILES fallback (drag&drop nativo Windows) ===
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern void DragAcceptFiles(IntPtr hWnd, bool fAccept);

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern uint DragQueryFileW(IntPtr hDrop, uint iFile, System.Text.StringBuilder lpszFile, uint cch);

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        static extern void DragFinish(IntPtr hDrop);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                DragAcceptFiles(this.Handle, true);
                if (emptyState != null) DragAcceptFiles(emptyState.Handle, true);
                if (headerBar != null) DragAcceptFiles(headerBar.Handle, true);
                if (lvMods != null) DragAcceptFiles(lvMods.Handle, true);
            }
            catch { }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_DROPFILES = 0x0233;
            if (m.Msg == WM_DROPFILES)
            {
                IntPtr hDrop = m.WParam;
                var paths = new System.Collections.Generic.List<string>();
                try
                {
                    uint count = DragQueryFileW(hDrop, 0xFFFFFFFF, null, 0);
                    for (uint i = 0; i < count; i++)
                    {
                        var sb = new System.Text.StringBuilder(512);
                        DragQueryFileW(hDrop, i, sb, 512);
                        paths.Add(sb.ToString());
                    }
                }
                catch { }
                try { DragFinish(hDrop); } catch { }
                if (paths.Count == 1 && paths[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    InstallFromZip(paths[0], 100, false);
                }
                return;
            }
            base.WndProc(ref m);
        }

        readonly Color C_BG      = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL   = Color.FromArgb(30, 30, 38);
        readonly Color C_HEAD    = Color.FromArgb(15, 15, 20);
        readonly Color C_MAGENTA = Color.FromArgb(220, 80, 220);
        readonly Color C_GRAY    = Color.FromArgb(160, 160, 160);
        readonly Color C_BTN     = Color.FromArgb(50, 50, 60);
        readonly Color C_BORDER  = Color.FromArgb(90, 90, 110);
        readonly Color C_OK      = Color.FromArgb(100, 220, 100);
        readonly Color C_WARN    = Color.FromArgb(230, 180, 74);
        readonly Color C_ERR     = Color.FromArgb(240, 100, 100);
        readonly Color C_TEXT    = Color.FromArgb(200, 200, 200);

        const int SIDE_PAD = 20;

        string gamePath;
        string toolkitRoot;

        Panel headerBar;
        Panel header;
        Label titleLbl;
        Label subLbl;
        Panel actionsBar;
        RoundedButton btnInstall;
        RoundedButton btnUninstall;
        RoundedButton btnRestoreAll;
        RoundedButton btnRefresh;
        RoundedButton btnPrioUp;
        RoundedButton btnPrioDown;
        TextBox searchBox;
        ListView lvMods;
        Label emptyState;
        Panel detailsPanel;
        Label detailsTitle;
        Label detailsArcs;
        Label detailsHashes;
        Label detailsLibrary;
        Panel conflictsPanel;
        Label conflictsTitle;
        ListView lvConflicts;
        Label lblNoConflicts;
        Panel footer;
        RoundedButton btnClose;
        Label statusLbl;

        public ModManagerForm() : this(null) { }

        public ModManagerForm(string gamePathIn)
        {
            gamePath = gamePathIn;
            toolkitRoot = AppContext.BaseDirectory;

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            this.Shown += (s, e) => { try { this.Invalidate(true); this.Refresh(); } catch { } };

            this.Text = "Mod Manager - RAGE 2 Modding Toolkit " + AppInfo.Display;
            this.ClientSize = new Size(1000, 740);
            this.MinimumSize = new Size(820, 550);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.AutoScaleMode = AutoScaleMode.Dpi;
            AppInfo.ApplyTo(this);

            this.AllowDrop = true;
            this.DragEnter += Form_DragEnter;
            this.DragDrop += Form_DragDrop;

            ResolveGamePath();
            BuildUI();
            RefreshMods();
        }

        void ResolveGamePath()
        {
            if (!string.IsNullOrEmpty(gamePath) && File.Exists(Path.Combine(gamePath, "RAGE2.exe"))) return;
            var guess = @"D:\Games\RAGE 2";
            if (File.Exists(Path.Combine(guess, "RAGE2.exe"))) { gamePath = guess; return; }
            using (var fb = new FolderBrowserDialog())
            {
                fb.Description = "Select RAGE 2 game folder (containing RAGE2.exe)";
                if (fb.ShowDialog(this) == DialogResult.OK) gamePath = fb.SelectedPath;
            }
        }

        RoundedButton MakeBtn(string text, Color color)
        {
            var b = new RoundedButton();
            b.Text = text;
            b.Size = new Size(160, 34);
            b.CornerRadius = 10;
            b.BorderColor = C_BORDER;
            b.BorderThickness = 1;
            b.BackColor = color;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 10);
            b.Cursor = Cursors.Hand;
            return b;
        }

        void BuildUI()
        {
            // Order matters: for Dock.Bottom, LAST-added = bottommost. For Dock.Top, LAST-added = topmost.

            // 1. FOOTER
            footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 60;
            footer.BackColor = C_HEAD;
            this.Controls.Add(footer);


            statusLbl = new Label();
            statusLbl.AutoSize = true;
            statusLbl.Location = new Point(SIDE_PAD, 20);
            statusLbl.Font = new Font("Segoe UI", 9);
            statusLbl.ForeColor = C_GRAY;
            statusLbl.Text = "Ready";
            footer.Controls.Add(statusLbl);

            // 2. CONFLICTS
            conflictsPanel = new Panel();
            conflictsPanel.Dock = DockStyle.Bottom;
            conflictsPanel.Height = 90;
            conflictsPanel.BackColor = C_HEAD;
            this.Controls.Add(conflictsPanel);

            conflictsTitle = new Label();
            conflictsTitle.Text = "CONFLICTS";
            conflictsTitle.Font = new Font("Segoe UI", 8, FontStyle.Bold);
            conflictsTitle.ForeColor = C_MAGENTA;
            conflictsTitle.Location = new Point(SIDE_PAD, 6);
            conflictsTitle.AutoSize = true;
            conflictsPanel.Controls.Add(conflictsTitle);

            lblNoConflicts = new Label();
            lblNoConflicts.Text = "\u2713  No conflicts detected between installed mods.";
            lblNoConflicts.Font = new Font("Segoe UI", 9);
            lblNoConflicts.ForeColor = C_OK;
            lblNoConflicts.Location = new Point(SIDE_PAD, 30);
            lblNoConflicts.AutoSize = true;
            conflictsPanel.Controls.Add(lblNoConflicts);

            lvConflicts = new ListView();
            lvConflicts.View = View.Details;
            lvConflicts.FullRowSelect = true;
            lvConflicts.MultiSelect = false;
            lvConflicts.BackColor = C_PANEL;
            lvConflicts.ForeColor = C_TEXT;
            lvConflicts.Font = new Font("Consolas", 9);
            lvConflicts.BorderStyle = BorderStyle.None;
            lvConflicts.Location = new Point(SIDE_PAD, 28);
            lvConflicts.Size = new Size(this.ClientSize.Width - SIDE_PAD * 2, 50);
            lvConflicts.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            lvConflicts.Columns.Add("Hash", 220);
            lvConflicts.Columns.Add("Mod A", 260);
            lvConflicts.Columns.Add("Mod B", 260);
            lvConflicts.Visible = false;
            conflictsPanel.Controls.Add(lvConflicts);

            // 3. DETAILS
            detailsPanel = new Panel();
            detailsPanel.Dock = DockStyle.Bottom;
            detailsPanel.Height = 110;
            detailsPanel.BackColor = C_BG;
            this.Controls.Add(detailsPanel);

            detailsTitle = new Label();
            detailsTitle.Text = "No mod selected";
            detailsTitle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            detailsTitle.ForeColor = C_MAGENTA;
            detailsTitle.Location = new Point(SIDE_PAD, 6);
            detailsTitle.AutoSize = true;
            detailsPanel.Controls.Add(detailsTitle);

            detailsArcs = new Label();
            detailsArcs.Font = new Font("Consolas", 9);
            detailsArcs.ForeColor = C_TEXT;
            detailsArcs.Location = new Point(SIDE_PAD, 30);
            detailsArcs.AutoSize = true;
            detailsPanel.Controls.Add(detailsArcs);

            detailsHashes = new Label();
            detailsHashes.Font = new Font("Consolas", 9);
            detailsHashes.ForeColor = C_TEXT;
            detailsHashes.Location = new Point(SIDE_PAD, 50);
            detailsHashes.AutoSize = true;
            detailsPanel.Controls.Add(detailsHashes);

            detailsLibrary = new Label();
            detailsLibrary.Font = new Font("Consolas", 9);
            detailsLibrary.ForeColor = C_GRAY;
            detailsLibrary.Location = new Point(SIDE_PAD, 70);
            detailsLibrary.AutoSize = true;
            detailsPanel.Controls.Add(detailsLibrary);

            // 4. LIST (Fill = middle)
            lvMods = new ListView();
            lvMods.Dock = DockStyle.Fill;
            lvMods.View = View.Details;
            lvMods.FullRowSelect = true;
            lvMods.MultiSelect = false;
            lvMods.BackColor = C_PANEL;
            lvMods.ForeColor = C_TEXT;
            lvMods.Font = new Font("Consolas", 10);
            lvMods.BorderStyle = BorderStyle.None;
            lvMods.Columns.Add("#", 40);
            lvMods.Columns.Add("Mod ID", 180);
            lvMods.Columns.Add("Name", 200);
            lvMods.Columns.Add("Version", 70);
            lvMods.Columns.Add("Priority", 60);
            lvMods.Columns.Add("Arcs", 50);
            lvMods.Columns.Add("Hashes", 60);
            lvMods.SelectedIndexChanged += (s, e) => UpdateDetails();
            lvMods.DoubleClick += (s, e) => OpenSelectedLibrary();
            var cm = new ContextMenuStrip();
            cm.Items.Add("Uninstall", null, (s, e) => UninstallSelected());
            cm.Items.Add("Open library folder", null, (s, e) => OpenSelectedLibraryFolder());
            cm.Items.Add("Open backups folder", null, (s, e) => OpenBackupsFolder());
            cm.Items.Add(new ToolStripSeparator());
            cm.Items.Add("Copy hashes", null, (s, e) => CopySelectedHashes());
            lvMods.ContextMenuStrip = cm;
            this.Controls.Add(lvMods);

            emptyState = new Label();
            emptyState.Text = "No mods installed.\r\n\r\nDrop a .zip anywhere in this window to install.";
            emptyState.Font = new Font("Segoe UI", 12);
            emptyState.ForeColor = C_GRAY;
            emptyState.TextAlign = ContentAlignment.MiddleCenter;
            emptyState.Dock = DockStyle.Fill;
            emptyState.BackColor = C_PANEL;
            lvMods.Controls.Add(emptyState);
            emptyState.BringToFront();

            // 5. ACTIONS
            actionsBar = new Panel();
            actionsBar.Dock = DockStyle.Top;
            actionsBar.Height = 60;
            actionsBar.BackColor = C_BG;
            this.Controls.Add(actionsBar);

            btnInstall = MakeBtn("+ Install .zip", C_MAGENTA);
            btnInstall.Location = new Point(SIDE_PAD, 12);
            btnInstall.Size = new Size(150, 36);
            btnInstall.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnInstall.Click += (s, e) => InstallFromDialog();
            actionsBar.Controls.Add(btnInstall);

            btnUninstall = MakeBtn("Uninstall", C_BTN);
            btnUninstall.Location = new Point(SIDE_PAD + 162, 12);
            btnUninstall.Size = new Size(120, 36);
            btnUninstall.Click += (s, e) => UninstallSelected();
            actionsBar.Controls.Add(btnUninstall);

            btnRestoreAll = MakeBtn("Restore All", C_BTN);
            btnRestoreAll.Location = new Point(SIDE_PAD + 294, 12);
            btnRestoreAll.Size = new Size(130, 36);
            btnRestoreAll.Click += (s, e) => RestoreAll();
            actionsBar.Controls.Add(btnRestoreAll);

            btnPrioUp = MakeBtn("\u25B2", C_BTN);
            btnPrioUp.Location = new Point(SIDE_PAD + 436, 12);
            btnPrioUp.Size = new Size(40, 36);
            btnPrioUp.Click += (s, e) => AdjustPriority(+1);
            actionsBar.Controls.Add(btnPrioUp);

            btnPrioDown = MakeBtn("\u25BC", C_BTN);
            btnPrioDown.Location = new Point(SIDE_PAD + 482, 12);
            btnPrioDown.Size = new Size(40, 36);
            btnPrioDown.Click += (s, e) => AdjustPriority(-1);
            actionsBar.Controls.Add(btnPrioDown);

            btnRefresh = MakeBtn("Refresh", C_BTN);
            btnRefresh.Location = new Point(SIDE_PAD + 534, 12);
            btnRefresh.Size = new Size(100, 36);
            btnRefresh.Click += (s, e) => RefreshMods();
            actionsBar.Controls.Add(btnRefresh);

            searchBox = new TextBox();
            searchBox.BackColor = C_PANEL;
            searchBox.ForeColor = Color.White;
            searchBox.Font = new Font("Segoe UI", 10);
            searchBox.BorderStyle = BorderStyle.FixedSingle;
            searchBox.Size = new Size(260, 28);
            searchBox.Location = new Point(this.ClientSize.Width - 280, 16);
            searchBox.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            searchBox.PlaceholderText = "Filter by ID / name...";
            searchBox.TextChanged += (s, e) => RefreshMods();
            actionsBar.Controls.Add(searchBox);

            // 6. HEADER
            header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 90;
            header.BackColor = C_HEAD;
            this.Controls.Add(header);

            titleLbl = new Label();
            titleLbl.Text = "MOD MANAGER";
            titleLbl.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            titleLbl.ForeColor = C_MAGENTA;
            titleLbl.Location = new Point(SIDE_PAD, 18);
            titleLbl.AutoSize = true;
            header.Controls.Add(titleLbl);

            subLbl = new Label();
            subLbl.Text = "Install / Uninstall mods, manage conflicts and backups";
            subLbl.Font = new Font("Segoe UI", 10);
            subLbl.ForeColor = C_GRAY;
            subLbl.Location = new Point(SIDE_PAD + 2, 58);
            subLbl.AutoSize = true;
            header.Controls.Add(subLbl);

            // 7. HEADER BAR (very top)
            headerBar = new AccentBar();
            headerBar.BackColor = C_MAGENTA;
            headerBar.Location = new Point(0, 0);
            headerBar.Size = new Size(this.ClientSize.Width, 6);
            headerBar.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(headerBar);
            headerBar.BringToFront();

            var btnHelp = new RoundedButton();
            btnHelp.Text = "?";
            btnHelp.CornerRadius = 20;
            btnHelp.BorderColor = C_MAGENTA;
            btnHelp.BorderThickness = 2;
            btnHelp.HoverColor = Color.FromArgb(60, 32, 60);
            btnHelp.BackColor = C_HEAD;
            btnHelp.ForeColor = C_MAGENTA;
            btnHelp.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            btnHelp.Size = new Size(36, 36);
            btnHelp.Location = new Point(this.ClientSize.Width - 56, 12);
            btnHelp.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnHelp.Cursor = Cursors.Hand;
            btnHelp.Click += (s, e) => { new ModManagerHelpForm().ShowDialog(this); };
            this.Controls.Add(btnHelp);
            btnHelp.BringToFront();
        }

        void Form_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length == 1 && files[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                { e.Effect = DragDropEffects.Copy; return; }
            }
            e.Effect = DragDropEffects.None;
        }

        void Form_DragDrop(object sender, DragEventArgs e)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1 && files[0].EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                InstallFromZip(files[0], 100, false);
        }

        string SelectedId()
        {
            if (lvMods.SelectedItems.Count == 0) return null;
            return lvMods.SelectedItems[0].SubItems[1].Text;
        }

        void AdjustPriority(int delta)
        {
            var id = SelectedId();
            if (id == null) { SetStatus("Select a mod first."); return; }
            try
            {
                var store = ModsStore.Load(toolkitRoot);
                InstalledMod sel = null;
                foreach (var m in store.Installed) if (m.Id == id) { sel = m; break; }
                if (sel == null) { SetStatus("Mod not in registry."); return; }
                int newPrio = sel.Priority + delta;
                if (newPrio < 0) newPrio = 0;
                ModsStore.UpdatePriority(toolkitRoot, id, newPrio);
                SetStatus("Priority of " + id + " -> " + newPrio + " (rebuild happens on next install/uninstall)");
                RefreshMods();
            }
            catch (Exception ex) { SetStatus("AdjustPriority error: " + ex.Message); }
        }

        void OpenSelectedLibrary()
        {
            var id = SelectedId();
            if (id == null) return;
            try
            {
                var store = ModsStore.Load(toolkitRoot);
                InstalledMod sel = null;
                foreach (var m in store.Installed) if (m.Id == id) { sel = m; break; }
                if (sel == null || string.IsNullOrEmpty(sel.LibraryFile) || !File.Exists(sel.LibraryFile))
                { DarkDialog.Info("Library zip not found.", "Mod Manager", Color.FromArgb(220, 80, 220)); return; }
                System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + sel.LibraryFile + "\"");
            }
            catch (Exception ex) { DarkDialog.Error("Exception: " + ex.Message, "Mod Manager", Color.FromArgb(220, 80, 220)); }
        }

        void OpenSelectedLibraryFolder()
        {
            try { var d = ModsStore.LibraryDir(toolkitRoot); if (Directory.Exists(d)) System.Diagnostics.Process.Start("explorer.exe", d); } catch { }
        }

        void OpenBackupsFolder()
        {
            try { var d = ModsStore.BackupsDir(toolkitRoot); if (Directory.Exists(d)) System.Diagnostics.Process.Start("explorer.exe", d); } catch { }
        }

        void CopySelectedHashes()
        {
            var id = SelectedId();
            if (id == null) return;
            try
            {
                var store = ModsStore.Load(toolkitRoot);
                InstalledMod sel = null;
                foreach (var m in store.Installed) if (m.Id == id) { sel = m; break; }
                if (sel == null || sel.Hashes == null) return;
                Clipboard.SetText(string.Join(Environment.NewLine, sel.Hashes.ToArray()));
                SetStatus("Hashes copied to clipboard (" + sel.Hashes.Count + ")");
            }
            catch { }
        }

        void InstallFromDialog()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Mod packages (*.zip)|*.zip";
                ofd.Title = "Select mod .zip to install";
                if (ofd.ShowDialog(this) != DialogResult.OK) return;
                InstallFromZip(ofd.FileName, 100, false);
            }
        }

        void InstallFromZip(string zipPath, int priority, bool force)
        {
            if (string.IsNullOrEmpty(gamePath))
            {
                DarkDialog.Warn("Game path not set.", "Mod Manager", Color.FromArgb(220, 80, 220));
                return;
            }
            Cursor = Cursors.WaitCursor;
            SetStatus("Installing " + Path.GetFileName(zipPath) + "...");
            try
            {
                Action<string> log = delegate(string s) { SetStatus(s); };
                var res = ModInstaller.Install(zipPath, gamePath, toolkitRoot, log, force, priority);
                Cursor = Cursors.Default;
                if (res.Success)
                {
                    DarkDialog.Info("Install OK" + Environment.NewLine + Environment.NewLine + res.AssetsInstalled + " assets in " + res.ArchivesModified + " archives." + Environment.NewLine + "Backups: " + res.BackupsCreated.Count, "Mod Manager", Color.FromArgb(220, 80, 220));
                }
                else
                {
                    var errMsg = res.Error ?? "(no error)";
                    var dr = DarkDialog.Confirm("Install FAILED" + Environment.NewLine + Environment.NewLine + errMsg + Environment.NewLine + Environment.NewLine + "Force install anyway?", "Mod Manager", Color.FromArgb(220, 80, 220));
                    if (dr == DialogResult.Yes)
                    {
                        var res2 = ModInstaller.Install(zipPath, gamePath, toolkitRoot, log, true, priority);
                        if (res2.Success) DarkDialog.Info("Install OK (forced).", "Mod Manager", Color.FromArgb(220, 80, 220));
                        else DarkDialog.Error("Force install failed:" + Environment.NewLine + (res2.Error ?? "?"), "Mod Manager", Color.FromArgb(220, 80, 220));
                    }
                }
                RefreshMods();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                DarkDialog.Error("Exception: " + ex.Message, "Mod Manager", Color.FromArgb(220, 80, 220));
            }
        }

        void UninstallSelected()
        {
            var id = SelectedId();
            if (id == null)
            {
                DarkDialog.Info("Select a mod first.", "Mod Manager", Color.FromArgb(220, 80, 220));
                return;
            }
            string arcsInfo = "";
            try
            {
                var store = ModsStore.Load(toolkitRoot);
                InstalledMod sel = null;
                foreach (var m in store.Installed) if (m.Id == id) { sel = m; break; }
                if (sel != null)
                {
                    if (sel.AffectedArcs != null && sel.AffectedArcs.Count > 0)
                        arcsInfo = "Arcs affected: " + sel.AffectedArcs.Count + Environment.NewLine;
                    if (sel.Hashes != null)
                        arcsInfo += "Hashes: " + sel.Hashes.Count + Environment.NewLine;
                }
            }
            catch { }
            var msg = "Uninstall mod " + id + " ?" + Environment.NewLine + Environment.NewLine + arcsInfo + Environment.NewLine + "Restores the affected .arc files (backup restore or rebuild with remaining mods).";
            var dr = DarkDialog.Confirm(msg, "Mod Manager", Color.FromArgb(220, 80, 220));
            if (dr != DialogResult.Yes) return;

            Cursor = Cursors.WaitCursor;
            SetStatus("Uninstalling " + id + "...");
            try
            {
                Action<string> log = delegate(string s) { SetStatus(s); };
                var res = UninstallEngine.Uninstall(id, gamePath, toolkitRoot, log);
                Cursor = Cursors.Default;
                if (res.Success)
                {
                    DarkDialog.Info("Uninstall OK" + Environment.NewLine + Environment.NewLine + "Restored: " + res.RestoredArcs.Count + Environment.NewLine + "Rebuilt: " + res.RebuiltArcs.Count, "Mod Manager", Color.FromArgb(220, 80, 220));
                }
                else
                {
                    DarkDialog.Error("Uninstall FAILED" + Environment.NewLine + Environment.NewLine + (res.Error ?? "(no error)"), "Mod Manager", Color.FromArgb(220, 80, 220));
                }
                RefreshMods();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                DarkDialog.Error("Exception: " + ex.Message, "Mod Manager", Color.FromArgb(220, 80, 220));
            }
        }

        void RestoreAll()
        {
            var dr = DarkDialog.Confirm("Uninstall ALL mods and restore backups?", "Mod Manager", Color.FromArgb(220, 80, 220));
            if (dr != DialogResult.Yes) return;
            try
            {
                var store = ModsStore.Load(toolkitRoot);
                var ids = new List<string>();
                foreach (var m in store.Installed) ids.Add(m.Id);
                int ok = 0;
                foreach (var id in ids)
                {
                    SetStatus("Uninstalling " + id + "...");
                    Action<string> log = delegate(string s) { };
                    try { var res = UninstallEngine.Uninstall(id, gamePath, toolkitRoot, log); if (res.Success) ok++; } catch { }
                }
                DarkDialog.Info("Restored " + ok + " mod(s).", "Mod Manager", Color.FromArgb(220, 80, 220));
                RefreshMods();
            }
            catch (Exception ex)
            {
                DarkDialog.Error("Exception: " + ex.Message, "Mod Manager", Color.FromArgb(220, 80, 220));
            }
        }

        void SetStatus(string s)
        {
            if (statusLbl == null) return;
            statusLbl.Text = s.Length > 200 ? s.Substring(0, 200) : s;
            Application.DoEvents();
        }

        void RefreshMods()
        {
            try
            {
                var filter = (searchBox != null && searchBox.Text != null) ? searchBox.Text.Trim().ToLowerInvariant() : "";
                lvMods.Items.Clear();
                lvConflicts.Items.Clear();
                var store = ModsStore.Load(toolkitRoot);

                long backupBytes = 0;
                var bakDir = ModsStore.BackupsDir(toolkitRoot);
                if (Directory.Exists(bakDir))
                {
                    foreach (var f in Directory.GetFiles(bakDir))
                    {
                        try { backupBytes += new FileInfo(f).Length; } catch { }
                    }
                }
                var gb = Math.Round(backupBytes / 1024.0 / 1024.0 / 1024.0, 2);
                subLbl.Text = store.Installed.Count + " mod(s) installed - " + store.Backups.Count + " backup(s) (" + gb + " GB)";

                int shown = 0;
                foreach (var m in store.Installed)
                {
                    var idTxt = m.Id ?? "";
                    var nameTxt = m.Name ?? "";
                    if (filter.Length > 0)
                    {
                        var hay = (idTxt + " " + nameTxt).ToLowerInvariant();
                        if (!hay.Contains(filter)) continue;
                    }
                    shown++;
                    var it = new ListViewItem(shown.ToString());
                    it.SubItems.Add(idTxt);
                    it.SubItems.Add(nameTxt);
                    it.SubItems.Add(m.Version ?? "");
                    it.SubItems.Add(m.Priority.ToString());
                    it.SubItems.Add((m.AffectedArcs != null ? m.AffectedArcs.Count.ToString() : "0"));
                    it.SubItems.Add((m.Hashes != null ? m.Hashes.Count.ToString() : "0"));
                    lvMods.Items.Add(it);
                }

                bool noModsAtAll = (store.Installed.Count == 0);
                if (emptyState != null)
                {
                    emptyState.Visible = noModsAtAll;
                    if (noModsAtAll) emptyState.BringToFront();
                }

                var installed = store.Installed;
                int conflictCount = 0;
                for (int a = 0; a < installed.Count; a++)
                {
                    for (int b = a + 1; b < installed.Count; b++)
                    {
                        var ma = installed[a];
                        var mb = installed[b];
                        if (ma.Hashes == null || mb.Hashes == null) continue;
                        foreach (var ha in ma.Hashes)
                        {
                            foreach (var hb in mb.Hashes)
                            {
                                if (string.Equals(ha, hb, StringComparison.OrdinalIgnoreCase))
                                {
                                    var ci = new ListViewItem(ha);
                                    ci.SubItems.Add(ma.Id ?? "?");
                                    ci.SubItems.Add(mb.Id ?? "?");
                                    lvConflicts.Items.Add(ci);
                                    conflictCount++;
                                }
                            }
                        }
                    }
                }

                if (conflictCount == 0)
                {
                    lblNoConflicts.Visible = true;
                    lvConflicts.Visible = false;
                }
                else
                {
                    lblNoConflicts.Visible = false;
                    lvConflicts.Visible = true;
                }

                SetStatus("Ready - " + store.Installed.Count + " mod(s), " + store.Backups.Count + " backup(s)");
                UpdateDetails();
            }
            catch (Exception ex)
            {
                SetStatus("Refresh error: " + ex.Message);
            }
        }

        void UpdateDetails()
        {
            try
            {
                if (lvMods.SelectedItems.Count == 0)
                {
                    detailsTitle.Text = "No mod selected";
                    detailsArcs.Text = "";
                    detailsHashes.Text = "";
                    detailsLibrary.Text = "";
                    return;
                }
                var id = lvMods.SelectedItems[0].SubItems[1].Text;
                var store = ModsStore.Load(toolkitRoot);
                InstalledMod sel = null;
                foreach (var m in store.Installed) if (m.Id == id) { sel = m; break; }
                if (sel == null) { detailsTitle.Text = "(mod not in registry)"; return; }

                detailsTitle.Text = "Selected: " + sel.Id + " - priority " + sel.Priority;
                var arcsStr = "Arcs: " + string.Join(", ", sel.AffectedArcs != null ? sel.AffectedArcs.ToArray() : new string[0]);
                detailsArcs.Text = arcsStr.Length > 200 ? arcsStr.Substring(0, 200) + "..." : arcsStr;
                var hashesStr = "Hashes: " + string.Join(", ", sel.Hashes != null ? sel.Hashes.ToArray() : new string[0]);
                detailsHashes.Text = hashesStr.Length > 250 ? hashesStr.Substring(0, 250) + "..." : hashesStr;
                detailsLibrary.Text = "Library: " + (sel.LibraryFile ?? "(none)") + "   [double-click mod to open]";
            }
            catch { }
        }
    }
}
