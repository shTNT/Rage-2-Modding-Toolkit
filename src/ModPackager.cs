using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace Rage2Toolkit
{
    public class PackResult
    {
        public bool Success { get; set; }
        public string ZipPath { get; set; }
        public int AssetsCount { get; set; }
        public long ZipSizeBytes { get; set; }
        public string Error { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class ModPackager
    {
        public static PackResult Build(string sourceDir, string outputZip, ModMetadata meta, Action<string> log)
        {
            var res = new PackResult();
            if (log == null) log = delegate(string s) { };

            if (!Directory.Exists(sourceDir))
            {
                res.Error = "source directory does not exist: " + sourceDir; log("[pack] FAIL: " + res.Error);
                return res;
            }

            log("[pack] validating " + sourceDir);
            var validation = ModValidator.ValidateFolder(sourceDir);
            log("[pack] OK=" + validation.OkCount + " WARN=" + validation.WarnCount
                + " FAIL=" + validation.FailCount + " SKIP=" + validation.SkippedCount);

            if (!validation.CanPackage)
            {
                res.Error = "cannot package: FAIL=" + validation.FailCount + " OK=" + validation.OkCount; log("[pack] FAIL: " + res.Error);
                return res;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in validation.Assets)
            {
                if (a.Status == "FAIL") continue;
                if (!seen.Add(a.HashHex))
                {
                    res.Error = "duplicate hash in mod: " + a.HashHex; log("[pack] FAIL: " + res.Error);
                    return res;
                }
                if (a.Status == "WARN") res.Warnings.Add(a.FileName + ": " + a.Reason);
            }

            if (meta == null) meta = ModMetadata.Default(Path.GetFileNameWithoutExtension(outputZip));
            if (string.IsNullOrEmpty(meta.Id)) meta.Id = ModMetadata.Slugify(meta.Name ?? "untitled");
            if (string.IsNullOrEmpty(meta.Name)) meta.Name = "Untitled Mod";
            if (string.IsNullOrEmpty(meta.Version)) meta.Version = "0.0.0";
            if (string.IsNullOrEmpty(meta.CreatedWith)) meta.CreatedWith = "RAGE2Toolkit v2.1.0";
            if (string.IsNullOrEmpty(meta.CreatedAt)) meta.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            if (File.Exists(outputZip)) File.Delete(outputZip);
            var outDir = Path.GetDirectoryName(outputZip);
            if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

            var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
            var metaJson = JsonSerializer.Serialize(meta, jsonOpts);

            int assetCount = 0;
            try
            {
                using (var fs = new FileStream(outputZip, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var mEntry = zip.CreateEntry("mod.json", CompressionLevel.Optimal);
                    using (var w = new StreamWriter(mEntry.Open(), new System.Text.UTF8Encoding(false)))
                        w.Write(metaJson);

                    foreach (var a in validation.Assets)
                    {
                        if (a.Status == "FAIL") continue;
                        var e = zip.CreateEntry(a.FileName, CompressionLevel.Optimal);
                        using (var w = e.Open())
                        using (var src = File.OpenRead(a.FullPath))
                            src.CopyTo(w);
                        assetCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                res.Error = "zip creation failed: " + ex.Message; log("[pack] FAIL: " + res.Error);
                return res;
            }

            try
            {
                using (var fs = new FileStream(outputZip, FileMode.Open, FileAccess.Read))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    if (zip.Entries.Count < 1 + assetCount)
                    {
                        res.Error = "zip integrity: expected " + (1 + assetCount) + " got " + zip.Entries.Count; log("[pack] FAIL: " + res.Error);
                        return res;
                    }
                }
            }
            catch (Exception ex)
            {
                res.Error = "zip verify failed: " + ex.Message; log("[pack] FAIL: " + res.Error);
                return res;
            }

            var fi = new FileInfo(outputZip);
            res.Success = true;
            res.ZipPath = outputZip;
            res.AssetsCount = assetCount;
            res.ZipSizeBytes = fi.Length;
            log("[pack] OK: " + outputZip + " (" + assetCount + " assets, " + fi.Length + " bytes)");
            return res;
        }
    }
}
