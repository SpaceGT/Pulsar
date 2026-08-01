using System;
using System.Collections.Generic;
using System.Linq;

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

public static class Flags
{
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

    static Flags()
    {
        if (HasArg("no", "splash"))
            SplashType = SplashType.None;
        else if (HasArg("se", "splash"))
            SplashType = SplashType.Native;
        else
            SplashType = SplashType.Pulsar;

        // Linux updater will come in a future pass
        if (HasArg("no", "update") || !Tools.IsWindows())
            UpdateType = UpdateType.None;
        else if (HasArg("pre", "release"))
            UpdateType = UpdateType.Tester;
        else
            UpdateType = UpdateType.Standard;

        ExternalDebug = HasArg("debug");
        DebugMenu = HasArg("f12", "menu");
        CustomSources = HasArg("sources");
        ContinueGame = HasArg("continue");
        CheckAllPlugins = HasArg("debug", "compile", "all");
        GameIntroVideo = HasArg("keep", "intro");
        MakeCheckFile = HasArg("mk", "check");
        TrustedMods = HasArg("hardened");
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

        if (changed.Count > 0)
            LogFile.WriteLine($"Enabled flags: {string.Join(" ", changed)}");
    }

    private static bool HasArg(params string[] words)
    {
        string joined = string.Join("", words);
        string unix = $"--{string.Join("-", words)}";
        string windows = $"/{joined}";
        string spaceEngineers = $"-{joined}";

        return Environment.GetCommandLineArgs().Any(arg =>
            arg.Equals(unix, StringComparison.OrdinalIgnoreCase)
            || arg.Equals(windows, StringComparison.OrdinalIgnoreCase)
            || arg.Equals(spaceEngineers, StringComparison.OrdinalIgnoreCase)
        );
    }
}
