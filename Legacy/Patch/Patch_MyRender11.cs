using HarmonyLib;
using ImGuiNET;
using Pulsar.Legacy.ImGuiBackends;
using SharpDX.DXGI;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using VRage;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch]
public static class Patch_MyRender11
{
    static bool _firstInit = false;

    static bool show_demo_window = true;
    static bool show_another_window = true;
    static Vector3 clear_color = new Vector3(0.45f, 0.55f, 0.60f);
    static float f = 0;
    static int counter = 0;

    static readonly Stopwatch sw = new();

    static Form GetGameWindow()
    {
        IVRageWindows windows = MyVRage.Platform.Windows;
        return (Form)AccessTools.Field(windows.GetType(), "m_form").GetValue(windows);
    }

    [HarmonyPatch("VRageRender.MyRender11", "Draw")]
    [HarmonyPostfix]
    public static unsafe void Draw_Postfix(SwapChain ___m_swapchain)
    {
        if (!_firstInit)
        {
            // TODO: move to postfix preloader for VRage.Platform.Windows.Render.MyPlatformRender.CreateRenderDevice
            ImGuiImpl.Init(GetGameWindow(), ___m_swapchain);
            _firstInit = true;
        }

        if (!ImGuiImpl.Initialized)
        {
            return;
        }

        sw.Stop();
        float deltaSeconds = sw.ElapsedTicks / (float)Stopwatch.Frequency;
        sw.Restart();
        ImGuiImpl.NewFrame(deltaSeconds);

        // 1. Show the big demo window (Most of the sample code is in ImGui.ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
        if (show_demo_window)
        {
            ImGui.ShowDemoWindow(ref show_demo_window);
        }

        // 2. Show a simple window that we create ourselves. We use a Begin/End pair to create a named window.
        {
            ImGui.Begin("Hello, world!");                          // Create a window called "Hello, world!" and append into it.
        
            ImGui.Text("This is some useful text.");               // Display some text (you can use a format strings too)
            ImGui.Checkbox("Demo Window", ref show_demo_window);      // Edit bools storing our window open/close state
            ImGui.Checkbox("Another Window", ref show_another_window);
        
            ImGui.SliderFloat("float", ref f, 0.0f, 1.0f);            // Edit 1 float using a slider from 0.0f to 1.0f
            ImGui.ColorEdit3("clear color", ref clear_color); // Edit 3 floats representing a color
        
            if (ImGui.Button("Button"))                            // Buttons return true when clicked (most widgets return true when edited/activated)
                counter++;
            ImGui.SameLine();
            ImGui.Text($"counter = {counter}");
        
            ImGuiIOPtr io = ImGui.GetIO();
            ImGui.Text($"Application average {1000.0f / io.Framerate:.000} ms/frame ({io.Framerate:.0} FPS)");
            ImGui.End();
        }

        // 3. Show another simple window.
        if (show_another_window)
        {
            ImGui.Begin("Another Window", ref show_another_window);   // Pass a pointer to our bool variable (the window will have a closing button that will clear the bool when clicked)
            ImGui.Text("Hello from another window!");
            if (ImGui.Button("Close Me"))
                show_another_window = false;
            ImGui.End();
        }

        {
            // submit render commands here
        }

        // Rendering
        ImGuiImpl.Render();
    }

    [HarmonyPatch("VRageRender.MyRender11", "ResizeSwapchain")]
    [HarmonyPrefix]
    public static void ResizeSwapchain_Prefix() => ImGuiImpl.DestroyBackbufferResources();

    [HarmonyPatch("VRageRender.MyRender11", "ResizeSwapchain")]
    [HarmonyPostfix]
    public static void ResizeSwapchain_Postfix(SwapChain ___m_swapchain) => ImGuiImpl.CreateBackbufferResources(___m_swapchain);

    [HarmonyPatch("VRageRender.MyRender11", "OnDeviceEnd")]
    [HarmonyPostfix]
    public static void OnDeviceEnd_Postfix(SwapChain ___m_swapchain) => ImGuiImpl.Shutdown();
}
