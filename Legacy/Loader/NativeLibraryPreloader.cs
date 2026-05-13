using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Pulsar.Legacy.Loader;

/// <summary>
/// Linux native-library bootstrap. Runs once at the very top of Main() and
/// is the single place that:
///   * dlopens every bundled lib*.so* next to the launcher with an absolute
///     path and RTLD_GLOBAL, so subsequent lookups never go to disk;
///   * dlopens a few external libraries (SDL3) via the system loader with a
///     small list of Steam-runtime fallback directories;
///   * resolves the Windows-style DLL names declared in Pulsar's bundled
///     Steamworks.NET, Keen's SharpDX / VRage wrappers, the Linux-compat
///     plugin, etc. against the preloaded handles.
///
/// Replaces the per-component resolvers that used to live in
/// Pulsar.Legacy.Program (Steam, SDL) and in the se-linux-compat plugin
/// (DxvkResolver, NativeWrapperResolver, D3DCompilerResolver). Centralising
/// here means plugins loaded into custom AssemblyLoadContexts (Pulsar's
/// .pl5 cache directories) no longer need their own resolver registration:
/// the ResolvingUnmanagedDll hook installed below fires for every ALC.
/// </summary>
internal static class NativeLibraryPreloader
{
    // libname asked by [DllImport(...)] -> handle of the underlying .so.
    // Filled in two passes: (1) preload populates the canonical filename,
    // (2) Aliases below add Windows / short / versioned aliases.
    private static readonly Dictionary<string, IntPtr> Handles =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<AssemblyLoadContext> HookedContexts = new();

    // Names that managed bindings request -> canonical Linux filename we
    // preloaded. The right-hand side is looked up in Handles after preload,
    // so every alias on the left gets the same dlopen handle.
    private static readonly (string Alias, string Target)[] Aliases =
    {
        // se-linux-compat PE-loader wrappers (HavokWrapper, RecastDetourWrapper,
        // VRage.NativeWrapper, and ClientPlugin's own DllImport sites).
        ("Havok.dll",        "libHavok.so"),
        ("RecastDetour.dll", "libRecastDetour.so"),
        ("VRage.Native.dll", "libVRageNative.so"),
        ("D3DCompiler.dll",  "libD3DCompiler.so"),

        // DXVK rendering stack (SharpDX.Direct3D11 / SharpDX.DXGI).
        ("d3d11",     "libdxvk_d3d11.so"),
        ("d3d11.dll", "libdxvk_d3d11.so"),
        ("dxgi",      "libdxvk_dxgi.so"),
        ("dxgi.dll",  "libdxvk_dxgi.so"),

        // Epic Online Services SDK (VRage.EOS / Epic.OnlineServices).
        ("EOSSDK-Shipping",     "libEOSSDK-Linux-Shipping.so"),
        ("EOSSDK-Shipping.dll", "libEOSSDK-Linux-Shipping.so"),

        // Steamworks (Pulsar's bundled Steamworks.NET.dll).
        ("steam_api64",     "libsteam_api.so"),
        ("steam_api64.dll", "libsteam_api.so"),

        // SDL3 (preloaded externally — not bundled with Pulsar). Different
        // DllImport sites in Pulsar's SplashManager and in se-linux-compat
        // use different name variants; all resolve to the same handle.
        ("SDL3",             "libSDL3.so"),
        ("SDL3.dll",         "libSDL3.so"),
        ("libSDL3.so.0",     "libSDL3.so"),
        ("SDL3_ttf",         "libSDL3_ttf.so"),
        ("SDL3_ttf.dll",     "libSDL3_ttf.so"),
        ("libSDL3_ttf.so.0", "libSDL3_ttf.so"),
    };

    private const int RTLD_NOW    = 0x2;
    private const int RTLD_GLOBAL = 0x100;

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen(string filename, int flags);

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int setenv(string name, string value, int overwrite);

    public static void Initialize(string baseDir)
    {
        if (!OperatingSystem.IsLinux())
            return;

        // DXVK 2.7+ resolves its WSI driver lazily via dlopen on first use.
        // Pin it to SDL3, which matches the WSI we preload below (and which
        // the splash + game window plugins use). Honour an existing value.
        setenv("DXVK_WSI_DRIVER", "SDL3", 0);
        Environment.SetEnvironmentVariable("DXVK_WSI_DRIVER",
            Environment.GetEnvironmentVariable("DXVK_WSI_DRIVER") ?? "SDL3");

        // 1. Preload every bundled lib*.so* with absolute path + RTLD_GLOBAL.
        //    Absolute path bypasses ld.so search order; RTLD_GLOBAL exposes
        //    symbols to subsequent dlopen calls (DXVK lazy-loads its WSI
        //    backend that way).
        PreloadBundled(baseDir);

        // 2. SDL3 is not bundled — try the system loader (LD_LIBRARY_PATH /
        //    ldconfig) first, then a list of well-known Steam runtime dirs.
        //    Same fallback list as the old SetupSdlNativeResolver.
        var externalDirs = ExternalSearchDirs(baseDir);
        PreloadExternal("libSDL3.so",     new[] { "libSDL3.so",     "libSDL3.so.0"     }, externalDirs);
        PreloadExternal("libSDL3_ttf.so", new[] { "libSDL3_ttf.so", "libSDL3_ttf.so.0" }, externalDirs);

        // 3. Materialise the alias table. Done after preload so every alias
        //    that points to a successfully loaded library gets cached too.
        foreach (var (alias, target) in Aliases)
        {
            if (Handles.TryGetValue(target, out var handle) && !Handles.ContainsKey(alias))
                Handles[alias] = handle;
        }

        // 4. Hook the resolver on every existing and future ALC. The
        //    AppDomain.AssemblyLoad event fires for loads in any ALC (incl.
        //    Pulsar's plugin ALCs whose default DllImport search paths do
        //    not contain baseDir), so newly-created plugin contexts get
        //    hooked the moment their first assembly is loaded.
        foreach (var alc in AssemblyLoadContext.All)
            HookContext(alc);
        AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
        {
            var alc = AssemblyLoadContext.GetLoadContext(args.LoadedAssembly);
            if (alc != null) HookContext(alc);
        };
    }

    private static void PreloadBundled(string baseDir)
    {
        if (!Directory.Exists(baseDir))
            return;

        foreach (var path in Directory.EnumerateFiles(baseDir, "lib*.so*"))
        {
            var fileName = Path.GetFileName(path);
            if (Handles.ContainsKey(fileName))
                continue;

            var handle = dlopen(path, RTLD_NOW | RTLD_GLOBAL);
            if (handle == IntPtr.Zero)
            {
                Console.WriteLine($"[Pulsar] dlopen failed: {path}");
                continue;
            }

            Handles[fileName] = handle;

            // Also alias under the unversioned name (e.g. libavcodec.so.62
            // and libavcodec.so.62.28.100 both resolve to libavcodec.so).
            // First wins, which keeps the most specific version on disk.
            var unversioned = StripVersionSuffix(fileName);
            if (unversioned != fileName && !Handles.ContainsKey(unversioned))
                Handles[unversioned] = handle;
        }
    }

    private static void PreloadExternal(string canonicalName, string[] candidates, IEnumerable<string> searchDirs)
    {
        if (Handles.ContainsKey(canonicalName))
            return;

        // Try the system loader first — most distros and the Steam runtime
        // expose libSDL3 via ldconfig, no absolute path needed.
        foreach (var candidate in candidates)
        {
            var handle = dlopen(candidate, RTLD_NOW | RTLD_GLOBAL);
            if (handle != IntPtr.Zero)
            {
                Handles[canonicalName] = handle;
                Handles[candidate] = handle;
                return;
            }
        }

        // Fall back to absolute paths in known locations.
        foreach (var dir in searchDirs)
        {
            foreach (var candidate in candidates)
            {
                var full = Path.Combine(dir, candidate);
                if (!File.Exists(full)) continue;
                var handle = dlopen(full, RTLD_NOW | RTLD_GLOBAL);
                if (handle != IntPtr.Zero)
                {
                    Handles[canonicalName] = handle;
                    Handles[candidate] = handle;
                    return;
                }
            }
        }

        Console.WriteLine($"[Pulsar] WARNING: failed to preload {canonicalName}");
    }

    private static IEnumerable<string> ExternalSearchDirs(string baseDir)
    {
        yield return baseDir;
        yield return Path.Combine(baseDir, "..", "Libraries"); // sibling of Bin
        yield return Path.Combine(baseDir, "Libraries");

        var home = Environment.GetEnvironmentVariable("HOME") ?? "";
        yield return Path.Combine(home, ".steam", "debian-installation", "ubuntu12_64");
        yield return Path.Combine(home, ".steam", "debian-installation", "steamrt64");
        yield return Path.Combine(home, ".steam", "steam", "ubuntu12_64");
        yield return Path.Combine(home, ".steam", "steam", "steamrt64");
        yield return Path.Combine(home, ".local", "share", "Steam", "ubuntu12_64");
    }

    // libfoo.so.62.28.100 -> libfoo.so   (anything after the first ".so." is
    // soname version metadata that no DllImport site ever spells out).
    private static string StripVersionSuffix(string fileName)
    {
        int idx = fileName.IndexOf(".so.", StringComparison.Ordinal);
        return idx < 0 ? fileName : fileName.Substring(0, idx + 3);
    }

    private static void HookContext(AssemblyLoadContext alc)
    {
        if (!HookedContexts.Add(alc))
            return;
        alc.ResolvingUnmanagedDll += Resolve;
    }

    // Fires only after the runtime's default native-probing fails, so the
    // common case (loading a bundled lib via its real name in a context
    // whose probe dirs contain baseDir) doesn't go through here.
    private static IntPtr Resolve(Assembly assembly, string libraryName)
    {
        return Handles.TryGetValue(libraryName, out var handle) ? handle : IntPtr.Zero;
    }
}
