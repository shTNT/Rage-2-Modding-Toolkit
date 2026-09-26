// Dialogs.cs - ventanas auxiliares (Guidelines, ConvertChoice).
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    // ============================================================
    // GuidelinesForm - matriz de formatos soportados y conversores
    // ============================================================
    public class GuidelinesForm : Form
    {
        public GuidelinesForm()
        {
            Color C_BG    = Color.FromArgb(24, 24, 30);
            Color C_HEAD  = Color.FromArgb(15, 15, 20);
            Color C_INFO  = Color.FromArgb(0, 200, 200);
            Color C_PANEL = Color.FromArgb(30, 30, 38);

            this.Text = "Format & Files Guidelines";
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Size = new Size(920, 720);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(720, 520);
            this.Font = new Font("Segoe UI", 9F);

            var title = new Label {
                Text = "Format && Files Guidelines",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = C_INFO,
                Location = new Point(20, 15),
                AutoSize = true
            };
            this.Controls.Add(title);

            var sub = new Label {
                Text = "Requirements for files before repacking. If a format is not marked as 'native', convert it first with the toolkit.",
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(20, 55),
                Size = new Size(880, 40)
            };
            this.Controls.Add(sub);

            var txt = new RichTextBox {
                Multiline = true,
                ScrollBars = RichTextBoxScrollBars.Both,
                WordWrap = false,
                ReadOnly = true,
                BackColor = C_HEAD,
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Consolas", 9F),
                Location = new Point(20, 105),
                Size = new Size(880, 550),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            txt.Text = BuildGuidelines();
            this.Controls.Add(txt);

            var btnClose = new Button {
                Text = "Close",
                Location = new Point(800, 665),
                Size = new Size(100, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                BackColor = C_PANEL,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btnClose.FlatAppearance.BorderColor = C_INFO;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        static string BuildGuidelines()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RAGE 2 Modding Toolkit - Format & Files Guidelines");
            sb.AppendLine("====================================================");
            sb.AppendLine();
            sb.AppendLine("  ##########################################################");
            sb.AppendLine("  #                                                        #");
            sb.AppendLine("  #   THE FILENAME IS THE HASH - READ THIS FIRST           #");
            sb.AppendLine("  #                                                        #");
            sb.AppendLine("  ##########################################################");
            sb.AppendLine();
            sb.AppendLine("RAGE 2 has NO NAMES inside its archives. Every asset is");
            sb.AppendLine("identified by a 64-bit hash derived from its original path.");
            sb.AppendLine("The toolkit uses the FILENAME (without extension) as that");
            sb.AppendLine("hash. That is the ONLY way it knows WHICH asset you are");
            sb.AppendLine("trying to replace.");
            sb.AppendLine();
            sb.AppendLine("  VALID:    4E37BD8EAD14BEA2.dds   (16 hex chars)");
            sb.AppendLine("  VALID:    4E37BD8EAD14BEA2.png");
            sb.AppendLine("  INVALID:  wallpaper.png           (not a hash)");
            sb.AppendLine("  INVALID:  my_texture.ddsc         (not a hash)");
            sb.AppendLine("  INVALID:  4E37BD8E.dds            (too short)");
            sb.AppendLine();
            sb.AppendLine("If the filename is not a valid 16-hex hash, the toolkit WILL");
            sb.AppendLine("IGNORE the file. It will NOT guess. It cannot.");
            sb.AppendLine();
            sb.AppendLine("WORKFLOW TO REPLACE A TEXTURE (or any asset):");
            sb.AppendLine();
            sb.AppendLine("  1. EXTRACT the game, or use the 'Browse & pick manually'");
            sb.AppendLine("     option in the Extract wizard to grab a single asset.");
            sb.AppendLine("  2. Locate what you want: e.g. 4E37BD8EAD14BEA2.dds");
            sb.AppendLine("  3. If you need to edit it, convert it to PNG:");
            sb.AppendLine("       4E37BD8EAD14BEA2.dds  ->  4E37BD8EAD14BEA2.png");
            sb.AppendLine("  4. Edit the PNG in Photoshop / GIMP / Paint.NET.");
            sb.AppendLine("  5. KEEP THE SAME FILENAME. Only the extension can change.");
            sb.AppendLine("     The hash MUST be preserved character for character.");
            sb.AppendLine("  6. Drop the folder in the wizard, choose 'Convert with toolkit'.");
            sb.AppendLine("  7. Toolkit converts PNG -> DDSC and repacks it into the");
            sb.AppendLine("     correct .arc entry automatically. Game sees new texture.");
            sb.AppendLine();
            sb.AppendLine("Common mistakes:");
            sb.AppendLine("  - Renaming the file to 'rifle.png'              -> ignored");
            sb.AppendLine("  - Saving as '4E37BD8EAD14BEA2 (copy).png'       -> ignored");
            sb.AppendLine("  - Converting extension to uppercase .DDS vs .dds -> OK, both work");
            sb.AppendLine("  - Editing size (256x256 -> 512x512)             -> often breaks game");
            sb.AppendLine();
            sb.AppendLine("  ========================================================");
            sb.AppendLine("  FORMAT REFERENCE (for each extension)");
            sb.AppendLine("  ========================================================");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("TEXTURES");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .ddsc   native    Already game-ready.");
            sb.AppendLine("  .avtx   native    AVTX container (magic 'AVTX' @ 0).");
            sb.AppendLine("  .atx1   native    Climate zone texture.");
            sb.AppendLine("  .dds    convert   Auto-converted to .ddsc by toolkit on rebuild.");
            sb.AppendLine("  .png    convert   NOT accepted by the game. Requires conversion:");
            sb.AppendLine("                    dimensions multiple of 4, full mipmap chain,");
            sb.AppendLine("                    format BC1/BC3/BC5/BC6H/BC7.");
            sb.AppendLine("  .jpg    convert   Same requirements as PNG.");
            sb.AppendLine("  .tga    convert   Same requirements as PNG.");
            sb.AppendLine("  .bmp    convert   Same requirements as PNG.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("AUDIO");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .ogg    native    OGG Vorbis. Editable with Audacity.");
            sb.AppendLine("  .riff   native    RIFF container.");
            sb.AppendLine("  .wav    convert   WAV not accepted. Convert to OGG (ffmpeg).");
            sb.AppendLine("  .mp3    convert   MP3 not accepted. Convert to OGG (ffmpeg).");
            sb.AppendLine("  .flac   convert   Same as MP3.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("VIDEO");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .bk2    native    Bink v2 video.");
            sb.AppendLine("  .bik    native    Bink v1 video (legacy).");
            sb.AppendLine("  .bikc   native    Bink video in .arc container.");
            sb.AppendLine("  .mp4    manual    NOT accepted. Requires RAD Video Tools.");
            sb.AppendLine("  .avi    manual    Same as MP4.");
            sb.AppendLine("  .mov    manual    Same as MP4.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("UI (Scaleform GFx)");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .gfx    native    Compiled Scaleform movie.");
            sb.AppendLine("  .cfx    native    Scaleform with CFX 1F magic + zlib.");
            sb.AppendLine("  .swf    manual    Compile to CFX with JPEXS Decompiler.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("SCRIPTS / DATA / CONFIG");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .adf    native    ADF script container.");
            sb.AppendLine("  .ee     native    Encounter editor data.");
            sb.AppendLine("  .nl     native    Location data.");
            sb.AppendLine("  .bl     native    Blend layer data.");
            sb.AppendLine("  .json   native    JSON. Any text editor.");
            sb.AppendLine("  .bin    native    Binary config.");
            sb.AppendLine("  .tag    native    Tag data.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("MESH / ANIMATION (no editor available - native only)");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .meshc     native   Static mesh.");
            sb.AppendLine("  .hrmeshc   native   Hierarchical mesh.");
            sb.AppendLine("  .navmeshc  native   Navigation mesh.");
            sb.AppendLine("  .graphc    native   Graph definition.");
            sb.AppendLine("  .ban       native   Animation.");
            sb.AppendLine("  .hikcc     native   Havok animation container.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("PROCEDURAL (not editable usefully)");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  .streampatch   native   Terrain patch (generated by engine).");
            sb.AppendLine("  .rawc          native   Raw data.");
            sb.AppendLine();
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("CONVERTERS AVAILABLE IN THE TOOLKIT");
            sb.AppendLine("--------------------------------------------------------");
            sb.AppendLine("  PNG/JPG/BMP/TGA  ->  DDS      (texconv, bundled)");
            sb.AppendLine("  DDS              ->  DDSC     (ddscConvert, bundled)");
            sb.AppendLine("  PNG/JPG          ->  DDSC     (combined, bundled)");
            sb.AppendLine("  MP3/WAV/FLAC     ->  OGG      (ffmpeg, on-demand download)");
            sb.AppendLine();
            sb.AppendLine("  External tools (manual install, not redistributed):");
            sb.AppendLine("    RAD Video Tools      https://www.radgametools.com/bnkdown.htm");
            sb.AppendLine("    JPEXS Decompiler     https://github.com/jindrapetrik/jpexs-decompiler");
            sb.AppendLine("    FMOD Studio          https://www.fmod.com/download");
            return sb.ToString();
        }
    }

    // ============================================================
    // ConvertChoiceForm - pregunta al usuario si sus archivos ya estan
    // preparados o quiere que el toolkit los convierta.
    // Retorna: 1 = ya preparados, 2 = convertir, 0 = cancelar
    // ============================================================
    public class ConvertChoiceForm : Form
    {
        public int Choice = 0;

        public ConvertChoiceForm(int fileCount, int convertibleCount)
        {
            Color C_BG    = Color.FromArgb(24, 24, 30);
            Color C_HEAD  = Color.FromArgb(15, 15, 20);
            Color C_INFO  = Color.FromArgb(0, 200, 200);
            Color C_MAGENTA = Color.FromArgb(220, 80, 220);
            Color C_PANEL = Color.FromArgb(30, 30, 38);
            Color C_GRAY  = Color.FromArgb(160, 160, 160);

            this.Text = "How should I process your files?";
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Size = new Size(600, 380);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            var title = new Label {
                Text = "How should I process your files?",
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = C_INFO,
                Location = new Point(20, 20),
                AutoSize = true
            };
            this.Controls.Add(title);

            var info = new Label {
                Text = "Detected " + fileCount + " files, " + convertibleCount + " of them convertible.",
                ForeColor = C_GRAY,
                Location = new Point(20, 55),
                Size = new Size(560, 22)
            };
            this.Controls.Add(info);

            var desc = new Label {
                Text = "Option A: files are already in a format the game accepts.\r\n" +
                       "  Only validation will run. If a file needs conversion, it will\r\n" +
                       "  be reported and skipped.\r\n\r\n" +
                       "Option B: use the toolkit converters.\r\n" +
                       "  PNG/JPG/TGA/BMP -> DDS -> DDSC using bundled texconv + ddscConvert.\r\n" +
                       "  MP3/WAV/FLAC -> OGG (requires ffmpeg; prompt if missing).\r\n" +
                       "  Native files are copied unmodified.",
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(20, 90),
                Size = new Size(560, 160)
            };
            this.Controls.Add(desc);

            var btnReady = new Button {
                Text = "A  ·  My files are game-ready",
                Location = new Point(30, 280),
                Size = new Size(270, 46),
                BackColor = C_PANEL,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            btnReady.FlatAppearance.BorderColor = C_INFO;
            btnReady.Click += (s, e) => { this.Choice = 1; this.Close(); };
            this.Controls.Add(btnReady);

            var btnConv = new Button {
                Text = "B  ·  Convert with toolkit",
                Location = new Point(310, 280),
                Size = new Size(270, 46),
                BackColor = C_PANEL,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10F)
            };
            btnConv.FlatAppearance.BorderColor = C_MAGENTA;
            btnConv.Click += (s, e) => { this.Choice = 2; this.Close(); };
            this.Controls.Add(btnConv);

            // v2.0: boton Cancel eliminado (redundante con la X de la ventana).
            // ESC tambien cancela y deja Choice=0, que el llamante trata como abort.
            this.KeyPreview = true;
            this.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Escape) { this.Choice = 0; this.Close(); }
            };
        }
    }

    // ============================================================
    // Cfa - deteccion de Windows Controlled Folder Access
    // ============================================================
    internal static class Cfa
    {
        static readonly string[] ProtectedSubs = new[] {
            "\\Desktop", "\\Documents", "\\Pictures", "\\Videos", "\\Music",
            "\\Favorites", "\\OneDrive", "\\3D Objects", "\\Saved Games",
            "\\Contacts", "\\Links", "\\Searches"
        };

        public static bool IsProtected(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                string full = System.IO.Path.GetFullPath(path).TrimEnd('\\');
                string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
                if (string.IsNullOrEmpty(profile)) return false;
                foreach (var s in ProtectedSubs)
                {
                    string sub = profile + s;
                    if (full.Equals(sub, StringComparison.OrdinalIgnoreCase) ||
                        full.StartsWith(sub + "\\", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { }
            return false;
        }

        public static bool WarnIfProtected(IWin32Window owner, string path)
        {
            if (!IsProtected(path)) return true;
            string msg =
                "The folder you selected is inside a Windows protected location\r\n" +
                "(Desktop / Documents / Pictures / Videos / Music / OneDrive).\r\n\r\n" +
                "Windows Defender's \"Controlled Folder Access\" will block texconv.exe,\r\n" +
                "ddscConvert.exe and ffmpeg from writing into it. The conversion will\r\n" +
                "fail mid-operation with an \"access blocked\" notification.\r\n\r\n" +
                "Recommended: pick a folder OUTSIDE these protected locations.\r\n" +
                "Example: D:\\RAGE2MODDING\\output or any non-system drive.\r\n\r\n" +
                "Press OK to keep this folder anyway,\r\n" +
                "or Cancel to pick a different folder.";
            var r = MessageBox.Show(owner, msg, "Protected folder detected",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            return r == DialogResult.OK;
        }
    }
}

