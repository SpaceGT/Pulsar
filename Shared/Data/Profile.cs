using System.Collections.Generic;
using System.Linq;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data;

public class Profile
{
    public string Key => Tools.CleanFileName(Name);
    public string Name { get; set; }

    public HashSet<GitHubPluginConfig> GitHub { get; set; }
    public HashSet<LocalFolderConfig> DevFolder { get; set; }
    public HashSet<string> Local { get; set; }
    public HashSet<ulong> Mods { get; set; }

    public Profile() { }

    public Profile(string name, IEnumerable<string> plugins = null)
    {
        Name = name;

        GitHub = [];
        DevFolder = [];
        Local = [];
        Mods = [];

        foreach (string pluginId in plugins ?? [])
            Update(pluginId);
    }

    public IEnumerable<string> GetPluginIDs(bool includeLocal = true)
    {
        foreach (GitHubPluginConfig config in GitHub)
            yield return config.Id;

        foreach (ulong id in Mods)
            yield return id.ToString();

        if (!includeLocal)
            yield break;

        foreach (LocalFolderConfig config in DevFolder)
            yield return config.Id;

        foreach (string id in Local)
            yield return id;
    }

    public bool Contains(string id) => GetPluginIDs().Contains(id);

    private static bool TryGetPlugin(string id, out PluginData pluginData) =>
        ConfigManager.Instance.List.TryGetPlugin(id, out pluginData);

    public IEnumerable<PluginData> GetPlugins()
    {
        foreach (string id in GetPluginIDs())
            if (TryGetPlugin(id, out PluginData plugin))
                yield return plugin;
    }

    public string GetDescription()
    {
        int locals = Local.Count + DevFolder.Count;
        int plugins = GitHub.Count;
        int mods = Mods.Count;

        List<string> infoItems = [];
        if (locals > 0)
            infoItems.Add(locals > 1 ? $"{locals} local plugins" : "1 local plugin");
        if (plugins > 0)
            infoItems.Add(plugins > 1 ? $"{plugins} plugins" : "1 plugin");
        if (mods > 0)
            infoItems.Add(mods > 1 ? $"{mods} mods" : "1 mod");

        return string.Join(", ", infoItems);
    }

    public void Update(string id)
    {
        if (!TryGetPlugin(id, out PluginData data))
            return;

        Remove(id);

        if (data is GitHubPlugin gitHubData)
            GitHub.Add(Tools.DeepCopy(gitHubData.Settings));
        else if (data is LocalFolderPlugin folderData)
            DevFolder.Add(Tools.DeepCopy(folderData.FolderSettings));
        else if (data is LocalPlugin localData)
            Local.Add(localData.Id);
        else if (data is ModPlugin modData)
            Mods.Add(ulong.Parse(modData.Id));
    }

    public void Remove(string id)
    {
        GitHub.RemoveWhere(x => x.Id == id);
        DevFolder.RemoveWhere(x => x.Id == id);
        Local.Remove(id);

        if (ulong.TryParse(id, out ulong mId))
            Mods.Remove(mId);
    }

    public bool Validate() => !new object[] { Name, GitHub, DevFolder, Local, Mods }.Contains(null);
}
