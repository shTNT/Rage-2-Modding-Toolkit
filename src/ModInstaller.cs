using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Rage2Toolkit
{
    public class InstallResult
    {
        public bool Success { get; set; }
        public string ModId { get; set; }
        public string ModName { get; set; }
        public string ModVersion { get; set; }
        public int AssetsInstalled { get; set; }
        public int ArchivesModified { get; set; }
        public List<string> BackupsCreated { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string Error { get; set; }
    }

    public static class ModInstaller
    {
        public static InstallResult Install(string zipPath, string gamePath, string toolkitRoot, Action<string> log, bool allowConflicts, int priority)
        {
            var res = new InstallResult();
            if (log == null) log = delegate(string s) { };

            if (!File.Exists(zipPath)) { res.Error = "zip does not exist: " + zipPath; log("[install] FAIL: " + res.Error); return res; }
            if (!Directory.Exists(gamePath)) { res.Error = "game path does not exist: " + gamePath; log("[install] FAIL: " + res.Error); return res; }
            var archivesRoot = Path.Combine(gamePath, "archives_win64");
            if (!Directory.Exists(archivesRoot)) { res.Error = "archives_win64 not found in game path"; log("[install] FAIL: " + res.Error); return res; }

            ModMetadata meta = null;
            var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            // Hashes de assets .rtpc/.bin del zip. Se pasan a RepackerCore como
            // forceRawHashes para preservar el formato raw vanilla. Solo afecta a
            // este subset; el resto de assets siguen el flujo normal.
            var _settingsHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // FASE 1: READ
            log("[install] FASE 1 READ: " + zipPath);
            try
            {
                using (var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName == "mod.json")
                        {
                            try
                            {
                                using (var sr = new StreamReader(entry.Open()))
                                {
                                    var json = sr.ReadToEnd();
                                    meta = JsonSerializer.Deserialize<ModMetadata>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                }
                            }
                            catch (Exception ex) { res.Warnings.Add("mod.json invalid: " + ex.Message); }
                            continue;
                        }

                        var name = Path.GetFileName(entry.FullName);
                        var noExt = Path.GetFileNameWithoutExtension(name);
                        var ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
                        var hash = ModValidator.ParseHash(noExt);
                        if (string.IsNullOrEmpty(hash)) continue;

                        // World Settings Editor: solo .rtpc/.bin preservan formato raw vanilla.
                        // Cualquier otro formato (ddsc, atx1, ogg, bik, meshc...) sigue
                        // comprimiendose con Oodle como siempre.
                        if (ext == "rtpc" || ext == "bin") _settingsHashes.Add(hash.ToUpperInvariant());

                        if (entry.Length > 100L * 1024 * 1024) { res.Warnings.Add(name + " > 100 MB, skipped"); continue; }

                        var buf = new byte[entry.Length];
                        using (var s = entry.Open())
                        {
                            int off = 0;
                            while (off < buf.Length)
                            {
                                int r = s.Read(buf, off, buf.Length - off);
                                if (r <= 0) break;
                                off += r;
                            }
                        }
                        assets[hash] = buf;
                    }
                }
            }
            catch (Exception ex)
            {
                res.Error = "zip read failed: " + ex.Message;
                log("[install] FAIL: " + res.Error);
                return res;
            }

            if (assets.Count == 0) { res.Error = "no assets found in zip"; log("[install] FAIL: " + res.Error); return res; }
            if (meta == null) meta = ModMetadata.Default(Path.GetFileNameWithoutExtension(zipPath));
            if (string.IsNullOrEmpty(meta.Id)) meta.Id = ModMetadata.Slugify(meta.Name ?? "untitled");
            res.ModId = meta.Id; res.ModName = meta.Name; res.ModVersion = meta.Version;
            log("[install] mod: " + meta.Name + " v" + meta.Version + " (" + assets.Count + " assets)");

            // FASE 2: DETECT target arcs
            log("[install] FASE 2 DETECT: sweeping .tab files...");
            var hashToArc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var arcDirs = new List<string>();
            arcDirs.Add(Path.Combine(archivesRoot, "initial"));
            arcDirs.Add(Path.Combine(archivesRoot, "supplemental"));

            foreach (var dir in arcDirs)
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var tabPath in Directory.GetFiles(dir, "*.tab"))
                {
                    try
                    {
                        var tb = File.ReadAllBytes(tabPath);
                        if (tb.Length < 0x20) continue;
                        uint fileCount = BitConverter.ToUInt32(tb, 0x0C);
                        uint blockCount = BitConverter.ToUInt32(tb, 0x10);
                        long fileStart = 0x20 + (long)blockCount * 8;
                        for (uint i = 0; i < fileCount; i++)
                        {
                            long off = fileStart + (long)i * 24;
                            if (off + 24 > tb.Length) break;
                            ulong h = BitConverter.ToUInt64(tb, (int)off);
                            var hHex = h.ToString("X16");
                            if (assets.ContainsKey(hHex) && !hashToArc.ContainsKey(hHex))
                            {
                                var dirName = Path.GetFileName(dir);
                                var tabBase = Path.GetFileNameWithoutExtension(tabPath);
                                hashToArc[hHex] = dirName + "/" + tabBase;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        res.Warnings.Add("tab read failed: " + Path.GetFileName(tabPath) + " - " + ex.Message);
                    }
                }
            }

            log("[install] detected target arcs: " + hashToArc.Count + "/" + assets.Count);

            // Conflict check contra mods instalados
            var allHashes = new List<string>();
            foreach (var kv in assets) allHashes.Add(kv.Key);
            var conflict = ConflictChecker.Check(toolkitRoot, allHashes, meta.Id, priority);
            if (conflict.HasConflicts)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append("conflicts with " + conflict.Conflicts.Count + " existing mod(s):");
                var shown = new HashSet<string>();
                foreach (var c in conflict.Conflicts)
                {
                    if (shown.Contains(c.ExistingModId)) continue;
                    shown.Add(c.ExistingModId);
                    sb.Append(" [" + c.ExistingModName + " v" + c.ExistingModVersion + " prio=" + c.ExistingPriority + "]");
                }
                if (!allowConflicts)
                {
                    res.Error = sb.ToString();
                    log("[install] FAIL: " + res.Error);
                    return res;
                }
                log("[install] WARN: conflicts allowed. " + sb.ToString());
                res.Warnings.Add(sb.ToString());
            }
            var missing = new List<string>();
            foreach (var kv in assets) if (!hashToArc.ContainsKey(kv.Key)) missing.Add(kv.Key);
            if (missing.Count > 0)
            {
                res.Error = "hash not found in any archive: " + string.Join(", ", missing);
                log("[install] FAIL: " + res.Error);
                return res;
            }

            // Group by target arc
            var byArc = new Dictionary<string, Dictionary<ulong, byte[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in assets)
            {
                var target = hashToArc[kv.Key];
                if (!byArc.ContainsKey(target)) byArc[target] = new Dictionary<ulong, byte[]>();
                ulong h = ulong.Parse(kv.Key, System.Globalization.NumberStyles.HexNumber);
                byArc[target][h] = kv.Value;
            }

            // FASE 3: PRE_INSTALL — verify all arcs exist, prepare backups
            log("[install] FASE 3 PRE_INSTALL");
            var backupsDir = ModsStore.BackupsDir(toolkitRoot);
            Directory.CreateDirectory(backupsDir);
            Directory.CreateDirectory(ModsStore.LibraryDir(toolkitRoot));
            var store = ModsStore.Load(toolkitRoot);

            // DRIFT CHECK: por cada .arc target, si tenemos un SHA registrado y no coincide
            // con el actual, alguien escribio por fuera (wizard repack, antivirus, otro tool).
            // Abortamos para no perder cambios no trackeados.
            {
                var driftArcs = new List<string>();
                foreach (var kv in byArc)
                {
                    var arcRel = kv.Key;
                    var parts = arcRel.Split('/');
                    if (parts.Length != 2) continue;
                    var arcDir = Path.Combine(archivesRoot, parts[0]);
                    var arcPath = Path.Combine(arcDir, parts[1] + ".arc");
                    if (!File.Exists(arcPath)) continue;
                    string recorded = ModsStore.GetLastWritten(toolkitRoot, arcRel);
                    if (string.IsNullOrEmpty(recorded)) continue;
                    string current;
                    try { current = ModsStore.Sha256Of(arcPath); } catch { continue; }
                    if (!string.Equals(current, recorded, StringComparison.OrdinalIgnoreCase))
                        driftArcs.Add(arcRel);
                }
                if (driftArcs.Count > 0)
                {
                    if (!allowConflicts)
                    {
                        res.Error = "drift detected on: " + string.Join(", ", driftArcs)
                            + " (arc modificado por fuera del ModManager). Abortado para no perder cambios. Usa --force para override.";
                        log("[install] FAIL: " + res.Error);
                        return res;
                    }
                    log("[install] WARN: drift ignored (--force) on: " + string.Join(", ", driftArcs) + " -- cambios externos se sobreescribiran");
                }
            }

            // Ensure Oodle is loaded (needed by RepackerCore.Rebuild)
            if (!Oodle.IsLoaded)
            {
                log("[install] loading Oodle...");
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
                    log("[install] FAIL: " + res.Error);
                    return res;
                }
                log("[install] oodle path: " + oodlePath);
                if (!Oodle.TryLoad(oodlePath))
                {
                    res.Error = "Oodle load failed: " + Oodle.LastError;
                    log("[install] FAIL: " + res.Error);
                    return res;
                }
                log("[install] Oodle loaded OK");
            }
            // FASE 4: BUILD_TMP
            log("[install] FASE 4 BUILD_TMP");
            var buildOk = true;
            foreach (var kv in byArc)
            {
                var arcRel = kv.Key; // initial/game8
                var parts = arcRel.Split('/');
                var arcDir = Path.Combine(archivesRoot, parts[0]);
                var arcBase = parts[1];
                var tabPath = Path.Combine(arcDir, arcBase + ".tab");
                var arcPath = Path.Combine(arcDir, arcBase + ".arc");
                if (!File.Exists(tabPath) || !File.Exists(arcPath))
                {
                    res.Error = "arc missing: " + arcPath;
                    log("[install] FAIL: " + res.Error);
                    buildOk = false; break;
                }

                var tmpTab = arcPath + ".mod_tmp.tab";
                var tmpArc = arcPath + ".mod_tmp.arc";
                try
                {
                    log("[install]   rebuild " + arcRel + " (" + kv.Value.Count + " replacements)");
                    // Solo pasamos force-raw para hashes .rtpc/.bin presentes en este arc.
                    HashSet<ulong> forceRaw = null;
                    if (_settingsHashes.Count > 0)
                    {
                        forceRaw = new HashSet<ulong>();
                        foreach (var kvh in kv.Value)
                        {
                            var hx = kvh.Key.ToString("X16");
                            if (_settingsHashes.Contains(hx)) forceRaw.Add(kvh.Key);
                        }
                        log("[install]   force-raw for " + forceRaw.Count + " settings (.rtpc/.bin)");
                    }
                    RepackerCore.Rebuild(tabPath, arcPath, kv.Value, tmpTab, tmpArc, log,
                        oodleLevel: 1, overrideThreads: 0, overrideWriteThreads: 0, overrideBufMB: 0,
                        forceRawHashes: forceRaw);
                }
                catch (Exception ex)
                {
                    res.Error = "rebuild failed for " + arcRel + ": " + ex.Message;
                    log("[install] FAIL: " + res.Error);
                    buildOk = false;
                    try { if (File.Exists(tmpTab)) File.Delete(tmpTab); } catch { }
                    try { if (File.Exists(tmpArc)) File.Delete(tmpArc); } catch { }
                    break;
                }
            }

            if (!buildOk)
            {
                // Cleanup any tmp remaining
                foreach (var kv in byArc)
                {
                    var parts = kv.Key.Split('/');
                    var arcPath = Path.Combine(archivesRoot, parts[0], parts[1] + ".arc");
                    try { if (File.Exists(arcPath + ".mod_tmp.tab")) File.Delete(arcPath + ".mod_tmp.tab"); } catch { }
                    try { if (File.Exists(arcPath + ".mod_tmp.arc")) File.Delete(arcPath + ".mod_tmp.arc"); } catch { }
                }
                return res;
            }

            // FASE 5: COMMIT (backup + move)
            log("[install] FASE 5 COMMIT");
            var committed = new List<string>(); // arcRel paths already committed
            var commitOk = true;
            foreach (var kv in byArc)
            {
                var arcRel = kv.Key;
                var parts = arcRel.Split('/');
                var arcDir = Path.Combine(archivesRoot, parts[0]);
                var arcBase = parts[1];
                var tabPath = Path.Combine(arcDir, arcBase + ".tab");
                var arcPath = Path.Combine(arcDir, arcBase + ".arc");
                var tmpTab = arcPath + ".mod_tmp.tab";
                var tmpArc = arcPath + ".mod_tmp.arc";

                // Backup once per arc
                var existingBackup = ModsStore.GetBackupForArc(toolkitRoot, arcRel);
                if (existingBackup == null)
                {
                    var bArc = Path.Combine(backupsDir, parts[0] + "_" + parts[1] + ".arc.original");
                    var bTab = Path.Combine(backupsDir, parts[0] + "_" + parts[1] + ".tab.original");
                    try
                    {
                        File.Copy(arcPath, bArc, false);
                        File.Copy(tabPath, bTab, false);
                        existingBackup = new BackupEntry
                        {
                            Arc = arcRel,
                            BackupPath = bArc,
                            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                            Size = new FileInfo(bArc).Length,
                        };
                        store.Backups.Add(existingBackup);
                        res.BackupsCreated.Add(bArc);
                        log("[install]   backup created: " + bArc);
                    }
                    catch (Exception ex)
                    {
                        res.Error = "backup failed for " + arcRel + ": " + ex.Message;
                        log("[install] FAIL: " + res.Error);
                        commitOk = false;
                        break;
                    }
                }

                try
                {
                    // Move .tmp → real (after moving .tab first to keep pair consistent enough)
                    File.Replace(tmpTab, tabPath, null);
                    File.Replace(tmpArc, arcPath, null);
                    committed.Add(arcRel);
                    log("[install]   committed: " + arcRel);
                }
                catch (Exception ex)
                {
                    res.Error = "commit failed for " + arcRel + ": " + ex.Message;
                    log("[install] FAIL: " + res.Error);
                    commitOk = false;
                    break;
                }
            }

            if (!commitOk)
            {
                // Rollback committed arcs from backup
                foreach (var arcRel in committed)
                {
                    var parts = arcRel.Split('/');
                    var arcDir = Path.Combine(archivesRoot, parts[0]);
                    var arcBase = parts[1];
                    var bArc = Path.Combine(backupsDir, parts[0] + "_" + parts[1] + ".arc.original");
                    var bTab = Path.Combine(backupsDir, parts[0] + "_" + parts[1] + ".tab.original");
                    try { if (File.Exists(bArc)) File.Copy(bArc, Path.Combine(arcDir, arcBase + ".arc"), true); } catch { }
                    try { if (File.Exists(bTab)) File.Copy(bTab, Path.Combine(arcDir, arcBase + ".tab"), true); } catch { }
                }
                // Delete remaining tmps
                foreach (var kv in byArc)
                {
                    var parts = kv.Key.Split('/');
                    var arcPath = Path.Combine(archivesRoot, parts[0], parts[1] + ".arc");
                    try { if (File.Exists(arcPath + ".mod_tmp.tab")) File.Delete(arcPath + ".mod_tmp.tab"); } catch { }
                    try { if (File.Exists(arcPath + ".mod_tmp.arc")) File.Delete(arcPath + ".mod_tmp.arc"); } catch { }
                }
                return res;
            }

            // FASE 6: REGISTER in mods.json
            log("[install] FASE 6 REGISTER");
            var libPath = Path.Combine(ModsStore.LibraryDir(toolkitRoot), meta.Id + "-v" + meta.Version + ".zip");
            try { File.Copy(zipPath, libPath, true); } catch (Exception ex) { res.Warnings.Add("library copy failed: " + ex.Message); }

            var newMod = new InstalledMod
            {
                Id = meta.Id,
                Name = meta.Name,
                Version = meta.Version,
                Author = meta.Author,
                Priority = priority,
                InstalledAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                LibraryFile = libPath,
            };
            foreach (var kv in byArc) newMod.AffectedArcs.Add(kv.Key);
            foreach (var kv in assets) newMod.Hashes.Add(kv.Key.ToUpperInvariant());
            store.Installed.Add(newMod);
            ModsStore.Save(toolkitRoot, store);

            // DRIFT BASELINE: registrar SHA256 del .arc tal como lo dejamos ahora.
            {
                var shaUpdates = new Dictionary<string, string>();
                foreach (var kv in byArc)
                {
                    var arcRel = kv.Key;
                    var parts = arcRel.Split('/');
                    if (parts.Length != 2) continue;
                    var arcDir = Path.Combine(archivesRoot, parts[0]);
                    var arcPath = Path.Combine(arcDir, parts[1] + ".arc");
                    if (!File.Exists(arcPath)) continue;
                    try { shaUpdates[arcRel] = ModsStore.Sha256Of(arcPath); } catch { }
                }
                if (shaUpdates.Count > 0) ModsStore.SetLastWrittenBatch(toolkitRoot, shaUpdates);
            }

            res.Success = true;
            res.AssetsInstalled = assets.Count;
            res.ArchivesModified = byArc.Count;
            log("[install] OK: " + assets.Count + " assets in " + byArc.Count + " archives");
            return res;
        }
    }
}
