using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Pulsar.Shared.Network;

public static class GitHub
{
    // Optional GitHub PAT to lift the anonymous rate limit
    public static string Token { get; set; } =
        Environment.GetEnvironmentVariable("PULSAR_GITHUB_TOKEN");

    internal static bool IsTokenHost(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase);

    private const string CommitInfo = "https://api.github.com/repos/{0}/commits/{1}";
    private const string ReleaseInfo = "https://api.github.com/repos/{0}/releases";
    private const string FetchRepo = "https://api.github.com/repos/{0}/zipball/{1}";
    private const string FetchFile = "https://api.github.com/repos/{0}/contents/{1}?ref={2}";
    private const string RawContent = "application/vnd.github.raw+json";

    public static Stream GetRepoArchive(string repo, string reference)
    {
        reference = Uri.EscapeDataString(reference);
        Uri uri = new(string.Format(FetchRepo, repo, reference), UriKind.Absolute);
        LogFile.WriteLine("Downloading " + uri);
        return NetworkClient.GetStreamAsync(uri).GetAwaiter().GetResult();
    }

    public static Stream GetRepoFile(string repo, string reference, string file)
    {
        reference = Uri.EscapeDataString(reference);
        file = Uri.EscapeDataString(file.TrimStart('/')).Replace("%2F", "/");
        Uri uri = new(string.Format(FetchFile, repo, file, reference), UriKind.Absolute);
        LogFile.WriteLine("Downloading " + uri);
        return NetworkClient.GetStreamAsync(uri, RawContent).GetAwaiter().GetResult();
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
        return NetworkClient.GetStringAsync(uri).GetAwaiter().GetResult();
    }
}
