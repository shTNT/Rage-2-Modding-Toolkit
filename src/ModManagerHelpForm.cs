// ModManagerHelpForm.cs - Quick help dialog for Mod Manager.
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class ModManagerHelpForm : Form
    {
        public ModManagerHelpForm()
        {
            Color C_BG = Color.FromArgb(24, 24, 30);
            Color C_HEAD = Color.FromArgb(15, 15, 20);
            Color C_MAGENTA = Color.FromArgb(220, 80, 220);
            Color C_TEXT = Color.FromArgb(210, 210, 210);

            this.Text = "Mod Manager - Quick help";
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Size = new Size(680, 640);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            var banner = new AccentBar();
            banner.BackColor = C_MAGENTA;
            banner.Dock = DockStyle.Top;
            banner.Height = 6;
            this.Controls.Add(banner);

            var title = new Label();
            title.Text = "Mod Manager - Quick help";
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = C_MAGENTA;
            title.Location = new Point(20, 20);
            title.AutoSize = true;
            this.Controls.Add(title);

            var txt = new RichTextBox();
            txt.Multiline = true;
            txt.ScrollBars = RichTextBoxScrollBars.Vertical;
            txt.WordWrap = true;
            txt.ReadOnly = true;
            txt.BackColor = C_HEAD;
            txt.ForeColor = C_TEXT;
            txt.Font = new Font("Consolas", 9F);
            txt.Location = new Point(20, 60);
            txt.Size = new Size(640, 460);
            txt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            txt.Text = BuildHelp();
            this.Controls.Add(txt);

            var btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.Location = new Point(560, 535);
            btnClose.Size = new Size(100, 32);
            btnClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnClose.BackColor = C_HEAD;
            btnClose.ForeColor = Color.White;
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderColor = C_MAGENTA;
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
        }

        static string BuildHelp()
        {
            var sb = new StringBuilder();
            sb.AppendLine("MOD MANAGER - QUICK HELP");
            sb.AppendLine("========================");
            sb.AppendLine();
            sb.AppendLine("WHERE THINGS LIVE");
            sb.AppendLine("  <toolkit>/mods/mods.json       installed mods registry");
            sb.AppendLine("  <toolkit>/mods/library/        pristine .zip of each installed mod");
            sb.AppendLine("  <toolkit>/mods/backups/        per-arc .original backups");
            sb.AppendLine();
            sb.AppendLine("INSTALL A MOD");
            sb.AppendLine("  1. Drag a .zip onto the window, or use Install .zip");
            sb.AppendLine("  2. Toolkit reads mod.json (if present), validates every asset");
            sb.AppendLine("  3. Hash-level conflict check against installed mods");
            sb.AppendLine("  4. Rebuild the target .arc with the new content");
            sb.AppendLine("  5. Backup <univ>_<gameN>.arc.original created if first touch");
            sb.AppendLine();
            sb.AppendLine("PRIORITY (MO2-style)");
            sb.AppendLine("  Higher number = higher priority. Default: 100.");
            sb.AppendLine("  When two mods touch the same hash, the highest priority wins.");
            sb.AppendLine("  Use the arrow buttons to adjust. Rebuilds reapply mods by priority ASC.");
            sb.AppendLine();
            sb.AppendLine("CONFLICTS");
            sb.AppendLine("  Hash-level, not file-level. Two mods touching the same .arc");
            sb.AppendLine("  with DIFFERENT hashes = no conflict. Same hash, different");
            sb.AppendLine("  content = conflict. Install fails unless you Force.");
            sb.AppendLine();
            sb.AppendLine("UNINSTALL");
            sb.AppendLine("  Removes the mod from the registry, then:");
            sb.AppendLine("    - No other mod touches the arc: restore .original");
            sb.AppendLine("    - Other mods remain: rebuild with the remaining mods");
            sb.AppendLine();
            sb.AppendLine("RESTORE ALL");
            sb.AppendLine("  Nuclear option: restores every backup, drops mods.json.");
            return sb.ToString();
        }
    }
}