using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
        bool sdlInited = false;
        string lastTitle = null;

        try
        {
            if (!Sdl3.SDL_Init(Sdl3.SDL_INIT_VIDEO))
                return;
            sdlInited = true;

            window = Sdl3.SDL_CreateWindow(
                Sdl3.Utf8("Pulsar"), WindowWidth, WindowHeight,
                Sdl3.SDL_WINDOW_BORDERLESS | Sdl3.SDL_WINDOW_HIDDEN | Sdl3.SDL_WINDOW_ALWAYS_ON_TOP);
            if (window == IntPtr.Zero) return;

            Sdl3.SDL_SetWindowPosition(window, Sdl3.SDL_WINDOWPOS_CENTERED, Sdl3.SDL_WINDOWPOS_CENTERED);

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
            Cleanup(window, renderer, logoTex, throbberTex, logoPath, throbberPath, sdlInited);
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

                Render(renderer, logoTex, logoW, logoH, throbberTex, throbberW, throbberH);

                Thread.Sleep(33); // ~30 FPS
            }
        }
        catch
        {
            // Don't let render errors leak — just exit the loop and clean up.
        }
        finally
        {
            Cleanup(window, renderer, logoTex, throbberTex, logoPath, throbberPath, sdlInited);
        }
    }

    private void Render(IntPtr renderer,
                        IntPtr logoTex, float logoW, float logoH,
                        IntPtr throbberTex, float throbberW, float throbberH)
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
            float textPx = DebugFontPx * TextScale;
            float textW = textCopy.Length * textPx;
            float textRowY = Padding + ImagesHeight;

            // Coordinates are pre-scale because we set a uniform render scale.
            float xScaled = (WindowWidth - textW) * 0.5f / TextScale;
            float yScaled = (textRowY + (TextRowHeight - textPx) * 0.5f) / TextScale;

            Sdl3.SDL_SetRenderDrawColor(renderer, 255, 255, 255, 255);
            Sdl3.SDL_SetRenderScale(renderer, TextScale, TextScale);
            Sdl3.SDL_RenderDebugText(renderer, xScaled, yScaled, Sdl3.Utf8(textCopy));
            Sdl3.SDL_SetRenderScale(renderer, 1f, 1f);
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
                                string logoPath, string throbberPath,
                                bool sdlInited)
    {
        try
        {
            if (logoTex != IntPtr.Zero) Sdl3.SDL_DestroyTexture(logoTex);
            if (throbberTex != IntPtr.Zero) Sdl3.SDL_DestroyTexture(throbberTex);
            if (renderer != IntPtr.Zero) Sdl3.SDL_DestroyRenderer(renderer);
            if (window != IntPtr.Zero) Sdl3.SDL_DestroyWindow(window);
            if (sdlInited) Sdl3.SDL_Quit();
        }
        catch
        {
        }

        TryDelete(logoPath);
        TryDelete(throbberPath);
    }

    private static void TryDelete(string path)
    {
        if (path == null) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }
}
