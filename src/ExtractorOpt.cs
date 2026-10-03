using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Rage2Toolkit
{
    static class ExtractorOpt
    {

        // --- TECH-15: resolver opcional para nombres descriptivos ---
        // Si es null, comportamiento legacy: <HASH16>.<ext>
        public static Func<ulong, string> NameResolver = null;
        public static Func<ulong, string> PathResolver = null;

        public static string SanitizeBasename(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int slash = path.LastIndexOf('/');
            string bn = slash >= 0 ? path.Substring(slash + 1) : path;
            int dot = bn.LastIndexOf('.');
            if (dot > 0) bn = bn.Substring(0, dot);
            if (string.IsNullOrEmpty(bn)) return null;
            var sb = new System.Text.StringBuilder(bn.Length);
            foreach (char c in bn)
            {
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            var result = sb.ToString();
            return result.Length > 0 ? result : null;
        }
        // --- /TECH-15 ---
        // TECH-15: cache lazy del filelist
        private static Dictionary<ulong, string> _filelistCache = null;
        public static Dictionary<ulong, string> LoadFilelistCache(string dataDir)
        {
            if (_filelistCache != null) return _filelistCache;
            var dict = new Dictionary<ulong, string>();
            string p = Path.Combine(dataDir, "filelist.txt");
            if (!File.Exists(p)) p = Path.Combine(dataDir, "data", "filelist.txt");
            if (File.Exists(p)) { foreach (string line in File.ReadAllLines(p)) {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(new char[] {(char)9, (char)32}, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                ulong h;
                if (!ulong.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out h)) continue;
                dict[h] = parts[1].Trim();
            } }
            _filelistCache = dict;
            return dict;
        }

        private static HashSet<ulong> _gameHashesCache = null;
        public static HashSet<ulong> LoadGameHashes(string gamePath)
        {
            if (_gameHashesCache != null) return _gameHashesCache;
            var set = new HashSet<ulong>();
            try {
                var tabList = new List<string>();
                string initDir = Path.Combine(gamePath, "archives_win64", "initial");
                string suppDir = Path.Combine(gamePath, "archives_win64", "supplemental");
                if (Directory.Exists(initDir)) tabList.AddRange(Directory.GetFiles(initDir, "*.tab", SearchOption.AllDirectories));
                if (Directory.Exists(suppDir)) tabList.AddRange(Directory.GetFiles(suppDir, "*.tab", SearchOption.AllDirectories));
                tabList.RemoveAll(p => p.Contains("\\languages\\"));
                foreach (var tabPath in tabList) {
                    TabFormat.Tab t;
                    try { t = TabFormat.Tab.Parse(tabPath); } catch { continue; }
                    foreach (var e in t.Entries) set.Add(e.Hash);
                }
            } catch { }
            _gameHashesCache = set;
            return set;
        }

        public delegate bool OodleDec(byte[] comp, int compOff, int compLen, byte[] dst, int dstOff, int dstLen);

        public class Stats {
            public int EntriesTotal, EntriesOk, EntriesFail;
            public long BytesWritten;
            public int PeakRamMB;
            public long TimeMs;
            public long CpuTimeMs;
            public int GC0, GC1, GC2;
        }

        static int PickBufferSize(long ramBytes) {
            long gb = ramBytes / (1024L * 1024 * 1024);
            if (gb < 4)  return 2 * 1024 * 1024;
            if (gb < 8)  return 4 * 1024 * 1024;
            return 8 * 1024 * 1024;
        }
        static int PickThreads(int totalJobs, long ramBytes) {
            int p = Environment.ProcessorCount;
            int t = p * 2;
            if (t > 32) t = 32;
            long gb = ramBytes / (1024L * 1024 * 1024);
            if (gb < 4)  t = Math.Min(t, 4);
            if (gb < 8)  t = Math.Min(t, 16);
            if (p <= 2) t = 2;
            if (t > totalJobs) t = Math.Max(1, totalJobs);
            return t;
        }

        public static Stats Extract(
            string gamePath, string outputBase, bool skipLanguages,
            OodleDec oodle,
            Action<int,int,string,int> onTabStart,
            Action<int,int,string,int,int> onTabDone,
            Action<string> log, System.Threading.CancellationToken cancelToken = default(System.Threading.CancellationToken))
        {
            var st = new Stats();
            var swTotal = Stopwatch.StartNew();
            int peakMB = 0;
            var ramTimer = new Timer(_ => {
                try {
                    long ws = Process.GetCurrentProcess().WorkingSet64;
                    int mb = (int)(ws / 1024 / 1024);
                    if (mb > peakMB) peakMB = mb;
                } catch { }
            }, null, 0, 200);
            var proc = Process.GetCurrentProcess();
            TimeSpan cpuStart = proc.TotalProcessorTime;
            int gc0Start = GC.CollectionCount(0);
            int gc1Start = GC.CollectionCount(1);
            int gc2Start = GC.CollectionCount(2);

            try {
                string initDir = Path.Combine(gamePath, "archives_win64", "initial");
                string suppDir = Path.Combine(gamePath, "archives_win64", "supplemental");
                var tabList = new List<string>();
                if (Directory.Exists(initDir)) tabList.AddRange(Directory.GetFiles(initDir, "*.tab", SearchOption.AllDirectories));
                if (Directory.Exists(suppDir)) tabList.AddRange(Directory.GetFiles(suppDir, "*.tab", SearchOption.AllDirectories));
                if (skipLanguages) tabList.RemoveAll(p => p.Contains("\\languages\\"));

                int totalTabs = tabList.Count;
                long ramBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                int nThreads = PickThreads(totalTabs * 100, ramBytes);
                int bufSize = PickBufferSize(ramBytes);
                if (log != null) log("Found " + totalTabs + " .tab files. threads=" + nThreads + "  buf=" + (bufSize / 1024) + " KB");

                var parsedTabs = new List<TabFormat.Tab>(totalTabs);
                var arcs = new List<string>(totalTabs);
                var tabNames = new List<string>(totalTabs);
                var outDirs = new List<string>(totalTabs);
                for (int i = 0; i < totalTabs; i++) {
                    string tabPath = tabList[i];
                    string ap = Path.ChangeExtension(tabPath, ".arc");
                    string bn = Path.GetFileNameWithoutExtension(tabPath);
                    string relDir = Path.GetDirectoryName(tabPath).Substring(Path.Combine(gamePath, "archives_win64").Length).TrimStart('\\');
                    string od = string.IsNullOrEmpty(relDir) ? Path.Combine(outputBase, bn) : Path.Combine(outputBase, relDir, bn);
                    TabFormat.Tab t;
                    try { t = TabFormat.Tab.Parse(tabPath); } catch { t = null; }
                    parsedTabs.Add(t);
                    arcs.Add(File.Exists(ap) ? ap : null);
                    tabNames.Add(bn);
                    outDirs.Add(od);
                }

                var jobs = new List<(int tabIdx, int entryIdx, uint uSize)>();
                for (int ti = 0; ti < totalTabs; ti++) {
                    if (parsedTabs[ti] == null || arcs[ti] == null) continue;
                    var entries = parsedTabs[ti].Entries;
                    for (int ei = 0; ei < entries.Count; ei++) {
                        jobs.Add((ti, ei, entries[ei].USize));
                    }
                }
                jobs.Sort((a, b) => b.uSize.CompareTo(a.uSize));

                var perTabOk = new int[totalTabs];
                var perTabFail = new int[totalTabs];
                var perTabStarted = new bool[totalTabs];
                long grandBytes = 0;
                var startLock = new object();

                var mmaps = new MemoryMappedFile[totalTabs];
                var arcLens = new long[totalTabs];
                for (int ti = 0; ti < totalTabs; ti++) {
                    if (arcs[ti] != null) {
                        arcLens[ti] = new FileInfo(arcs[ti]).Length;
                        mmaps[ti] = MemoryMappedFile.CreateFromFile(arcs[ti], FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                    }
                }

                try {
                    Parallel.ForEach(jobs, new ParallelOptions { MaxDegreeOfParallelism = nThreads, CancellationToken = cancelToken },
                        () => new Ctx(bufSize),
                        (job, state, ctx) => {
                        cancelToken.ThrowIfCancellationRequested();
                            int ti = job.tabIdx, ei = job.entryIdx;
                            var tab = parsedTabs[ti];
                            var e = tab.Entries[ei];

                            lock (startLock) {
                                if (!perTabStarted[ti]) {
                                    perTabStarted[ti] = true;
                                    Directory.CreateDirectory(outDirs[ti]);
                                    if (onTabStart != null) onTabStart(ti + 1, totalTabs, tabNames[ti], tab.Entries.Count);
                                }
                            }

                            if (ctx.acc == null || ctx.currentTab != ti) {
                                ctx.acc?.Dispose();
                                ctx.acc = mmaps[ti].CreateViewAccessor(0, arcLens[ti], MemoryMappedFileAccess.Read);
                                ctx.currentTab = ti;
                            }

                            bool ok = ExtractAndWrite(ctx.acc, tab, e, ctx, outDirs[ti], oodle);
                            if (ok) {
                                Interlocked.Increment(ref perTabOk[ti]);
                                Interlocked.Add(ref grandBytes, e.USize);
                            } else {
                                Interlocked.Increment(ref perTabFail[ti]);
                            }
                            return ctx;
                        },
                        ctx => { ctx.Dispose(); }
                    );
                } finally {
                    for (int ti = 0; ti < totalTabs; ti++) {
                        try { mmaps[ti]?.Dispose(); } catch { }
                    }
                }

                int grandOk = 0, grandFail = 0;
                for (int ti = 0; ti < totalTabs; ti++) {
                    if (parsedTabs[ti] != null && arcs[ti] != null) {
                        if (onTabDone != null) onTabDone(ti + 1, totalTabs, tabNames[ti], perTabOk[ti], perTabFail[ti]);
                        grandOk += perTabOk[ti];
                        grandFail += perTabFail[ti];
                    }
                }

                st.EntriesTotal = grandOk + grandFail;
                st.EntriesOk = grandOk;
                st.EntriesFail = grandFail;
                st.BytesWritten = grandBytes;
            } finally {
                ramTimer.Dispose();
                swTotal.Stop();
                TimeSpan cpuEnd = proc.TotalProcessorTime;
                st.TimeMs = swTotal.ElapsedMilliseconds;
                st.PeakRamMB = peakMB;
                st.CpuTimeMs = (long)(cpuEnd - cpuStart).TotalMilliseconds;
                st.GC0 = GC.CollectionCount(0) - gc0Start;
                st.GC1 = GC.CollectionCount(1) - gc1Start;
                st.GC2 = GC.CollectionCount(2) - gc2Start;
            }
            return st;
        }

        class Ctx : IDisposable {
            public MemoryMappedViewAccessor acc;
            public int currentTab = -1;
            public byte[] compBuf;
            public byte[] decompBuf;
            public int bufSize;
            bool owns = true;

            public Ctx(int bufSize) {
                this.bufSize = bufSize;
                compBuf = new byte[bufSize];
                decompBuf = new byte[bufSize];
            }

            public void Dispose() {
                try { acc?.Dispose(); } catch { }
                if (owns) { compBuf = null; decompBuf = null; owns = false; }
            }
        }

        static string ResolveTarget(string outDir, ulong hash, string filename) {
            string sub = null;
            try { if (PathResolver != null) sub = PathResolver(hash); } catch { }
            if (string.IsNullOrEmpty(sub)) return Path.Combine(outDir, filename);
            string dir = Path.Combine(outDir, sub);
            try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
            return Path.Combine(dir, filename);
        }

        static bool ExtractAndWrite(MemoryMappedViewAccessor acc, TabFormat.Tab t, TabFormat.Entry e, Ctx ctx, string outDir, OodleDec oodle) {
            EnsureNameResolver();
            try {
                if (e.BIdx == 0 && e.CSize == e.USize) {
                    int headLen = (int)Math.Min(16, e.USize);
                    var head = new byte[headLen];
                    acc.ReadArray((long)e.Offset, head, 0, headLen);
                    string ext = Extractor.DetectExt(head);
                    if (ext == "unknown") ext = LookupExtFromFilelist(e.Hash, ext);
                    string _fn = (NameResolver != null && !string.IsNullOrEmpty(NameResolver(e.Hash)) ? NameResolver(e.Hash) + "_" : "") + e.Hash.ToString("X16") + "." + ext;
                    string target = ResolveTarget(outDir, e.Hash, _fn);

                    using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, ctx.bufSize, FileOptions.SequentialScan)) {
                        long remaining = e.USize;
                        long pos = e.Offset;
                        var wbuf = ctx.compBuf;
                        while (remaining > 0) {
                            int chunk = (int)Math.Min(wbuf.Length, remaining);
                            acc.ReadArray(pos, wbuf, 0, chunk);
                            fs.Write(wbuf, 0, chunk);
                            pos += chunk; remaining -= chunk;
                        }
                    }
                    return true;
                }

                if (e.BIdx == 0) {
                    byte[] cbuf = (e.CSize <= ctx.compBuf.Length) ? ctx.compBuf : ArrayPool<byte>.Shared.Rent((int)e.CSize);
                    bool rentedBigC = (cbuf != ctx.compBuf);
                    byte[] dbuf = (e.USize <= ctx.decompBuf.Length) ? ctx.decompBuf : ArrayPool<byte>.Shared.Rent((int)e.USize);
                    bool rentedBigD = (dbuf != ctx.decompBuf);
                    try {
                        acc.ReadArray((long)e.Offset, cbuf, 0, (int)e.CSize);
                        if (!oodle(cbuf, 0, (int)e.CSize, dbuf, 0, (int)e.USize)) return false;
                        string ext = Extractor.DetectExt(dbuf);
                        if (ext == "unknown") ext = LookupExtFromFilelist(e.Hash, ext);
                        string _fn = (NameResolver != null && !string.IsNullOrEmpty(NameResolver(e.Hash)) ? NameResolver(e.Hash) + "_" : "") + e.Hash.ToString("X16") + "." + ext;
                        string target = ResolveTarget(outDir, e.Hash, _fn);
                        using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, ctx.bufSize, FileOptions.SequentialScan)) {
                            fs.Write(dbuf, 0, (int)e.USize);
                        }
                        return true;
                    } finally {
                        if (rentedBigC) ArrayPool<byte>.Shared.Return(cbuf);
                        if (rentedBigD) ArrayPool<byte>.Shared.Return(dbuf);
                    }
                }

                long ro = e.Offset;
                int bi = e.BIdx;
                long remTotal = e.USize;

                if (bi >= t.Blocks.Count) return false;
                uint bc = t.Blocks[bi].CSize, bu = t.Blocks[bi].USize;
                if (bc == 0xFFFFFFFF || bu == 0xFFFFFFFF) return false;

                byte[] cbuf1 = (bc <= ctx.compBuf.Length) ? ctx.compBuf : ArrayPool<byte>.Shared.Rent((int)bc);
                bool rentedBig1 = (cbuf1 != ctx.compBuf);
                byte[] dbuf1 = (bu <= ctx.decompBuf.Length) ? ctx.decompBuf : ArrayPool<byte>.Shared.Rent((int)bu);
                bool rentedBigD1 = (dbuf1 != ctx.decompBuf);
                try {
                    acc.ReadArray(ro, cbuf1, 0, (int)bc);
                    if (!oodle(cbuf1, 0, (int)bc, dbuf1, 0, (int)bu)) return false;
                    string ext = Extractor.DetectExt(dbuf1);
if (ext == "unknown") ext = LookupExtFromFilelist(e.Hash, ext);
                    string _fn = (NameResolver != null && !string.IsNullOrEmpty(NameResolver(e.Hash)) ? NameResolver(e.Hash) + "_" : "") + e.Hash.ToString("X16") + "." + ext;
                    string target = ResolveTarget(outDir, e.Hash, _fn);

                    using (var fs = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, ctx.bufSize, FileOptions.SequentialScan)) {
                        int take = (int)Math.Min(bu, remTotal);
                        fs.Write(dbuf1, 0, take);
                        ro += bc; bi++; remTotal -= take;

                        while (remTotal > 0 && bi < t.Blocks.Count) {
                            uint bc2 = t.Blocks[bi].CSize, bu2 = t.Blocks[bi].USize;
                            if (bc2 == 0xFFFFFFFF || bu2 == 0xFFFFFFFF) return false;
                            byte[] cbuf2 = (bc2 <= ctx.compBuf.Length) ? ctx.compBuf : ArrayPool<byte>.Shared.Rent((int)bc2);
                            bool rentedC2 = (cbuf2 != ctx.compBuf);
                            byte[] dbuf2 = (bu2 <= ctx.decompBuf.Length) ? ctx.decompBuf : ArrayPool<byte>.Shared.Rent((int)bu2);
                            bool rentedD2 = (dbuf2 != ctx.decompBuf);
                            try {
                                acc.ReadArray(ro, cbuf2, 0, (int)bc2);
                                if (!oodle(cbuf2, 0, (int)bc2, dbuf2, 0, (int)bu2)) return false;
                                int take2 = (int)Math.Min(bu2, remTotal);
                                fs.Write(dbuf2, 0, take2);
                                ro += bc2; bi++; remTotal -= take2;
                            } finally {
                                if (rentedC2) ArrayPool<byte>.Shared.Return(cbuf2);
                                if (rentedD2) ArrayPool<byte>.Shared.Return(dbuf2);
                            }
                        }
                        if (remTotal != 0) return false;
                    }
                    return true;
                } finally {
                    if (rentedBig1) ArrayPool<byte>.Shared.Return(cbuf1);
                    if (rentedBigD1) ArrayPool<byte>.Shared.Return(dbuf1);
                }
            } catch { return false; }
        }
            private static string LookupExtFromFilelist(ulong hash, string fallback)
        {
            try {
                if (_filelistCache == null) {
                    string dd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                    if (!Directory.Exists(dd)) dd = AppDomain.CurrentDomain.BaseDirectory;
                    LoadFilelistCache(dd);
                }
                string p;
                if (_filelistCache != null && _filelistCache.TryGetValue(hash, out p)) {
                    string ex = Path.GetExtension(p);
                    if (!string.IsNullOrEmpty(ex)) return ex.TrimStart('.');
                }
            } catch { }
            return fallback;
        }

        public static void SortOutputDirectory(string root)
        {
            if (!Directory.Exists(root)) return;
            var stack = new System.Collections.Generic.Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                string cur = stack.Pop();
                string[] fs;
                try { fs = Directory.GetFiles(cur); } catch { fs = new string[0]; }
                System.Array.Sort(fs, System.StringComparer.OrdinalIgnoreCase);
                foreach (string f in fs)
                    try { string t = f + ".ts"; File.Move(f, t); File.Move(t, f); } catch { }
                try { foreach (string s in Directory.GetDirectories(cur)) stack.Push(s); } catch { }
            }
        }

        public static void EnsureNameResolver()
        {
            if (NameResolver != null) return;
            try {
                string _dd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                if (!Directory.Exists(_dd)) _dd = AppDomain.CurrentDomain.BaseDirectory;
                var _fl = LoadFilelistCache(_dd);
                if (_fl.Count > 0) NameResolver = (h) => { string p; return _fl.TryGetValue(h, out p) ? SanitizeBasename(p) : null; };
            } catch { }
        }

        public static int ExtractMany(string gamePath, string outputDir, System.Collections.Generic.IEnumerable<string> hashHexes, Action<int, int, string> progress)
        {
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

            var wanted = new HashSet<ulong>();
            foreach (var h in hashHexes) {
                try { wanted.Add(Convert.ToUInt64(h, 16)); } catch { }
            }
            if (wanted.Count == 0) return 0;

            string initDir = Path.Combine(gamePath, "archives_win64", "initial");
            string suppDir = Path.Combine(gamePath, "archives_win64", "supplemental");
            var tabList = new List<string>();
            if (Directory.Exists(initDir)) tabList.AddRange(Directory.GetFiles(initDir, "*.tab", SearchOption.AllDirectories));
            if (Directory.Exists(suppDir)) tabList.AddRange(Directory.GetFiles(suppDir, "*.tab", SearchOption.AllDirectories));
            tabList.RemoveAll(p => p.Contains("\\languages\\"));

            var parsedTabs = new Dictionary<string, TabFormat.Tab>();
            var jobsByTab = new Dictionary<string, List<TabFormat.Entry>>();
            foreach (var tabPath in tabList) {
                TabFormat.Tab t;
                try { t = TabFormat.Tab.Parse(tabPath); } catch { continue; }
                var matches = new List<TabFormat.Entry>();
                foreach (var e in t.Entries) {
                    if (wanted.Contains(e.Hash)) matches.Add(e);
                }
                if (matches.Count > 0) {
                    parsedTabs[tabPath] = t;
                    jobsByTab[tabPath] = matches;
                }
            }

            // DIAG: log hashes no encontrados en ningun tab
            try {
                var foundH = new HashSet<ulong>();
                foreach (var kv in jobsByTab) { foreach (var e in kv.Value) foundH.Add(e.Hash); }
                var missing = new List<ulong>();
                foreach (var h in wanted) { if (!foundH.Contains(h)) missing.Add(h); }
                string diagPath = Path.Combine(outputDir, "_extract_fail_log.txt");
                using (var sw = new StreamWriter(diagPath, false)) {
                    sw.WriteLine("Wanted: " + wanted.Count + " | Found: " + foundH.Count + " | Missing: " + missing.Count);
                    foreach (var h in missing) sw.WriteLine(h.ToString("X16"));
                }
            } catch { }

            var oodle = new OodleDec(delegate(byte[] c, int co, int cl, byte[] d, int doff, int dl) {
                return Oodle.Decompress(c, co, cl, d, doff, dl);
            });

            int done = 0;
            int attempted = 0;
            int total = wanted.Count;
            int nThreads = Math.Min(Environment.ProcessorCount * 2, 32);

            foreach (var kv in jobsByTab) {
                string tabPath = kv.Key;
                string arcPath = Path.ChangeExtension(tabPath, ".arc");
                if (!File.Exists(arcPath)) continue;
                TabFormat.Tab t = parsedTabs[tabPath];
                var entries = kv.Value;

                long arcLen = new FileInfo(arcPath).Length;
                using (var mmf = MemoryMappedFile.CreateFromFile(arcPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read)) {
                    string stuckLog = Path.Combine(outputDir, "_extract_stuck.log");
                    var tasks = new List<System.Threading.Tasks.Task<bool>>();
                    var taskHashes = new List<ulong>();
                    var sem = new System.Threading.SemaphoreSlim(nThreads);
                    foreach (var e in entries) {
                        var entry = e;
                        var tabRef = t;
                        var tk = System.Threading.Tasks.Task.Run(async () => {
                            await sem.WaitAsync().ConfigureAwait(false);
                            try {
                                using (var acc = mmf.CreateViewAccessor(0, arcLen, MemoryMappedFileAccess.Read)) {
                                    var c = new Ctx(4 * 1024 * 1024);
                                    c.acc = acc;
                                    c.currentTab = 0;
                                    bool w = ExtractAndWrite(acc, tabRef, entry, c, outputDir, oodle);
                                    return w;
                                }
                            } catch (Exception ex) {
                                return false;
                            } finally {
                                try { sem.Release(); } catch { }
                            }
                        });
                        tasks.Add(tk);
                        taskHashes.Add(entry.Hash);
                    }
                    int budgetMs = 120000;
                    var swTab = Stopwatch.StartNew();
                    var activeIdx = Enumerable.Range(0, tasks.Count).ToList();
                    while (activeIdx.Count > 0) {
                        int rem = budgetMs - (int)swTab.ElapsedMilliseconds;
                        if (rem <= 0) break;
                        var arr = activeIdx.Select(i => tasks[i]).ToArray();
                        int idx;
                        try { idx = System.Threading.Tasks.Task.WaitAny(arr, Math.Min(2000, rem)); } catch { break; }
                        if (idx < 0) continue;
                        int realIdx = activeIdx[idx];
                        activeIdx.RemoveAt(idx);
                        bool ok = false;
                        try { ok = tasks[realIdx].Result; } catch { }
                        if (ok) System.Threading.Interlocked.Increment(ref done);
                        int n = System.Threading.Interlocked.Increment(ref attempted);
                    if (progress != null && (n % 20 == 0 || n == total)) progress(n, total, tabPath);
                    }
                    if (activeIdx.Count > 0) {
                        try {
                            using (var swl = new StreamWriter(stuckLog, true)) {
                                swl.WriteLine("[" + DateTime.Now.ToString("HH:mm:ss") + "] tab=" + Path.GetFileName(tabPath) + " stuck=" + activeIdx.Count + "/" + tasks.Count);
                                foreach (var i in activeIdx) swl.WriteLine("  " + taskHashes[i].ToString("X16"));
                            }
                        } catch { }
                    }
                }
            }
            return done;
        }

        public static bool ExtractSingle(string gamePath, string outputDir, string hashHex)
        {
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            ulong targetHash = 0;
            try { targetHash = Convert.ToUInt64(hashHex, 16); } catch { return false; }

            string initDir = Path.Combine(gamePath, "archives_win64", "initial");
            string suppDir = Path.Combine(gamePath, "archives_win64", "supplemental");
            var tabList = new List<string>();
            if (Directory.Exists(initDir)) tabList.AddRange(Directory.GetFiles(initDir, "*.tab", SearchOption.AllDirectories));
            if (Directory.Exists(suppDir)) tabList.AddRange(Directory.GetFiles(suppDir, "*.tab", SearchOption.AllDirectories));
            tabList.RemoveAll(p => p.Contains("\\languages\\"));

            foreach (var tabPath in tabList) {
                TabFormat.Tab t;
                try { t = TabFormat.Tab.Parse(tabPath); } catch { continue; }
                TabFormat.Entry found = null;
                for (int i = 0; i < t.Entries.Count; i++) {
                    if (t.Entries[i].Hash == targetHash) { found = t.Entries[i]; break; }
                }
                if (found == null) continue;

                string arcPath = Path.ChangeExtension(tabPath, ".arc");
                if (!File.Exists(arcPath)) continue;

                long arcLen = new FileInfo(arcPath).Length;
                using (var mmf = MemoryMappedFile.CreateFromFile(arcPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read))
                using (var acc = mmf.CreateViewAccessor(0, arcLen, MemoryMappedFileAccess.Read))
                using (var ctx = new Ctx(4 * 1024 * 1024)) {
                    ctx.acc = acc;
                    ctx.currentTab = 0;
                    var oodle = new OodleDec(delegate(byte[] c, int co, int cl, byte[] d, int doff, int dl) {
                        return Oodle.Decompress(c, co, cl, d, doff, dl);
                    });
                    return ExtractAndWrite(acc, t, found, ctx, outputDir, oodle);
                }
            }
            return false;
        }
    }
}
