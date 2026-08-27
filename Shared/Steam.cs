using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
using Pulsar.Protocol.Interface;
using Pulsar.Shared.Arguments;
using Steamworks;

namespace Pulsar.Shared;

public static class Steam
{
    public const uint AppIdSe1 = 244850u;
    public const uint AppIdSe2 = 1133870u;
    private const int SteamTimeout = 60; // seconds
    private const string registryKey = @"SOFTWARE\Valve\Steam";
    private const string registryName = "SteamPath";

    public static bool IsInitialized { get; private set; }

    [DllImport("libc", EntryPoint = "setenv")]
    private static extern int SetEnvLinux(string name, string value, int overwrite);

    public static void SubscribeToItem(ulong id) =>
        SteamUGC.SubscribeItem(new PublishedFileId_t(id));

    public static bool IsSubscribed(ulong id)
    {
        uint state = SteamUGC.GetItemState(new PublishedFileId_t(id));
        return (state & (uint)EItemState.k_EItemStateSubscribed) != 0;
    }

    public static ulong GetSteamId() => SteamUser.GetSteamID().m_SteamID;

    public static void Init(uint AppId)
    {
        string appId = AppId.ToString();
        Environment.SetEnvironmentVariable("SteamAppId", appId);
        if (!Tools.IsWindows()) // Unmanaged Linux assemblies bypass .NET env cache
            SetEnvLinux("SteamAppId", appId, 1);

        if (Flags.Current.LazySteam)
        {
            IsInitialized = SteamAPI.Init();

            if (!IsInitialized)
                LogFile.Warn("Steam is missing or unavailable!");

            return;
        }

        if (!SteamAPI.IsSteamRunning())
        {
            ProcessStartInfo startInfo;
            if (!Tools.IsWindows())
                startInfo = new ProcessStartInfo(
                    "/bin/sh",
                    """
                    -c "exec env -u SteamAppId setsid -f steam -silent </dev/null >/dev/null 2>&1"
                    """
                );
            else if (GetSteamPath() is string path)
                startInfo = new ProcessStartInfo(Path.Combine(path, "steam.exe"), "-silent");
            else
                startInfo = new ProcessStartInfo("steam://open/main") { UseShellExecute = true };

            try
            {
                Process.Start(startInfo)?.Dispose();
            }
            catch (Win32Exception) // This is cross-platform despite the misleading name
            {
                ShowWarning();
                Environment.Exit(1);
            }
        }

        for (int i = 0; i < SteamTimeout; i++)
        {
            if (SteamAPI.IsSteamRunning() && SteamAPI.Init())
            {
                IsInitialized = true;
                return;
            }

            Thread.Sleep(1000);
        }

        ShowWarning();
        Environment.Exit(1);
    }

    public static string GetSteamPath()
    {
        if (!Tools.IsWindows())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] paths =
            [
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".steam", "root"),
                Path.Combine(home, ".local", "share", "Steam"),
            ];

            foreach (string steamPath in paths)
                if (Directory.Exists(steamPath))
                    return steamPath;

            return null;
        }

        using var baseKey = RegistryKey.OpenBaseKey(
            RegistryHive.CurrentUser,
            RegistryView.Registry64
        );

        using var key = baseKey.OpenSubKey(registryKey);
        if (key is null)
            return null;

        var path = key.GetValue(registryName) as string;
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return path;
    }

    private static void ShowWarning()
    {
        LogFile.Error("Steam failed to start!");
        string message =
            "Failed to start Steam automatically!\n"
            + "Space Engineers requires a running Steam instance.";
        Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
    }
}
