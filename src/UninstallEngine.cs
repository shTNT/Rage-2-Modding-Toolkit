using System;
using System.Collections.Generic;
using System.IO;

namespace Rage2Toolkit
{
    public class UninstallResult
    {
        public bool Success { get; set; }
        public string ModId { get; set; }
        public string Error { get; set; }
        public List<string> RestoredArcs { get; set; } = new List<string>();
        public List<string> RebuiltArcs { get; set; } = new List<string>();
        public int ModsReplayed { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class UninstallEngine
    {
        private static string ArcOriginalToTabOriginal(string arcOriginal)
        {
            const string suffix = ".arc.original";
            if (arcOriginal.EndsWith(suffix))
                return arcOriginal.Substring(0, arcOriginal.Length - suffix.Length) + ".tab.original";
            return arcOriginal + ".tab.original";
        }

        public static UninstallResult Uninstall(string modId, string gamePath, string toolkitRoot, Action<string> log, bool allowDrift = false)
        {
            var res = new UninstallResult();
            if (log == null) log = delegate(string s) { };
            res.ModId = modId;

            if (!Directory.Exists(gamePath)) { res.Error = "game path not found: " + gamePath; log("[uninstall] FAIL: " + res.Error); return res; }
            var archivesRoot = Path.Combine(gamePath, "archives_win64");
            if (!Directory.Exists(archivesRoot)) { res.Error = "archives_win64 not found"; log("[uninstall] FAIL: " + res.Error); return res; }

            var toRemove = ModsStore.FindById(toolkitRoot, modId);
            if (toRemove == null) { res.Error = "mod not found in registry: " + modId; log("[uninstall] FAIL: " + res.Error); return res; }
            log("[uninstall] mod: " + toRemove.Name + " v" + toRemove.Version);

            var affectedArcs = new List<string>();
            if (toRemove.AffectedArcs != null) affectedArcs.AddRange(toRemove.AffectedArcs);

            // PRE-CHECK DRIFT: verificar TODOS los arcs antes de tocar el registry.
            // Si hay drift y no allowDrift -> abortamos SIN haber modificado nada.
            foreach (var arcRel in affectedArcs)
            {
                var parts = arcRel.Split('/');
                if (parts.Length != 2) continue;
                var arcPathChk = Path.Combine(archivesRoot, parts[0], parts[1] + ".arc");
                if (!File.Exists(arcPathChk)) continue;
                string recordedChk = ModsStore.GetLastWritten(toolkitRoot, arcRel);
                if (string.IsNullOrEmpty(recordedChk)) continue;
                string currentChk;
                try { currentChk = ModsStore.Sha256Of(arcPathChk); } catch { currentChk = null; }
                if (string.IsNullOrEmpty(currentChk)) continue;
                if (!string.Equals(currentChk, recordedChk, StringComparison.OrdinalIgnoreCase))
                {
                    if (!allowDrift)
                    {
                        res.Error = "drift on " + arcRel + ": .arc modificado por fuera del ModManager. Uninstall abortado para no perder cambios. Usa --force para override.";
                        log("[uninstall] FAIL: " + res.Error);
                        return res;
                    }
                    log("[uninstall] WARN: drift ignored (--force) on " + arcRel + " -- cambios externos se perderan");
                }
            }

            if (!ModsStore.RemoveById(toolkitRoot, modId))
            {
                res.Error = "failed to remove mod from registry";
                log("[uninstall] FAIL: " + res.Error);
                return res;
            }
            log("[uninstall] removed from registry, " + affectedArcs.Count + " arcs affected");

            foreach (var arcRel in affectedArcs)
            {
                log("[uninstall] processing arc: " + arcRel);
                var parts = arcRel.Split('/');
                if (parts.Length != 2) { res.Warnings.Add("bad arc path: " + arcRel); continue; }
                var arcDir = Path.Combine(archivesRoot, parts[0]);
                var arcBase = parts[1];
                var tabPath = Path.Combine(arcDir, arcBase + ".tab");
                var arcPath = Path.Combine(arcDir, arcBase + ".arc");

                var remaining = ModsStore.FindByArc(toolkitRoot, arcRel);

                if (remaining.Count == 0)
                {
                    var bak = ModsStore.GetBackupByArc(toolkitRoot, arcRel);
                    if (bak == null) { res.Warnings.Add("no backup for " + arcRel); log("[uninstall] WARN: no backup"); continue; }
                    var bakArc = bak.BackupPath;
                    var bakTab = ArcOriginalToTabOriginal(bakArc);
                    if (!File.Exists(bakArc)) { res.Warnings.Add("backup missing: " + bakArc); log("[uninstall] WARN: " + bakArc); continue; }

                    try
                    {
                        if (File.Exists(bakTab))
                        {
                            File.Copy(bakTab, tabPath, true);
                            File.Copy(bakArc, arcPath, true);
                        }
                        else
                        {
                            File.Copy(bakArc, arcPath, true);
                        }
                        res.RestoredArcs.Add(arcRel);
                        log("[uninstall] restored from backup: " + arcRel);
                        try { if (File.Exists(arcPath)) ModsStore.SetLastWrittenBatch(toolkitRoot, new Dictionary<string, string> { { arcRel, ModsStore.Sha256Of(arcPath) } }); } catch { }
                    }
                    catch (Exception ex)
                    {
                        res.Error = "restore failed for " + arcRel + ": " + ex.Message;
                        log("[uninstall] FAIL: " + res.Error);
                        return res;
                    }
                }
                else
                {
                    log("[uninstall] rebuild: " + remaining.Count + " mods still touch " + arcRel);
                    var replacements = new Dictionary<ulong, byte[]>();
                    var sorted = new List<InstalledMod>(remaining);
                    sorted.Sort(delegate(InstalledMod a, InstalledMod b) { return a.Priority.CompareTo(b.Priority); });

                    foreach (var m in sorted)
                    {
                        if (string.IsNullOrEmpty(m.LibraryFile) || !File.Exists(m.LibraryFile))
                        {
                            res.Warnings.Add("library missing: " + m.Id);
                            continue;
                        }
                        try
                        {
                            using (var fs = new FileStream(m.LibraryFile, FileMode.Open, FileAccess.Read))
                            using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                            {
                                foreach (var entry in zip.Entries)
                                {
                                    if (entry.FullName == "mod.json") continue;
                                    var name = Path.GetFileName(entry.FullName);
                                    var noExt = Path.GetFileNameWithoutExtension(name);
                                    var hash = ModValidator.ParseHash(noExt);
                                    if (string.IsNullOrEmpty(hash)) continue;
                                    ulong h = ulong.Parse(hash, System.Globalization.NumberStyles.HexNumber);
                                    if (replacements.ContainsKey(h)) continue;
                                    var buf = new byte[entry.Length];
                                    using (var s = entry.Open())
                                    {
                                        int off = 0;
                                        while (off < buf.Length)
                                        {
                                            int rd = s.Read(buf, off, buf.Length - off);
                                            if (rd <= 0) break;
                                            off += rd;
                                        }
                                    }
                                    replacements[h] = buf;
                                }
                            }
                            res.ModsReplayed++;
                        }
                        catch (Exception ex)
                        {
                            res.Warnings.Add("replay fail " + m.Id + ": " + ex.Message);
                        }
                    }

                    if (replacements.Count == 0)
                    {
                        res.Warnings.Add("no replacements for " + arcRel);
                        continue;
                    }

                    // Ensure Oodle is loaded (needed by RepackerCore.Rebuild)
                    if (!Oodle.IsLoaded)
                    {
                        log("[uninstall] loading Oodle...");
                        string oodlePath = null;
                        string directDll = Path.Combine(gamePath, "oo2core_7_win64.dll");
                        if (File.Exists(directDll)) oodlePath = directDll;
                        else
                        {
                            try
                            {
                                var found = Directory.GetFiles(gamePath, "oo2core_7_win64.dll", SearchOption.AllDirectories);
                                if (found.Length > 0) oodlePath = found[0];
                            }
                            catch { }
                        }
                        if (string.IsNullOrEmpty(oodlePath))
                        {
                            res.Error = "oo2core_7_win64.dll not found in game path: " + gamePath;
                            log("[uninstall] FAIL: " + res.Error);
                            return res;
                        }
                        log("[uninstall] oodle path: " + oodlePath);
                        if (!Oodle.TryLoad(oodlePath))
                        {
                            res.Error = "Oodle load failed: " + Oodle.LastError;
                            log("[uninstall] FAIL: " + res.Error);
                            return res;
                        }
                        log("[uninstall] Oodle loaded OK");
                    }
                    var tmpTab = tabPath + ".uninstall_tmp.tab";
                    var tmpArc = arcPath + ".uninstall_tmp.arc";
                    try
                    {
                        RepackerCore.Rebuild(tabPath, arcPath, replacements, tmpTab, tmpArc, log);
                    }
                    catch (Exception ex)
                    {
                        res.Error = "rebuild failed for " + arcRel + ": " + ex.Message;
                        log("[uninstall] FAIL: " + res.Error);
                        try { if (File.Exists(tmpTab)) File.Delete(tmpTab); } catch { }
                        try { if (File.Exists(tmpArc)) File.Delete(tmpArc); } catch { }
                        return res;
                    }

                    try
                    {
                        CommitReplace(tmpTab, tabPath);
                        CommitReplace(tmpArc, arcPath);
                        res.RebuiltArcs.Add(arcRel);
                        log("[uninstall] rebuilt: " + arcRel);
                        try { if (File.Exists(arcPath)) ModsStore.SetLastWrittenBatch(toolkitRoot, new Dictionary<string, string> { { arcRel, ModsStore.Sha256Of(arcPath) } }); } catch { }
                    }
                    catch (Exception ex)
                    {
                        res.Error = "commit failed for " + arcRel + ": " + ex.Message;
                        log("[uninstall] FAIL: " + res.Error);
                        return res;
                    }
                }
            }

            var store2 = ModsStore.Load(toolkitRoot);
            var orphans = new List<BackupEntry>();
            foreach (var b in store2.Backups)
            {
                if (ModsStore.FindByArc(toolkitRoot, b.Arc).Count == 0) orphans.Add(b);
            }
            foreach (var ob in orphans)
            {
                try { if (File.Exists(ob.BackupPath)) File.Delete(ob.BackupPath); } catch { }
                var obTab = ArcOriginalToTabOriginal(ob.BackupPath);
                try { if (File.Exists(obTab)) File.Delete(obTab); } catch { }
                ModsStore.RemoveBackupEntry(toolkitRoot, ob.Arc);
                log("[uninstall] orphan backup removed: " + ob.Arc);
            }

            res.Success = true;
            log("[uninstall] OK: " + res.RestoredArcs.Count + " restored, " + res.RebuiltArcs.Count + " rebuilt, " + res.ModsReplayed + " replayed");
            return res;
        }

        private static void CommitReplace(string src, string dst)
        {
            try
            {
                File.Replace(src, dst, null);
            }
            catch
            {
                var bak = dst + ".bak_uninstall";
                if (File.Exists(bak)) File.Delete(bak);
                File.Move(dst, bak);
                File.Move(src, dst);
                try { File.Delete(bak); } catch { }
            }
        }
    }
}
