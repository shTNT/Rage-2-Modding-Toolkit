// RepackerCore.cs v5 - instrumentacion por fase + write paralelo por segmentos.
//
// Cambios v5 respecto v4:
//  - Instrumentacion por fase: PlanMs, CompressMs, AllocMs, WriteMs, CommitMs.
//  - AutoThreads mejorado: log de la decision, cap por RAM mas agresivo.
//  - Skip pipeline si jobs==0 (arregla overhead E4 de ~750ms).
//  - Write paralelo por segmentos con pre-allocacion (FileStream.SetLength).
//  - Determinismo preservado: los offsets finales se calculan en Fase 1.
//
// Estrategia de write paralelo:
//  - Fase 1 calcula offset final de cada entry y cada bloque output.
//  - Fase 1.5: SetLength(totalSize) sobre .arc.tmp (pre-alloc).
//  - Fase 3: N segmentos sobre entries consecutivos. Parallel.For:
//      - copia bytes de entries no-reemplazadas (mmap read -> RandomAccess.Write)
//      - escribe bloques de entries reemplazadas (buffer pre-comprimido -> RandomAccess.Write)
//  - Fase 4: fs.Flush(true) + commit.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Rage2Toolkit
{
    public static class RepackerCore
    {
        public sealed class Stats
        {
            public int EntriesTotal;
            public int EntriesReplaced;
            public int EntriesCopied;
            public int OriginalBlocks;
            public int OrphansDropped;
            public int NewBlocks;
            public int TotalBlocks;
            public int ThreadsUsed;
            public long BytesWritten;
            public long TimeMs;
            public long PlanMs;
            public long CompressMs;
            public long AllocMs;
            public long WriteMs;
            public long CommitMs;
            public string AutoThreadsReason;
        }

        private const int ALIGN = 0x1000;
        private const byte PAD = 0x30;
        private const int DEFAULT_WRITE_THREADS = 8;

        private sealed class BlockJob
        {
            public int TargetBlockIdx;
            public byte[] Payload;
            public long RawSize;
        }

        // Segmento de write: entries consecutivos [StartIdx, EndIdx)
        private sealed class WriteSegment
        {
            public int StartIdx;
            public int EndIdx;
        }

        private static int AutoThreads(int overrideThreads, out string reason)
        {
            if (overrideThreads > 0) { reason = "override"; return Math.Min(overrideThreads, 32); }
            int cpus = Environment.ProcessorCount;
            long ramBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
            long ramGB = ramBytes / (1024L * 1024 * 1024);
            int byCpu = cpus;
            int byRam = ramGB >= 32 ? 32 : ramGB >= 16 ? 16 : ramGB >= 8 ? 8 : 4;
            int result = Math.Max(1, Math.Min(byCpu, Math.Min(byRam, 32)));
            reason = "cpu=" + cpus + " ram=" + ramGB + "GB -> " + result;
            return result;
        }

        public static Stats Rebuild(
            string tabPath, string arcPath,
            Dictionary<ulong, byte[]> replacements,
            string outTabPath, string outArcPath,
            Action<string> log,
            int oodleLevel = 1,
            int overrideThreads = 0,
            int overrideWriteThreads = 0,
            int overrideBufMB = 0)
        {
            Action<string> L = log ?? (_ => { });
            long lastEntryEnd = 0;
            long finalPadBytes = 0;
            var sw = Stopwatch.StartNew();
            var st = new Stats();
            replacements = replacements ?? new Dictionary<ulong, byte[]>();
            int threads = AutoThreads(overrideThreads, out string reason);
            st.ThreadsUsed = threads;
            st.AutoThreadsReason = reason;

            L("[core v5.2] === START ===");
            var t = TabFormat.Tab.Parse(tabPath);
            int UncompressedBlockSize = (int)t.UncompressedBlockSize;
            bool hasHeaders = UncompressedBlockSize > 0;

            L("[core v5.2] tab=" + t.FileCount + " entries, " + t.BlockCount + " blocks, F1C=0x" + UncompressedBlockSize.ToString("X") + " headers=" + hasHeaders);
            L("[core v5.2] replacements=" + replacements.Count + " oodle=" + Oodle.IsLoaded + " threads=" + threads + " (" + reason + ") level=" + oodleLevel);

            if (!Oodle.IsLoaded && replacements.Count > 0)
                throw new InvalidOperationException("Oodle no cargado - imposible escribir bloques");

            using var mmap = MemoryMappedFile.CreateFromFile(arcPath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var view = mmap.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

            string tmpArc = outArcPath + ".tmp";
            string tmpTab = outTabPath + ".tmp";

            // ============================================================
            // FASE 1: PLAN (single-thread, calcula offset final de todo)
            // ============================================================
            var planSw = Stopwatch.StartNew();

            bool sentinel0 = (t.Blocks.Count > 0 && t.Blocks[0].CSize == 0xFFFFFFFF && t.Blocks[0].USize == 0xFFFFFFFF);
            bool sentinel1 = (t.Blocks.Count > 1 && t.Blocks[1].CSize == 0xFFFFFFFF && t.Blocks[1].USize == 0xFFFFFFFF);

            var newBlocks = new List<TabFormat.Block>((int)t.BlockCount + 256);
            if (sentinel0) newBlocks.Add(new TabFormat.Block { CSize = 0xFFFFFFFF, USize = 0xFFFFFFFF, Sentinel = true });
            if (sentinel1) newBlocks.Add(new TabFormat.Block { CSize = 0xFFFFFFFF, USize = 0xFFFFFFFF, Sentinel = true });
            int headerBlocks = newBlocks.Count;

            var sorted = t.Entries.OrderBy(e => e.Offset).ToList();
            var newBIdx = new Dictionary<int, int>();
            var jobs = new List<BlockJob>();
            var replacementsInOrder = new Dictionary<ulong, int>();
            int totalCopiedBlocks = 0;

            // Plan de escritura
            var outEntries = new List<TabFormat.Entry>((int)t.FileCount);
            var outOffsets = new long[sorted.Count]; // offset final de cada entry
            var blockOutOffset = new Dictionary<int, long>(); // blockIdx -> offset final en .arc
            long currentOffset = 0;
            long[] padAfter = new long[sorted.Count]; // padding antes de cada entry

            for (int si = 0; si < sorted.Count; si++)
            {
                var e = sorted[si];
                long aligned = AlignUp(currentOffset, ALIGN);
                padAfter[si] = aligned - currentOffset;
                currentOffset = aligned;
                outOffsets[si] = currentOffset;

                var newE = e.Clone();
                newE.Offset = (uint)currentOffset;

                if (replacements.TryGetValue(e.Hash, out var payload))
                {
                    if (payload == null || payload.Length == 0)
                        throw new ArgumentException("empty replacement for " + e.Hash.ToString("X16"));

                    int firstBlockIdx = newBlocks.Count;
                    replacementsInOrder[e.Hash] = firstBlockIdx;

                    if (!hasHeaders)
                    {
                        // F1C=0: write raw, sin bloques
                        currentOffset += payload.Length;
                        newE.BIdx = 0;
                        newE.CType = 0;
                        newE.CFlags = 0;
                        newE.CSize = (uint)payload.Length;
                        newE.USize = (uint)payload.Length;
                        newBIdx[si] = 0;
                    }
                    else if (payload.Length <= UncompressedBlockSize)
                    {
                        newBlocks.Add(new TabFormat.Block { CSize = 0, USize = (uint)payload.Length, Sentinel = false });
                        jobs.Add(new BlockJob { TargetBlockIdx = firstBlockIdx, Payload = payload, RawSize = payload.Length });
                        newBIdx[si] = firstBlockIdx;
                        newE.BIdx = (ushort)firstBlockIdx;
                        newE.CType = 4;
                        newE.CFlags = 1;
                        newE.CSize = 0; // se rellena tras compress
                        newE.USize = (uint)payload.Length;
                    }
                    else
                    {
                        int off = 0;
                        while (off < payload.Length)
                        {
                            int len = Math.Min(UncompressedBlockSize, payload.Length - off);
                            byte[] chunk = new byte[len];
                            Buffer.BlockCopy(payload, off, chunk, 0, len);
                            int thisBlockIdx = newBlocks.Count;
                            newBlocks.Add(new TabFormat.Block { CSize = 0, USize = (uint)len, Sentinel = false });
                            jobs.Add(new BlockJob { TargetBlockIdx = thisBlockIdx, Payload = chunk, RawSize = len });
                            off += len;
                        }
                        newBIdx[si] = firstBlockIdx;
                        newE.BIdx = (ushort)firstBlockIdx;
                        newE.CType = 4;
                        newE.CFlags = 1;
                        newE.CSize = 0;
                        newE.USize = (uint)payload.Length;
                    }
                }
                else
                {
                    if (e.BIdx == 0 && e.CType == 0 && e.CSize == e.USize)
                    {
                        newBIdx[si] = 0;
                        currentOffset += e.CSize;
                    }
                    else if (e.BIdx >= 1 && e.BIdx < t.Blocks.Count)
                    {
                        int firstNew = newBlocks.Count;
                        long remain = e.USize;
                        int oldIdx = e.BIdx;
                        int iter = 0;
                        while (remain > 0 && oldIdx < t.Blocks.Count && iter < 10000)
                        {
                            var ob = t.Blocks[oldIdx];
                            if (ob.CSize == 0xFFFFFFFF) break;
                            newBlocks.Add(new TabFormat.Block { CSize = ob.CSize, USize = ob.USize, Sentinel = false });
                            remain -= ob.USize;
                            oldIdx++;
                            iter++;
                            totalCopiedBlocks++;
                        }
                        newBIdx[si] = firstNew;
                        newE.BIdx = (ushort)firstNew;
                        currentOffset += e.CSize;
                    }
                    else
                    {
                        newBIdx[si] = 0;
                        currentOffset += e.CSize;
                    }
                    newE.CType = e.CType;
                    newE.CFlags = e.CFlags;
                }

                outEntries.Add(newE);
            }

            // Padding final: bytes de 0x30 entre el ultimo entry y el proximo 0x1000.
            lastEntryEnd = currentOffset;
            long totalSizeAligned = AlignUp(lastEntryEnd, ALIGN);
            finalPadBytes = totalSizeAligned - lastEntryEnd;

            planSw.Stop();
            st.PlanMs = planSw.ElapsedMilliseconds;
            L("[core v5.2] plan: blocks=" + newBlocks.Count + " jobs=" + jobs.Count + " totalSize=" + totalSizeAligned + " planTime=" + st.PlanMs + "ms");

            // ============================================================
            // FASE 2: COMPRESS (parallel) - solo si hay jobs
            // ============================================================
            var compSw = Stopwatch.StartNew();
            byte[][] compressed = null;

            if (jobs.Count > 0)
            {
                compressed = new byte[jobs.Count][];
                int parErrors = 0;
                Exception parEx = null;
                try
                {
                    Parallel.For(0, jobs.Count,
                        new ParallelOptions { MaxDegreeOfParallelism = threads },
                        i =>
                        {
                            try
                            {
                                compressed[i] = Oodle.Compress(jobs[i].Payload, Oodle.KRAKEN, oodleLevel);
                                if (compressed[i] == null || compressed[i].Length == 0)
                                    throw new InvalidOperationException("Oodle devolvio vacio");
                            }
                            catch (Exception ex)
                            {
                                Interlocked.Increment(ref parErrors);
                                if (parEx == null) parEx = ex;
                            }
                        });
                }
                catch (Exception ex) { parEx = ex; }

                if (parErrors > 0)
                    throw new InvalidOperationException("Compress fallo en " + parErrors + " jobs: " + parEx?.Message);

                // Actualizar CSize de bloques y de entries
                for (int i = 0; i < jobs.Count; i++)
                {
                    newBlocks[jobs[i].TargetBlockIdx].CSize = (uint)compressed[i].Length;
                }
                // Recalcular CSize/USize de entries reemplazadas y offsets finales
                // Los offsets finales cambian porque los bloques comprimidos ocupan distinto que lo reservado en Fase 1.
                // Rehacer el plan con los tamanios reales.
                long realOffset = 0;
                for (int si = 0; si < sorted.Count; si++)
                {
                    var orig = sorted[si];
                    long aligned = AlignUp(realOffset, ALIGN);
                    padAfter[si] = aligned - realOffset;
                    realOffset = aligned;
                    outOffsets[si] = realOffset;
                    outEntries[si].Offset = (uint)realOffset;

                    if (replacements.TryGetValue(orig.Hash, out var payload))
                    {
                        if (!hasHeaders)
                        {
                            realOffset += payload.Length;
                            outEntries[si].CSize = (uint)payload.Length;
                        }
                        else
                        {
                            int firstBIdx = replacementsInOrder[orig.Hash];
                            long totalCSize = 0;
                            long remain = payload.Length;
                            int bi = firstBIdx;
                            while (remain > 0 && bi < newBlocks.Count)
                            {
                                var nb = newBlocks[bi];
                                if (nb.CSize == 0xFFFFFFFF) break;
                                totalCSize += nb.CSize;
                                remain -= nb.USize;
                                bi++;
                            }
                            realOffset += totalCSize;
                            outEntries[si].CSize = (uint)totalCSize;
                        }
                    }
                    else
                    {
                        realOffset += orig.CSize;
                    }
                }
                lastEntryEnd = realOffset;
                totalSizeAligned = AlignUp(lastEntryEnd, ALIGN);
                finalPadBytes = totalSizeAligned - lastEntryEnd;
            }
            compSw.Stop();
            st.CompressMs = compSw.ElapsedMilliseconds;
            L("[core v5.2] compress: jobs=" + jobs.Count + " time=" + st.CompressMs + "ms totalSize(final)=" + totalSizeAligned);

            // ============================================================
            // FASE 3: ALLOC + WRITE (paralelo por segmentos)
            // ============================================================
            var allocSw = Stopwatch.StartNew();

            // FIX v5.1: NO usar SetLength pre-alloc. Windows a veces lo trata como
            // reserva fisica (no sparse) y con .arc grandes + disco apretado falla.
            // Alternativa: crear el archivo vacio, RandomAccess.Write lo extiende
            // conforme escribe a offsets mayores. Fragmentacion minima en NVMe.
            using (var fsInit = new FileStream(tmpArc, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.None))
            {
                fsInit.WriteByte(0); // 1 byte para forzar tamaño minimo
                // fsInit.Flush(true) quitado: fsync varible sin valor, el commit final ya sincroniza
            }
            allocSw.Stop();
            st.AllocMs = allocSw.ElapsedMilliseconds;
            L("[core v5.2] init: size_target=" + totalSizeAligned + " time=" + st.AllocMs + "ms");

            // Escribir padding + datos con RandomAccess.Write en paralelo
            var writeSw = Stopwatch.StartNew();

            // Preparar buffer de padding
            var padBuf = new byte[ALIGN];
            for (int i = 0; i < padBuf.Length; i++) padBuf[i] = PAD;

            // Determinar threads de write: min(overrideWriteThreads o DEFAULT, threads)
            int writeThreads = overrideWriteThreads > 0 ? overrideWriteThreads : Math.Min(DEFAULT_WRITE_THREADS, threads);
            if (writeThreads < 1) writeThreads = 1;

            // Dividir entries en segmentos
            var segments = new List<WriteSegment>();
            int segCount = Math.Min(writeThreads, sorted.Count);
            int segSize = (sorted.Count + segCount - 1) / segCount;
            for (int i = 0; i < sorted.Count; i += segSize)
            {
                segments.Add(new WriteSegment { StartIdx = i, EndIdx = Math.Min(i + segSize, sorted.Count) });
            }

            using (var handle = File.OpenHandle(tmpArc, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                // Diccionario blockIdx -> offset final del bloque (calculado ahora)
                // Necesario para saber donde va cada bloque comprimido
                var blockFinalOffset = new Dictionary<int, long>();
                // Iterar entries y bloques para asignar offsets
                // (usar el mismo orden que el plan real)
                // El primer bloque de cada replacement esta en replacementsInOrder.
                // Los demas bloques van contiguos tras el primero.
                foreach (var kv in replacementsInOrder)
                {
                    // Encontrar entry con ese hash
                    for (int si = 0; si < sorted.Count; si++)
                    {
                        if (sorted[si].Hash == kv.Key)
                        {
                            long entryOff = outOffsets[si];
                            int bi = kv.Value;
                            long remain = replacements[kv.Key].Length;
                            long blockOff = entryOff;
                            while (remain > 0 && bi < newBlocks.Count)
                            {
                                blockFinalOffset[bi] = blockOff;
                                var nb = newBlocks[bi];
                                if (nb.CSize == 0xFFFFFFFF) break;
                                blockOff += nb.CSize;
                                remain -= nb.USize;
                                bi++;
                            }
                            break;
                        }
                    }
                }

                var writeErrors = new System.Collections.Concurrent.ConcurrentBag<string>();
                try
                {
                    Parallel.ForEach(segments,
                        new ParallelOptions { MaxDegreeOfParallelism = writeThreads },
                        seg =>
                        {
                            try
                            {
                                int bufBytes = (overrideBufMB > 0 ? overrideBufMB : 8) * 1024 * 1024;
                                var buf = new byte[bufBytes]; // buffer por thread
                                for (int si = seg.StartIdx; si < seg.EndIdx; si++)
                                {
                                    var orig = sorted[si];

                                    // Padding antes del entry
                                    if (padAfter[si] > 0)
                                    {
                                        int pl = (int)padAfter[si];
                                        while (pl > 0)
                                        {
                                            int c = Math.Min(buf.Length, pl);
                                            Array.Fill(buf, PAD, 0, c);
                                            RandomAccess.Write(handle, new ReadOnlySpan<byte>(buf, 0, c), outOffsets[si] - padAfter[si] + (padAfter[si] - pl));
                                            pl -= c;
                                        }
                                    }

                                    if (replacements.TryGetValue(orig.Hash, out var payload))
                                    {
                                        if (!hasHeaders)
                                        {
                                            // F1C=0: escribir payload crudo
                                            long o = outOffsets[si];
                                            int remaining = payload.Length;
                                            int srcOff = 0;
                                            while (remaining > 0)
                                            {
                                                int c = Math.Min(buf.Length, remaining);
                                                Buffer.BlockCopy(payload, srcOff, buf, 0, c);
                                                RandomAccess.Write(handle, new ReadOnlySpan<byte>(buf, 0, c), o);
                                                o += c; srcOff += c; remaining -= c;
                                            }
                                        }
                                        else
                                        {
                                            // Cadena de bloques comprimidos
                                            int firstBIdx = replacementsInOrder[orig.Hash];
                                            int bi = firstBIdx;
                                            long remain = payload.Length;
                                            while (remain > 0 && bi < newBlocks.Count && blockFinalOffset.ContainsKey(bi))
                                            {
                                                int jobIdx = -1;
                                                for (int ji = 0; ji < jobs.Count; ji++)
                                                    if (jobs[ji].TargetBlockIdx == bi) { jobIdx = ji; break; }
                                                if (jobIdx < 0) throw new InvalidOperationException("job no encontrado para bloque " + bi);
                                                var comp = compressed[jobIdx];
                                                long o = blockFinalOffset[bi];
                                                int remaining = comp.Length;
                                                int srcOff = 0;
                                                while (remaining > 0)
                                                {
                                                    int c = Math.Min(buf.Length, remaining);
                                                    Buffer.BlockCopy(comp, srcOff, buf, 0, c);
                                                    RandomAccess.Write(handle, new ReadOnlySpan<byte>(buf, 0, c), o);
                                                    o += c; srcOff += c; remaining -= c;
                                                }
                                                var nb = newBlocks[bi];
                                                remain -= nb.USize;
                                                bi++;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Copia raw de mmap a .arc.tmp
                                        if (orig.CSize > 0)
                                        {
                                            long srcOff = orig.Offset;
                                            long dstOff = outOffsets[si];
                                            long remaining = orig.CSize;
                                            while (remaining > 0)
                                            {
                                                int c = (int)Math.Min(buf.Length, remaining);
                                                view.ReadArray(srcOff, buf, 0, c);
                                                RandomAccess.Write(handle, new ReadOnlySpan<byte>(buf, 0, c), dstOff);
                                                srcOff += c; dstOff += c; remaining -= c;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                writeErrors.Add(ex.Message);
                            }
                        });
                }
                catch (Exception ex) { writeErrors.Add(ex.Message); }

                if (writeErrors.Count > 0)
                    throw new InvalidOperationException("Write fallo: " + string.Join("; ", writeErrors.Take(3)));

                // FIX v5.2: escribir padding final 0x30 despues del ultimo entry.
                // Antes faltaba esta escritura -> ultimos bytes quedaban a 0 en vez de 0x30.
                if (finalPadBytes > 0)
                {
                    byte[] fpad = new byte[finalPadBytes];
                    for (int i = 0; i < fpad.Length; i++) fpad[i] = PAD;
                    RandomAccess.Write(handle, new ReadOnlySpan<byte>(fpad, 0, fpad.Length), lastEntryEnd);
                    L("[core v5.2] final pad: " + finalPadBytes + " bytes at offset " + lastEntryEnd);
                }
            }
            writeSw.Stop();
            st.WriteMs = writeSw.ElapsedMilliseconds;
            L("[core v5.2] write: segments=" + segments.Count + " threads=" + writeThreads + " bufMB=" + (overrideBufMB > 0 ? overrideBufMB : 8) + " time=" + st.WriteMs + "ms");

            // ============================================================
            // FIX v5.3: TAB 3.1 - dos quirks del engine.
            // (1) Block table debe terminar con sentinel FFFFFFFF FFFFFFFF.
            //     block_count INCLUYE el sentinel. Sin el, el motor calcula
            //     file_table_offset = 0x20 + block_count * 8 desfasado 8 bytes
            //     y lee garbage -> game hang on load.
            // (2) File entries deben estar ordenadas por hash asc. El engine
            //     hace binary search sobre la file table. Sin ordenar, no
            //     encuentra nada -> game hang.
            // Ambas corregidas: verificacion = round-trip puro produce .tab
            // byte-identico al original (SHA256 match).
            // ============================================================
            bool origHasTailSentinel = (t.Blocks.Count > 0 &&
                t.Blocks[t.Blocks.Count - 1].CSize == 0xFFFFFFFF &&
                t.Blocks[t.Blocks.Count - 1].USize == 0xFFFFFFFF);
            if (origHasTailSentinel)
            {
                newBlocks.Add(new TabFormat.Block { CSize = 0xFFFFFFFF, USize = 0xFFFFFFFF, Sentinel = true });
                L("[core v5.3] tail sentinel appended (block_count -> " + newBlocks.Count + ")");
            }
            outEntries = outEntries.OrderBy(e => e.Hash).ToList();
            L("[core v5.3] entries sorted by hash asc (" + outEntries.Count + " entries)");

            // Escribir .tab
            var newTab = new TabFormat.Tab
            {
                Major = t.Major, Minor = t.Minor, Alignment = t.Alignment,
                FileCount = (uint)outEntries.Count, BlockCount = (uint)newBlocks.Count,
                Padding = t.Padding, MaxCompressedBlockSize = t.MaxCompressedBlockSize, UncompressedBlockSize = t.UncompressedBlockSize,
                Blocks = newBlocks, Entries = outEntries,
            };
            File.WriteAllBytes(tmpTab, newTab.Serialize());

            st.OriginalBlocks = (int)t.BlockCount;
            st.OrphansDropped = (int)t.BlockCount - headerBlocks - totalCopiedBlocks;
            st.NewBlocks = jobs.Count;
            st.TotalBlocks = newBlocks.Count;
            st.EntriesTotal = sorted.Count;
            st.EntriesReplaced = replacements.Count;
            st.EntriesCopied = sorted.Count - replacements.Count;
            st.BytesWritten = totalSizeAligned;

            // ============================================================
            // FASE 4: COMMIT
            // ============================================================
            var commitSw = Stopwatch.StartNew();
            bool inPlace = string.Equals(Path.GetFullPath(arcPath), Path.GetFullPath(outArcPath), StringComparison.OrdinalIgnoreCase);
            if (inPlace)
            {
                string bakArc = arcPath + ".original";
                string bakTab = tabPath + ".original";
                if (!File.Exists(bakArc)) File.Copy(arcPath, bakArc);
                if (!File.Exists(bakTab)) File.Copy(tabPath, bakTab);
                ReplaceWithRetry(tmpArc, arcPath);
                ReplaceWithRetry(tmpTab, tabPath);
            }
            else
            {
                if (File.Exists(outArcPath)) File.Delete(outArcPath);
                if (File.Exists(outTabPath)) File.Delete(outTabPath);
                File.Move(tmpArc, outArcPath);
                File.Move(tmpTab, outTabPath);
            }
            commitSw.Stop();
            st.CommitMs = commitSw.ElapsedMilliseconds;

            sw.Stop();
            st.TimeMs = sw.ElapsedMilliseconds;

            L("[core v5.2] === DONE ===");
            L("[core v5.2] entries=" + st.EntriesTotal + " replaced=" + st.EntriesReplaced + " copied=" + st.EntriesCopied);
            L("[core v5.2] blocks orig=" + st.OriginalBlocks + " orphans=" + st.OrphansDropped + " new=" + st.NewBlocks + " total=" + st.TotalBlocks);
            L("[core v5.2] phases: plan=" + st.PlanMs + "ms compress=" + st.CompressMs + "ms alloc=" + st.AllocMs + "ms write=" + st.WriteMs + "ms commit=" + st.CommitMs + "ms");
            L("[core v5.2] wall=" + st.TimeMs + "ms bytes=" + st.BytesWritten + " threads=" + threads);
            return st;
        }

        private static long AlignUp(long v, long a) => ((v + a - 1) / a) * a;

        private static void ReplaceWithRetry(string source, string dest)
        {
            for (int i = 0; i < 5; i++)
            {
                try { File.Replace(source, dest, null); return; }
                catch (IOException) when (i < 4) { Thread.Sleep(200 * (i + 1)); }
            }
        }
    }
}


