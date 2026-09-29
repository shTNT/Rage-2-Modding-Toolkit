using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Rage2Toolkit
{
    public class InstalledMod
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public int Priority { get; set; } = 100;
        public string InstalledAt { get; set; }
        public string LibraryFile { get; set; }
        public List<string> AffectedArcs { get; set; } = new List<string>();
        public List<string> Hashes { get; set; } = new List<string>();
    }

    public class BackupEntry
    {
        public string Arc { get; set; }
        public string BackupPath { get; set; }
        public string CreatedAt { get; set; }
        public long Size { get; set; }
        public string Sha256 { get; set; }
    }

    public class ModsStoreData
    {
        public int Schema { get; set; } = 1;
        public List<InstalledMod> Installed { get; set; } = new List<InstalledMod>();
        public List<BackupEntry> Backups { get; set; } = new List<BackupEntry>();
        // arcRel ("initial/game8") -> SHA256 del .arc tal como lo dejo ModManager.
        // Si el SHA actual no coincide, alguien escribio por fuera -> drift.
        public Dictionary<string, string> ArcLastWritten { get; set; } = new Dictionary<string, string>();
    }

    public static class ModsStore
    {
        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string StoreFile(string root) { return Path.Combine(root, "mods", "mods.json"); }
        public static string LibraryDir(string root) { return Path.Combine(root, "mods", "library"); }
        public static string BackupsDir(string root) { return Path.Combine(root, "mods", "backups"); }

        static string NormArc(string a) { return (a ?? "").Trim().Replace('\\', '/').ToLowerInvariant(); }

        public static string Sha256Of(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var fs = File.OpenRead(path))
            {
                var hash = sha.ComputeHash(fs);
                var sb = new System.Text.StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string GetLastWritten(string root, string arcRel)
        {
            var data = Load(root);
            if (data.ArcLastWritten == null) return null;
            string key = NormArc(arcRel);
            foreach (var kv in data.ArcLastWritten)
                if (NormArc(kv.Key) == key) return kv.Value;
            return null;
        }

        public static void SetLastWrittenBatch(string root, System.Collections.Generic.IDictionary<string, string> entries)
        {
            var data = Load(root);
            if (data.ArcLastWritten == null) data.ArcLastWritten = new Dictionary<string, string>();
            foreach (var kv in entries) data.ArcLastWritten[kv.Key] = kv.Value;
            Save(root, data);
        }

        /// <summary>
        /// Crea <root>/mods/, <root>/mods/library/ y <root>/mods/backups/ si no existen.
        /// Llamado al arrancar el toolkit para que las carpetas esten visibles antes
        /// de cualquier install. Idempotente. Silencioso.
        /// </summary>
        public static void EnsureFolders(string root)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "mods"));
                Directory.CreateDirectory(LibraryDir(root));
                Directory.CreateDirectory(BackupsDir(root));
            }
            catch { }
        }

        public static ModsStoreData Load(string root)
        {
            var file = StoreFile(root);
            if (!File.Exists(file)) return new ModsStoreData();
            try
            {
                var txt = File.ReadAllText(file);
                var data = JsonSerializer.Deserialize<ModsStoreData>(txt, JsonOpts);
                return data ?? new ModsStoreData();
            }
            catch { return new ModsStoreData(); }
        }

        public static void Save(string root, ModsStoreData data)
        {
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            Directory.CreateDirectory(LibraryDir(root));
            Directory.CreateDirectory(BackupsDir(root));
            var json = JsonSerializer.Serialize(data, JsonOpts);
            File.WriteAllText(StoreFile(root), json);
        }

        public static InstalledMod FindById(string root, string id)
        {
            var data = Load(root);
            foreach (var m in data.Installed)
                if (string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase))
                    return m;
            return null;
        }
        public static bool RemoveById(string root, string id)
        {
            var data = Load(root);
            int removed = data.Installed.RemoveAll(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save(root, data);
                return true;
            }
            return false;
        }

        public static bool UpdatePriority(string root, string id, int newPriority)
        {
            var data = Load(root);
            bool found = false;
            foreach (var m in data.Installed)
            {
                if (string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    m.Priority = newPriority;
                    found = true;
                    break;
                }
            }
            if (found) { Save(root, data); return true; }
            return false;
        }

        public static List<InstalledMod> FindByArc(string root, string arcRel)
        {
            var res = new List<InstalledMod>();
            var data = Load(root);
            foreach (var m in data.Installed)
                if (m.AffectedArcs != null && m.AffectedArcs.Contains(arcRel))
                    res.Add(m);
            return res;
        }

        public static BackupEntry GetBackupByArc(string root, string arcRel)
        {
            var data = Load(root);
            foreach (var b in data.Backups)
                if (string.Equals(b.Arc, arcRel, StringComparison.OrdinalIgnoreCase))
                    return b;
            return null;
        }

        public static bool RemoveBackupEntry(string root, string arcRel)
        {
            var data = Load(root);
            int removed = data.Backups.RemoveAll(b => string.Equals(b.Arc, arcRel, StringComparison.OrdinalIgnoreCase));
            if (removed > 0) { Save(root, data); return true; }
            return false;
        }

        public static List<InstalledMod> FindByHash(string root, string hashHex)
        {
            var res = new List<InstalledMod>();
            var data = Load(root);
            foreach (var m in data.Installed)
                if (m.Hashes != null && m.Hashes.Contains(hashHex.ToUpperInvariant()))
                    res.Add(m);
            return res;
        }

        public static BackupEntry GetBackupForArc(string root, string arcRelPath)
        {
            var data = Load(root);
            foreach (var b in data.Backups)
                if (string.Equals(b.Arc, arcRelPath, StringComparison.OrdinalIgnoreCase))
                    return b;
            return null;
        }
    }
}
