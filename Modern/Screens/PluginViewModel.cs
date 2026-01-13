using Avalonia.Controls;
using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Modern.Screens
{
    internal class PluginViewModel : AttachedViewModel
    {
        public PluginData PluginData { get; private set; }
        public string DescriptionShort { get; private set; }

        public string DescriptionFull { get; private set; }
        public PluginStat PluginStat { get; private set; }

        public bool Enabled { get; private set; }
        public string StatusString { get; set; }
        public string VersionString { get; set; }
        public string ToolTipString { get; set; }

        public PluginViewModel(PluginData pluginData, bool enabled)
        {
            PluginStats stats = null;

            if (!Design.IsDesignMode)
                stats = ConfigManager.Instance.Stats ?? new PluginStats();

            PluginData = pluginData;

            if (string.IsNullOrEmpty(PluginData.Tooltip))
            {
                string shortDescription = PluginData.Description;
                if (!string.IsNullOrEmpty(shortDescription))
                {
                    DescriptionShort = shortDescription;
                }
            }
            else
            {
                DescriptionShort = PluginData.Tooltip;
            }

            if (string.IsNullOrEmpty(PluginData.Description))
            {
                if (string.IsNullOrEmpty(PluginData.Tooltip))
                    DescriptionFull = "No description";
                else
                    DescriptionFull = PluginData.Tooltip;
            }
            else
                DescriptionFull = PluginData.Description;


            if (!Design.IsDesignMode)
                PluginStat = stats.GetStatsForPlugin(PluginData);
            else
                PluginStat = new PluginStat();

            Enabled = enabled;
        }
    }
}
