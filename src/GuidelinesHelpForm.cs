// GuidelinesHelpForm.cs - explains the Guidelines subwindow.
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class GuidelinesHelpForm : Form
    {
        public GuidelinesHelpForm()
        {
            Color C_BG = Color.FromArgb(24, 24, 30);
            Color C_HEAD = Color.FromArgb(15, 15, 20);
            Color C_MAGENTA = Color.FromArgb(220, 80, 220);
            Color C_TEXT = Color.FromArgb(210, 210, 210);

            this.Text = "Guidelines - How this works";
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.Size = new Size(720, 620);
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
            title.Text = "Guidelines - How this works";
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
            txt.Size = new Size(680, 480);
            txt.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            txt.Text = BuildHelp();
            this.Controls.Add(txt);

            var btnClose = new Button();
            btnClose.Text = "Close";
            btnClose.Location = new Point(600, 555);
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
            sb.AppendLine("GUIDELINES - HOW THIS WINDOW WORKS");
            sb.AppendLine("==================================");
            sb.AppendLine();
            sb.AppendLine("This window is a REFERENCE. It is not the wizard.");
            sb.AppendLine("You will only see it if you click the Help button or");
            sb.AppendLine("the question-mark icon in some toolkit dialogs.");
            sb.AppendLine();
            sb.AppendLine("HOW TO READ THE FORMAT TABLE");
            sb.AppendLine("  Each extension has a STATUS tag:");
            sb.AppendLine();
            sb.AppendLine("  [NATIVE]   Already game-ready. Drop it as-is. The");
            sb.AppendLine("             Repack wizard detects it and copies it");
            sb.AppendLine("             into the correct .arc entry without");
            sb.AppendLine("             touching the bytes.");
            sb.AppendLine();
            sb.AppendLine("  [CONVERT]  The toolkit will run the bundled converter");
            sb.AppendLine("             (texconv / ddscConvert / ffmpeg) to turn");
            sb.AppendLine("             your file into a [NATIVE] format before");
            sb.AppendLine("             repacking.");
            sb.AppendLine();
            sb.AppendLine("  [MANUAL]   The toolkit cannot convert this. You must");
            sb.AppendLine("             produce a [NATIVE] file yourself using an");
            sb.AppendLine("             external tool (listed at the bottom of the");
            sb.AppendLine("             Guidelines window). The wizard warns and");
            sb.AppendLine("             skips it if left as-is.");
            sb.AppendLine();
            sb.AppendLine("  [SKIP]     Toolkit silently ignores the file. It will");
            sb.AppendLine("             not be repacked. Useful for README, notes,");
            sb.AppendLine("             or anything you do not want in the .arc.");
            sb.AppendLine();
            sb.AppendLine("HOW VALIDATION WORKS");
            sb.AppendLine("  Every time you drop a folder on the wizard, the toolkit:");
            sb.AppendLine("    1. Lists every file recursively.");
            sb.AppendLine("    2. Checks that the filename is a 16-hex hash.");
            sb.AppendLine("    3. Parses the magic bytes (first 4 bytes of the file).");
            sb.AppendLine("    4. Classifies as NATIVE / CONVERT / MANUAL / SKIP.");
            sb.AppendLine("    5. Reports what will happen BEFORE any write.");
            sb.AppendLine();
            sb.AppendLine("  If you see a red X next to a file, the wizard has");
            sb.AppendLine("  rejected it. Fix the filename or format and re-drop.");
            sb.AppendLine();
            sb.AppendLine("WHAT THIS WINDOW IS NOT");
            sb.AppendLine("  - Not a list of files you currently have.");
            sb.AppendLine("  - Not a preview of what will be repacked.");
            sb.AppendLine("  - Not a live status. It is a static reference table.");
            sb.AppendLine();
            sb.AppendLine("RELATED WINDOWS");
            sb.AppendLine("  - Repack Wizard: click Choose folder or drop a folder");
            sb.AppendLine("    on the main Extract screen, then MODDING, then Repacker.");
            sb.AppendLine("  - Mod Manager: handles .zip install/uninstall.");
            sb.AppendLine();
            sb.AppendLine("Extract / Convert / Repack - that is the workflow.");
            return sb.ToString();
        }
    }
}