using ProtoBuf;
using avaness.PluginLoader.GUI;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using VRage.Utils;
using avaness.PluginLoader.Config;
using Sandbox.Game.World;

namespace avaness.PluginLoader.Data
{
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
                switch (Status)
                {
                    case PluginStatus.PendingUpdate:
                        return "Pending Update";
                    case PluginStatus.Updated:
                        return "Updated";
                    case PluginStatus.Error:
                        return "Error!";
                    case PluginStatus.Blocked:
                        return "Not whitelisted!";
                    default:
                        return "";
                }
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
        public List<PluginData> Group { get; } = new List<PluginData>();

        [XmlIgnore]
        public bool Enabled => Main.Instance.Config.IsEnabled(Id);


        protected PluginData()
        {
        }

        /// <summary>
        /// Loads the user settings into the plugin. Returns true if the config was modified.
        /// </summary>
        public virtual bool LoadData(ref PluginDataConfig config, bool enabled)
        {
            return false;
        }

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

                if (a == null)
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
                LogFile.Error($"Failed to load {name} because of an error: " + e);
                if (e is MemberAccessException)
                {
                    LogFile.Error($"Is {name} up to date?");
                    InvalidateCache();
                }

                if (e is NotSupportedException && e.Message.Contains("loadFromRemoteSources"))
                    Error($"The plugin {name} was blocked by windows. Please unblock the file in the dll file properties.");
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
            return other != null &&
                   Id == other.Id;
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
            if (Main.Instance.DebugCompileAll)
                return;
            if (msg == null)
                msg = $"The plugin '{this}' caused an error. It is recommended that you disable this plugin and restart. The game may be unstable beyond this point. See loader.log or the game log for details.";
            string file = MyLog.Default.GetFilePath();
            if(File.Exists(file) && file.EndsWith(".log"))
            {
                MyLog.Default.Flush();
                msg += "\n\nWould you like to open the game log?";
                DialogResult result = LoaderTools.ShowMessageBox(msg, MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                if (result == DialogResult.Yes)
                    Process.Start(file);
            }
            else
            {
                LoaderTools.ShowMessageBox(msg, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public abstract void Show();

        public virtual void GetDescriptionText(MyGuiControlMultilineText textbox)
        {
            textbox.Visible = true;
            textbox.Clear();
            if (string.IsNullOrEmpty(Description))
            {
                if (string.IsNullOrEmpty(Tooltip))
                    textbox.AppendText("No description");
                else
                    textbox.AppendText(CapLength(Tooltip, 1000));
                return;
            }
            else
            {
                string text = CapLength(Description, 1000);
                int textStart = 0;
                foreach (Match m in Regex.Matches(text, @"https?:\/\/(www\.)?[\w-.]{2,256}\.[a-z]{2,4}\b[\w-.@:%\+~#?&//=]*"))
                {
                    int textLen = m.Index - textStart;
                    if (textLen > 0)
                        textbox.AppendText(text.Substring(textStart, textLen));

                    textbox.AppendLink(m.Value, m.Value);
                    textStart = m.Index + m.Length;
                }

                if (textStart < text.Length)
                    textbox.AppendText(text.Substring(textStart));
            }
        }

        private string CapLength(string s, int len)
        {
            if (s.Length > len)
                return s.Substring(0, len);
            return s;
        }

        public virtual bool UpdateEnabledPlugins(HashSet<string> enabledPlugins, bool enable)
        {
            bool changed;

            if (enable)
            {
                changed = enabledPlugins.Add(Id);

                foreach (PluginData other in Group)
                {
                    if (!ReferenceEquals(other, this) && other.UpdateEnabledPlugins(enabledPlugins, false))
                        changed = true;
                }
            }
            else
            {
                changed = enabledPlugins.Remove(Id);
            }

            return changed;
        }

        /// <summary>
        /// Invalidate any compiled assemblies on the disk
        /// </summary>
        public virtual void InvalidateCache()
        {

        }

        public virtual void AddDetailControls(PluginDetailMenu screen, MyGuiControlBase bottomControl, out MyGuiControlBase topControl)
        {
            topControl = null;
        }

        public virtual string GetAssetPath()
        {
            return null;
        }
    }
}