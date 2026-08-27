using System;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace Pulsar.Shared.Arguments;

public enum SplashType
{
    None,
    Native,
    Pulsar,
}

public enum UpdateType
{
    None,
    Standard,
    Tester,
}

[Command(
    OptionsComparison = StringComparison.OrdinalIgnoreCase,
    UnrecognizedArgumentHandling = UnrecognizedArgumentHandling.CollectAndContinue
)]
[VersionOptionFromMember(
    "-version",
    MemberName = nameof(ProgramVersion),
    Description = "Show the Pulsar version."
)]
[HelpOption("-help", Description = "Show this help screen.")]
public sealed class Flags
{
    public static Flags Current { get; internal set; }

    private static string ProgramVersion
    {
        get
        {
            Assembly assembly = Assembly.GetEntryAssembly();
            Version version = assembly.GetName().Version;
            return $"Pulsar v{version.ToString(3)}";
        }
    }

    // Summary arguments
    public SplashType SplashType =>
        NoSplash ? SplashType.None
        : SeSplash ? SplashType.Native
        : SplashType.Pulsar;

    public UpdateType UpdateType =>
        NoUpdate || !Tools.IsWindows() ? UpdateType.None
        : PreRelease ? UpdateType.Tester
        : UpdateType.Standard;

    // Simple flags
    [Option("-noSplash", Description = "Disable the splash screen.")]
    public bool NoSplash { get; internal set; }

    [Option("-seSplash", Description = "Use the game's native splash screen.")]
    public bool SeSplash { get; internal set; }

    [Option("-noUpdate", Description = "Disable Pulsar updates.")]
    public bool NoUpdate { get; internal set; }

    [Option("-preRelease", Description = "Use pre-release Pulsar updates.")]
    public bool PreRelease { get; internal set; }

    [Option("-debug", Description = "Launch the debugger at startup.")]
    public bool ExternalDebug { get; internal set; }

    [Option("-f12Menu", Description = "Enable the game's F12 debug menu.")]
    public bool DebugMenu { get; internal set; }

    [Option("-sources", Description = "Enable custom plugin sources.")]
    public bool CustomSources { get; internal set; }

    [Option("-continue", Description = "Continue the last game automatically.")]
    public bool ContinueGame { get; internal set; }

    [Option("-debugCompileAll", Description = "Compile and check all plugins.")]
    public bool CheckAllPlugins { get; internal set; }

    [Option("-debugMods", Description = "Build game mods in debug mode.")]
    public bool DebugMods { get; internal set; }

    [Option("-keepIntro", Description = "Keep the game intro video.")]
    public bool GameIntroVideo { get; internal set; }

    [Option("-mkCheck", Description = "Create a library checksum file.")]
    public bool MakeCheckFile { get; internal set; }

    [Option("-hardened", Description = "Load only trusted mods.")]
    public bool TrustedMods { get; internal set; }

    [Option("-safeMode", Description = "Start with user plugins disabled.")]
    public bool SafeMode { get; internal set; }

    [Option("-useHome", Description = "Store Pulsar data in the User's app-data folder.")]
    public bool UseHome { get; internal set; }

    [Option("-bare", Description = "Disable force-loading core plugins.")]
    public bool Bare { get; internal set; }

    [Option("-stableLogs", Description = "Overwrite game logs instead of timestamping them.")]
    public bool StableLogs { get; internal set; }

    [Option("-noPrompt", Description = "Dismiss Pulsar dialogs automatically.")]
    public bool NoPrompt { get; internal set; }

    [Option("-lazySteam", Description = "Attempt to start even if Steam is offline.")]
    public bool LazySteam { get; internal set; }

    [Option("-multiInstance", Description = "Allow multiple Pulsar instances.")]
    public bool MultiInstance { get; internal set; }

    [Option("-lazyPreload", Description = "Reuse existing preloader assemblies.")]
    public bool LazyPreload { get; internal set; }

    [Option("-profile <name>", Description = "Force a specific plugin profile.")]
    public string Profile { get; internal set; }

    // SE1 Specific
    [Option("-bin64 <path>", Description = "Use a specific Space Engineers Bin64 directory.")]
    public string Bin64 { get; internal set; }

    // SE2 Specific
    [Option("-game2 <path>", Description = "Use a specific Space Engineers 2 Game2 directory.")]
    public string Game2 { get; internal set; }
}
