using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ImVec2 = System.Numerics.Vector2;

namespace Pulsar.Legacy.ImGuiBackends;

public static class ImGui_ImplWin32
{
    static Form Window;
    static IntPtr Hwnd;
    
    static void UpdateKeyboardCodePage(ImGuiIOPtr io)
    {
        // Retrieve keyboard code page, required for handling of non-Unicode Windows.
        //HKL keyboard_layout = ::GetKeyboardLayout(0);
        //LCID keyboard_lcid = MAKELCID(HIWORD(keyboard_layout), SORT_DEFAULT);
        //if (::GetLocaleInfoA(keyboard_lcid, (LOCALE_RETURN_NUMBER | LOCALE_IDEFAULTANSICODEPAGE), (LPSTR)&KeyboardCodePage, sizeof(KeyboardCodePage)) == 0)
        //    KeyboardCodePage = CP_ACP; // Fallback to default ANSI code page when fails.
    }

    private static bool InitEx(Form window, bool platform_has_own_dc)
    {
        if (Window != null)
        {
            throw new Exception("Already initialized a platform backend!");
        }

        ImGuiIOPtr io = ImGui.GetIO();
        io.BackendPlatformUserData = window.Handle;

        // Setup backend capabilities flags
        //io.BackendPlatformName = "imgui_impl_win32";
        io.BackendFlags |= ImGuiBackendFlags.HasMouseCursors;         // We can honor GetMouseCursor() values (optional)
        io.BackendFlags |= ImGuiBackendFlags.HasSetMousePos;          // We can honor io.WantSetMousePos requests (optional, rarely used)

        Window = window;
        Hwnd = window.Handle;
        UpdateKeyboardCodePage(io);

        // Set platform dependent data in viewport
        ImGuiViewportPtr main_viewport = ImGui.GetMainViewport();
        main_viewport.PlatformHandle = main_viewport.PlatformHandleRaw = Hwnd;

        return true;
    }

    public static bool Init(Form window)
    {
        return InitEx(window, false);
    }

    public static void Shutdown()
    {
        if (Window == null)
        {
            throw new Exception("No platform backend to shutdown, or already shutdown?");
        }

        ImGuiIOPtr io = ImGui.GetIO();
        //io.BackendPlatformName = null;
        io.BackendPlatformUserData = IntPtr.Zero;
        io.BackendFlags &= ~(ImGuiBackendFlags.HasMouseCursors | ImGuiBackendFlags.HasSetMousePos | ImGuiBackendFlags.HasGamepad);
    }

    public static void NewFrame()
    {
        if (Window == null)
        {
            throw new Exception("Context or backend not initialized? Did you call ImGui_ImplWin32_Init()?");
        }

        ImGuiIOPtr io = ImGui.GetIO();

        // Setup display size (every frame to accommodate for window resizing)
        Rectangle rect = Window.ClientRectangle;
        io.DisplaySize = new ImVec2(rect.Right - rect.Left, rect.Bottom - rect.Top);

        // Setup time step
        long current_time = 0;
        //::QueryPerformanceCounter((LARGE_INTEGER*)&current_time);
        //io.DeltaTime = (float)(current_time - bd->Time) / bd->TicksPerSecond;
        io.DeltaTime = 1f / 120f;

        // Update OS mouse position
        //UpdateMouseData(io);

        // Process workarounds for known Windows key handling issues
        //ProcessKeyEventsWorkarounds(io);

        // Update OS mouse cursor with the cursor requested by imgui
        //ImGuiMouseCursor mouse_cursor = io.MouseDrawCursor ? ImGuiMouseCursor_None : ImGui::GetMouseCursor();
        //if (bd->LastMouseCursor != mouse_cursor)
        //{
        //    bd->LastMouseCursor = mouse_cursor;
        //    UpdateMouseCursor(io, mouse_cursor);
        //}

        // Update game controllers (if enabled and available)
        //ImGui_ImplWin32_UpdateGamepads(io);
    }
}
