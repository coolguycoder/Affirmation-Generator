using System.Windows.Forms;

namespace AffirmationImageGeneratorNice
{
    public class ProgressForm : Form
    {
        private ProgressBar progressBar;

        public ProgressForm(int max)
        {
            this.Width = 300;
            this.Height = 100;
            this.Text = "Generating...";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ControlBox = false;

            progressBar = new ProgressBar()
            {
                Minimum = 0,
                Maximum = max,
                Value = 0,
                Dock = DockStyle.Fill
            };

            this.Controls.Add(progressBar);
        }

        public void Increment()
        {
            progressBar.Value++;
        }
    }
}
