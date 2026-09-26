// AssetConverters.cs - conversores de formato para assets moddeados.
// Version 2.0.0 - block 51e.
//
// Conversores soportados:
//   PNG/JPG/BMP/TGA -> DDS (via texconv bundled)
//   DDS             -> DDSC (via ddscConvert bundled)
//   PNG/JPG/...     -> DDSC (paso doble: texconv + ddscConvert)
//   MP3/WAV/FLAC    -> OGG (via ffmpeg externo, on-demand)
//
// El output para texturas es ".ddsc" (extension real del juego).
// El engine detecta el tipo por magic interno ("AVTX"), no por extension.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Rage2Toolkit
{
    public static class AssetConverters
    {
        public sealed class ConvertResult
        {
            public string InputPath;
            public string OutputPath;
            public string Status; // OK / SKIP / FAIL
            public List<string> Steps = new List<string>();
            public List<string> Errors = new List<string>();
        }

        public static string FindTexconv(string toolkitReleaseDir)
        {
            string p = Path.Combine(toolkitReleaseDir, "bin", "texconv.exe");
            return File.Exists(p) ? p : null;
        }

        public static string FindDdscConvert(string toolkitReleaseDir)
        {
            string p = Path.Combine(toolkitReleaseDir, "bin", "ddscConvert.exe");
            return File.Exists(p) ? p : null;
        }

        public static string FindFfmpeg(string toolsDir)
        {
            string p = Path.Combine(toolsDir, "ffmpeg", "bin", "ffmpeg.exe");
            if (File.Exists(p)) return p;
            p = Path.Combine(toolsDir, "ffmpeg", "ffmpeg.exe");
            return File.Exists(p) ? p : null;
        }

        // Clasifica un archivo por nivel de intervencion requerida.
        // Status posibles:
        //   "native"   -> ya es formato nativo del juego, sin conversion necesaria
        //   "convert"  -> convertible automaticamente con toolkit (bundled o descargable)
        //   "manual"   -> formato nativo que requiere herramienta externa (RAD, JPEXS, FMOD)
        //   "unknown"  -> extension no reconocida; el writer lo acepta bajo responsabilidad del usuario
        public sealed class NeedsResult
        {
            public string Status;      // native | convert | manual | unknown
            public string Tool;        // que herramienta (vacío si native/unknown)
            public string Reason;      // descripcion humana
            public bool BlocksRepack;  // si es true, el wizard deberia bloquear
        }

        public static NeedsResult Classify(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            var r = new NeedsResult { Tool = "", Reason = "", BlocksRepack = false };

            switch (ext)
            {
                // ---------- Texturas ----------
                case ".png": case ".jpg": case ".jpeg": case ".bmp": case ".tga":
                    r.Status = "convert"; r.Tool = "texconv+ddscConvert";
                    r.Reason = "image -> DDS -> DDSC"; return r;
                case ".dds":
                    r.Status = "convert"; r.Tool = "ddscConvert";
                    r.Reason = "DDS -> DDSC"; return r;
                case ".ddsc": case ".avtx":
                case ".atx1": case ".atx2": case ".atx3": case ".atx4":
                case ".atx5": case ".atx6": case ".atx7": case ".atx8": case ".atx9":
                    if (ext == ".atx1" || ext == ".atx2" || ext == ".atx3" || ext == ".atx4" || ext == ".atx5" || ext == ".atx6" || ext == ".atx7" || ext == ".atx8" || ext == ".atx9") { r.Status = "native"; r.Reason = "raw BC1 mip blob (no descriptor sibling -> not editable)"; } else { r.Status = "native"; r.Reason = "texture descriptor (AVTX/DDSC) - editable"; } return r;

                // ---------- Audio ----------
                case ".mp3": case ".flac":
                    r.Status = "convert"; r.Tool = "ffmpeg";
                    r.Reason = "audio -> OGG Vorbis (ffmpeg requerido, descarga on-demand)"; return r;
                case ".wav":
                    r.Status = "convert"; r.Tool = "ffmpeg";
                    r.Reason = "audio -> OGG Vorbis (ffmpeg requerido, o convertir a RIFF manualmente)"; return r;
                case ".ogg": case ".riff":
                    r.Status = "native"; r.Reason = "audio nativo del juego"; return r;

                // ---------- Video ----------
                case ".mp4": case ".avi": case ".mov": case ".mkv":
                    r.Status = "manual"; r.Tool = "RAD Video Tools";
                    r.Reason = "video -> BIK (RAD Video Tools, NO redistribuible). Editar manualmente."; return r;
                case ".bik": case ".bk2": case ".bikc":
                    r.Status = "native"; r.Reason = "video nativo del juego"; return r;

                // ---------- UI ----------
                case ".swf": case ".as": case ".fla":
                    r.Status = "manual"; r.Tool = "JPEXS Decompiler";
                    r.Reason = "UI -> CFX (JPEXS Decompiler, open source). Editar manualmente."; return r;
                case ".gfx": case ".cfx":
                    r.Status = "native"; r.Reason = "UI nativo del juego"; return r;

                // ---------- Audio banks FMOD ----------
                case ".fsb": case ".bank":
                    r.Status = "manual"; r.Tool = "FMOD Studio";
                    r.Reason = "FMOD bank (requiere FMOD Studio para edicion)"; return r;
                case ".fmod_bankc": case ".fmod_sbankc":
                    r.Status = "native"; r.Reason = "FMOD bank nativo del juego"; return r;

                // ---------- Data / texto (out-of-the-box) ----------
                case ".json": case ".txt": case ".xml": case ".csv":
                    r.Status = "native"; r.Reason = "texto/browseable, editar con editor de texto"; return r;
                case ".bin": case ".tag":
                    r.Status = "native"; r.Reason = "data binaria; editar bajo responsabilidad"; return r;
                case ".stringlookup":
                    r.Status = "native"; r.Reason = "localizacion; conversion CSV no implementada aun"; return r;
                case ".adf": case ".ee": case ".nl": case ".bl":
                    r.Status = "native"; r.Reason = "script/data nativo"; return r;
                case ".ttfc": case ".bmpc":
                    r.Status = "native"; r.Reason = "fuente/LUT nativa"; return r;

                // ---------- Meshes / animaciones ----------
                case ".modelc": case ".epe": case ".epeb": case ".epeo":
                    r.Status = "native"; r.Reason = "model/entity container (native; no editor available)"; return r;
                case ".meshc": case ".hrmeshc": case ".navmeshc":
                case ".graphc": case ".ban": case ".hikcc":
                    r.Status = "native"; r.Reason = "mesh/animacion nativa; sin editor disponible"; return r;

                // ---------- Placeholders / terrain procedural ----------
                case ".streampatch": case ".rawc":
                    r.Status = "native"; r.Reason = "procedural generado por engine; no editable utilmente"; return r;
            }

            r.Status = "unknown";
            r.Reason = "extension desconocida; el writer la acepta bajo responsabilidad";
            r.BlocksRepack = false;
            return r;
        }

        // Wrapper legacy: retorna (bool, string) por compatibilidad.
        public static (bool, string) NeedsConversion(string path)
        {
            var n = Classify(path);
            return (n.Status == "convert", n.Reason);
        }

        static ConvertResult RunTool(string exe, string arguments, string workingDir, string label, ConvertResult r, Action<string> L)
        {
            L("[conv] " + label + ": " + Path.GetFileName(exe) + " " + arguments);
            try
            {
                var psi = new ProcessStartInfo(exe)
                {
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    WorkingDirectory = workingDir ?? Environment.CurrentDirectory
                };
                using (var p = Process.Start(psi))
                {
                    try { p.StandardInput.Close(); } catch { }
                    if (!p.WaitForExit(30000))
                    {
                        try { p.Kill(); } catch { }
                        r.Status = "FAIL";
                        r.Errors.Add(label + " timeout 30s");
                        return r;
                    }
                    string so = p.StandardOutput.ReadToEnd();
                    string se = p.StandardError.ReadToEnd();
                    if (p.ExitCode != 0)
                    {
                        r.Status = "FAIL";
                        r.Errors.Add(label + " exit=" + p.ExitCode + " " + se.Trim());
                        return r;
                    }
                    r.Steps.Add(label + " OK");
                }
            }
            catch (Exception ex)
            {
                r.Status = "FAIL";
                r.Errors.Add(label + " exception: " + ex.Message);
            }
            return r;
        }

        public static string PickFormatForPath(string inputPath)
        {
            try {
                string bn = Path.GetFileNameWithoutExtension(inputPath);
                var mm = System.Text.RegularExpressions.Regex.Match(bn, @"^(.*?)_[0-9A-Fa-f]{16}$");
                if (mm.Success) bn = mm.Groups[1].Value;
                int idx = bn.LastIndexOf('_');
                string sfx = idx > 0 ? bn.Substring(idx + 1).ToLowerInvariant() : "";
                if (sfx == "dif" || sfx == "emc" || sfx == "albedo" || sfx == "color" || sfx == "diffuse")
                    return "BC7_UNORM_SRGB";
                return "BC7_UNORM";
            } catch { return "BC7_UNORM"; }
        }

        public static ConvertResult Convert(string inputPath, string outputDir, string toolkitReleaseDir,
                                             string targetType = "auto", Action<string> log = null)
        {
            Action<string> L = log ?? (_ => { });
            var r = new ConvertResult { InputPath = inputPath };
            if (!File.Exists(inputPath)) { r.Status = "FAIL"; r.Errors.Add("input no existe"); return r; }
            Directory.CreateDirectory(outputDir);

            string ext = Path.GetExtension(inputPath).ToLowerInvariant();

            // DDSC -> DDS -> editable (PNG/DDS) - TECH-15 reverse
            if ((ext == ".ddsc" || ext == ".avtx") &&
                (targetType == "png" || targetType == "dds" || targetType == "editable"))
            {
                string ddsc = FindDdscConvert(toolkitReleaseDir);
                if (ddsc == null) { r.Status = "FAIL"; r.Errors.Add("ddscConvert no encontrado"); return r; }
                string work = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + ".dds");
                File.Copy(inputPath, work, true);
                string tmpDir = Path.Combine(outputDir, "__tmp_rev");
                Directory.CreateDirectory(tmpDir);
                string tmpDdsc = Path.Combine(tmpDir, Path.GetFileNameWithoutExtension(inputPath) + ".ddsc");
                File.Move(work, tmpDdsc);
                r = RunTool(ddsc, "\"" + tmpDdsc + "\"", tmpDir, "ddscConvert (DDSC->DDS)", r, L);
                if (r.Status == "FAIL") return r;
                string outDds = Path.Combine(tmpDir, Path.GetFileNameWithoutExtension(inputPath) + ".dds");
                if (!File.Exists(outDds)) { r.Status = "FAIL"; r.Errors.Add("ddscConvert reverse no produjo DDS"); return r; }
                if (targetType == "dds") {
                    File.Copy(outDds, Path.Combine(outputDir, Path.GetFileName(outDds)), true);
                    r.OutputPath = Path.Combine(outputDir, Path.GetFileName(outDds));
                    r.Steps.Add("DDSC -> DDS");
                    try { if (File.Exists(inputPath)) File.Delete(inputPath); } catch { }
                } else {
                    string texconv = FindTexconv(toolkitReleaseDir);
                    if (texconv == null) { r.Status = "FAIL"; r.Errors.Add("texconv no encontrado"); return r; }
                    string pngDir = tmpDir;
                    // sRGB input hint para _dif/_emc
                    string _bn = Path.GetFileNameWithoutExtension(inputPath).ToLowerInvariant();
                    var _mm = System.Text.RegularExpressions.Regex.Match(_bn, @"^(.*?)_[0-9a-f]{16}$");
                    if (_mm.Success) _bn = _mm.Groups[1].Value;
                    int _ix = _bn.LastIndexOf('_');
                    string _sfx = _ix > 0 ? _bn.Substring(_ix + 1) : "";
                    bool _needSrgb = (_sfx == "dif" || _sfx == "emc" || _sfx == "albedo" || _sfx == "color" || _sfx == "diffuse");
                    string _srgbFlags = _needSrgb ? "-srgbi -srgbo -f R8G8B8A8_UNORM_SRGB " : "-f R8G8B8A8_UNORM ";
                    L("[conv] sRGB flags for " + _sfx + ": " + (_needSrgb ? "ON" : "off"));
                    r = RunTool(texconv, "-ft png " + _srgbFlags + "-y -o \"" + pngDir + "\" \"" + outDds + "\"", pngDir, "texconv (DDS->PNG)", r, L);
                    if (r.Status == "FAIL") return r;
                    string outPng = Path.Combine(pngDir, Path.GetFileNameWithoutExtension(inputPath) + ".png");
                    if (!File.Exists(outPng)) { r.Status = "FAIL"; r.Errors.Add("texconv no produjo PNG"); return r; }
                    File.Copy(outPng, Path.Combine(outputDir, Path.GetFileName(outPng)), true);
                    r.OutputPath = Path.Combine(outputDir, Path.GetFileName(outPng));
                    // cleanup: borrar .ddsc/.avtx source original
                    try { if (File.Exists(inputPath)) File.Delete(inputPath); } catch { }
                    r.Steps.Add("DDSC -> DDS -> PNG");
                }
                try {
                    // Borrar .dds intermedio tambien
                    if (File.Exists(outDds)) File.Delete(outDds);
                    Directory.Delete(tmpDir, true);
                } catch { }
                r.Status = "OK";
                return r;
            }

            // Texturas: PNG/JPG/BMP/TGA/DDS -> DDSC
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".tga" ||
                ext == ".dds" || targetType == "ddsc" || targetType == "avtx" || targetType == "texture")
            {
                string texconv = FindTexconv(toolkitReleaseDir);
                string ddsc = FindDdscConvert(toolkitReleaseDir);
                if (ddsc == null) { r.Status = "FAIL"; r.Errors.Add("ddscConvert no encontrado en " + toolkitReleaseDir + "\\bin"); return r; }

                string ddsPath;

                if (ext == ".dds")
                {
                    ddsPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + ".dds");
                    File.Copy(inputPath, ddsPath, true);
                    r.Steps.Add("copy DDS a workdir");
                }
                else
                {
                    if (texconv == null) { r.Status = "FAIL"; r.Errors.Add("texconv no encontrado en " + toolkitReleaseDir + "\\bin"); return r; }
                    string _fmt = PickFormatForPath(inputPath);
            string texconvArgs = "-f " + _fmt + " -m 0 -y -o \"" + outputDir + "\" \"" + inputPath + "\"";
                    r = RunTool(texconv, texconvArgs, outputDir, "texconv (imagen->DDS)", r, L);
                    if (r.Status == "FAIL") return r;

                    string baseName = Path.GetFileNameWithoutExtension(inputPath);
                    ddsPath = Path.Combine(outputDir, baseName + ".dds");
                    if (!File.Exists(ddsPath))
                    { r.Status = "FAIL"; r.Errors.Add("texconv no produjo " + ddsPath); return r; }
                }

                string ddscArgs = "\"" + ddsPath + "\"";
                r = RunTool(ddsc, ddscArgs, outputDir, "ddscConvert (DDS->DDSC)", r, L);
                if (r.Status == "FAIL") return r;

                string ddscPath = Path.ChangeExtension(ddsPath, ".ddsc");
                if (!File.Exists(ddscPath))
                { r.Status = "FAIL"; r.Errors.Add("ddscConvert no produjo " + ddscPath); return r; }

                r.OutputPath = ddscPath;
                r.Status = "OK";
                return r;
            }

            // Audio -> OGG
            if (ext == ".mp3" || ext == ".wav" || ext == ".flac")
            {
                string toolsRoot = Path.GetDirectoryName(toolkitReleaseDir);
                string ffmpeg = FindFfmpeg(toolsRoot ?? "");
                if (ffmpeg == null)
                {
                    r.Status = "FAIL";
                    r.Errors.Add("ffmpeg no disponible. Descargar de https://www.gyan.dev/ffmpeg/builds/ y descomprimir a _tools\\ffmpeg\\");
                    return r;
                }

                string oggPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + ".ogg");
                string ffArgs = "-y -i \"" + inputPath + "\" -c:a libvorbis -q:a 6 \"" + oggPath + "\"";
                r = RunTool(ffmpeg, ffArgs, outputDir, "ffmpeg (audio->OGG)", r, L);
                if (r.Status == "FAIL") return r;
                if (!File.Exists(oggPath)) { r.Status = "FAIL"; r.Errors.Add("ffmpeg no produjo " + oggPath); return r; }
                r.OutputPath = oggPath;
                r.Status = "OK";
                return r;
            }

            if (ext == ".mp4" || ext == ".avi" || ext == ".mov")
            {
                r.Status = "FAIL";
                r.Errors.Add("Video -> BIK no soportado. Requiere RAD Video Tools (manual).");
                return r;
            }

            r.Status = "SKIP";
            r.Steps.Add("no conversion aplica para extension " + ext);
            return r;
        }
    }
}

