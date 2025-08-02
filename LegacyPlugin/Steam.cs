using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using HarmonyLib;
using ParallelTasks;
using Pulsar.Shared;
using Sandbox.Engine.Networking;
using VRage.Game;
using VRage.Utils;

namespace Pulsar.Legacy.Plugin
{
    public static class Steam
    {
        private static MethodInfo DownloadModsBlocking;

        public static void Update(IEnumerable<ulong> ids)
        {
            var modItems = new List<MyObjectBuilder_Checkpoint.ModItem>(
                ids.Select(x => new MyObjectBuilder_Checkpoint.ModItem(x, "Steam"))
            );
            if (modItems.Count == 0)
                return;
            LogFile.WriteLine($"Updating {modItems.Count} workshop items");

            // Source: MyWorkshop.DownloadWorldModsBlocking
            MyWorkshop.ResultData result = new();
            Task task = Parallel.Start(
                delegate
                {
                    result = UpdateInternal(modItems);
                }
            );
            while (!task.IsComplete)
            {
                MyGameService.Update();
                Thread.Sleep(10);
            }

            if (result.Result != VRage.GameServices.MyGameServiceCallResult.OK)
            {
                Exception[] exceptions = task.Exceptions;
                if (exceptions != null && exceptions.Length > 0)
                {
                    StringBuilder sb = new();
                    sb.AppendLine("An error occurred while updating workshop items:");
                    foreach (Exception e in exceptions)
                        sb.Append(e);
                    LogFile.Error(sb.ToString());
                }
                else
                {
                    LogFile.Error("Unable to update workshop items. Result: " + result.Result);
                }
            }
        }

        public static MyWorkshop.ResultData UpdateInternal(
            List<MyObjectBuilder_Checkpoint.ModItem> mods
        )
        {
            // Source: MyWorkshop.DownloadWorldModsBlockingInternal

            MyLog.Default.IncreaseIndent();

            List<WorkshopId> list =
            [
                .. mods.Select(x => new WorkshopId(x.PublishedFileId, x.PublishedServiceName)),
            ];

            if (DownloadModsBlocking == null)
                DownloadModsBlocking = AccessTools.Method(
                    typeof(MyWorkshop),
                    "DownloadModsBlocking"
                );

            MyWorkshop.ResultData resultData = (MyWorkshop.ResultData)
                DownloadModsBlocking.Invoke(
                    mods,
                    [mods, new MyWorkshop.ResultData(), list, new MyWorkshop.CancelToken()]
                );

            MyLog.Default.DecreaseIndent();
            return resultData;
        }
    }
}
