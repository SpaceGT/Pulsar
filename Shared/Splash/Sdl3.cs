using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Pulsar.Shared.Splash;

// Minimal P/Invoke surface for the SDL3 functions used by the splash screen.
// Resolves to libSDL3.so via the system loader (ldconfig). The splash assets
// are embedded as BMP and loaded via SDL_LoadBMP, so SDL3_image is not needed.
internal static class Sdl3
{
    private const string SDL = "SDL3";

    public const uint SDL_INIT_VIDEO = 0x00000020u;

    public const ulong SDL_WINDOW_BORDERLESS = 0x0000000000000010UL;
    public const ulong SDL_WINDOW_HIDDEN = 0x0000000000000008UL;
    public const ulong SDL_WINDOW_ALWAYS_ON_TOP = 0x0000000000010000UL;

    public const int SDL_WINDOWPOS_CENTERED = unchecked((int)0x2FFF0000);

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_FRect
    {
        public float x;
        public float y;
        public float w;
        public float h;
    }

    // SDL_Event is a union padded to a fixed 128-byte size in SDL3.
    // We never inspect the contents — we just drain the queue.
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct SDL_Event
    {
        public uint type;
    }

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_Init(uint flags);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_Quit();

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_CreateWindow(byte[] title, int w, int h, ulong flags);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyWindow(IntPtr window);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetWindowPosition(IntPtr window, int x, int y);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetWindowTitle(IntPtr window, byte[] title);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_ShowWindow(IntPtr window);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RaiseWindow(IntPtr window);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_CreateRenderer(IntPtr window, byte[] name);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyRenderer(IntPtr renderer);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetRenderDrawColor(IntPtr renderer, byte r, byte g, byte b, byte a);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderClear(IntPtr renderer);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderPresent(IntPtr renderer);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderFillRect(IntPtr renderer, ref SDL_FRect rect);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderTexture(
        IntPtr renderer, IntPtr texture, IntPtr srcrect, ref SDL_FRect dstrect);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_CreateTextureFromSurface(IntPtr renderer, IntPtr surface);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroyTexture(IntPtr texture);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_DestroySurface(IntPtr surface);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_PollEvent(out SDL_Event evt);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_GetTextureSize(IntPtr texture, out float w, out float h);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_RenderDebugText(IntPtr renderer, float x, float y, byte[] str);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SDL_SetRenderScale(IntPtr renderer, float scaleX, float scaleY);

    [DllImport(SDL, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_LoadBMP(byte[] file);

    public static byte[] Utf8(string s)
    {
        if (s == null) s = "";
        int len = Encoding.UTF8.GetByteCount(s);
        byte[] buf = new byte[len + 1];
        Encoding.UTF8.GetBytes(s, 0, s.Length, buf, 0);
        // null terminator already set by array init
        return buf;
    }
}
