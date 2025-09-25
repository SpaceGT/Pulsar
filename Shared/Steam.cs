using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Win32;
using Steamworks;

namespace Pulsar.Shared;

public static class Steam
{
    public const uint AppId = 244850u;
    private const int SteamTimeout = 30; // seconds
    private const string registryKey = @"SOFTWARE\Valve\Steam";
    private const string registryName = "SteamPath";
    private const string Steamworks = "Steamworks.NET";

    public static void SubscribeToItem(ulong id) =>
        SteamUGC.SubscribeItem(new PublishedFileId_t(id));

    public static ulong GetSteamId() => SteamUser.GetSteamID().m_SteamID;

    public static void Init()
    {
        Environment.SetEnvironmentVariable("SteamAppId", AppId.ToString());

        if (SteamAPI.IsSteamRunning())
        {
            SteamAPI.Init();
            return;
        }

        string path = GetSteamPath();

        try
        {
            if (path is not null)
                Process.Start(Path.Combine(path, "steam.exe"), "-silent");
            else
                Process.Start(new ProcessStartInfo("steam://open/main") { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            ShowWarning();
            Environment.Exit(1);
        }

        for (int i = 0; i < SteamTimeout; i++)
        {
            Thread.Sleep(1000);

            if (SteamAPI.Init())
                return;
        }

        ShowWarning();
        Environment.Exit(1);
    }

    public static ResolveEventHandler SteamworksResolver(string baseDir)
    {
        return (sender, args) =>
        {
            string targetName = new AssemblyName(args.Name).Name;
            if (targetName != Steamworks)
                return null;

            string targetPath = Path.Combine(baseDir, $"{Steamworks}.dll");
            if (File.Exists(targetPath))
                return Assembly.LoadFrom(targetPath);

            return null;
        };
    }

    public static string GetSteamPath()
    {
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
        LogFile.WriteLine("Steam failed to start!");
        Tools.ShowMessageBox(
            "Failed to start Steam automatically!\n"
                + "Space Engineers requires a running Steam instance."
        );
    }
}
