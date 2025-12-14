using HarmonyLib;
using ImGuiNET;
using Pulsar.Legacy.ImGuiBackends;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRage;

namespace Pulsar.Legacy.Patch;

[HarmonyPatchCategory("Late")]
[HarmonyPatch]
public static class Patch_MyRender11
{
    static bool show_demo_window = true;
    static bool show_another_window = true;
    static Vector4 clear_color = new Vector4(0.45f, 0.55f, 0.60f, 1.00f);

    static readonly Stopwatch sw = new();

    static Form GetGameWindow()
    {
        IVRageWindows windows = MyVRage.Platform.Windows;
        return (Form)AccessTools.Field(windows.GetType(), "m_form").GetValue(windows);
    }

    [HarmonyPatch("VRageRender.MyRender11", "Present")]
    [HarmonyPrefix]
    public static unsafe void Present_Prefix(SwapChain ___m_swapchain)
    {
        if (!ImGuiImpl.Initialized)
        {
            ImGuiImpl.Init(GetGameWindow(), ___m_swapchain);
        }

        sw.Stop();
        float deltaSeconds = sw.ElapsedTicks / (float)Stopwatch.Frequency;
        ImGuiImpl.NewFrame(deltaSeconds);
        sw.Restart();

        // 1. Show the big demo window (Most of the sample code is in ImGui.ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
        if (show_demo_window)
        {
            ImGui.ShowDemoWindow(ref show_demo_window);
        }

        // 2. Show a simple window that we create ourselves. We use a Begin/End pair to create a named window.
        {
            float f = 0.0f;
            int counter = 0;
        
            ImGui.Begin("Hello, world!");                          // Create a window called "Hello, world!" and append into it.
        
            ImGui.Text("This is some useful text.");               // Display some text (you can use a format strings too)
            ImGui.Checkbox("Demo Window", ref show_demo_window);      // Edit bools storing our window open/close state
            ImGui.Checkbox("Another Window", ref show_another_window);
        
            ImGui.SliderFloat("float", ref f, 0.0f, 1.0f);            // Edit 1 float using a slider from 0.0f to 1.0f
            ImGui.ColorEdit3("clear color", ref Unsafe.As<Vector4, Vector3>(ref clear_color)); // Edit 3 floats representing a color
        
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
            if (ImGui.ColorButton("Close Me", new Vector4(0.5f, 0.5f, 0.5f, 1)))
                show_another_window = false;
            ImGui.End();
        }

        // Rendering
        ImGuiImpl.Render();
    }
}
