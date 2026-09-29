using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Rage2Toolkit
{
    public class ModAssetValidation
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string HashHex { get; set; }
        public string Extension { get; set; }
        public long SizeBytes { get; set; }
        public string Status { get; set; }
        public string Reason { get; set; }
    }

    public class ModValidationResult
    {
        public List<ModAssetValidation> Assets { get; set; } = new List<ModAssetValidation>();
        public List<ModAssetValidation> Shadowed { get; set; } = new List<ModAssetValidation>();
        public int OkCount { get; set; }
        public int WarnCount { get; set; }
        public int FailCount { get; set; }
        public int SkippedCount { get; set; }
        public bool CanPackage { get { return FailCount == 0 && (OkCount + WarnCount) > 0; } }
    }

    public static class ModValidator
    {
        private static readonly Regex HashRegex = new Regex("^([0-9A-Fa-f]{16})$", RegexOptions.Compiled);
        private static readonly Regex HashEmbedded = new Regex("_([0-9A-Fa-f]{16})$", RegexOptions.Compiled);

        private static readonly HashSet<string> KnownMeta = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "mod.json", "README.txt", "preview.png", "LICENSE.txt", "CHANGELOG.txt" };

        private static readonly HashSet<string> KnownExt = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "atx1","atx2","atx3","atx4","atx5","atx6","atx7","atx8","atx9",
            "ddsc","avtx","dds","png","jpg","jpeg","bmp","tga",
            "ogg","wav","mp3","flac",
            "adf","ee","nl","bl","fl","stringlookup","rtpc",
            "meshc","hrmeshc","modelc","navmeshc",
            "cfx","gfx","bik","bk2","bikc",
            "graphc","ban","hkcc","hikcc",
        };

        public static string ParseHash(string nameNoExt)
        {
            if (string.IsNullOrEmpty(nameNoExt)) return null;
            var m = HashRegex.Match(nameNoExt);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
            m = HashEmbedded.Match(nameNoExt);
            if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
            return null;
        }

        private static int GetPriority(string ext)
        {
            switch (ext)
            {
                case "atx1": case "atx2": case "atx3": case "atx4": case "atx5":
                case "atx6": case "atx7": case "atx8": case "atx9": return 100;
                case "ddsc": case "avtx": return 95;
                case "ogg": case "riff": return 90;
                case "ee": case "adf": case "nl": case "bl": case "fl": return 85;
                case "cfx": case "gfx": return 80;
                case "bik": case "bk2": case "bikc": return 75;
                case "meshc": case "hrmeshc": case "modelc": case "navmeshc": return 70;
                case "graphc": case "ban": case "hkcc": case "hikcc": return 65;
                case "stringlookup": return 60;
                case "dds": return 35;
                case "png": case "jpg": case "jpeg": case "bmp": case "tga": return 30;
                case "mp3": case "wav": case "flac": return 30;
                default: return 10;
            }
        }

        public static ModValidationResult ValidateFolder(string folder)
        {
            var res = new ModValidationResult();
            if (!Directory.Exists(folder)) return res;

            var byHash = new Dictionary<string, List<ModAssetValidation>>(StringComparer.OrdinalIgnoreCase);

            foreach (var f in Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(f);
                if (KnownMeta.Contains(name)) continue;

                var noExt = Path.GetFileNameWithoutExtension(name);
                var ext = Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
                var hash = ParseHash(noExt);
                if (string.IsNullOrEmpty(hash)) { res.SkippedCount++; continue; }

                var v = new ModAssetValidation
                {
                    FileName = name,
                    FullPath = f,
                    HashHex = hash,
                    Extension = ext,
                    SizeBytes = new FileInfo(f).Length,
                };

                if (!byHash.ContainsKey(hash)) byHash[hash] = new List<ModAssetValidation>();
                byHash[hash].Add(v);
            }

            foreach (var kv in byHash)
            {
                var list = kv.Value;
                list.Sort(delegate(ModAssetValidation a, ModAssetValidation b)
                {
                    return GetPriority(b.Extension).CompareTo(GetPriority(a.Extension));
                });
                var winner = list[0];

                if (winner.SizeBytes <= 0)
                {
                    winner.Status = "FAIL";
                    winner.Reason = "empty file";
                    res.FailCount++;
                }
                else if (winner.SizeBytes > 100L * 1024 * 1024)
                {
                    winner.Status = "WARN";
                    winner.Reason = "asset > 100 MB";
                    res.WarnCount++;
                }
                else if (!string.IsNullOrEmpty(winner.Extension) && !KnownExt.Contains(winner.Extension))
                {
                    winner.Status = "WARN";
                    winner.Reason = "unknown extension ." + winner.Extension;
                    res.WarnCount++;
                }
                else
                {
                    winner.Status = "OK";
                    winner.Reason = "valid";
                    res.OkCount++;
                }
                res.Assets.Add(winner);

                for (int i = 1; i < list.Count; i++)
                {
                    var loser = list[i];
                    loser.Status = "SHADOWED";
                    loser.Reason = "same hash as " + winner.FileName;
                    res.Shadowed.Add(loser);
                    res.SkippedCount++;
                }
            }
            return res;
        }
    }
}
