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
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // TODO: Adapt the existing installer into an updater

            // Headless udpating is the main focus but the existing UI may be ported over and used if ran independently
            // Application.Run(new Form1());
        }
    }
}
