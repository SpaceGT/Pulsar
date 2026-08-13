using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using Pulsar.Shared.Config;
using Pulsar.Shared.Network;

namespace Pulsar.Shared.Data;

public partial class GitHubPlugin
{
    public class CacheManifest
    {
        private const string manifestFile = "manifest.xml";
        private const string commitFile = "commit.sha1";
        private const string assetFolder = "Assets";
        private const string libFolder = "Bin";

        private string cacheDir;
        private string assetDir;
        private string libDir;
        private Dictionary<string, AssetFile> assetFiles = [];

        [XmlIgnore]
        public string DllFile { get; private set; }
        public string AssetFolder => assetDir;
        public string LibDir => libDir;

        public string Commit { get; set; }

        public string Runtime { get; set; }

        public string RuntimeIdentifier { get; set; }

        public string Packages { get; set; }

        [XmlIgnore]
        public Version PulsarVersion { get; set; }

        [XmlElement("PulsarVersion")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string PulsarVersionString
        {
            get => PulsarVersion?.ToString();
            set => PulsarVersion = string.IsNullOrWhiteSpace(value) ? null : new Version(value);
        }

        [XmlIgnore]
        public Version GameVersion { get; set; }

        [XmlElement("GameVersion")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public string GameVersionString
        {
            get => GameVersion?.ToString();
            set => GameVersion = string.IsNullOrWhiteSpace(value) ? null : new Version(value);
        }

        [XmlArray]
        [XmlArrayItem("File")]
        public AssetFile[] AssetFiles
        {
            get { return [.. assetFiles.Values]; }
            set { assetFiles = value.ToDictionary(GetAssetKey); }
        }

        public CacheManifest() { }

        private void Init(string cacheDir)
        {
            this.cacheDir = cacheDir;
            assetDir = Path.Combine(cacheDir, assetFolder);
            libDir = Path.Combine(cacheDir, libFolder);
            DllFile = Path.Combine(libDir, NuGetRestore.PluginFileName);

            foreach (AssetFile file in assetFiles.Values)
                SetBaseDir(file);

            // Backwards compatibility
            string oldCommitFile = Path.Combine(cacheDir, commitFile);
            if (File.Exists(oldCommitFile))
            {
                try
                {
                    Commit = File.ReadAllText(oldCommitFile).Trim();
                    File.Delete(oldCommitFile);
                    Save();
                }
                catch (Exception e)
                {
                    LogFile.WriteLine("Error while reading old commit file: " + e);
                }
            }
        }

        public static CacheManifest Load(string userName, string repoName)
        {
            string cacheDir = Path.Combine(
                ConfigManager.Instance.PulsarDir,
                "GitHub",
                userName,
                repoName
            );
            Directory.CreateDirectory(cacheDir);

            CacheManifest manifest;

            string manifestLocation = Path.Combine(cacheDir, manifestFile);
            if (!File.Exists(manifestLocation))
            {
                manifest = new CacheManifest();
            }
            else
            {
                XmlSerializer serializer = new(typeof(CacheManifest));
                try
                {
                    using Stream file = File.OpenRead(manifestLocation);
                    manifest = (CacheManifest)serializer.Deserialize(file);
                }
                catch (Exception e)
                {
                    LogFile.WriteLine("Error while loading manifest file: " + e);
                    manifest = new CacheManifest();
                }
            }

            manifest.Init(cacheDir);
            return manifest;
        }

        public bool IsCacheValid(
            string currentCommit,
            Version currentGameVersion,
            bool requiresAssets,
            string packages
        )
        {
            Version currentPulsarVersion = Assembly.GetEntryAssembly().GetName().Version;

            if (
                !File.Exists(DllFile)
                || Commit != currentCommit
                || Runtime != RuntimeInformation.FrameworkDescription
                || RuntimeIdentifier != Tools.RuntimeIdentifier
                || Packages != packages
                || PulsarVersion != currentPulsarVersion
            )
                return false;

            if (currentGameVersion is not null)
            {
                if (GameVersion is null || GameVersion != currentGameVersion)
                    return false;
            }

            if (requiresAssets && !assetFiles.Values.Any(x => x.Type == AssetFile.AssetType.Asset))
                return false;

            foreach (AssetFile file in assetFiles.Values)
            {
                if (!file.IsValid())
                    return false;
            }

            return true;
        }

        public void ClearAssets()
        {
            assetFiles.Clear();
            if (Directory.Exists(assetDir))
                Directory.Delete(assetDir, true);
            if (Directory.Exists(libDir))
                Directory.Delete(libDir, true);
        }

        public AssetFile CreateAsset(
            string file,
            AssetFile.AssetType type = AssetFile.AssetType.Asset
        )
        {
            file = file.Replace('\\', '/').TrimStart('/');
            AssetFile asset = new(file, type);
            SetBaseDir(asset);
            asset.GetFileInfo();
            assetFiles[GetAssetKey(asset)] = asset;
            return asset;
        }

        private string GetAssetKey(AssetFile asset)
        {
            if (asset.Type == AssetFile.AssetType.Asset)
                return assetFolder + "/" + asset.NormalizedFileName;
            return libFolder + "/" + asset.NormalizedFileName;
        }

        private void SetBaseDir(AssetFile asset)
        {
            asset.BaseDir = asset.Type == AssetFile.AssetType.Asset ? assetDir : libDir;
        }

        public bool IsAssetValid(AssetFile asset)
        {
            return asset.IsValid();
        }

        public void SaveAsset(AssetFile asset, Stream stream)
        {
            asset.Save(stream);
        }

        public void Save()
        {
            string manifestLocation = Path.Combine(cacheDir, manifestFile);
            XmlSerializer serializer = new(typeof(CacheManifest));
            try
            {
                using Stream file = File.Create(manifestLocation);
                serializer.Serialize(file, this);
            }
            catch (Exception e)
            {
                LogFile.WriteLine("Error while saving manifest file: " + e);
            }
        }

        public void DeleteUnknownFiles()
        {
            DeleteUnknownFiles(assetDir);
            DeleteUnknownFiles(libDir);
        }

        public void DeleteUnknownFiles(string assetDir)
        {
            if (!Directory.Exists(assetDir))
                return;

            StringComparison pathComparison = Tools.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            foreach (
                string file in Directory.EnumerateFiles(assetDir, "*", SearchOption.AllDirectories)
            )
            {
                if (file.Equals(DllFile, pathComparison))
                    continue;

                string relativePath = Tools.GetRelativePath(cacheDir, file).Replace('\\', '/');
                if (!assetFiles.ContainsKey(relativePath))
                    File.Delete(file);
            }
        }

        public void Invalidate()
        {
            Commit = null;
            Save();
        }
    }
}
