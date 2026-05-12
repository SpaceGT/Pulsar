using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Pulsar.Shared.Splash;

// SDL3-backed replacement for the WinForms splash screen.
// The SDL render loop runs on its own thread; public setters mutate
// shared state behind a lock and the loop redraws on its next tick.
internal sealed class SplashScreen
{
    // Match the WinForms splash dimensions exactly.
    private const int WindowWidth = 675;
    private const int WindowHeight = 375;
    private const int Padding = 10;

    private const float InnerWidth = WindowWidth - 2 * Padding;   // 655
    private const float InnerHeight = WindowHeight - 2 * Padding; // 355

    // Same row/column proportions as the WinForms TableLayoutPanel:
    //   columns 45% / 55%, rows 80% / 10% / 10%.
    private const float ColumnSplit = 0.45f;
    private const float ImagesHeight = InnerHeight * 0.80f;
    private const float TextRowHeight = InnerHeight * 0.10f;
    private const float BarRowHeight = InnerHeight * 0.10f;

    // SDL_RenderDebugText uses an 8x8 bitmap font; we scale it up for legibility.
    private const float TextScale = 2f;
    private const float DebugFontPx = 8f;

    // TTF point size used when SDL3_ttf is available.
    private const float FontPtSize = 14f;

    // The font file shipped as an embedded resource alongside the splash
    // assets. Extracted to a temp file at startup and opened via TTF_OpenFont.
    private const string FontResourceName = "NotoSans-Regular.ttf";

    private readonly object stateLock = new();
    private string title = "Pulsar";
    private string text = "";
    private float barValue = float.NaN;
    private bool running = true;

    private readonly ManualResetEventSlim ready = new();
    private readonly Thread thread;

    public float BarValue
    {
        get { lock (stateLock) return barValue; }
    }

    public SplashScreen()
    {
        thread = new Thread(Run) { IsBackground = true, Name = "Pulsar.Splash" };
        thread.Start();
        ready.Wait();
    }

    public void SetTitle(string newTitle)
    {
        lock (stateLock) title = newTitle ?? "";
    }

    public void SetText(string msg)
    {
        lock (stateLock)
        {
            barValue = float.NaN;
            text = msg ?? "";
        }
    }

    public void SetBarValue(float ratio = float.NaN)
    {
        if (!float.IsNaN(ratio))
            ratio = Math.Min(1f, Math.Max(0f, ratio));
        lock (stateLock) barValue = ratio;
    }

    public void Delete()
    {
        lock (stateLock) running = false;
        thread.Join();
    }

    private void Run()
    {
        IntPtr window = IntPtr.Zero;
        IntPtr renderer = IntPtr.Zero;
        IntPtr logoTex = IntPtr.Zero;
        IntPtr throbberTex = IntPtr.Zero;
        float logoW = 0, logoH = 0;
        float throbberW = 0, throbberH = 0;
        string logoPath = null;
        string throbberPath = null;
        string fontPath = null;
        bool sdlInited = false;
        bool ttfInited = false;
        IntPtr font = IntPtr.Zero;
        IntPtr cachedTextTex = IntPtr.Zero;
        string cachedText = null;
        float cachedTextW = 0, cachedTextH = 0;
        string lastTitle = null;

        try
        {
            if (!Sdl3.SDL_Init(Sdl3.SDL_INIT_VIDEO))
                return;
            sdlInited = true;

            window = Sdl3.SDL_CreateWindow(
                Sdl3.Utf8("Pulsar"), WindowWidth, WindowHeight,
                Sdl3.SDL_WINDOW_BORDERLESS | Sdl3.SDL_WINDOW_HIDDEN);
            if (window == IntPtr.Zero) return;

            Sdl3.SDL_SetWindowPosition(window, Sdl3.SDL_WINDOWPOS_CENTERED, Sdl3.SDL_WINDOWPOS_CENTERED);

            // Set window icon before showing so the taskbar picks it up
            // on first map (X11 _NET_WM_ICON / Wayland xdg_toplevel).
            TrySetWindowIcon(window);

            // Force the software renderer. The default (GL) backend pulls Steam's
            // overlay/fossilize layers into the splash thread and conflicts with
            // SE's DXVK/Vulkan + Xlib WSI later.
            renderer = Sdl3.SDL_CreateRenderer(window, Sdl3.Utf8("software"));
            if (renderer == IntPtr.Zero) return;

            // Show the window before attempting any non-essential work so the
            // splash always appears even if asset loading fails.
            Sdl3.SDL_ShowWindow(window);
            Sdl3.SDL_RaiseWindow(window);

            // text.bmp is the "Pulsar" logo, throbber.bmp is the first frame of
            // the throbber GIF flattened against black. BMPs avoid the
            // SDL3_image dependency.
            logoPath = ExtractEmbedded("text.bmp");
            throbberPath = ExtractEmbedded("throbber.bmp");

            (logoTex, logoW, logoH) = LoadTexture(renderer, logoPath);
            (throbberTex, throbberW, throbberH) = LoadTexture(renderer, throbberPath);

            // Optional: load SDL3_ttf and the embedded Noto Sans font. If
            // anything fails the splash falls back to SDL_RenderDebugText.
            fontPath = ExtractEmbedded(FontResourceName);
            (ttfInited, font) = TryLoadFont(fontPath);
        }
        catch
        {
            // Initialization failed (SDL libs missing, display unavailable, etc.) —
            // fall through and let cleanup run. Splash silently disabled.
        }
        finally
        {
            ready.Set();
        }

        if (renderer == IntPtr.Zero)
        {
            Cleanup(window, renderer, logoTex, throbberTex, cachedTextTex, font,
                logoPath, throbberPath, fontPath, sdlInited, ttfInited);
            return;
        }

        try
        {
            while (true)
            {
                string currentTitle;
                lock (stateLock)
                {
                    if (!running) break;
                    currentTitle = title;
                }

                if (currentTitle != lastTitle)
                {
                    Sdl3.SDL_SetWindowTitle(window, Sdl3.Utf8(currentTitle ?? ""));
                    lastTitle = currentTitle;
                }

                while (Sdl3.SDL_PollEvent(out _)) { }

                Render(renderer, logoTex, logoW, logoH, throbberTex, throbberW, throbberH,
                    font, ref cachedTextTex, ref cachedText, ref cachedTextW, ref cachedTextH);

                Thread.Sleep(33); // ~30 FPS
            }
        }
        catch
        {
            // Don't let render errors leak — just exit the loop and clean up.
        }
        finally
        {
            Cleanup(window, renderer, logoTex, throbberTex, cachedTextTex, font,
                logoPath, throbberPath, fontPath, sdlInited, ttfInited);
        }
    }

    private void Render(IntPtr renderer,
                        IntPtr logoTex, float logoW, float logoH,
                        IntPtr throbberTex, float throbberW, float throbberH,
                        IntPtr font,
                        ref IntPtr cachedTextTex, ref string cachedText,
                        ref float cachedTextW, ref float cachedTextH)
    {
        string textCopy;
        float bar;
        lock (stateLock)
        {
            textCopy = text;
            bar = barValue;
        }

        Sdl3.SDL_SetRenderDrawColor(renderer, 0, 0, 0, 255);
        Sdl3.SDL_RenderClear(renderer);

        float colSplitX = Padding + InnerWidth * ColumnSplit;

        // Throbber: left column, top row.
        if (throbberTex != IntPtr.Zero)
        {
            DrawZoomed(renderer, throbberTex, throbberW, throbberH,
                Padding, Padding, InnerWidth * ColumnSplit, ImagesHeight);
        }

        // Logo: right column, top row.
        if (logoTex != IntPtr.Zero)
        {
            DrawZoomed(renderer, logoTex, logoW, logoH,
                colSplitX, Padding, InnerWidth * (1f - ColumnSplit), ImagesHeight);
        }

        // Progress text: middle row, full width.
        if (!string.IsNullOrEmpty(textCopy))
        {
            float textRowY = Padding + ImagesHeight;

            if (font != IntPtr.Zero)
            {
                // Re-render to texture only when the text actually changes.
                if (cachedText != textCopy)
                {
                    if (cachedTextTex != IntPtr.Zero)
                    {
                        Sdl3.SDL_DestroyTexture(cachedTextTex);
                        cachedTextTex = IntPtr.Zero;
                    }
                    cachedText = textCopy;
                    (cachedTextTex, cachedTextW, cachedTextH) =
                        RenderTextTexture(renderer, font, textCopy);
                }

                if (cachedTextTex != IntPtr.Zero)
                {
                    Sdl3.SDL_FRect dst = new()
                    {
                        x = (WindowWidth - cachedTextW) * 0.5f,
                        y = textRowY + (TextRowHeight - cachedTextH) * 0.5f,
                        w = cachedTextW,
                        h = cachedTextH,
                    };
                    Sdl3.SDL_RenderTexture(renderer, cachedTextTex, IntPtr.Zero, ref dst);
                }
            }
            else
            {
                // SDL3_ttf unavailable — fall back to the built-in 8x8 bitmap font.
                float textPx = DebugFontPx * TextScale;
                float textW = textCopy.Length * textPx;

                // Coordinates are pre-scale because we set a uniform render scale.
                float xScaled = (WindowWidth - textW) * 0.5f / TextScale;
                float yScaled = (textRowY + (TextRowHeight - textPx) * 0.5f) / TextScale;

                Sdl3.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                Sdl3.SDL_SetRenderScale(renderer, TextScale, TextScale);
                Sdl3.SDL_RenderDebugText(renderer, xScaled, yScaled, Sdl3.Utf8(textCopy));
                Sdl3.SDL_SetRenderScale(renderer, 1f, 1f);
            }
        }

        // Progress bar: bottom row, full width. Background DimGray, fill white
        // (matches WinForms: progressBar.BackColor=DimGray, ForeColor inherits
        // form White).
        if (!float.IsNaN(bar))
        {
            float barY = Padding + ImagesHeight + TextRowHeight;

            Sdl3.SDL_FRect bg = new() { x = Padding, y = barY, w = InnerWidth, h = BarRowHeight };
            Sdl3.SDL_SetRenderDrawColor(renderer, 0x69, 0x69, 0x69, 255);
            Sdl3.SDL_RenderFillRect(renderer, ref bg);

            if (bar > 0f)
            {
                Sdl3.SDL_FRect fg = new() { x = Padding, y = barY, w = InnerWidth * bar, h = BarRowHeight };
                Sdl3.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
                Sdl3.SDL_RenderFillRect(renderer, ref fg);
            }
        }

        Sdl3.SDL_RenderPresent(renderer);
    }

    // Aspect-preserving fit (matches WinForms PictureBoxSizeMode.Zoom).
    private static void DrawZoomed(IntPtr renderer, IntPtr tex, float texW, float texH,
                                   float cellX, float cellY, float cellW, float cellH)
    {
        if (texW <= 0 || texH <= 0) return;
        float scale = Math.Min(cellW / texW, cellH / texH);
        float w = texW * scale;
        float h = texH * scale;
        Sdl3.SDL_FRect dst = new()
        {
            x = cellX + (cellW - w) * 0.5f,
            y = cellY + (cellH - h) * 0.5f,
            w = w,
            h = h
        };
        Sdl3.SDL_RenderTexture(renderer, tex, IntPtr.Zero, ref dst);
    }

    // Inits SDL3_ttf and opens the embedded font extracted to fontPath.
    // Returns (ttfInited, font). On any failure (libSDL3_ttf.so missing,
    // TTF_Init failure, font extraction failed, TTF_OpenFont failure) returns
    // (false, IntPtr.Zero) and the splash uses SDL_RenderDebugText instead.
    private static (bool ttfInited, IntPtr font) TryLoadFont(string fontPath)
    {
        if (fontPath == null) return (false, IntPtr.Zero);
        bool inited = false;
        try
        {
            if (!Sdl3.TTF_Init())
                return (false, IntPtr.Zero);
            inited = true;

            IntPtr font = Sdl3.TTF_OpenFont(Sdl3.Utf8(fontPath), FontPtSize);
            if (font != IntPtr.Zero)
                return (true, font);

            // TTF inited but font failed to open; tear down so cleanup doesn't
            // double-quit.
            Sdl3.TTF_Quit();
            return (false, IntPtr.Zero);
        }
        catch
        {
            if (inited)
            {
                try { Sdl3.TTF_Quit(); } catch { }
            }
            return (false, IntPtr.Zero);
        }
    }

    private static (IntPtr tex, float w, float h) RenderTextTexture(
        IntPtr renderer, IntPtr font, string text)
    {
        try
        {
            byte[] bytes = Sdl3.Utf8(text);
            // bytes is null-terminated; pass the byte length excluding the
            // terminator.
            UIntPtr length = (UIntPtr)(bytes.Length - 1);
            Sdl3.SDL_Color white = new() { r = 255, g = 255, b = 255, a = 255 };
            IntPtr surf = Sdl3.TTF_RenderText_Blended(font, bytes, length, white);
            if (surf == IntPtr.Zero) return (IntPtr.Zero, 0, 0);
            IntPtr tex = Sdl3.SDL_CreateTextureFromSurface(renderer, surf);
            Sdl3.SDL_DestroySurface(surf);
            if (tex == IntPtr.Zero) return (IntPtr.Zero, 0, 0);
            Sdl3.SDL_GetTextureSize(tex, out float w, out float h);
            return (tex, w, h);
        }
        catch
        {
            return (IntPtr.Zero, 0, 0);
        }
    }

    private static (IntPtr tex, float w, float h) LoadTexture(IntPtr renderer, string path)
    {
        if (path == null) return (IntPtr.Zero, 0, 0);
        // Wrap each load so a single asset failure cannot abort the whole splash.
        try
        {
            IntPtr surf = Sdl3.SDL_LoadBMP(Sdl3.Utf8(path));
            if (surf == IntPtr.Zero) return (IntPtr.Zero, 0, 0);
            IntPtr tex = Sdl3.SDL_CreateTextureFromSurface(renderer, surf);
            Sdl3.SDL_DestroySurface(surf);
            if (tex == IntPtr.Zero) return (IntPtr.Zero, 0, 0);
            Sdl3.SDL_GetTextureSize(tex, out float w, out float h);
            return (tex, w, h);
        }
        catch
        {
            return (IntPtr.Zero, 0, 0);
        }
    }

    // SDL_image works against file paths; the simplest portable thing is to
    // drop the embedded resource into a temp file.
    private static string ExtractEmbedded(string fileName)
    {
        try
        {
            Assembly asm = typeof(SplashScreen).Assembly;
            string resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resName == null) return null;

            using Stream s = asm.GetManifestResourceStream(resName);
            if (s == null) return null;

            string path = Path.Combine(Path.GetTempPath(), $"pulsar_splash_{Guid.NewGuid():N}_{fileName}");
            using FileStream fs = File.Create(path);
            s.CopyTo(fs);
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static void Cleanup(IntPtr window, IntPtr renderer,
                                IntPtr logoTex, IntPtr throbberTex,
                                IntPtr cachedTextTex, IntPtr font,
                                string logoPath, string throbberPath, string fontPath,
                                bool sdlInited, bool ttfInited)
    {
        try
        {
            if (cachedTextTex != IntPtr.Zero) Sdl3.SDL_DestroyTexture(cachedTextTex);
            if (logoTex != IntPtr.Zero) Sdl3.SDL_DestroyTexture(logoTex);
            if (throbberTex != IntPtr.Zero) Sdl3.SDL_DestroyTexture(throbberTex);
            if (renderer != IntPtr.Zero) Sdl3.SDL_DestroyRenderer(renderer);
            if (window != IntPtr.Zero) Sdl3.SDL_DestroyWindow(window);
            if (sdlInited) Sdl3.SDL_Quit();
        }
        catch
        {
        }

        if (ttfInited)
        {
            try
            {
                if (font != IntPtr.Zero) Sdl3.TTF_CloseFont(font);
                Sdl3.TTF_Quit();
            }
            catch
            {
            }
        }

        TryDelete(logoPath);
        TryDelete(throbberPath);
        TryDelete(fontPath);
    }

    private static void TryDelete(string path)
    {
        if (path == null) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    // ICO loading and SDL_SetWindowIcon plumbing. Ported from the
    // LinuxCompat plugin's SdlIconHelper so the splash window gets a
    // proper taskbar icon on X11 (_NET_WM_ICON) and Wayland.

    private const uint SdlPixelFormatBgra32 = 0x16862004u;

    private static void TrySetWindowIcon(IntPtr window)
    {
        try
        {
            Assembly asm = typeof(SplashScreen).Assembly;
            string resName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("icon.ico", StringComparison.OrdinalIgnoreCase));
            if (resName == null) return;

            using Stream s = asm.GetManifestResourceStream(resName);
            if (s == null) return;

            if (!TryLoadIcoSurface(s, out byte[] pixels, out int w, out int h, out int pitch))
                return;

            GCHandle pin = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                IntPtr surface = Sdl3.SDL_CreateSurfaceFrom(w, h, SdlPixelFormatBgra32, pin.AddrOfPinnedObject(), pitch);
                if (surface == IntPtr.Zero) return;
                try
                {
                    Sdl3.SDL_SetWindowIcon(window, surface);
                }
                finally
                {
                    Sdl3.SDL_DestroySurface(surface);
                }
            }
            finally
            {
                pin.Free();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WARNING: Failed to set splash window icon: {ex}");
        }
    }

    private static bool TryLoadIcoSurface(Stream stream, out byte[] pixelData, out int width, out int height, out int pitch)
    {
        pixelData = null;
        width = 0;
        height = 0;
        pitch = 0;

        using BinaryReader reader = new BinaryReader(stream);

        // ICO header: reserved (0), type (1 = icon), image count.
        if (reader.ReadUInt16() != 0 || reader.ReadUInt16() != 1)
            return false;

        int imageCount = reader.ReadUInt16();
        if (imageCount <= 0)
            return false;

        // Read directory entries.
        var entries = new IcoEntry[imageCount];
        for (int i = 0; i < imageCount; i++)
        {
            byte bw = reader.ReadByte();
            byte bh = reader.ReadByte();
            entries[i] = new IcoEntry
            {
                Width = bw == 0 ? 256 : bw,
                Height = bh == 0 ? 256 : bh,
                ColorCount = reader.ReadByte(),
                Reserved = reader.ReadByte(),
                Planes = reader.ReadUInt16(),
                BitCount = reader.ReadUInt16(),
                BytesInRes = reader.ReadUInt32(),
                ImageOffset = reader.ReadUInt32()
            };
        }

        // Pick the highest-quality entry (largest area × bit depth).
        Array.Sort(entries, (a, b) =>
        {
            int sa = a.Width * a.Height * Math.Max((int)a.BitCount, 1);
            int sb = b.Width * b.Height * Math.Max((int)b.BitCount, 1);
            return sb.CompareTo(sa);
        });

        for (int i = 0; i < entries.Length; i++)
        {
            if (TryDecodeIcoEntry(stream, entries[i], out pixelData, out width, out height, out pitch))
                return true;
        }

        return false;
    }

    private static bool TryDecodeIcoEntry(Stream stream, IcoEntry entry, out byte[] pixelData, out int width, out int height, out int pitch)
    {
        pixelData = null;
        width = 0;
        height = 0;
        pitch = 0;

        if (entry.ImageOffset == 0 || entry.BytesInRes < 40)
            return false;

        stream.Position = entry.ImageOffset;
        using BinaryReader reader = new BinaryReader(stream);

        uint headerSize = reader.ReadUInt32();
        if (headerSize < 40) return false;

        int dibWidth = reader.ReadInt32();
        int dibHeight = reader.ReadInt32();
        reader.ReadUInt16(); // planes
        ushort bitsPerPixel = reader.ReadUInt16();
        uint compression = reader.ReadUInt32();

        // Only handle uncompressed 32-bit BGRA entries.
        if (compression != 0 || bitsPerPixel != 32 || dibWidth <= 0 || dibHeight <= 0)
            return false;

        // Skip remaining BITMAPINFOHEADER fields.
        reader.ReadUInt32(); // biSizeImage
        reader.ReadInt32();  // biXPelsPerMeter
        reader.ReadInt32();  // biYPelsPerMeter
        reader.ReadUInt32(); // biClrUsed
        reader.ReadUInt32(); // biClrImportant

        width = dibWidth;
        height = dibHeight / 2; // ICO stores double-height (XOR + AND mask).
        pitch = width * 4;
        pixelData = new byte[pitch * height];

        int xorStride = ((width * bitsPerPixel + 31) / 32) * 4;
        byte[] xorData = reader.ReadBytes(xorStride * height);
        int andStride = ((width + 31) / 32) * 4;
        byte[] andMask = reader.ReadBytes(andStride * height);

        if (xorData.Length < xorStride * height)
        {
            pixelData = null;
            return false;
        }

        // Flip bottom-up rows to top-down and apply the AND mask for
        // entries that carry transparency there instead of in alpha.
        for (int y = 0; y < height; y++)
        {
            int srcRow = (height - 1 - y) * xorStride;
            int maskRow = (height - 1 - y) * andStride;
            int dstRow = y * pitch;
            for (int x = 0; x < width; x++)
            {
                int si = srcRow + x * 4;
                int di = dstRow + x * 4;
                pixelData[di] = xorData[si];
                pixelData[di + 1] = xorData[si + 1];
                pixelData[di + 2] = xorData[si + 2];
                byte alpha = xorData[si + 3];
                if (alpha == 0 && andMask.Length >= andStride * height)
                {
                    int maskByte = andMask[maskRow + x / 8];
                    bool transparent = ((maskByte >> (7 - x % 8)) & 1) != 0;
                    alpha = transparent ? (byte)0 : byte.MaxValue;
                }
                pixelData[di + 3] = alpha;
            }
        }

        return true;
    }

    private struct IcoEntry
    {
        public int Width;
        public int Height;
        public byte ColorCount;
        public byte Reserved;
        public ushort Planes;
        public ushort BitCount;
        public uint BytesInRes;
        public uint ImageOffset;
    }
}
