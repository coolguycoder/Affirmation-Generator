
using System;
using System.Windows.Forms;

namespace AffirmationImageGeneratorNice
{
    public class SettingsForm : Form
    {
        public CheckBox chkRandomBase;
        public CheckBox chkProcessAllImages;

        public SettingsForm(bool randomBase, bool processAllImages)
        {
            this.Text = "Settings";
            this.Width = 400;
            this.Height = 200;
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            chkRandomBase = new CheckBox()
            {
                Text = "Randomize image from folder for each affirmation",
                Checked = randomBase,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 20)
            };

            chkProcessAllImages = new CheckBox()
            {
                Text = "Generate an image for every file for every affirmation",
                Checked = processAllImages,
                AutoSize = true,
                Location = new System.Drawing.Point(20, 60)
            };

            Button btnOk = new Button()
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(150, 120)
            };

            this.Controls.Add(chkRandomBase);
            this.Controls.Add(chkProcessAllImages);
            this.Controls.Add(btnOk);
            this.AcceptButton = btnOk;
        }
    }
}
