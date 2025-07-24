using System;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Data
{
    public interface IDependency
    {
        Version GetGameVersion();
        ulong GetSteamID();
        void InvokeOnGameThread(Action action);
        void ViewGameLog();
        bool HasGameLog();
        void WriteGameLog(string line);

        // Note that mod plugins were removed from the workshop
        // The functions are here for legacy reasons
        void SubscribeToItem(ulong id);
        void UpdateWorkshopItems(PluginList pluginList, PluginConfig config);
    }
}
