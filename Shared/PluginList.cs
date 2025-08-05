using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Serialization;
using ProtoBuf;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Network;
using Pulsar.Shared.Splash;

namespace Pulsar.Shared
{
    public class PluginList : IEnumerable<PluginData>
    {
        private Dictionary<string, Dictionary<string, PluginData>> remoteHubPlugins = [];
        private Dictionary<string, Dictionary<string, PluginData>> localHubPlugins = [];
        private readonly Dictionary<string, PluginData> modPlugins = [];
        private readonly Dictionary<string, PluginData> purePlugins = [];
        private readonly Dictionary<string, PluginData> localPlugins = [];

        private Dictionary<string, PluginData> Plugins =>
            purePlugins
                .Concat(modPlugins)
                .Concat(localPlugins)
                .Concat(localHubPlugins.Values.SelectMany(h => h))
                .Concat(remoteHubPlugins.Values.SelectMany(h => h))
                .GroupBy(kv => kv.Key)
                .ToDictionary(g => g.Key, g => g.First().Value);

        public int Count => Plugins.Count;

        public PluginData this[string key]
        {
            get => Plugins[key];
        }

        private readonly SourcesConfig SourcesConfig;
        private readonly PluginConfig PluginConfig;
        private readonly string SourceDir;
        private string PluginSourceDir => Path.Combine(SourceDir, "Plugins");
        private string HubSourceDir => Path.Combine(SourceDir, "Hubs");
        private readonly string LocalPluginDir;

        public bool Contains(string id) => Plugins.ContainsKey(id);

        public bool TryGetPlugin(string id, out PluginData pluginData) =>
            Plugins.TryGetValue(id, out pluginData);

        public List<ISteamItem> GetSteamPlugins() => [.. Plugins.Values.OfType<ISteamItem>()];

        public PluginList(string mainDirectory, PluginConfig config, SourcesConfig sources)
        {
            LocalPluginDir = Path.Combine(mainDirectory, "Local");
            SourceDir = Path.Combine(mainDirectory, "Sources");
            SourcesConfig = sources;
            PluginConfig = config;

            EnsureDirectories();

            SplashManager.Instance?.SetText("Downloading plugin list...");
            InitRemoteList();

            if (Plugins.Count == 0)
            {
                LogFile.Warn(
                    "No plugins in the plugin list. Plugin list will contain local plugins only."
                );
            }

            FindLocalPlugins();
            LogFile.WriteLine($"Found {Plugins.Count} plugins");

            FindPluginGroups();
            FindModDependencies();
        }

        /// <summary>
        /// Ensures the user is subscribed to the steam plugin.
        /// </summary>
        public void SubscribeToItem(string id)
        {
            if (Plugins.TryGetValue(id, out PluginData data) && data is ISteamItem steam)
                ConfigManager.Instance.Dependencies.SteamSubscribe(steam.WorkshopId);
        }

        private void FindPluginGroups()
        {
            int groups = 0;
            foreach (
                var group in Plugins
                    .Values.Where(x => !string.IsNullOrWhiteSpace(x.GroupId))
                    .GroupBy(x => x.GroupId)
            )
            {
                groups++;
                foreach (PluginData data in group)
                    data.Group.AddRange(group.Where(x => x != data));
            }
            if (groups > 0)
                LogFile.WriteLine($"Found {groups} plugin groups");
        }

        private void FindModDependencies()
        {
            foreach (PluginData data in GetSteamPlugins())
            {
                if (data is ModPlugin mod)
                    FindModDependencies(mod);
            }
        }

        private void FindModDependencies(ModPlugin mod)
        {
            if (mod.DependencyIds == null)
                return;

            Dictionary<ulong, ModPlugin> dependencies = new() { { mod.WorkshopId, mod } };
            Stack<ModPlugin> toProcess = new();
            toProcess.Push(mod);

            while (toProcess.Count > 0)
            {
                ModPlugin temp = toProcess.Pop();

                if (temp.DependencyIds == null)
                    continue;

                foreach (ulong id in temp.DependencyIds)
                {
                    if (
                        !dependencies.ContainsKey(id)
                        && Plugins.TryGetValue(id.ToString(), out PluginData data)
                        && data is ModPlugin dependency
                    )
                    {
                        toProcess.Push(dependency);
                        dependencies[id] = dependency;
                    }
                }
            }

            dependencies.Remove(mod.WorkshopId);
            mod.Dependencies = [.. dependencies.Values];
        }

        private void InitRemoteHub(RemoteHubConfig source)
        {
            if (!remoteHubPlugins.ContainsKey(source.Repo))
                remoteHubPlugins.Add(source.Repo, []);

            PluginData[] list;
            string hubFile = Path.Combine(HubSourceDir, source.Repo.Replace('/', '-') + ".bin");

            if (
                source.LastCheck is not null
                    && DateTime.UtcNow - source.LastCheck
                        <= TimeSpan.FromHours(SourcesConfig.MaxSourceAge)
                || !GitHub.GetRepoHash(source.Repo, source.Branch, out string currentHash)
            )
            {
                if (!TryReadHubFile(hubFile, out list))
                    return;
            }
            else
            {
                source.LastCheck = DateTime.UtcNow;

                if (source.Hash is null || currentHash != source.Hash)
                {
                    // Plugin list changed, try downloading new version first
                    if (
                        !TryDownloadHubFile(source.Repo, source.Branch, hubFile, out list)
                        && !TryReadHubFile(hubFile, out list)
                    )
                        return;
                }
                else
                {
                    // Plugin list did not change, try reading the current version first
                    if (
                        !TryReadHubFile(hubFile, out list)
                        && !TryDownloadHubFile(source.Repo, source.Branch, hubFile, out list)
                    )
                        return;
                }

                source.Hash = currentHash;
            }

            AddHubPluginData(ref remoteHubPlugins, list, source.Repo, source.Name);
        }

        private void AddHubPluginData(
            ref Dictionary<string, Dictionary<string, PluginData>> dict,
            PluginData[] list,
            string sourceKey,
            string sourceLabel
        )
        {
            if (list is null)
                return;

            var plugins = new Dictionary<string, PluginData>();
            foreach (PluginData data in list)
            {
                data.Source = sourceLabel;
                plugins[data.Id] = data;
            }
            dict[sourceKey] = plugins;
        }

        private void InitRemotePlugin(RemotePluginConfig source)
        {
            PluginData pluginData;
            string pluginFile = Path.Combine(
                PluginSourceDir,
                source.Repo.Replace('/', '-') + ".bin"
            );

            if (
                source.LastCheck is not null
                && DateTime.UtcNow - source.LastCheck
                    <= TimeSpan.FromHours(SourcesConfig.MaxSourceAge)
            )
            {
                if (!TryReadPluginFile(pluginFile, out pluginData))
                    return;
            }
            else
            {
                source.LastCheck = DateTime.UtcNow;

                // Plugin list changed, try downloading new version first
                if (
                    !TryDownloadPluginFile(
                        source.Repo,
                        source.Branch,
                        source.File,
                        pluginFile,
                        out pluginData
                    ) && !TryReadPluginFile(pluginFile, out pluginData)
                )
                    return;
            }

            pluginData.Source = "GitHub";
            purePlugins[pluginData.Id] = pluginData;
        }

        private void AddLocalPlugin(LocalPluginConfig source)
        {
            if (Directory.Exists(source.Folder))
            {
                LocalFolderPlugin local = new(source.Folder) { Source = "Development Folder" };

                if (File.Exists(source.File))
                {
                    local.DeserializeFile(source.File);
                    if (PluginConfig.LoadPluginData(local))
                        PluginConfig.Save();
                }

                localPlugins[source.Folder] = local;
            }
        }

        private void UpdateRemoteHub(RemoteHubConfig source)
        {
            string hubFile = Path.Combine(HubSourceDir, source.Repo.Replace('/', '-') + ".bin");
            GitHub.GetRepoHash(source.Repo, source.Branch, out string currentHash);
            source.LastCheck = DateTime.UtcNow;

            if (source.Hash is not null && currentHash == source.Hash)
                return;

            if (!TryDownloadHubFile(source.Repo, source.Branch, hubFile, out PluginData[] list))
                return;

            source.Hash = currentHash;

            AddHubPluginData(ref remoteHubPlugins, list, source.Repo, source.Name);
        }

        private void UpdateRemotePlugin(RemotePluginConfig source)
        {
            string pluginFile = Path.Combine(
                PluginSourceDir,
                source.Repo.Replace('/', '-') + ".bin"
            );

            source.LastCheck = DateTime.UtcNow;

            if (
                !TryDownloadPluginFile(
                    source.Repo,
                    source.Branch,
                    source.File,
                    pluginFile,
                    out PluginData pluginData
                )
            )
                return;

            if (pluginData != null)
            {
                pluginData.Source = "GitHub";
                purePlugins[pluginData.Id] = pluginData;
            }
        }

        private void AddMod(ModConfig source)
        {
            // TODO: Support fetching mod metadata from Steam

            ModPlugin modPlugin = new()
            {
                Id = source.ID.ToString(),
                FriendlyName = source.Name,
                Author = "Unknown",
                Source = "Mod",
            };

            modPlugins[modPlugin.Id] = modPlugin;
        }

        private void EnsureDirectories()
        {
            foreach (var dir in new[] { SourceDir, HubSourceDir, PluginSourceDir, LocalPluginDir })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
        }

        private void InitRemoteList()
        {
            foreach (RemoteHubConfig source in SourcesConfig.RemoteHubSources)
                if (source.Enabled)
                    InitRemoteHub(source);

            foreach (RemotePluginConfig source in SourcesConfig.RemotePluginSources)
                if (source.Enabled)
                    InitRemotePlugin(source);

            foreach (ModConfig source in SourcesConfig.ModSources)
                if (source.Enabled)
                    AddMod(source);
        }

        public void UpdateRemoteList(bool force = false)
        {
            foreach (
                RemoteHubConfig source in new List<RemoteHubConfig>(SourcesConfig.RemoteHubSources)
            )
            {
                if (source.Enabled)
                {
                    if (!remoteHubPlugins.ContainsKey(source.Repo))
                        InitRemoteHub(source);
                    else if (
                        force
                        || source.LastCheck is null
                        || DateTime.UtcNow - source.LastCheck
                            > TimeSpan.FromHours(SourcesConfig.MaxSourceAge)
                    )
                        UpdateRemoteHub(source);
                }
                else
                    remoteHubPlugins.Remove(source.Repo);
            }
            foreach (string source in new List<string>(remoteHubPlugins.Keys))
                if (!SourcesConfig.RemoteHubSources.Any(x => x.Repo == source))
                    remoteHubPlugins.Remove(source);

            foreach (
                RemotePluginConfig source in new List<RemotePluginConfig>(
                    SourcesConfig.RemotePluginSources
                )
            )
            {
                if (source.Enabled)
                {
                    if (!purePlugins.ContainsKey(source.Repo))
                        InitRemotePlugin(source);
                    else if (
                        force
                        || source.LastCheck is null
                        || DateTime.UtcNow - source.LastCheck
                            > TimeSpan.FromHours(SourcesConfig.MaxSourceAge)
                    )
                        UpdateRemotePlugin(source);
                }
                else
                    purePlugins.Remove(source.Repo);
            }
            foreach (string source in new List<string>(purePlugins.Keys))
                if (!SourcesConfig.RemotePluginSources.Any(x => x.Repo == source))
                    purePlugins.Remove(source);

            foreach (ModConfig source in new List<ModConfig>(SourcesConfig.ModSources))
                if (source.Enabled)
                    AddMod(source);
                else
                    modPlugins.Remove(source.ID.ToString());
            foreach (string source in new List<string>(modPlugins.Keys))
                if (!SourcesConfig.ModSources.Any(x => x.ID.ToString() == source))
                    modPlugins.Remove(source);
        }

        public void UpdateLocalList()
        {
            foreach (
                LocalHubConfig source in new List<LocalHubConfig>(SourcesConfig.LocalHubSources)
            )
                if (source.Enabled && Directory.Exists(source.Folder))
                    UpdateLocalHub(source);
                else
                    localHubPlugins.Remove(source.Folder);

            foreach (string source in new List<string>(localHubPlugins.Keys))
            {
                if (SourcesConfig.LocalHubSources.Any(x => x.Folder == source))
                    continue;

                localHubPlugins.Remove(source);
            }

            foreach (
                LocalPluginConfig source in new List<LocalPluginConfig>(
                    SourcesConfig.LocalPluginSources
                )
            )
                if (source.Enabled)
                    AddLocalPlugin(source);
                else
                {
                    if (PluginConfig.IsEnabled(source.Folder))
                        PluginConfig.SetEnabled(source.Folder, false);

                    if (PluginConfig.RemovePluginData(source.Folder))
                        PluginConfig.Save();

                    localPlugins.Remove(source.Folder);
                }

            foreach (
                string dll in Directory.EnumerateFiles(
                    LocalPluginDir,
                    "*.dll",
                    SearchOption.AllDirectories
                )
            )
            {
                LocalPlugin local = new(dll) { Source = "Local" };
                string name = local.FriendlyName;
                if (!name.StartsWith("0Harmony") && !name.StartsWith("Microsoft"))
                    localPlugins[local.Id] = local;
            }

            foreach (PluginData source in new List<PluginData>(localPlugins.Values))
            {
                if (
                    source is LocalPlugin
                    && Directory
                        .EnumerateFiles(LocalPluginDir, "*.dll", SearchOption.AllDirectories)
                        .Any(x => x == source.Id)
                )
                    continue;

                if (
                    source is LocalFolderPlugin
                    && SourcesConfig.LocalPluginSources.Any(x => x.Folder == source.Id)
                )
                    continue;

                localPlugins.Remove(source.Id);
            }
        }

        private void UpdateLocalHub(LocalHubConfig source)
        {
            string hash = Tools.GetFolderHash(source.Folder, "*.xml");

            if (
                source.Hash is not null
                && source.Hash == hash
                && localHubPlugins.ContainsKey(source.Folder)
            )
                return;

            source.Hash = hash;

            PluginData[] list = null;
            Dictionary<string, PluginData> newPlugins = [];

            try
            {
                XmlSerializer xml = new(typeof(PluginData));
                foreach (
                    string filePath in Directory.EnumerateFiles(
                        source.Folder,
                        "*.xml",
                        SearchOption.AllDirectories
                    )
                )
                {
                    using FileStream fs = File.OpenRead(filePath);
                    using StreamReader entryReader = new(fs);
                    try
                    {
                        PluginData data = (PluginData)xml.Deserialize(entryReader);
                        newPlugins[data.Id] = data;
                    }
                    catch (InvalidOperationException e)
                    {
                        LogFile.Error(
                            "An error occurred while reading "
                                + filePath
                                + ": "
                                + (e.InnerException ?? e)
                        );
                    }
                }

                list = [.. newPlugins.Values];
            }
            catch (Exception e)
            {
                LogFile.Error("Error while parsing whitelist: " + e);
            }

            AddHubPluginData(ref localHubPlugins, list, source.Folder, source.Name);
        }

        private bool TryReadHubFile(string file, out PluginData[] list)
        {
            list = null;

            if (File.Exists(file) && new FileInfo(file).Length > 0)
            {
                LogFile.WriteLine("Reading whitelist from cache");
                try
                {
                    PluginData[] rawData;
                    using (Stream binFile = File.OpenRead(file))
                    {
                        rawData = Serializer.Deserialize<PluginData[]>(binFile);
                    }

                    int obsolete = 0;
                    List<PluginData> tempList = new(rawData.Length);
                    foreach (PluginData data in rawData)
                    {
                        if (data is ObsoletePlugin)
                            obsolete++;
                        else
                            tempList.Add(data);
                    }
                    LogFile.WriteLine("Whitelist retrieved from disk");
                    list = [.. tempList];
                    if (obsolete > 0)
                        LogFile.Warn(obsolete + " obsolete plugins found in the whitelist file.");
                    return true;
                }
                catch (Exception e)
                {
                    LogFile.Error("Error while reading whitelist: " + e);
                }
            }
            else
            {
                LogFile.WriteLine("No whitelist cache exists");
            }

            return false;
        }

        private bool TryDownloadHubFile(
            string repoName,
            string branch,
            string file,
            out PluginData[] list
        )
        {
            list = null;
            Dictionary<string, PluginData> newPlugins = [];

            try
            {
                using (Stream zipFileStream = GitHub.DownloadRepo(repoName, branch))
                using (ZipArchive zipFile = new(zipFileStream))
                {
                    XmlSerializer xml = new(typeof(PluginData));
                    foreach (ZipArchiveEntry entry in zipFile.Entries)
                    {
                        if (!entry.FullName.EndsWith("xml", StringComparison.OrdinalIgnoreCase))
                            continue;

                        using Stream entryStream = entry.Open();
                        using StreamReader entryReader = new(entryStream);

                        try
                        {
                            PluginData data = (PluginData)xml.Deserialize(entryReader);
                            newPlugins[data.Id] = data;
                        }
                        catch (InvalidOperationException e)
                        {
                            LogFile.Error(
                                "An error occurred while reading "
                                    + entry.FullName
                                    + ": "
                                    + (e.InnerException ?? e)
                            );
                        }
                    }
                }

                list = [.. newPlugins.Values];
                return TrySaveFile(file, list);
            }
            catch (Exception e)
            {
                LogFile.Error("Error while downloading whitelist: " + e);
            }

            return false;
        }

        private bool TrySaveFile(string file, PluginData data)
        {
            return TrySaveFile(file, [data]);
        }

        private bool TrySaveFile(string file, PluginData[] list)
        {
            try
            {
                LogFile.WriteLine("Saving whitelist to disk");
                using (MemoryStream mem = new())
                {
                    Serializer.Serialize(mem, list);
                    using Stream binFile = File.Create(file);
                    mem.WriteTo(binFile);
                }

                SourcesConfig.Save();

                LogFile.WriteLine("Whitelist updated");
                return true;
            }
            catch (Exception e)
            {
                LogFile.Error("Error while saving whitelist: " + e);
                try
                {
                    File.Delete(file);
                }
                catch { }
                return false;
            }
        }

        private bool TryReadPluginFile(string file, out PluginData data)
        {
            data = null;

            if (File.Exists(file) && new FileInfo(file).Length > 0)
            {
                LogFile.WriteLine("Reading whitelist from cache");
                try
                {
                    using Stream binFile = File.OpenRead(file);
                    data = Serializer.Deserialize<PluginData[]>(binFile).First();

                    return true;
                }
                catch (Exception e)
                {
                    LogFile.Error("Error while reading whitelist: " + e);
                }
            }
            else
            {
                LogFile.WriteLine("No whitelist cache exists");
            }

            return false;
        }

        private bool TryDownloadPluginFile(
            string repoName,
            string branch,
            string infoFile,
            string saveFile,
            out PluginData data
        )
        {
            XmlSerializer xml = new(typeof(PluginData));
            data = null;

            try
            {
                using (Stream dataStream = GitHub.DownloadFile(repoName, branch, infoFile))
                using (StreamReader dataStreamReader = new(dataStream))
                {
                    try
                    {
                        data = (PluginData)xml.Deserialize(dataStreamReader);
                    }
                    catch (InvalidOperationException e)
                    {
                        LogFile.Error(
                            "An error occurred while reading "
                                + branch
                                + "/"
                                + infoFile
                                + ": "
                                + (e.InnerException ?? e)
                        );

                        return false;
                    }
                }

                return TrySaveFile(saveFile, data);
            }
            catch (Exception e)
            {
                LogFile.Error("Error while downloading plugin data: " + e);
                return false;
            }
        }

        private void FindLocalPlugins()
        {
            foreach (
                string dll in Directory.EnumerateFiles(
                    LocalPluginDir,
                    "*.dll",
                    SearchOption.AllDirectories
                )
            )
            {
                LocalPlugin local = new(dll) { Source = "Local" };
                localPlugins[local.Id] = local;
            }

            foreach (LocalPluginConfig source in SourcesConfig.LocalPluginSources)
                AddLocalPlugin(source);

            foreach (LocalHubConfig source in SourcesConfig.LocalHubSources)
                AddLocalHub(source);
        }

        private void AddLocalHub(LocalHubConfig source)
        {
            if (Directory.Exists(source.Folder))
            {
                string hash = Tools.GetFolderHash(source.Folder, "*.xml");

                if (
                    source.Hash is not null
                    && source.Hash == hash
                    && localHubPlugins.ContainsKey(source.Folder)
                )
                    return;

                source.Hash = hash;

                TryLoadLocalHub(source.Folder, out PluginData[] list);

                AddHubPluginData(ref localHubPlugins, list, source.Folder, source.Name);
            }
        }

        private bool TryLoadLocalHub(string folder, out PluginData[] list)
        {
            list = null;
            Dictionary<string, PluginData> newPlugins = [];

            try
            {
                XmlSerializer xml = new(typeof(PluginData));
                foreach (
                    string filePath in Directory.EnumerateFiles(
                        folder,
                        "*.xml",
                        SearchOption.AllDirectories
                    )
                )
                {
                    using FileStream fs = File.OpenRead(filePath);
                    using StreamReader entryReader = new(fs);
                    try
                    {
                        PluginData data = (PluginData)xml.Deserialize(entryReader);
                        newPlugins[data.Id] = data;
                    }
                    catch (InvalidOperationException e)
                    {
                        LogFile.Error(
                            "An error occurred while reading "
                                + filePath
                                + ": "
                                + (e.InnerException ?? e)
                        );
                    }
                }

                list = [.. newPlugins.Values];
            }
            catch (Exception e)
            {
                LogFile.Error("Error while parsing whitelist: " + e);
            }

            return false;
        }

        public IEnumerator<PluginData> GetEnumerator()
        {
            return Plugins.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Plugins.Values.GetEnumerator();
        }
    }
}
