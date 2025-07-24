using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace Pulsar.Shared.Splash
{
    public class SplashScreen : Form
    {
        public float BarValue { get; private set; } = float.NaN;

        private static readonly Size originalSplashSize = new Size(1024, 576);
        private static readonly PointF originalSplashScale = new PointF(0.7f, 0.7f);

        private const float barWidth = 0.98f; // 98% of width
        private const float barHeight = 0.06f; // 6% of height
        private static readonly Color backgroundColor = Color.FromArgb(0, 0, 0);

        public readonly bool invalid;
        private readonly Label lbl;
        private readonly PictureBox gifBox;
        private readonly PictureBox splashText;
        private readonly RectangleF bar;

        public SplashScreen()
        {
            if (!TryLoadImages(out Image gif, out Image text))
            {
                invalid = true;
                return;
            }

            Size = new Size(
                (int)(originalSplashSize.Width * originalSplashScale.X),
                (int)(originalSplashSize.Height * originalSplashScale.Y)
            );

            Name = "SplashScreenPulsar";
            FormBorderStyle = FormBorderStyle.None;
            BackColor = backgroundColor;

            Assembly assembly = typeof(SplashScreen).Assembly;
            Icon = new Icon(assembly.GetManifestResourceStream("Pulsar.Shared.Splash.icon.ico"));

            SizeF barSize = new(Size.Width * barWidth, Size.Height * barHeight);
            float padding = (1 - barWidth) * Size.Width * 0.5f;
            PointF barStart = new(padding, Size.Height - barSize.Height - padding);
            bar = new RectangleF(barStart, barSize);

            Font lblFont = new(FontFamily.GenericSansSerif, 12, FontStyle.Bold);
            lbl = new Label
            {
                Name = "PulsarInfo",
                Font = lblFont,
                BackColor = backgroundColor,
                ForeColor = Color.White,
                MaximumSize = Size,
                Size = new Size(Size.Width, lblFont.Height),
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(0, (int)(barStart.Y - lblFont.Height - 1)),
            };
            Controls.Add(lbl);

            gifBox = new PictureBox()
            {
                Name = "PulsarAnimation",
                Image = gif,
                Size = new Size(250, 250),
                AutoSize = false,
                SizeMode = PictureBoxSizeMode.StretchImage,
                Location = new Point((Size.Width / 2) - 295, (Size.Height / 2) - 175),
            };
            Controls.Add(gifBox);

            splashText = new PictureBox()
            {
                Name = "PulsarSplashText",
                Image = text,
                Size = new Size(340, 250),
                AutoSize = false,
                SizeMode = PictureBoxSizeMode.Normal,
                Location = new Point(gifBox.Location.X + gifBox.Width, gifBox.Location.Y),
            };
            Controls.Add(splashText);

            Paint += DrawBar;

            CenterToScreen();
            Show();
            ForceUpdate();
        }

        private bool TryLoadImages(out Image img, out Image spashText)
        {
            Assembly assembly = typeof(SplashScreen).Assembly;

            try
            {
                Stream throbberStream = assembly.GetManifestResourceStream(
                    "Pulsar.Shared.Splash.throbber.gif"
                );
                img = new Bitmap(throbberStream);

                Stream textStream2 = assembly.GetManifestResourceStream(
                    "Pulsar.Shared.Splash.text.png"
                );
                spashText = new Bitmap(textStream2);
                return true;
            }
            catch
            {
                img = null;
                spashText = null;
                return false;
            }
        }

        public void SetText(string msg)
        {
            if (invalid)
                return;

            lbl.Text = msg;
            BarValue = float.NaN;
            Invalidate();
            ForceUpdate();
        }

        public void SetBarValue(float ratio = float.NaN)
        {
            if (invalid)
                return;

            BarValue = ratio;
            Invalidate();
            ForceUpdate();
        }

        private void ForceUpdate()
        {
            Application.DoEvents();
        }

        private void DrawBar(object sender, PaintEventArgs e)
        {
            if (float.IsNaN(BarValue))
                return;

            Graphics graphics = e.Graphics;
            graphics.FillRectangle(Brushes.DarkSlateGray, bar);
            graphics.FillRectangle(
                Brushes.White,
                new RectangleF(bar.Location, new SizeF(bar.Width * BarValue, bar.Height))
            );
        }

        public void Delete()
        {
            if (invalid)
                return;

            Paint -= DrawBar;
            Close();
            Dispose();
            ForceUpdate();
        }
    }
}
