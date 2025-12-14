using HarmonyLib;
using ImGuiNET;
using Pulsar.Legacy.ImGuiBackends;
using SharpDX;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRage;
using VRageRender;

namespace Pulsar.Legacy.Patch;

//[HarmonyPatchCategory("Late")]
//[HarmonyPatch("VRage.Platform.Windows.Render.MyPlatformRender", "CreateRenderDevice")]
public static class Patch_MyWindowsRender_CreateRenderDevice
{
    [HarmonyPostfix]
    public static unsafe void Postfix(Form window, object deviceInstance)
    {
        ImGui.CreateContext();
        ImGuiIOPtr io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;

        ImGui.StyleColorsDark();

        ImGui_ImplWin32.Init(window);

        Device device = (Device)deviceInstance;
        ImGui_ImplDX11.Init(device, device.ImmediateContext);
    }
}

[HarmonyPatchCategory("Late")]
[HarmonyPatch]
public static class Patch_MyRender11
{
    static bool init = false;

    static bool show_demo_window = true;
    static bool show_another_window = false;
    static Vector4 clear_color = new Vector4(0.45f, 0.55f, 0.60f, 1.00f);

    static Device1 Device;
    static Texture2D Backbuffer;
    static RenderTargetView BackbufferRtv;

    static void Init(SharpDX.DXGI.SwapChain swapchain)
    {
        Device = swapchain.GetDevice<Device1>();
        Backbuffer = swapchain.GetBackBuffer<Texture2D>(0);
        BackbufferRtv = new RenderTargetView(Device, Backbuffer);
    }

    [HarmonyPatch("VRageRender.MyRender11", "Present")]
    [HarmonyPrefix]
    public static unsafe void Present_Prefix(SharpDX.DXGI.SwapChain ___m_swapchain)
    {
        if (!init)
        {
            Init(___m_swapchain);
            IVRageWindows windows = MyVRage.Platform.Windows;
            Form form = (Form)AccessTools.Field(windows.GetType(), "m_form").GetValue(windows);
            Patch_MyWindowsRender_CreateRenderDevice.Postfix(form, Device);
            init = true;
        }

        ImGui_ImplWin32.NewFrame();
        ImGui_ImplDX11.NewFrame();
        ImGui.NewFrame();

        // 1. Show the big demo window (Most of the sample code is in ImGui.ShowDemoWindow()! You can browse its code to learn more about Dear ImGui!).
        if (show_demo_window)
        {
            bool showDemoWindow = true;
            ImGui.ShowDemoWindow(ref showDemoWindow);
        }

        //// 2. Show a simple window that we create ourselves. We use a Begin/End pair to create a named window.
        //{
        //    float f = 0.0f;
        //    int counter = 0;
        //
        //    ImGui.Begin("Hello, world!");                          // Create a window called "Hello, world!" and append into it.
        //
        //    ImGui.Text("This is some useful text.");               // Display some text (you can use a format strings too)
        //    ImGui.Checkbox("Demo Window", ref show_demo_window);      // Edit bools storing our window open/close state
        //    ImGui.Checkbox("Another Window", ref show_another_window);
        //
        //    ImGui.SliderFloat("float", ref f, 0.0f, 1.0f);            // Edit 1 float using a slider from 0.0f to 1.0f
        //    ImGui.ColorEdit3("clear color", ref Unsafe.As<Vector4, Vector3>(ref clear_color)); // Edit 3 floats representing a color
        //
        //    if (ImGui.Button("Button"))                            // Buttons return true when clicked (most widgets return true when edited/activated)
        //        counter++;
        //    ImGui.SameLine();
        //    ImGui.Text($"counter = {counter}");
        //
        //    ImGuiIOPtr io = ImGui.GetIO();
        //    ImGui.Text($"Application average {1000.0f / io.Framerate:.000} ms/frame ({io.Framerate:.0} FPS)");
        //    ImGui.End();
        //}

        //// 3. Show another simple window.
        //if (show_another_window)
        //{
        //    ImGui.Begin("Another Window", ref show_another_window);   // Pass a pointer to our bool variable (the window will have a closing button that will clear the bool when clicked)
        //    ImGui.Text("Hello from another window!");
        //    if (ImGui.Button("Close Me"))
        //        show_another_window = false;
        //    ImGui.End();
        //}
        
        // Rendering
        ImGui.Render();

        //float[] clear_color_with_alpha = { clear_color.X * clear_color.W, clear_color.Y * clear_color.W, clear_color.Z * clear_color.W, clear_color.W };
        //g_pd3dDeviceContext->OMSetRenderTargets(1, &g_mainRenderTargetView, nullptr);
        //g_pd3dDeviceContext->ClearRenderTargetView(g_mainRenderTargetView, clear_color_with_alpha);
        Device.ImmediateContext.ClearState();
        Device.ImmediateContext.OutputMerger.SetRenderTargets(BackbufferRtv);
        ImGui_ImplDX11.RenderDrawData(ImGui.GetDrawData());

        //ImGui.UpdatePlatformWindows();
        //ImGui.RenderPlatformWindowsDefault();
    }

    [HarmonyPrefix, HarmonyPatch(typeof(SharpDX.DXGI.SwapChain), nameof(SharpDX.DXGI.SwapChain.Present), [typeof(int), typeof(SharpDX.DXGI.PresentFlags)])]
    public static bool SwapChain_Present(SharpDX.DXGI.SwapChain __instance, ref Result __result, int syncInterval, SharpDX.DXGI.PresentFlags flags)
    {
        __result = __instance.TryPresent(syncInterval, flags);
        __result.CheckError();
        return false;
    }
}
