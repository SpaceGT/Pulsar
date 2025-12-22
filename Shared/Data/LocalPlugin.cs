using System;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data;

public class LocalPlugin : PluginData
{
    public override bool IsLocal => true;
    public override bool IsCompiled => false;

    public string Dll;
    private GitHubPlugin github;
    private AssemblyResolver resolver;

    private LocalPlugin() { }

    public LocalPlugin(string dll)
    {
        Dll = dll;
        Id = Path.GetFileName(dll);
        FriendlyName = Path.GetFileNameWithoutExtension(dll);
        Status = PluginStatus.None;

        TryLoadDataFile(Dll + ".xml");
    }

    public override Assembly GetAssembly()
    {
        if (File.Exists(Dll))
        {
            resolver = new AssemblyResolver();
            resolver.AddSourceFolder(Path.GetDirectoryName(Dll));
            resolver.AddAllowedAssemblyFile(Dll);
            Assembly a = Assembly.LoadFile(Dll);
            Version = a.GetName().Version;
            return a;
        }
        return null;
    }

    public void TryLoadDataFile(string file)
    {
        if (!File.Exists(file))
            return;

        try
        {
            XmlSerializer xml = new(typeof(PluginData));

            using StreamReader reader = File.OpenText(file);
            object resultObj = xml.Deserialize(reader);
            if (resultObj.GetType() != typeof(GitHubPlugin))
            {
                throw new Exception("Xml file is not of type GitHubPlugin!");
            }

            GitHubPlugin github = (GitHubPlugin)resultObj;
            FriendlyName = github.FriendlyName;
            Tooltip = github.Tooltip;
            Author = github.Author;
            Description = github.Description;

            this.github = github;
        }
        catch (Exception e)
        {
            LogFile.Error($"Error while reading the xml file {file} for {Id}: " + e);
        }
    }

    public override void UpdateProfile(Profile draft, bool enabled)
    {
        base.UpdateProfile(draft, enabled);

        if (enabled)
            draft.Local.Add(Id);
    }

    public override string GetAssetPath()
    {
        if (string.IsNullOrEmpty(github?.AssetFolder) || !Path.IsPathRooted(github.AssetFolder))
            return null;

        return Path.GetFullPath(github.AssetFolder);
    }

    public override string ToString() => Id;
}
