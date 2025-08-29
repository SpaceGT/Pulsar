using System;
using System.Drawing;
using System.Windows.Forms;

namespace Pulsar.Shared.Splash;

public partial class SplashScreen : Form
{
    public float BarValue { get; private set; } = float.NaN;

    private readonly Color barColor;
    private readonly RectangleF barRectangle;

    public SplashScreen()
    {
        InitializeComponent();

        barColor = progressBar.ForeColor;
        barRectangle = progressBar.ClientRectangle;

        progressText.Text = "";
        progressBar.Visible = false;
        progressBar.Paint += DrawBar;

        CenterToScreen();
        Show();
    }

    public void SetText(string msg)
    {
        BarValue = float.NaN;
        progressBar.Visible = false;
        progressText.Text = msg;
        progressText.Invalidate();
    }

    public void SetBarValue(float ratio = float.NaN)
    {
        if (float.IsNaN(ratio))
            progressBar.Visible = false;
        else
        {
            progressBar.Visible = true;
            ratio = Math.Min(1f, Math.Max(0f, ratio));
        }

        BarValue = ratio;
        progressBar.Invalidate();
    }

    private void DrawBar(object sender, PaintEventArgs e)
    {
        if (float.IsNaN(BarValue))
            return;

        Graphics graphics = e.Graphics;
        SizeF size = new(barRectangle.Width * BarValue, barRectangle.Height);
        RectangleF currentBar = new(barRectangle.Location, size);
        graphics.FillRectangle(new SolidBrush(barColor), currentBar);
    }

    public void Delete()
    {
        Paint -= DrawBar;
        Close();
        Dispose();
    }
}
