using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Network;

public static class GitHub
{
    private const string CommitInfo = "https://api.github.com/repos/{0}/commits/{1}";
    private const string ReleaseInfo = "https://api.github.com/repos/{0}/releases";
    private const string FetchRepo = "https://github.com/{0}/archive/{1}.zip";
    private const string FetchFile = "https://raw.githubusercontent.com/{0}/{1}/";

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
        request.UserAgent = "Pulsar";
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        CoreConfig config = ConfigManager.Instance.Core;
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

    public static Stream GetRepoArchive(string repo, string reference)
    {
        Uri uri = new(string.Format(FetchRepo, repo, reference), UriKind.Absolute);
        LogFile.WriteLine("Downloading " + uri);
        return GetStream(uri);
    }

    public static Stream GetRepoFile(string repo, string reference, string file)
    {
        Uri uri = new(
            string.Format(FetchFile, repo, reference) + file.TrimStart('/'),
            UriKind.Absolute
        );
        LogFile.WriteLine("Downloading " + uri);
        return GetStream(uri);
    }

    public static bool GetRepoHash(string repo, string reference, out string hash)
    {
        hash = null;
        LogFile.WriteLine("Hashing " + repo + "/" + reference);

        try
        {
            string text = GetText(string.Format(CommitInfo, repo, reference));
            hash = JObject.Parse(text)["sha"].ToString();
        }
        catch (Exception e)
        {
            LogFile.Error("Error while fetching repository hash: " + e);
            return false;
        }

        return true;
    }

    public static bool GetReleaseVersion(string repo, out Version version, bool beta = false)
    {
        version = null;
        LogFile.WriteLine("Checking version of " + repo);

        try
        {
            string text = GetText(string.Format(ReleaseInfo, repo));
            foreach (JToken item in JArray.Parse(text))
            {
                if (!beta && (bool)item["prerelease"])
                    continue;

                string strVersion = item["tag_name"].ToString().TrimStart('v');
                version = new Version(strVersion);

                return true;
            }
        }
        catch (Exception e)
        {
            LogFile.Error("Error while fetching version: " + e);
            return false;
        }

        LogFile.Error("Could not find version in JSON! ");
        return false;
    }

    public static JObject GetReleaseJson(string repo, string tag)
    {
        string url = string.Format(ReleaseInfo, repo) + "/tags/" + tag;
        return JObject.Parse(GetText(url));
    }

    private static string GetText(string url)
    {
        Uri uri = new(url, UriKind.Absolute);
        using Stream stream = GetStream(uri);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
