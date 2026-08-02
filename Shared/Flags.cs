using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Pulsar.Shared;

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

file static class Arguments
{
    public static readonly string[] NoSplash = ["no", "splash"];
    public static readonly string[] SeSplash = ["se", "splash"];
    public static readonly string[] NoUpdate = ["no", "update"];
    public static readonly string[] PreRelease = ["pre", "release"];
    public static readonly string[] Debug = ["debug"];
    public static readonly string[] F12Menu = ["f12", "menu"];
    public static readonly string[] Sources = ["sources"];
    public static readonly string[] Continue = ["continue"];
    public static readonly string[] DebugCompileAll = ["debug", "compile", "all"];
    public static readonly string[] KeepIntro = ["keep", "intro"];
    public static readonly string[] MakeCheck = ["mk", "check"];
    public static readonly string[] Hardened = ["hardened"];
    public static readonly string[] SafeMode = ["safe", "mode"];

    public static readonly string[] HelpAliases = ["help", "h", "?"];

    public static readonly (string[] Words, string Description)[] Annotated =
    [
        ([HelpAliases[0]], "Show this help screen."),
        (NoSplash, "Disable the splash screen."),
        (SeSplash, "Use the game's native splash screen."),
        (NoUpdate, "Disable Pulsar updates."),
        (PreRelease, "Use pre-release Pulsar updates."),
        (Debug, "Launch the debugger at startup."),
        (F12Menu, "Enable the game's F12 debug menu."),
        (Sources, "Enable custom plugin sources."),
        (Continue, "Continue the last game automatically."),
        (DebugCompileAll, "Compile and check all plugins."),
        (KeepIntro, "Keep the game intro video."),
        (MakeCheck, "Create a library checksum file."),
        (Hardened, "Load only trusted mods."),
        (SafeMode, "Start with user plugins disabled."),
    ];
}

public static class Flags
{
    private const uint AttachParentProcess = unchecked((uint)-1);

    public static SplashType SplashType { get; private set; }
    public static UpdateType UpdateType { get; private set; }
    public static bool ExternalDebug { get; private set; }
    public static bool DebugMenu { get; private set; }
    public static bool CustomSources { get; private set; }
    public static bool ContinueGame { get; private set; }
    public static bool CheckAllPlugins { get; private set; }
    public static bool GameIntroVideo { get; private set; }
    public static bool MakeCheckFile { get; private set; }
    public static bool TrustedMods { get; private set; }
    public static bool SafeMode { get; private set; }

    static Flags()
    {
        if (HasArg(Arguments.NoSplash))
            SplashType = SplashType.None;
        else if (HasArg(Arguments.SeSplash))
            SplashType = SplashType.Native;
        else
            SplashType = SplashType.Pulsar;

        // Linux updater will come in a future pass
        if (HasArg(Arguments.NoUpdate) || !Tools.IsWindows())
            UpdateType = UpdateType.None;
        else if (HasArg(Arguments.PreRelease))
            UpdateType = UpdateType.Tester;
        else
            UpdateType = UpdateType.Standard;

        ExternalDebug = HasArg(Arguments.Debug);
        DebugMenu = HasArg(Arguments.F12Menu);
        CustomSources = HasArg(Arguments.Sources);
        ContinueGame = HasArg(Arguments.Continue);
        CheckAllPlugins = HasArg(Arguments.DebugCompileAll);
        GameIntroVideo = HasArg(Arguments.KeepIntro);
        MakeCheckFile = HasArg(Arguments.MakeCheck);
        TrustedMods = HasArg(Arguments.Hardened);
        SafeMode = HasArg(Arguments.SafeMode);
    }

    public static bool HelpRequested => Arguments.HelpAliases.Any(alias => HasArg([alias]));

    public static void LogHelp()
    {
        if (Tools.IsWindows() && AttachConsole(AttachParentProcess))
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.WriteLine();
        }

        string options = string.Join(
            Environment.NewLine,
            Arguments.Annotated.Select(x => $"  {ToSpaceEngineersArg(x.Words), -18}{x.Description}")
        );

        Console.WriteLine(
            $"""
            Usage: Pulsar [options] [Space Engineers arguments]

            Options:
            {options}

            Options are case-insensitive. Linux form --no-splash and Windows form
            /NoSplash are also accepted. Help aliases include --help, -h, and /?.
            """
        );
    }

    public static void LogFlags()
    {
        List<string> changed = [];

        if (SplashType == SplashType.None)
            changed.Add("NoSplash");
        else if (SplashType == SplashType.Native)
            changed.Add("NativeSplash");

        if (UpdateType == UpdateType.None)
            changed.Add("NoUpdates");
        else if (UpdateType == UpdateType.Tester)
            changed.Add("EarlyUpdates");

        if (ExternalDebug)
            changed.Add("ExternalDebug");
        if (DebugMenu)
            changed.Add("DebugMenu");
        if (CustomSources)
            changed.Add("CustomSources");
        if (ContinueGame)
            changed.Add("ContinueGame");
        if (CheckAllPlugins)
            changed.Add("CheckAllPlugins");
        if (GameIntroVideo)
            changed.Add("GameIntroVideo");
        if (MakeCheckFile)
            changed.Add("MakeCheckFile");
        if (TrustedMods)
            changed.Add("TrustedMods");
        if (SafeMode)
            changed.Add("SafeMode");

        if (changed.Count > 0)
            LogFile.WriteLine($"Enabled flags: {string.Join(" ", changed)}");
    }

    private static bool HasArg(string[] words)
    {
        string linux = ToLinuxArg(words);
        string windows = ToWindowsArg(words);
        string spaceEngineers = ToSpaceEngineersArg(words);

        return Environment
            .GetCommandLineArgs()
            .Any(arg =>
                arg.Equals(linux, StringComparison.OrdinalIgnoreCase)
                || arg.Equals(windows, StringComparison.OrdinalIgnoreCase)
                || arg.Equals(spaceEngineers, StringComparison.OrdinalIgnoreCase)
            );
    }

    private static string ToLinuxArg(string[] words) => $"--{string.Join("-", words)}";

    private static string ToWindowsArg(string[] words) => $"/{ToPascalCase(words)}";

    private static string ToSpaceEngineersArg(string[] words) =>
        $"-{words[0]}{ToPascalCase(words.Skip(1))}";

    private static string ToPascalCase(IEnumerable<string> words) =>
        string.Concat(words.Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1)));

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
}
