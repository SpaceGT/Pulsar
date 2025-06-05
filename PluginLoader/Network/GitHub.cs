using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;

namespace avaness.PluginLoader.Network
{
    public static class GitHub
    {
        private const string hashUrl = "https://api.github.com/repos/{0}/commits/{1}";
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
                LogFile.Error("An error occurred while setting up networking, web requests will probably fail: " + e);
            }
        }

        public static Stream GetStream(Uri uri)
        {
            HttpWebRequest request = WebRequest.CreateHttp(uri);
            request.UserAgent = "avaness.PluginLoader";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            Config.PluginConfig config = Main.Instance.Config;
            request.Timeout = config.NetworkTimeout;
            if(!config.AllowIPv6)
                request.ServicePoint.BindIPEndPointDelegate = BlockIPv6;

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            MemoryStream output = new MemoryStream();
            using (Stream responseStream = response.GetResponseStream())
                responseStream.CopyTo(output);
            output.Position = 0;
            return output;
        }

        private static IPEndPoint BlockIPv6(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
        {
            if (remoteEndPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                return new IPEndPoint(IPAddress.Any, 0);

            throw new InvalidOperationException("No IPv4 address");
        }

        public static Stream DownloadRepo(string name, string branch)
        {
            Uri uri = new Uri(string.Format(repoZipUrl, name, branch), UriKind.Absolute);
            LogFile.WriteLine("Downloading " + uri);
            return GetStream(uri);
        }

        public static Stream DownloadFile(string name, string branch, string path)
        {
            Uri uri = new Uri(string.Format(rawUrl, name, branch) + path.TrimStart('/'), UriKind.Absolute);
            LogFile.WriteLine("Downloading " + uri);
            return GetStream(uri);
        }

        public static bool GetRepoHash(string name, string branch, out string hash)
        {
            hash = null;

            try
            {
                Uri uri = new Uri(string.Format(hashUrl, name, branch), UriKind.Absolute);
                LogFile.WriteLine("Downloading " + uri);
                using Stream stream = GetStream(uri);
                using (StreamReader reader = new StreamReader(stream))
                {
                    string text = reader.ReadToEnd();
                    var json = JObject.Parse(text);
                    hash = json["sha"].ToString();
                }
            }
            catch (Exception e)
            {
                LogFile.Error("Error while downloading whitelist hash: " + e);
                return false;
            }

            return true;
        }
    }
}
