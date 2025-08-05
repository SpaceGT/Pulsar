namespace Pulsar.Updater
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // TODO: Adapt the existing installer into an updater

            MessageBox.Show(
                "Updater is still under development",
                null,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
    }
}
