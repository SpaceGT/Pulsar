using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Network
{
    public static class GitHub
    {
        private const string hashUrl = "https://api.github.com/repos/{0}/commits/{1}";
        private const string versionUrl = "https://api.github.com/repos/{0}/releases/latest";
        private const string repoZipUrl = "https://github.com/{0}/archive/{1}.zip";
        private const string rawUrl = "https://raw.githubusercontent.com/{0}/{1}/";

        public static void Init()
        {
            // Fix tls 1.2 not supported on Windows 7 - github.com is tls 1.2 only
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch (NotSupportedException e)
            {
                LogFile.Error(
                    "An error occurred while setting up networking, web requests will probably fail: "
                        + e
                );
            }
        }

        public static Stream GetStream(Uri uri)
        {
            HttpWebRequest request = WebRequest.CreateHttp(uri);
            request.UserAgent = "Pulsar.Shared";
            request.AutomaticDecompression =
                DecompressionMethods.GZip | DecompressionMethods.Deflate;
            PluginConfig config = ConfigManager.Instance.Config;
            request.Timeout = config.NetworkTimeout;
            if (!config.AllowIPv6)
                request.ServicePoint.BindIPEndPointDelegate = BlockIPv6;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            MemoryStream output = new();
            using (Stream responseStream = response.GetResponseStream())
                responseStream.CopyTo(output);
            output.Position = 0;
            return output;
        }

        private static IPEndPoint BlockIPv6(
            ServicePoint servicePoint,
            IPEndPoint remoteEndPoint,
            int retryCount
        )
        {
            if (remoteEndPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return new IPEndPoint(IPAddress.Any, 0);

            throw new InvalidOperationException("No IPv4 address");
        }

        public static Stream DownloadRepo(string name, string branch)
        {
            Uri uri = new(string.Format(repoZipUrl, name, branch), UriKind.Absolute);
            LogFile.WriteLine("Downloading " + uri);
            return GetStream(uri);
        }

        public static Stream DownloadFile(string name, string branch, string path)
        {
            Uri uri = new(
                string.Format(rawUrl, name, branch) + path.TrimStart('/'),
                UriKind.Absolute
            );
            LogFile.WriteLine("Downloading " + uri);
            return GetStream(uri);
        }

        public static bool GetRepoHash(string name, string branch, out string hash)
        {
            hash = null;
            LogFile.WriteLine("Hashing " + name + "/" + branch);

            try
            {
                Uri uri = new(string.Format(hashUrl, name, branch), UriKind.Absolute);
                using Stream stream = GetStream(uri);
                using StreamReader reader = new(stream);
                string text = reader.ReadToEnd();
                var json = JObject.Parse(text);
                hash = json["sha"].ToString();
            }
            catch (Exception e)
            {
                LogFile.Error("Error while fetching repository hash: " + e);
                return false;
            }

            return true;
        }

        public static bool GetRepoVersion(string name, out Version version)
        {
            version = null;
            LogFile.WriteLine("Checking version of " + name);

            try
            {
                Uri uri = new(string.Format(versionUrl, name), UriKind.Absolute);
                using Stream stream = GetStream(uri);
                using StreamReader reader = new(stream);
                string text = reader.ReadToEnd();
                var json = JObject.Parse(text);
                string strVersion = json["tag_name"].ToString().TrimStart('v');
                version = new Version(strVersion);
            }
            catch (Exception e)
            {
                LogFile.Error("Error while fetching repository version: " + e);
                return false;
            }

            return true;
        }
    }
}
