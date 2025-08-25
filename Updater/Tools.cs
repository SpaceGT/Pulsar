using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Pulsar.Updater
{
    internal static class Tools
    {
        public static string? GetCommandArg(string argument)
        {
            string[] args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!args[i].Equals(argument, StringComparison.InvariantCultureIgnoreCase))
                    continue;

                return args[i + 1];
            }

            return null;
        }

        public static bool HasCommandArg(string argument)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.Equals(argument, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        }

        public static string GetFolderHash(string folderPath, string glob = "*")
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            List<string> files =
            [
                .. Directory
                    .GetFiles(folderPath, glob, SearchOption.AllDirectories)
                    .OrderBy(Path.GetFileName),
            ];
            StringBuilder timestamps = new("");

            foreach (string path in files)
            {
                DateTime accessTimeUtc = File.GetLastWriteTimeUtc(path);
                timestamps.Append(accessTimeUtc.Ticks.ToString());
            }

            using var md5 = MD5.Create();
            byte[] inputBytes = new UTF8Encoding().GetBytes(timestamps.ToString());
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            string hash = BitConverter.ToString(hashBytes);
            return hash.Replace("-", "").ToLowerInvariant();
        }
    }
}
