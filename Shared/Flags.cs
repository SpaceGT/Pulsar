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

    static Flags()
    {
        if (HasArg("nosplash"))
            SplashType = SplashType.None;
        else if (HasArg("sesplash"))
            SplashType = SplashType.Native;
        else
            SplashType = SplashType.Pulsar;

        if (HasArg("noupdate"))
            UpdateType = UpdateType.None;
        else if (HasArg("prerelease"))
            UpdateType = UpdateType.Tester;
        else
            UpdateType = UpdateType.Standard;

        ExternalDebug = HasArg("debug");
        DebugMenu = HasArg("f12menu");
        CustomSources = HasArg("sources");
        ContinueGame = HasArg("continue");
        CheckAllPlugins = HasArg("debugCompileAll");
        GameIntroVideo = HasArg("keepintro");
        MakeCheckFile = HasArg("mkcheck");
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

        if (changed.Count > 0)
            LogFile.WriteLine($"Enabled flags: {string.Join(" ", changed)}");
    }

    private static bool HasArg(string argument) =>
        Environment
            .GetCommandLineArgs()
            .Any(arg => arg.Equals($"-{argument}", StringComparison.OrdinalIgnoreCase));
}
