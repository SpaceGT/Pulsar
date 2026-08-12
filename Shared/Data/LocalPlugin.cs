using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Serialization;
using Mono.Cecil;

namespace Pulsar.Shared.Data;

public class LocalPlugin : PluginData
{
    public override bool IsLocal => true;
    public override bool IsCompiled => false;

    public string Dll;
    private GitHubPlugin github;

    private LocalPlugin() { }

    public LocalPlugin(string dll)
    {
        Dll = dll;
        Id = Path.GetFileName(dll);
        FriendlyName = Path.GetFileNameWithoutExtension(dll);
        Status = PluginStatus.None;
        (Runtimes, Platforms) = GetEnvironment(dll);

        TryLoadDataFile(Dll + ".xml");
    }

    private static (string runtimes, string platforms) GetEnvironment(string dll)
    {
        const string platformAttribute = "System.Runtime.Versioning.TargetPlatformAttribute";

        using var assembly = AssemblyDefinition.ReadAssembly(dll);
        var references = assembly.MainModule.AssemblyReferences;

        string runtimes = null;
        if (references.Any(r => r.Name == "System.Runtime"))
            runtimes = "CoreCLR";
        else if (references.Any(r => r.Name == "mscorlib"))
            runtimes = "CLR;Mono";

        string platforms = null;
        foreach (var attribute in assembly.CustomAttributes)
        {
            if (attribute.AttributeType.FullName != platformAttribute)
                continue;

            string name = (string)attribute.ConstructorArguments[0].Value;
            if (name.StartsWith("Windows", StringComparison.OrdinalIgnoreCase))
                platforms = "Windows";

            break;
        }

        return (runtimes, platforms);
    }

    public override Assembly GetAssembly()
    {
        if (File.Exists(Dll))
        {
            Assembly a = Assembly.LoadFrom(Dll);
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
            Runtimes = github.Runtimes ?? Runtimes;
            Platforms = github.Platforms ?? Platforms;
            DependencyIds = github.DependencyIds;

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
