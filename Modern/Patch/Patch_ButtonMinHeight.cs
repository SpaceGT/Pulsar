using Avalonia;
using Avalonia.Data;
using Avalonia.Layout;
using HarmonyLib;

namespace CustomScreenBackgrounds.Patches;

[HarmonyPatchCategory("Early")]
[HarmonyPatch("CompiledAvaloniaXaml.!AvaloniaResources+XamlClosure_631, Game2.Client", "Build")]
internal class Patch_ButtonMinHeight
{
    // This is likely will break easily after a game update due to class names likely changing.
    private static void Postfix(ref object __result)
    {
        (__result as AvaloniaObject)?.SetValue(Layoutable.MinHeightProperty, 0.0, BindingPriority.Template);
    }
}
