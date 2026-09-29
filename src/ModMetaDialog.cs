// ModMetaDialog.cs - pide Name y Author antes de empaquetar el .zip.
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Rage2Toolkit
{
    public class ModMetaDialog : Form
    {
        public string ModName = "";
        public string ModAuthor = "";

        public ModMetaDialog(string defaultName, string defaultAuthor)
        {
            Color C_BG = Color.FromArgb(24, 24, 30);
            Color C_HEAD = Color.FromArgb(15, 15, 20);
            Color C_MAGENTA = Color.FromArgb(220, 80, 220);
            Color C_PANEL = Color.FromArgb(30, 30, 38);

            this.Text = "Pack mod - metadata";
            this.BackColor = C_BG;
            this.ForeColor = Color.White;
            this.ClientSize = new Size(520, 320);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            var banner = new AccentBar();
            banner.BackColor = C_MAGENTA;
            banner.Location = new Point(0, 0);
            banner.Size = new Size(this.ClientSize.Width, 6);
            banner.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(banner);

            var title = new Label();
            title.Text = "Sign your mod";
            title.Font = new Font("Segoe UI", 14F, FontStyle.Bold);
            title.ForeColor = C_MAGENTA;
            title.Location = new Point(20, 20);
            title.AutoSize = true;
            this.Controls.Add(title);

            var sub = new Label();
            sub.Text = "These fields go into the mod.json inside the .zip.\r\nBoth are required. The mod will be signed under this name.";
            sub.ForeColor = Color.FromArgb(200, 200, 200);
            sub.Location = new Point(20, 55);
            sub.Size = new Size(480, 44);
            this.Controls.Add(sub);

            var lblName = new Label();
            lblName.Text = "Mod name";
            lblName.ForeColor = Color.White;
            lblName.Location = new Point(20, 115);
            lblName.AutoSize = true;
            this.Controls.Add(lblName);

            var txtName = new TextBox();
            txtName.Text = defaultName ?? "";
            txtName.BackColor = C_PANEL;
            txtName.ForeColor = Color.White;
            txtName.BorderStyle = BorderStyle.FixedSingle;
            txtName.Font = new Font("Segoe UI", 10F);
            txtName.Location = new Point(20, 135);
            txtName.Size = new Size(480, 26);
            this.Controls.Add(txtName);

            var lblAuthor = new Label();
            lblAuthor.Text = "Author";
            lblAuthor.ForeColor = Color.White;
            lblAuthor.Location = new Point(20, 175);
            lblAuthor.AutoSize = true;
            this.Controls.Add(lblAuthor);

            var txtAuthor = new TextBox();
            txtAuthor.Text = defaultAuthor ?? "";
            txtAuthor.BackColor = C_PANEL;
            txtAuthor.ForeColor = Color.White;
            txtAuthor.BorderStyle = BorderStyle.FixedSingle;
            txtAuthor.Font = new Font("Segoe UI", 10F);
            txtAuthor.Location = new Point(20, 195);
            txtAuthor.Size = new Size(480, 26);
            this.Controls.Add(txtAuthor);

            var btnCancel = new Button();
            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(300, 260);
            btnCancel.Size = new Size(90, 34);
            btnCancel.BackColor = C_PANEL;
            btnCancel.ForeColor = Color.White;
            btnCancel.FlatStyle = FlatStyle.Flat;
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 110);
            btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);

            var btnOk = new Button();
            btnOk.Text = "Pack .zip";
            btnOk.Location = new Point(400, 260);
            btnOk.Size = new Size(100, 34);
            btnOk.BackColor = C_MAGENTA;
            btnOk.ForeColor = Color.Black;
            btnOk.FlatStyle = FlatStyle.Flat;
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            btnOk.Click += (s, e) =>
            {
                string n = (txtName.Text ?? "").Trim();
                string a = (txtAuthor.Text ?? "").Trim();
                if (string.IsNullOrEmpty(n)) { MessageBox.Show(this, "Mod name required.", "Missing name", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (string.IsNullOrEmpty(a)) { MessageBox.Show(this, "Author required.", "Missing author", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                this.ModName = n;
                this.ModAuthor = a;
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            this.Controls.Add(btnOk);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;
        }
    }
}