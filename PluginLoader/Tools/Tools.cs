using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Steamworks;
using VRage;

namespace avaness.PluginLoader.Tools
{
    public static class Tools
    {
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding();

        public static string Sha1HexDigest(string text)
        {
            using var sha1 = new SHA1Managed();
            var buffer = Utf8.GetBytes(text);
            var digest = sha1.ComputeHash(buffer);
            return BytesToHex(digest);
        }

        private static string BytesToHex(IReadOnlyCollection<byte> ba)
        {
            var hex = new StringBuilder(2 * ba.Count);

            foreach (var t in ba)
                hex.Append(t.ToString("x2"));

            return hex.ToString();
        }

        public static string FormatDateIso8601(DateTime dt) => dt.ToString("s").Substring(0, 10);

        public static ulong GetSteamId()
        {
            return SteamUser.GetSteamID().m_SteamID;
        }

        // FIXME: Replace this with the proper library call, I could not find one
        public static string FormatUriQueryString(Dictionary<string, string> parameters)
        {
            var query = new StringBuilder();
            foreach (var p in parameters)
            {
                if (query.Length > 0)
                    query.Append('&');
                query.Append($"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
            }
            return query.ToString();
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
            StringBuilder timestamps = new StringBuilder("");

            foreach (string path in files)
            {
                DateTime accessTimeUtc = File.GetLastWriteTimeUtc(path);
                timestamps.Append(accessTimeUtc.Ticks.ToString());
            }

            using (var md5 = MD5.Create())
            {
                byte[] inputBytes = new UTF8Encoding().GetBytes(timestamps.ToString());
                byte[] hashBytes = md5.ComputeHash(inputBytes);

                string hash = BitConverter.ToString(hashBytes);
                return hash.Replace("-", "").ToLowerInvariant();
            }
        }

        public static string GetClipboard()
        {
            string cliptext = string.Empty;

            Thread thread = new Thread(
                new ThreadStart(() => cliptext = MyVRage.Platform.System.Clipboard)
            );
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return cliptext;
        }

        public static string DateToString(DateTime? lastCheck)
        {
            if (lastCheck is null)
                return "Never";

            TimeSpan time = DateTime.UtcNow - lastCheck.Value;

            if (time.TotalMinutes < 5)
                return "Just Now";

            if (time.TotalHours < 1)
                return $"{time.Minutes} minutes ago";

            if (time.TotalHours == 1)
                return $"{time.Hours} hour ago";

            if (time.TotalDays < 3)
                return $"{time.Hours} hours ago";

            if (time.TotalDays == 3)
                return $"{time.Days} day ago";

            return $"{time.Days} days ago";
        }

        public static bool HasCommandArg(string argument)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.Equals(argument, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        }
    }
}
