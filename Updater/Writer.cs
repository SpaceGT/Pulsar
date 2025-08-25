using System.IO;
using System.IO.Compression;
using System.Text;

namespace Pulsar.Updater
{
    internal static class Writer
    {
        public static void Update(ZipArchive source, string destination)
        {
            string libraries = Path.Combine(destination, "Libraries");
            string checkfile = Path.Combine(destination, "checksum.txt");

            if (Directory.Exists(libraries))
                Directory.Delete(libraries, recursive: true);

            TryDeleteFile(destination, "Legacy.exe", true);
            TryDeleteFile(destination, "Modern.exe", true);
            TryDeleteFile(destination, "LICENSE", false);

            source.ExtractToDirectory(destination);
            WriteCheckSum(checkfile, libraries);
        }

        private static void TryDeleteFile(string folder, string name, bool hasConfig = false)
        {
            string path = Path.Combine(folder, name);

            if (File.Exists(path))
                File.Delete(path);

            if (hasConfig && File.Exists(path + ".config"))
                File.Delete(path + ".config");
        }

        private static void WriteCheckSum(string file, string folder)
        {
            UTF8Encoding encoding = new();
            string checksum = Tools.GetFolderHash(folder);
            File.WriteAllText(file, checksum, encoding);
        }
    }
}
