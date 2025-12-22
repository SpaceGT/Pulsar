using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;
using FuzzySharp;
using ProtoBuf;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data;

[XmlInclude(typeof(GitHubPlugin))]
[XmlInclude(typeof(ModPlugin))]
[ProtoContract]
[ProtoInclude(100, typeof(ObsoletePlugin))]
[ProtoInclude(103, typeof(GitHubPlugin))]
[ProtoInclude(104, typeof(ModPlugin))]
public abstract class PluginData : IEquatable<PluginData>
{
    public string Source;
    public abstract bool IsLocal { get; }
    public abstract bool IsCompiled { get; }

    [XmlIgnore]
    public Version Version { get; protected set; }

    [XmlIgnore]
    public virtual PluginStatus Status { get; set; } = PluginStatus.None;
    public virtual string StatusString
    {
        get
        {
            return Status switch
            {
                PluginStatus.Network => "Network!",
                PluginStatus.Updated => "Updated",
                PluginStatus.Error => "Error!",
                PluginStatus.Blocked => "Blocked!",
                _ => "",
            };
        }
    }

    [ProtoMember(1)]
    public virtual string Id { get; set; }

    [ProtoMember(2)]
    public string FriendlyName { get; set; } = "Unknown";

    [ProtoMember(3)]
    public bool Hidden { get; set; } = false;

    [ProtoMember(4)]
    public string GroupId { get; set; }

    [ProtoMember(5)]
    public string Tooltip { get; set; }

    [ProtoMember(6)]
    public string Author { get; set; }

    [ProtoMember(7)]
    public string Description { get; set; }

    [XmlIgnore]
    public List<PluginData> Group { get; } = [];

    [XmlIgnore]
    public bool Enabled => ConfigManager.Instance.Profiles.Current.Contains(Id);

    protected PluginData() { }

    /// <summary>
    /// Loads the user settings into the plugin.
    /// </summary>
    public virtual void LoadData(PluginDataConfig config) { }

    public abstract Assembly GetAssembly();

    public virtual bool TryLoadAssembly(out Assembly a)
    {
        if (Status == PluginStatus.Error)
        {
            a = null;
            return false;
        }

        try
        {
            // Get the file path
            a = GetAssembly();
            if (Status == PluginStatus.Blocked)
                return false;

            if (a is null)
            {
                LogFile.Error("Failed to load " + ToString());
                Error();
                return false;
            }

            // Precompile the entire assembly in order to force any missing method exceptions
            //LogFile.WriteLine("Precompiling " + a);
            //LoaderTools.Precompile(a);
            return true;
        }
        catch (Exception e)
        {
            string name = ToString();

            if (e is AggregateException aggEx)
            {
                LogFile.Error($"Failed to build {name}:");
                foreach (var ex in aggEx.InnerExceptions)
                {
                    LogFile.Error(ex.Message);
                }

                Error();
                a = null;

                return false;
            }

            LogFile.Error($"Failed to load {name} because of an error: " + e);
            if (e is MemberAccessException)
            {
                LogFile.Error($"Is {name} up to date?");
                InvalidateCache();
            }

            if (e is NotSupportedException && e.Message.Contains("loadFromRemoteSources"))
                Error(
                    $"The plugin {name} was blocked by windows. "
                        + "Please unblock the file in the dll file properties."
                );
            else if (e is WebException)
                Status = PluginStatus.Network;
            else
                Error();
            a = null;
            return false;
        }
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as PluginData);
    }

    public bool Equals(PluginData other)
    {
        return other is not null && Id == other.Id;
    }

    public override int GetHashCode()
    {
        return 2108858624 + EqualityComparer<string>.Default.GetHashCode(Id);
    }

    public static bool operator ==(PluginData left, PluginData right)
    {
        return EqualityComparer<PluginData>.Default.Equals(left, right);
    }

    public static bool operator !=(PluginData left, PluginData right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return Id + '|' + FriendlyName;
    }

    public void Error(string msg = null)
    {
        Status = PluginStatus.Error;
        if (Flags.CheckAllPlugins)
            return;
        msg ??=
            $"The plugin '{this}' caused an error. "
            + "It is recommended that you disable this plugin and restart. "
            + "The game may be unstable beyond this point. ";

        if (LogFile.GameLog?.Exists() ?? false)
            msg +=
                "See info.log or the game log for details.\n\n"
                + "Would you like to open the Space Engineers and Pulsar logs?";
        else
            msg += "See info.log for details.\n\nWould you like to open the Pulsar log?";

        MessageBoxButtons buttons = MessageBoxButtons.YesNo;
        DialogResult result = Tools.ShowMessageBox(msg, buttons, MessageBoxIcon.Error);

        if (result == DialogResult.No)
            return;

        if (LogFile.GameLog?.Exists() ?? false)
            LogFile.GameLog.Open();

        LogFile.Open();
    }

    public int FuzzyRank(string query)
    {
        string[] terms = query.Split([';'], StringSplitOptions.RemoveEmptyEntries);

        int nameScore = 0;
        if (FriendlyName is not null)
            foreach (string term in terms)
                nameScore += Fuzz.PartialRatio(term, FriendlyName);

        int authorScore = 0;
        if (Author is not null)
            foreach (string term in terms)
                authorScore += Fuzz.Ratio(term, Author);

        int tooltipScore = 0;
        if (Tooltip is not null)
            foreach (string term in terms)
                tooltipScore += Fuzz.TokenSetRatio(term, Tooltip);

        return nameScore + authorScore + tooltipScore;
    }

    public virtual void UpdateProfile(Profile profile, bool enabled)
    {
        if (enabled)
            foreach (PluginData other in Group)
                other.UpdateProfile(profile, false);
        else
            profile.Remove(Id);
    }

    /// <summary>
    /// Invalidate any compiled assemblies on the disk
    /// </summary>
    public virtual void InvalidateCache() { }

    public virtual string GetAssetPath()
    {
        return null;
    }

    public string GetConfigPath(string name, string extension = null)
    {
        string data = Path.Combine(ConfigManager.Instance.PulsarDir, "Data");

        if (!Directory.Exists(data))
            Directory.CreateDirectory(data);

        string config = Path.Combine(data, name);
        if (extension is null)
        {
            config += @"\";
            if (!Directory.Exists(config))
                Directory.CreateDirectory(config);
        }
        else
        {
            config += "." + extension;
        }

        return config;
    }
}
