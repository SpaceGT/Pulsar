using HarmonyLib;
using Keen.Game2.Client.UI.Menu;
using Keen.Game2.Client.UI.Menu.MainMenu;
using Pulsar.Shared;

namespace Pulsar.Modern.Patch
{
    [HarmonyPatchCategory("Late")]
    [HarmonyPatch(typeof(GameMenu), "OnLoaded")]
    internal class Patch_ContinueGame
    {
        private static bool usedAutoRejoin = false;

        private static void Postfix(GameMenu __instance)
        {
            if (
            __instance.DataContext is MainMenuScreenViewModel viewModel
            && viewModel.LatestMetaData != null
            && !usedAutoRejoin
            && Flags.ContinueGame
        )
            {
                viewModel.ContinueGame.Invoke();
                usedAutoRejoin = true;
            }
        }
    }
}
