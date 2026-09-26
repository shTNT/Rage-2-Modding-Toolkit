// AssetValidators.cs - validacion estructural + semantica por tipo de asset.
// Version 2.0.0 - primera version. Cobertura:
//   - Todos los tipos con magic conocido (estructural pura)
//   - Tipos "editables" (AVTX, DDS, OGG, RIFF, CFX, BIK, RTPC) con semantica adicional
//   - Unknown para archivos sin magic reconocible
//
// Devuelve ValidationResult con Status (Valid/Invalid/Warn/Unknown) + Errors/Warnings/Suggestions.
using System;
using System.Collections.Generic;
using System.IO;

namespace Rage2Toolkit
{
    public enum ValidationStatus { Valid, Warn, Invalid, Unknown }

    public sealed class ValidationResult
    {
        public string Path;
        public string Type = "unknown";
        public ValidationStatus Status = ValidationStatus.Unknown;
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<string> Suggestions = new List<string>();
        public long SizeBytes;

        public void Error(string m) { Errors.Add(m); if (Status != ValidationStatus.Invalid) Status = ValidationStatus.Invalid; }
        public void Warn(string m) { Warnings.Add(m); if (Status == ValidationStatus.Valid || Status == ValidationStatus.Unknown) Status = ValidationStatus.Warn; }
        public void Suggest(string m) { Suggestions.Add(m); }
        public void SetValid(string t) { Type = t; Status = ValidationStatus.Valid; }
        public void SetUnknown(string t, string reason) { Type = t; Status = ValidationStatus.Unknown; Warnings.Add(reason); }
    }

    public static class AssetValidators
    {
        public static ValidationResult ValidateFile(string path)
        {
            var r = new ValidationResult { Path = path };
            try
            {
                if (!File.Exists(path)) { r.Error("file does not exist"); return r; }
                var fi = new FileInfo(path);
                r.SizeBytes = fi.Length;
                if (fi.Length == 0) { r.Error("empty file"); return r; }

                // Leer cabecera (32 bytes suficiente para detectar magic)
                int headLen = (int)Math.Min(32, fi.Length);
                byte[] head = new byte[headLen];
                using (var fs = File.OpenRead(path)) { fs.Read(head, 0, headLen); }

                // Detectar por extension primero (rapido)
                string ext = System.IO.Path.GetExtension(path).ToLowerInvariant().TrimStart('.');
                switch (ext)
                {
                    case "avtx": return ValidateAvtx(path, head);
                    case "ddsc": return ValidateAvtx(path, head);
                    case "dds": return ValidateDds(path, head);
                    case "ogg": return ValidateOgg(path, head);
                    case "riff": return ValidateRiff(path, head);
                    case "bik": case "bk2": return ValidateBik(path, head);
                    case "cfx": return ValidateCfx(path, head);
                    case "rtpc": return ValidateRtpc(path, head);
                    case "stringlookup": return ValidateStringLookup(path, head);
                    case "adf": return ValidateAdf(path, head);
                }

                // Fallback: detectar por magic
                if (headLen >= 4)
                {
                    if (head[0]=='A' && head[1]=='V' && head[2]=='T' && head[3]=='X') return ValidateAvtx(path, head);
                    if (head[0]=='D' && head[1]=='D' && head[2]=='S' && head[3]==' ') return ValidateDds(path, head);
                    if (head[0]=='O' && head[1]=='g' && head[2]=='g' && head[3]=='S') return ValidateOgg(path, head);
                    if (head[0]=='R' && head[1]=='I' && head[2]=='F' && head[3]=='F') return ValidateRiff(path, head);
                    if (head[0]==0x43 && head[1]==0x46 && head[2]==0x58 && head[3]==0x1F) return ValidateCfx(path, head);
                }
                // Fallback: detectar por magic extendido (HK, FSB, mesh, etc.)
                if (headLen >= 4)
                {
                    // Havok: "hk" o "TAG0"
                    if (head[0]==0x54 && head[1]==0x41 && head[2]==0x47 && head[3]==0x30) return ValidateHavok(path, head);
                    if (head[0]==0x68 && head[1]==0x6B) return ValidateHavok(path, head);
                    // FMOD bank
                    if (head[0]==0x46 && head[1]==0x53 && head[2]==0x42) return ValidateFmod(path, head);
                    // mesh tags comunes
                    if (head[0]==0x4D && head[1]==0x65 && head[2]==0x73 && head[3]==0x68) return ValidateMeshGeneric(path, head);
                }
                r.SetUnknown("unknown", "no magic reconocible; ni la extension ni los primeros bytes coinciden con un tipo conocido");
                r.Suggest("Legacy type sin validador. Repack bajo responsabilidad del usuario. Los tipos soportados hoy: avtx, dds, ogg, riff, bik, cfx, rtpc, stringlookup, adf, havok, fmod, mesh.");
                return r;
            }
            catch (Exception ex) { r.Error("exception: " + ex.Message); return r; }
        }

        // ---------- AVTX ----------
        // Header 128 bytes. Magic "AVTX" @0. Version u16 @4/6. Dimensiones @8 (u16) y @10 (u16).
        // Format @0x14 (u32). Header_size @0x20 = 128. Payload_size @0x24.
        static ValidationResult ValidateAvtx(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("avtx");

            if (head.Length < 4 || head[0]!='A' || head[1]!='V' || head[2]!='T' || head[3]!='X')
            { r.Error("magic AVTX incorrecto"); return r; }

            if (r.SizeBytes < 128) { r.Error("tamaño < 128 bytes (header incompleto)"); return r; }

            // Leer header completo
            byte[] h128 = new byte[128];
            using (var fs = File.OpenRead(path)) { fs.Read(h128, 0, 128); }

            // AVTX header real (verificado con hex dump de 284 AVTX de game8):
            //   0x04: u16 version_major (siempre 1)
            //   0x06: u16 version_minor (0x020C en game8)
            //   0x08: u16 count_unknown_1 (varia por archivo)
            //   0x0A: u16 count_unknown_2 (0)
            //   0x0C: u16 width
            //   0x0E: u16 height
            //   0x14: u32 format
            //   0x20: u32 header_size (128)
            //   0x24: u32 payload_size
            //   0x28: u32 mip_count (16 en game8)
            ushort vmaj = BitConverter.ToUInt16(h128, 0x04);
            ushort vmin = BitConverter.ToUInt16(h128, 0x06);
            ushort w = BitConverter.ToUInt16(h128, 0x0C);
            ushort h = BitConverter.ToUInt16(h128, 0x0E);
            uint fmt = BitConverter.ToUInt32(h128, 0x14);
            uint hdrSize = BitConverter.ToUInt32(h128, 0x20);
            uint payloadSize = BitConverter.ToUInt32(h128, 0x24);
            uint mipCount = BitConverter.ToUInt32(h128, 0x28);

            if (hdrSize != 128) r.Warn("header_size=" + hdrSize + " (esperado 128)");
            if (w == 0 || h == 0) r.Error("dimensiones invalidas " + w + "x" + h);
            if (w > 8192 || h > 8192) r.Warn("dimensiones muy grandes " + w + "x" + h);
            if (w % 4 != 0 || h % 4 != 0) r.Warn("dimensiones no multiplo de 4 (" + w + "x" + h + ")");

            // FIX 51f: variante DDSC puede tener payload_size=0 y usar SizeBytes - header_size
            // como payload real. Variante AVTX normal tiene payload_size != 0.
            long effectivePayload = payloadSize;
            if (payloadSize == 0 && r.SizeBytes > hdrSize)
                effectivePayload = r.SizeBytes - hdrSize;

            if (r.SizeBytes != hdrSize + effectivePayload)
                r.Warn("tamaño real=" + r.SizeBytes + " != header+payload=" + (hdrSize + effectivePayload));

            r.Warnings.Add("version=" + vmaj + "." + vmin + " dims=" + w + "x" + h + " fmt=0x" + fmt.ToString("X4") + " mips=" + mipCount);
            return r;
        }

        // ---------- DDS ----------
        // Magic "DDS " @0. Header 124 bytes mas magic. width @16 (u32), height @12 (u32),
        // mipmapCount @28 (u32). FourCC @84 (u32) o DX10 header si dxgiFormat.
        static ValidationResult ValidateDds(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("dds");

            if (head.Length < 4 || head[0]!='D' || head[1]!='D' || head[2]!='S' || head[3]!=' ')
            { r.Error("magic DDS incorrecto"); return r; }

            if (r.SizeBytes < 128) { r.Error("tamaño < 128 bytes (header incompleto)"); return r; }

            byte[] h = new byte[128];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, 128); }

            uint hdrSize = BitConverter.ToUInt32(h, 4);
            uint height = BitConverter.ToUInt32(h, 12);
            uint width = BitConverter.ToUInt32(h, 16);
            uint mipCount = BitConverter.ToUInt32(h, 28);
            uint pfFlags = BitConverter.ToUInt32(h, 80);
            uint fourCC = BitConverter.ToUInt32(h, 84);

            if (hdrSize != 124) r.Warn("DDSD header size=" + hdrSize + " (esperado 124)");
            if (width == 0 || height == 0) r.Error("dimensiones invalidas " + width + "x" + height);
            if (width % 4 != 0 || height % 4 != 0) r.Warn("dimensiones no multiplo de 4 (" + width + "x" + height + ")");
            if (mipCount == 0) r.Warn("sin mipmaps (mipmapCount=0)");

            string fccStr = "";
            if ((pfFlags & 0x4) != 0 && fourCC != 0)
            {
                var bytes = BitConverter.GetBytes(fourCC);
                fccStr = new string(new char[] { (char)bytes[0], (char)bytes[1], (char)bytes[2], (char)bytes[3] });
                if (fccStr == "DX10") r.Warnings.Add("DX10 extended header (dxgiFormat)");
            }
            else
            {
                r.Warn("no FourCC (formato legacy; puede no ser aceptado)");
            }

            r.Warnings.Add("dims=" + width + "x" + height + " mips=" + mipCount + " fourCC=" + (fccStr == "" ? "(none)" : fccStr));
            return r;
        }

        // ---------- OGG ----------
        // Vorbis: "OggS" @0. Header 27 bytes. Byte 28+ = packet type + "vorbis" + channels u8 + sample_rate u32.
        static ValidationResult ValidateOgg(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("ogg");

            if (head.Length < 4 || head[0]!='O' || head[1]!='g' || head[2]!='g' || head[3]!='S')
            { r.Error("magic OggS incorrecto"); return r; }

            if (r.SizeBytes < 64) { r.Error("tamaño < 64 bytes (header incompleto)"); return r; }

            // Leer 64 bytes para ver el identification header de Vorbis
            byte[] h = new byte[(int)Math.Min(64, r.SizeBytes)];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, h.Length); }

            // El byte 28 deberia ser 0x01 (identification header) y 29-34 = "vorbis"
            if (h.Length >= 35)
            {
                if (h[28] != 0x01 || h[29]!='v' || h[30]!='o' || h[31]!='r' || h[32]!='b' || h[33]!='i' || h[34]!='s')
                {
                    r.Warn("Vorbis identification header no encontrado en offset 28 (puede no ser Vorbis, o payload distinto)");
                }
                else
                {
                    byte channels = h[39];
                    uint sampleRate = (uint)(h[40] | (h[41]<<8) | (h[42]<<16) | (h[43]<<24));
                    if (channels == 0 || channels > 8) r.Error("canales invalidos: " + channels);
                    if (sampleRate < 8000 || sampleRate > 192000) r.Warn("sample rate fuera de rango razonable: " + sampleRate);
                    r.Warnings.Add("channels=" + channels + " sample_rate=" + sampleRate);
                }
            }
            return r;
        }

        // ---------- RIFF (WAV) ----------
        // "RIFF" @0, size @4, "WAVE" @8. Chunks a partir de @12. Buscar "fmt " chunk.
        static ValidationResult ValidateRiff(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("riff");

            if (head.Length < 12) { r.Error("header < 12 bytes"); return r; }
            if (head[0]!='R' || head[1]!='I' || head[2]!='F' || head[3]!='F')
            { r.Error("magic RIFF incorrecto"); return r; }
            if (head[8]!='W' || head[9]!='A' || head[10]!='V' || head[11]!='E')
            { r.Warn("RIFF sin 'WAVE' (puede ser AVI u otro contenedor)"); return r; }

            // Leer hasta 64 bytes para encontrar fmt
            byte[] h = new byte[(int)Math.Min(64, r.SizeBytes)];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, h.Length); }

            // Buscar 'fmt ' en los primeros 40 bytes
            int fmtOff = -1;
            for (int i = 12; i < h.Length - 4; i++)
            {
                if (h[i]=='f' && h[i+1]=='m' && h[i+2]=='t' && h[i+3]==' ') { fmtOff = i; break; }
            }
            if (fmtOff < 0) { r.Warn("fmt chunk no encontrado en primeros 64 bytes"); return r; }

            if (fmtOff + 16 <= h.Length)
            {
                ushort audioFormat = (ushort)(h[fmtOff+8] | (h[fmtOff+9]<<8));
                ushort channels = (ushort)(h[fmtOff+10] | (h[fmtOff+11]<<8));
                uint sampleRate = (uint)(h[fmtOff+12] | (h[fmtOff+13]<<8) | (h[fmtOff+14]<<16) | (h[fmtOff+15]<<24));
                if (audioFormat != 1 && audioFormat != 3 && audioFormat != 0xFFFE)
                    r.Warn("audio_format=" + audioFormat + " (PCM=1, IEEE=3, extensible=0xFFFE). Otros pueden no ser soportados");
                if (channels == 0 || channels > 8) r.Error("canales invalidos: " + channels);
                if (sampleRate < 8000 || sampleRate > 192000) r.Warn("sample rate fuera de rango: " + sampleRate);
                r.Warnings.Add("fmt=" + audioFormat + " channels=" + channels + " sample_rate=" + sampleRate);
            }
            return r;
        }

        // ---------- BIK ----------
        // "BIKi" @0 (Bink v1) o "KB2g" @0 (Bink v2). Solo validacion estructural.
        static ValidationResult ValidateBik(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("bik");

            if (head.Length < 4) { r.Error("header < 4 bytes"); return r; }

            bool bink1 = (head[0]=='B' && head[1]=='I' && head[2]=='K' && head[3]=='i');
            bool bink2 = (head[0]=='K' && head[1]=='B' && head[2]=='2' && head[3]=='g');
            if (!bink1 && !bink2)
            { r.Error("magic Bink desconocido (esperado BIKi o KB2g)"); return r; }

            r.Warnings.Add("variante=" + (bink2 ? "Bink v2 (KB2g)" : "Bink v1 (BIKi)"));
            // Nota: la validacion profunda (resolucion multiple de 16, framerate, codec) requiere parser Bink completo.
            r.Suggest("Validacion profunda de Bink requiere RAD Video Tools. Verificar manualmente si el video se abre correctamente.");
            return r;
        }

        // ---------- CFX ----------
        // "CFX\x1F" @0. u32 uncompressed_size @4. zlib stream desde @8 (magic 78 DA).
        static ValidationResult ValidateCfx(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("cfx");

            if (head.Length < 8) { r.Error("header < 8 bytes"); return r; }
            if (head[0]!=0x43 || head[1]!=0x46 || head[2]!=0x58 || head[3]!=0x1F)
            { r.Error("magic CFX incorrecto (esperado 43 46 58 1F)"); return r; }

            uint uncompressedSize = (uint)(head[4] | (head[5]<<8) | (head[6]<<16) | (head[7]<<24));
            if (uncompressedSize == 0) r.Warn("uncompressed_size=0");

            // Verificar zlib en offset 8
            if (r.SizeBytes >= 10)
            {
                byte[] z = new byte[2];
                using (var fs = File.OpenRead(path)) { fs.Seek(8, SeekOrigin.Begin); fs.Read(z, 0, 2); }
                if (z[0] == 0x78 && (z[1] == 0xDA || z[1] == 0x9C || z[1] == 0x01))
                    r.Warnings.Add("zlib stream OK (0x" + z[0].ToString("X2") + z[1].ToString("X2") + ")");
                else
                    r.Warn("zlib stream no detectado en offset 8 (esperado 78 DA/9C/01)");
            }
            r.Warnings.Add("uncompressed_size=" + uncompressedSize);
            return r;
        }

        // ---------- RTPC ----------
        // RuntimeNodeHeader: u32 name_hash @0, u32 data_offset @4, u16 prop_count @8, u16 child_count @10.
        static ValidationResult ValidateRtpc(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("rtpc");

            if (r.SizeBytes < 12) { r.Error("tamaño < 12 bytes"); return r; }
            byte[] h = new byte[12];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, 12); }
            uint nameHash = BitConverter.ToUInt32(h, 0);
            uint dataOffset = BitConverter.ToUInt32(h, 4);
            ushort propCount = BitConverter.ToUInt16(h, 8);
            ushort childCount = BitConverter.ToUInt16(h, 10);

            if (dataOffset >= r.SizeBytes) r.Warn("data_offset=" + dataOffset + " fuera del archivo (tamaño=" + r.SizeBytes + ")");
            r.Warnings.Add("name_hash=0x" + nameHash.ToString("X8") + " data_offset=" + dataOffset + " props=" + propCount + " children=" + childCount);
            return r;
        }

        // ---------- StringLookup ----------
        // Magic 28 02 00 00 @0. u32 count @4. Resto tabla + strings.
        static ValidationResult ValidateStringLookup(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("stringlookup");

            if (r.SizeBytes < 8) { r.Error("tamaño < 8 bytes"); return r; }
            byte[] h = new byte[8];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, 8); }
            if (h[0]!=0x28 || h[1]!=0x02 || h[2]!=0x00 || h[3]!=0x00)
            { r.Error("magic incorrecto (esperado 28 02 00 00)"); return r; }
            uint count = BitConverter.ToUInt32(h, 4);
            if (count == 0 || count > 1000000) r.Warn("count=" + count + " (fuera de rango tipico)");
            r.Warnings.Add("count=" + count);
            return r;
        }

        // ---------- Havok ----------
        // Formatos: TAG0 (binary tagfile), hk (Havok header), hkcc (AnimationContainer).
        static ValidationResult ValidateHavok(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("havok");

            if (r.SizeBytes < 16) { r.Error("tamaño < 16 bytes"); return r; }

            byte[] h = new byte[(int)Math.Min(32, r.SizeBytes)];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, h.Length); }

            bool tag0 = (h[0]==0x54 && h[1]==0x41 && h[2]==0x47 && h[3]==0x30);
            bool hkcc = (h.Length >= 4 && h[0]==0x68 && h[1]==0x6B && h[2]==0x63 && h[3]==0x63);
            bool hk = (h.Length >= 2 && h[0]==0x68 && h[1]==0x6B);

            if (tag0) r.Warnings.Add("magic TAG0 (binary tagfile)");
            else if (hkcc) r.Warnings.Add("magic hkcc (AnimationContainer)");
            else if (hk) r.Warnings.Add("magic hk (Havok header)");

            // Tamano minimo tipico > 1000 bytes
            if (r.SizeBytes < 512) r.Warn("tamaño < 512 bytes (¿asset truncado?)");
            r.Suggest("Validacion profunda requiere parser Havok (SDKV, TSTR, chunk chain). Structural OK.");
            return r;
        }

        // ---------- FMOD ----------
        // FSB5/FSB4 (audio samples) o bankc/sbankc (FMOD studio). Structural.
        static ValidationResult ValidateFmod(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("fmod");

            if (r.SizeBytes < 16) { r.Error("tamaño < 16 bytes"); return r; }
            byte[] h = new byte[(int)Math.Min(16, r.SizeBytes)];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, h.Length); }

            if (h[0]==0x46 && h[1]==0x53 && h[2]==0x42)
            {
                string variant = ((char)h[3]).ToString();
                r.Warnings.Add("FSB" + variant + " (FMOD sample bank)");
                if (h.Length >= 8)
                {
                    uint sampleCount = (uint)(h[4] | (h[5]<<8) | (h[6]<<16) | (h[7]<<24));
                    if (sampleCount > 1000000) r.Warn("sample_count irreal: " + sampleCount);
                    r.Warnings.Add("sample_count~" + sampleCount);
                }
            }
            else
            {
                r.Warn("magic FSB no reconocido en primeros bytes");
            }
            r.Suggest("Validacion profunda requiere parser FSB completo. Structural OK.");
            return r;
        }

        // ---------- Mesh generico ----------
        // meshc, hrmeshc tienen header propio. Structural.
        static ValidationResult ValidateMeshGeneric(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("mesh");

            if (r.SizeBytes < 32) { r.Error("tamaño < 32 bytes"); return r; }
            r.Suggest("Validacion semantica de mesh requiere entender el schema de vertices/indices. Structural OK.");
            return r;
        }

        // ---------- ADF ----------
        // Magic " FDA" (0x20 0x46 0x44 0x41) en offset 0. Version u8 @4.
        static ValidationResult ValidateAdf(string path, byte[] head)
        {
            var r = new ValidationResult { Path = path, SizeBytes = new FileInfo(path).Length };
            r.SetValid("adf");

            if (r.SizeBytes < 16) { r.Error("tamaño < 16 bytes"); return r; }
            byte[] h = new byte[16];
            using (var fs = File.OpenRead(path)) { fs.Read(h, 0, 16); }
            if (h[0]!=0x20 || h[1]!=0x46 || h[2]!=0x44 || h[3]!=0x41)
            { r.Error("magic ADF incorrecto (esperado ' FDA')"); return r; }
            r.Warnings.Add("type=0x" + h[4].ToString("X2") + " subtype=0x" + h[5].ToString("X2"));
            r.Suggest("Validacion semantica de ADF requiere entender el schema. Structural OK.");
            return r;
        }
    }
}





