using System.Windows.Forms;

namespace Pulsar.Modern;

static class Program
{
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();

        MessageBox.Show(
            "Pulsar for SE2 is still under development",
            null,
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }
}
