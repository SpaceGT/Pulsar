using Keen.VRage.Core;
using Keen.VRage.Library.Threading;
using Keen.VRage.Library.Utils;
using Keen.VRage.Steam.EngineComponents;
using Pulsar.Shared;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar.Modern.Loader;

internal static class SteamMods
{
    public static void Update(IEnumerable<ulong> ids)
    {
        if (!ids.Any())
            return;

        LogFile.WriteLine($"Updating {ids.Count()} workshop items");

        SteamUGCServiceComponent steamService = Singleton<VRageCore>.Instance.Engine.Get<SteamUGCServiceComponent>();

        Parallel.ForEach(ids, delegate (ulong id) 
        {
            try
            {
                steamService.DownloadItem(new PublishedFileId_t(id));
            }
            catch (Exception ex)
            {
                LogFile.Error($"An error occurred while updating workshop items: {ex}");
            }
        });
    }
}
