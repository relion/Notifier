using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Notifier
{
    public partial class NotificationForm : Form
    {
        private Timer timer;
        
        public NotificationForm()
        {
            InitializeComponent();
            this.Text = "Notifier";
            this.Icon = SystemIcons.Information;
            this.FormBorderStyle = FormBorderStyle.Fixed3D;
            this.StartPosition = FormStartPosition.Manual;
            this.TopMost = true;
            this.BackColor = Color.LightYellow;
            this.Size = new Size(300, 100);

            return;
            timer = new Timer();
            timer.Interval = 5000; // 5 seconds
            timer.Tick += (s, e) => { this.Close(); };
        }

        public void SetMessage(string message)
        {
            foreach (Control control in this.Controls.OfType<Label>().ToList())
            {
                this.Controls.Remove(control);
                control.Dispose();
            }

            Label label = new Label()
            {
                Text = message,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 12, FontStyle.Bold)
            };
            this.Controls.Add(label);
            //timer.Start();
        }
    }
}
