using Avalonia.Controls;
using Keen.VRage.UI.Screens;
using Pulsar.Shared.Config;
using Pulsar.Shared.Data;
using Pulsar.Shared.Stats.Model;

namespace Pulsar.Modern.Screens.AddPluginScreen
{
    internal class PluginViewModel : AttachedViewModel
    {
        public PluginData PluginData { get; private set; }
        public string DescriptionShort { get; private set; }
        public PluginStat PluginStat { get; private set; }

        public PluginViewModel(PluginData pluginData)
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

            if (!Design.IsDesignMode)
                PluginStat = stats.GetStatsForPlugin(PluginData);
            else
                PluginStat = new PluginStat();
        }
    }
}
