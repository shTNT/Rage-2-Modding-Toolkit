using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    // ============================================================
    // TabFormat - parser + serializer TAB 3.1
    // ============================================================
    static class TabFormat
    {
        public const uint TAB_MAGIC = 0x00424154;
        public const long ALIGN = 0x1000;
        public const byte PAD = 0x30;

        public class Block { public uint CSize, USize; public bool Sentinel; }

        public class Entry
        {
            public ulong Hash;
            public uint Offset, CSize, USize;
            public ushort BIdx;
            public byte CType, CFlags;
            public Entry Clone() { return (Entry)MemberwiseClone(); }
        }

        public class Tab
        {
            public ushort Major, Minor;
            public uint Alignment, FileCount, BlockCount, Padding, MaxCompressedBlockSize, UncompressedBlockSize;
            public List<Block> Blocks = new List<Block>();
            public List<Entry> Entries = new List<Entry>();

            public static Tab Parse(string path)
            {
                byte[] d = File.ReadAllBytes(path);
                if (d.Length < 0x20) throw new Exception("TAB demasiado corto");
                if (BitConverter.ToUInt32(d, 0) != TAB_MAGIC) throw new Exception("No es un TAB");

                Tab t = new Tab();
                t.Major = BitConverter.ToUInt16(d, 4);
                t.Minor = BitConverter.ToUInt16(d, 6);
                t.Alignment = BitConverter.ToUInt32(d, 8);
                t.FileCount = BitConverter.ToUInt32(d, 0x0C);
                t.BlockCount = BitConverter.ToUInt32(d, 0x10);
                t.Padding = BitConverter.ToUInt32(d, 0x14);
                t.MaxCompressedBlockSize = BitConverter.ToUInt32(d, 0x18);
                t.UncompressedBlockSize = BitConverter.ToUInt32(d, 0x1C);

                int off = 0x20;
                for (int i = 0; i < t.BlockCount; i++)
                {
                    Block b = new Block();
                    b.CSize = BitConverter.ToUInt32(d, off);
                    b.USize = BitConverter.ToUInt32(d, off + 4);
                    b.Sentinel = (b.CSize == 0xFFFFFFFF && b.USize == 0xFFFFFFFF);
                    t.Blocks.Add(b);
                    off += 8;
                }
                for (int i = 0; i < t.FileCount; i++)
                {
                    Entry e = new Entry();
                    e.Hash = BitConverter.ToUInt64(d, off);
                    e.Offset = BitConverter.ToUInt32(d, off + 8);
                    e.CSize = BitConverter.ToUInt32(d, off + 12);
                    e.USize = BitConverter.ToUInt32(d, off + 16);
                    e.BIdx = BitConverter.ToUInt16(d, off + 20);
                    e.CType = d[off + 22];
                    e.CFlags = d[off + 23];
                    t.Entries.Add(e);
                    off += 24;
                }
                return t;
            }

            public byte[] Serialize()
            {
                int total = 0x20 + (int)BlockCount * 8 + (int)FileCount * 24;
                byte[] d = new byte[total];
                Array.Copy(BitConverter.GetBytes(TAB_MAGIC), 0, d, 0, 4);
                Array.Copy(BitConverter.GetBytes(Major), 0, d, 4, 2);
                Array.Copy(BitConverter.GetBytes(Minor), 0, d, 6, 2);
                Array.Copy(BitConverter.GetBytes(Alignment), 0, d, 8, 4);
                Array.Copy(BitConverter.GetBytes(FileCount), 0, d, 0x0C, 4);
                Array.Copy(BitConverter.GetBytes(BlockCount), 0, d, 0x10, 4);
                Array.Copy(BitConverter.GetBytes(Padding), 0, d, 0x14, 4);
                Array.Copy(BitConverter.GetBytes(MaxCompressedBlockSize), 0, d, 0x18, 4);
                Array.Copy(BitConverter.GetBytes(UncompressedBlockSize), 0, d, 0x1C, 4);

                int off = 0x20;
                foreach (var b in Blocks)
                {
                    Array.Copy(BitConverter.GetBytes(b.CSize), 0, d, off, 4);
                    Array.Copy(BitConverter.GetBytes(b.USize), 0, d, off + 4, 4);
                    off += 8;
                }
                foreach (var e in Entries)
                {
                    Array.Copy(BitConverter.GetBytes(e.Hash), 0, d, off, 8);
                    Array.Copy(BitConverter.GetBytes(e.Offset), 0, d, off + 8, 4);
                    Array.Copy(BitConverter.GetBytes(e.CSize), 0, d, off + 12, 4);
                    Array.Copy(BitConverter.GetBytes(e.USize), 0, d, off + 16, 4);
                    Array.Copy(BitConverter.GetBytes(e.BIdx), 0, d, off + 20, 2);
                    d[off + 22] = e.CType;
                    d[off + 23] = e.CFlags;
                    off += 24;
                }
                return d;
            }
        }
    }

    // ============================================================
    // Repacker - extraer + rebuild de .arc
    // ============================================================
    static class Repacker
    {
        public class Replacement { public byte[] Data; }

        public static byte[] ExtractEntry(TabFormat.Tab t, byte[] arc, TabFormat.Entry e)
        {
            if (!Oodle.IsLoaded) throw new Exception("Oodle no cargado");

            if (e.CType == 0 || e.BIdx == 0)
            {
                byte[] buf = new byte[e.USize];
                if (e.Offset + e.USize > arc.Length) throw new Exception("offset fuera de rango");
                Array.Copy(arc, (int)e.Offset, buf, 0, (int)e.USize);
                return buf;
            }

            MemoryStream ms = new MemoryStream((int)e.USize);
            long pos = e.Offset;
            long remaining = e.USize;
            int i = e.BIdx;
            while (remaining > 0)
            {
                if (i >= t.Blocks.Count) throw new Exception("block index out of range");
                var b = t.Blocks[i];
                if (b.Sentinel) throw new Exception("unexpected sentinel block");
                byte[] cbuf = new byte[b.CSize];
                Array.Copy(arc, pos, cbuf, 0, (int)b.CSize);
                byte[] ubuf = Oodle.Decompress(cbuf, (int)b.USize);
                ms.Write(ubuf, 0, ubuf.Length);
                pos += b.CSize;
                remaining -= b.USize;
                i++;
            }
            return ms.ToArray();
        }

        public static void Rebuild(
            string tabPath, string arcPath,
            Dictionary<ulong, Replacement> replacements,
            string outTabPath, string outArcPath,
            Action<string> log)
        {
            TabFormat.Tab t = TabFormat.Tab.Parse(tabPath);
            byte[] arc = File.ReadAllBytes(arcPath);

            var origEntries = t.Entries.Select(e => e.Clone()).ToList();
            var order = origEntries.OrderBy(x => x.Offset).ToList();
            MemoryStream newArc = new MemoryStream(arc.Length + 16 * 1024 * 1024);
            Dictionary<ulong, uint> newOffsets = new Dictionary<ulong, uint>();

            foreach (var e in order)
            {
                long pad = ((newArc.Length + TabFormat.ALIGN - 1) / TabFormat.ALIGN) * TabFormat.ALIGN - newArc.Length;
                for (long p = 0; p < pad; p++) newArc.WriteByte(TabFormat.PAD);
                newOffsets[e.Hash] = (uint)newArc.Length;

                if (replacements.ContainsKey(e.Hash))
                {
                    byte[] nd = replacements[e.Hash].Data;
                    newArc.Write(nd, 0, nd.Length);
                    if (log != null) log("  Reemplazado " + e.Hash.ToString("X16") + " -> " + nd.Length + " bytes");
                }
                else
                {
                    byte[] data;
                    if (e.CType == 0 || e.BIdx == 0)
                    {
                        data = new byte[e.CSize];
                        Array.Copy(arc, (int)e.Offset, data, 0, (int)e.CSize);
                    }
                    else
                    {
                        long total = 0;
                        int i = e.BIdx;
                        long rem = e.USize;
                        while (rem > 0)
                        {
                            total += t.Blocks[i].CSize;
                            rem -= t.Blocks[i].USize;
                            i++;
                        }
                        data = new byte[total];
                        Array.Copy(arc, (int)e.Offset, data, 0, (int)total);
                    }
                    newArc.Write(data, 0, data.Length);
                }
            }

            long fp = ((newArc.Length + TabFormat.ALIGN - 1) / TabFormat.ALIGN) * TabFormat.ALIGN - newArc.Length;
            for (long p = 0; p < fp; p++) newArc.WriteByte(TabFormat.PAD);

            byte[] newArcBytes = newArc.ToArray();

            TabFormat.Tab tNew = new TabFormat.Tab
            {
                Major = t.Major,
                Minor = t.Minor,
                Alignment = t.Alignment,
                FileCount = t.FileCount,
                BlockCount = t.BlockCount,
                Padding = t.Padding,
                MaxCompressedBlockSize = t.MaxCompressedBlockSize,
                UncompressedBlockSize = t.UncompressedBlockSize,
                Blocks = t.Blocks.Select(b => new TabFormat.Block { CSize = b.CSize, USize = b.USize, Sentinel = b.Sentinel }).ToList()
            };

            foreach (var origEntry in t.Entries)
            {
                var ne = origEntry.Clone();
                ne.Offset = newOffsets[origEntry.Hash];
                if (replacements.ContainsKey(origEntry.Hash))
                {
                    byte[] nd = replacements[origEntry.Hash].Data;
                    ne.CSize = (uint)nd.Length;
                    ne.USize = (uint)nd.Length;
                    ne.BIdx = 0;
                    ne.CType = 0;
                    ne.CFlags = 0;
                }
                tNew.Entries.Add(ne);
            }

            File.WriteAllBytes(outTabPath, tNew.Serialize());
            File.WriteAllBytes(outArcPath, newArcBytes);
            if (log != null) log("  Escrito: " + outTabPath + " (" + new FileInfo(outTabPath).Length + " bytes)");
            if (log != null) log("  Escrito: " + outArcPath + " (" + newArcBytes.Length + " bytes)");
        }
    }

    // ============================================================
    // RepackForm - GUI de modificacion de texturas dentro de .arc
    // ============================================================
    public class RepackForm : Form
    {
        readonly Color C_BG      = Color.FromArgb(24, 24, 30);
        readonly Color C_PANEL   = Color.FromArgb(30, 30, 38);
        readonly Color C_LOG     = Color.FromArgb(10, 10, 15);
        readonly Color C_HEAD    = Color.FromArgb(15, 15, 20);
        readonly Color C_INFO    = Color.FromArgb(0, 200, 200);
        readonly Color C_OK      = Color.FromArgb(100, 220, 100);
        readonly Color C_WARN    = Color.FromArgb(230, 180, 74);
        readonly Color C_ERR     = Color.FromArgb(240, 100, 100);
        readonly Color C_GRAY    = Color.FromArgb(160, 160, 160);
        readonly Color C_TEXT    = Color.FromArgb(200, 200, 200);
        readonly Color C_BTN     = Color.FromArgb(50, 50, 60);
        readonly Color C_SECTION = Color.FromArgb(220, 80, 220);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

        string gamePath, outputPath, workDir;
        string currentTabPath, currentArcPath;
        TabFormat.Tab currentTab;
        byte[] currentArcBytes;
        Dictionary<ulong, string> namesByHash = new Dictionary<ulong, string>();
        Dictionary<ulong, byte[]> pendingMods = new Dictionary<ulong, byte[]>();
        bool busy = false;

        ComboBox arcCombo;
        TextBox filterBox;
        ListView entryList;
        RichTextBox logBox;
        Button btnExtractDds, btnApplyDds, btnRebuild, btnInstall, btnOpenWork, btnClear;
        Label pendingLbl;

        public RepackForm(Form owner, string gamePath, string outputPath)
        {
            this.gamePath = gamePath;
            this.outputPath = outputPath;
            this.Owner = owner;
            this.Text = "Repack .ARC - Modificar texturas";
            this.ClientSize = new Size(1200, 820);
            this.MinimumSize = new Size(1000, 700);
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

            workDir = Path.Combine(Path.GetTempPath(), "RAGE2Repack");
            Directory.CreateDirectory(workDir);

            LoadFilelist();
            BuildUI();
            PopulateArcList();

            Log("Repack tool listo.", C_INFO);
            Log("Carpeta de trabajo: " + workDir, C_GRAY);
            Log("Selecciona un .arc arriba para empezar.", C_GRAY);
        }

        void BuildUI()
        {
            int headerH = 60;

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = headerH;
            header.BackColor = C_HEAD;
            this.Controls.Add(header);

            Label title = new Label();
            title.Text = "  REPACK .ARC  -  Modificar texturas dentro de los archivos del juego";
            title.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            title.ForeColor = C_INFO;
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            header.Controls.Add(title);

            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 50;
            top.BackColor = C_PANEL;
            this.Controls.Add(top);
            top.BringToFront();

            Label lblArc = new Label();
            lblArc.Text = "Archivo .arc:";
            lblArc.Location = new Point(15, 15);
            lblArc.AutoSize = true;
            lblArc.ForeColor = C_TEXT;
            top.Controls.Add(lblArc);

            arcCombo = new ComboBox();
            arcCombo.Location = new Point(110, 12);
            arcCombo.Size = new Size(240, 28);
            arcCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            arcCombo.BackColor = Color.FromArgb(40, 40, 50);
            arcCombo.ForeColor = Color.White;
            arcCombo.FlatStyle = FlatStyle.Flat;
            arcCombo.SelectedIndexChanged += (s, e) => LoadSelectedArc();
            top.Controls.Add(arcCombo);

            Label lblFilter = new Label();
            lblFilter.Text = "Filtro (hash o nombre):";
            lblFilter.Location = new Point(370, 15);
            lblFilter.AutoSize = true;
            lblFilter.ForeColor = C_TEXT;
            top.Controls.Add(lblFilter);

            filterBox = new TextBox();
            filterBox.Location = new Point(540, 12);
            filterBox.Size = new Size(300, 28);
            filterBox.BackColor = Color.FromArgb(40, 40, 50);
            filterBox.ForeColor = Color.White;
            filterBox.BorderStyle = BorderStyle.FixedSingle;
            filterBox.TextChanged += (s, e) => RefreshList();
            top.Controls.Add(filterBox);

            btnOpenWork = new Button();
            btnOpenWork.Text = "Abrir carpeta trabajo";
            btnOpenWork.Location = new Point(860, 11);
            btnOpenWork.Size = new Size(170, 30);
            btnOpenWork.FlatStyle = FlatStyle.Flat;
            btnOpenWork.FlatAppearance.BorderSize = 0;
            btnOpenWork.BackColor = C_BTN;
            btnOpenWork.ForeColor = Color.White;
            btnOpenWork.Cursor = Cursors.Hand;
            btnOpenWork.Click += (s, e) => Process.Start("explorer.exe", workDir);
            top.Controls.Add(btnOpenWork);

            btnClear = new Button();
            btnClear.Text = "Limpiar modificaciones";
            btnClear.Location = new Point(1040, 11);
            btnClear.Size = new Size(150, 30);
            btnClear.FlatStyle = FlatStyle.Flat;
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.BackColor = Color.FromArgb(140, 50, 50);
            btnClear.ForeColor = Color.White;
            btnClear.Cursor = Cursors.Hand;
            btnClear.Click += (s, e) => { pendingMods.Clear(); UpdatePendingLbl(); RefreshList(); Log("Modificaciones pendientes limpiadas.", C_WARN); };
            top.Controls.Add(btnClear);

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 220;
            bottom.BackColor = C_PANEL;
            this.Controls.Add(bottom);

            int by = 12;
            int bx = 15;
            int bw = 200, bh = 40;
            int gap = 10;

            btnExtractDds = MakeActionBtn("1. Extraer a DDS", bx, by, bw, bh, C_BTN);
            btnExtractDds.Click += (s, e) => DoExtractDds();
            bottom.Controls.Add(btnExtractDds);
            bx += bw + gap;

            btnApplyDds = MakeActionBtn("2. Aplicar DDS modificado", bx, by, bw, bh, C_BTN);
            btnApplyDds.Click += (s, e) => DoApplyDds();
            bottom.Controls.Add(btnApplyDds);
            bx += bw + gap;

            btnRebuild = MakeActionBtn("3. Rebuild .arc", bx, by, bw, bh, C_OK);
            btnRebuild.Click += (s, e) => DoRebuild();
            bottom.Controls.Add(btnRebuild);
            bx += bw + gap;

            btnInstall = MakeActionBtn("4. Instalar en el juego", bx, by, bw, bh, Color.FromArgb(140, 50, 50));
            btnInstall.Click += (s, e) => DoInstall();
            bottom.Controls.Add(btnInstall);

            pendingLbl = new Label();
            pendingLbl.Text = "Modificaciones pendientes: 0";
            pendingLbl.Location = new Point(15, by + bh + 8);
            pendingLbl.Size = new Size(500, 24);
            pendingLbl.ForeColor = C_WARN;
            pendingLbl.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            bottom.Controls.Add(pendingLbl);

            Panel logPanel = new Panel();
            logPanel.Location = new Point(15, by + bh + 40);
            logPanel.Size = new Size(1170, 140);
            logPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            logPanel.BackColor = C_HEAD;
            bottom.Controls.Add(logPanel);

            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.BackColor = C_LOG;
            logBox.ForeColor = C_TEXT;
            logBox.Font = new Font("Consolas", 9);
            logBox.ReadOnly = true;
            logBox.BorderStyle = BorderStyle.None;
            logBox.WordWrap = false;
            logBox.ScrollBars = RichTextBoxScrollBars.Both;
            logPanel.Controls.Add(logBox);

            entryList = new ListView();
            entryList.Dock = DockStyle.Fill;
            entryList.View = View.Details;
            entryList.FullRowSelect = true;
            entryList.GridLines = false;
            entryList.BackColor = Color.FromArgb(20, 20, 26);
            entryList.ForeColor = Color.White;
            entryList.BorderStyle = BorderStyle.None;
            entryList.Font = new Font("Consolas", 9);
            entryList.Columns.Add("#", 50);
            entryList.Columns.Add("Hash", 180);
            entryList.Columns.Add("Tamano", 120);
            entryList.Columns.Add("Tipo", 60);
            entryList.Columns.Add("Nombre", 600);
            entryList.Columns.Add("Mod", 60);
            this.Controls.Add(entryList);
            entryList.BringToFront();
        }

        Button MakeActionBtn(string text, int x, int y, int w, int h, Color color)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, h);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = color;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
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
                    Log("Filelist: " + namesByHash.Count + " entries loaded from " + p, C_OK);
                    loaded = true;
                    break;
                }
                if (!loaded) Log("Filelist not found - hashes will be shown without names", C_WARN);
            }
            catch (Exception ex) { Log("Filelist error: " + ex.Message, C_WARN); }
        }

        void PopulateArcList()
        {
            arcCombo.Items.Clear();
            if (gamePath == null || !Directory.Exists(gamePath))
            {
                Log("Game path no configurado en el toolkit principal.", C_ERR);
                return;
            }
            string initDir = Path.Combine(gamePath, "archives_win64", "initial");
            string suppDir = Path.Combine(gamePath, "archives_win64", "supplemental");
            if (!Directory.Exists(initDir) && !Directory.Exists(suppDir))
            {
                Log("No existe " + initDir + " ni " + suppDir, C_ERR);
                return;
            }
            var arcs = new List<string>();
            if (Directory.Exists(initDir)) arcs.AddRange(Directory.GetFiles(initDir, "game*.arc"));
            if (Directory.Exists(suppDir)) arcs.AddRange(Directory.GetFiles(suppDir, "game*.arc"));
            var arcsSorted = arcs.OrderBy(f => f).ToList();
            foreach (string a in arcsSorted) arcCombo.Items.Add(a);
            if (arcCombo.Items.Count > 0) arcCombo.SelectedIndex = 0;
        }

        void LoadSelectedArc()
        {
            if (arcCombo.SelectedItem == null) return;
            string arcPath = arcCombo.SelectedItem.ToString();
            string tabPath = Path.ChangeExtension(arcPath, ".tab");
            if (!File.Exists(tabPath))
            {
                Log("No existe " + tabPath, C_ERR);
                return;
            }
            try
            {
                LogSection("Cargando " + Path.GetFileName(arcPath));
                var sw = Stopwatch.StartNew();
                currentTab = TabFormat.Tab.Parse(tabPath);
                currentArcBytes = File.ReadAllBytes(arcPath);
                currentArcPath = arcPath;
                currentTabPath = tabPath;
                sw.Stop();

                pendingMods.Clear();
                UpdatePendingLbl();

                Log(string.Format("  {0} entries, {1} blocks, {2:N0} bytes .arc ({3} ms)",
                    currentTab.FileCount, currentTab.BlockCount, currentArcBytes.Length, sw.ElapsedMilliseconds), C_OK);
                RefreshList();
            }
            catch (Exception ex)
            {
                Log("Error al parsear: " + ex.Message, C_ERR);
            }
        }

        void RefreshList()
        {
            entryList.BeginUpdate();
            entryList.Items.Clear();
            if (currentTab == null) { entryList.EndUpdate(); return; }

            string filter = filterBox.Text.Trim().ToLower();
            int shown = 0;
            foreach (var e in currentTab.Entries)
            {
                string name = "";
                if (namesByHash.ContainsKey(e.Hash)) name = namesByHash[e.Hash];
                string hashStr = e.Hash.ToString("X16");
                if (filter.Length > 0)
                {
                    bool hit = hashStr.ToLower().Contains(filter) || name.ToLower().Contains(filter);
                    if (!hit) continue;
                }
                var item = new ListViewItem(new string[] {
                    shown.ToString(),
                    hashStr,
                    e.USize.ToString("N0"),
                    "ct=" + e.CType,
                    name.Length > 0 ? name : "(huerfano)",
                    pendingMods.ContainsKey(e.Hash) ? "SI" : ""
                });
                item.Tag = e;
                if (pendingMods.ContainsKey(e.Hash))
                {
                    item.ForeColor = C_WARN;
                }
                entryList.Items.Add(item);
                shown++;
            }
            entryList.EndUpdate();
            if (filter.Length > 0) Log("Filtro: " + shown + " entradas mostradas", C_GRAY);
        }

        void UpdatePendingLbl()
        {
            pendingLbl.Text = "Modificaciones pendientes: " + pendingMods.Count +
                (pendingMods.Count > 0 ? "  (pulsa 3. Rebuild .arc para aplicar)" : "");
        }

        TabFormat.Entry GetSelectedEntry()
        {
            if (entryList.SelectedItems.Count == 0) return null;
            return entryList.SelectedItems[0].Tag as TabFormat.Entry;
        }

        void DoExtractDds()
        {
            var e = GetSelectedEntry();
            if (e == null) { Log("Selecciona una entry primero", C_WARN); return; }
            if (busy) return;

            try
            {
                busy = true;
                LogSection("Extrayendo hash " + e.Hash.ToString("X16"));
                byte[] data = Repacker.ExtractEntry(currentTab, currentArcBytes, e);
                string hashStr = e.Hash.ToString("X16");
                string avtxPath = Path.Combine(workDir, hashStr + ".avtx");
                File.WriteAllBytes(avtxPath, data);
                Log("  AVTX: " + avtxPath + " (" + data.Length + " bytes)", C_OK);

                string magic = data.Length >= 4 ? Encoding.ASCII.GetString(data, 0, 4) : "";
                Log("  Magic: " + magic, C_GRAY);

                if (magic == "AVTX")
                {
                    string ddscExe = Path.Combine(Paths.BinDir, "ddscConvert.exe");
                    if (!File.Exists(ddscExe))
                    {
                        Log("  ddscConvert.exe no encontrado en " + Paths.BinDir, C_ERR);
                        return;
                    }
                    RunProc(ddscExe, "\"" + avtxPath + "\"");
                    string ddsPath = Path.ChangeExtension(avtxPath, ".dds");
                    if (File.Exists(ddsPath))
                    {
                        Log("  DDS: " + ddsPath, C_OK);
                        Process.Start("explorer.exe", "/select,\"" + ddsPath + "\"");
                    }
                    else Log("  ddscConvert no genero .dds", C_ERR);
                }
                else Log("  No es AVTX - no se convierte", C_WARN);
            }
            catch (Exception ex) { Log("  Error: " + ex.Message, C_ERR); }
            finally { busy = false; }
        }

        void DoApplyDds()
        {
            var e = GetSelectedEntry();
            if (e == null) { Log("Selecciona una entry primero", C_WARN); return; }
            if (busy) return;

            string hashStr = e.Hash.ToString("X16");
            string expectedDds = Path.Combine(workDir, hashStr + ".dds");

            string ddsPath;
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = "Selecciona el DDS modificado para " + hashStr;
                dlg.Filter = "DDS|*.dds|Todos|*.*";
                if (File.Exists(expectedDds)) dlg.InitialDirectory = Path.GetDirectoryName(expectedDds);
                if (dlg.ShowDialog() != DialogResult.OK) return;
                ddsPath = dlg.FileName;
            }

            try
            {
                busy = true;
                LogSection("Aplicando DDS modificado a " + hashStr);
                Log("  DDS: " + ddsPath, C_GRAY);

                string texconv = Path.Combine(Paths.BinDir, "texconv.exe");
                if (File.Exists(texconv))
                {
                    string mipDir = Path.Combine(workDir, "_mipfix_" + hashStr);
                    Directory.CreateDirectory(mipDir);
                    RunProc(texconv, "-m 0 -y -o \"" + mipDir + "\" \"" + ddsPath + "\"");
                    string mipDds = Path.Combine(mipDir, Path.GetFileName(ddsPath));
                    if (File.Exists(mipDds)) { ddsPath = mipDds; Log("  Mipmaps regenerados con texconv", C_OK); }
                    else Log("  texconv no genero salida, uso original", C_WARN);
                }

                string ddsc = Path.Combine(Paths.BinDir, "ddscConvert.exe");
                RunProc(ddsc, "\"" + ddsPath + "\"");
                string avtxOut = Path.ChangeExtension(ddsPath, ".avtx");
                if (!File.Exists(avtxOut))
                {
                    Log("  ddscConvert no genero .avtx (falta mipmap chain?)", C_ERR);
                    return;
                }
                byte[] newAvtx = File.ReadAllBytes(avtxOut);
                Log("  AVTX nuevo: " + newAvtx.Length + " bytes (original " + e.USize + " bytes)", C_OK);
                pendingMods[e.Hash] = newAvtx;
                UpdatePendingLbl();
                RefreshList();
                Log("  Anadido a modificaciones pendientes", C_OK);
                Log("  Pulsa 3. Rebuild .arc cuando termines", C_WARN);
            }
            catch (Exception ex) { Log("  Error: " + ex.Message, C_ERR); }
            finally { busy = false; }
        }

        void DoRebuild()
        {
            if (currentTab == null) { Log("Carga un .arc primero", C_WARN); return; }
            if (pendingMods.Count == 0) { Log("No hay modificaciones pendientes. Aplica algun DDS primero.", C_WARN); return; }
            if (busy) return;

            try
            {
                busy = true;
                LogSection("Rebuild .arc con " + pendingMods.Count + " modificaciones");

                string bn = Path.GetFileNameWithoutExtension(currentArcPath);
                string outArc = Path.Combine(Path.GetDirectoryName(currentArcPath), bn + "_mod.arc");
                string outTab = Path.Combine(Path.GetDirectoryName(currentArcPath), bn + "_mod.tab");

                var repl = new Dictionary<ulong, byte[]>();
                foreach (var kv in pendingMods) repl[kv.Key] = kv.Value;

                RepackerCore.Rebuild(currentTabPath, currentArcPath, repl, outTab, outArc, m => Log(m, C_GRAY));
                Log("[OK] Rebuild completo", C_OK);
            }
            catch (Exception ex) { Log("Error rebuild: " + ex.Message, C_ERR); }
            finally { busy = false; }
        }

        void DoInstall()
        {
            if (currentTab == null) return;
            string bn = Path.GetFileNameWithoutExtension(currentArcPath);
            string modArc = Path.Combine(Path.GetDirectoryName(currentArcPath), bn + "_mod.arc");
            string modTab = Path.Combine(Path.GetDirectoryName(currentArcPath), bn + "_mod.tab");
            if (!File.Exists(modArc) || !File.Exists(modTab))
            {
                Log("No hay _mod.arc / _mod.tab. Pulsa 3. Rebuild primero.", C_WARN);
                return;
            }

            var r = MessageBox.Show(
                "Se van a reemplazar " + bn + ".arc y " + bn + ".tab en el juego.\n\n" +
                "Backup automatico la primera vez (.original).\n\nContinuar?",
                "Instalar en el juego", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;

            string gameArc = currentArcPath;
            string gameTab = currentTabPath;
            string bkpArc = gameArc + ".original";
            string bkpTab = gameTab + ".original";

            if (!File.Exists(bkpArc))
            {
                File.Copy(gameArc, bkpArc, true);
                File.Copy(gameTab, bkpTab, true);
                Log("  Backups creados: " + bkpArc, C_OK);
            }
            else Log("  Backups ya existian (no se sobreescriben)", C_GRAY);

            File.Copy(modArc, gameArc, true);
            File.Copy(modTab, gameTab, true);
            Log("[OK] Instalado. Lanza el juego para ver el cambio.", C_OK);
            Log("     Para restaurar: renombra los .original quitando la extension .original", C_WARN);
        }

        void RunProc(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, args);
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe);
                var p = Process.Start(psi);
                string outp = p.StandardOutput.ReadToEnd();
                string errp = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (outp.Length > 0) Log("    " + outp.Trim(), C_GRAY);
                if (errp.Length > 0) Log("    ERR: " + errp.Trim(), C_ERR);
            }
            catch (Exception ex) { Log("    process error: " + ex.Message, C_ERR); }
        }

        void Log(string msg, Color color)
        {
            if (logBox.InvokeRequired) { logBox.Invoke(new Action(() => Log(msg, color))); return; }
            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionColor = color;
            logBox.AppendText(msg + Environment.NewLine);
            logBox.SelectionColor = logBox.ForeColor;
            logBox.ScrollToCaret();
        }

        void Log(string msg) { Log(msg, C_TEXT); }

        void LogSection(string title)
        {
            Log("");
            Log(new string('-', 70), C_INFO);
            Log("  " + title, C_INFO);
            Log(new string('-', 70), C_INFO);
        }
    }
}