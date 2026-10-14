using System;
using System.Collections.Generic;
using Microsoft.Win32;
#if NETCOREAPP
using System.Runtime.Versioning;
#endif

namespace Pulsar.Shared;

// Startup check of system limits which make the game, server or plugins fail in
// ways that are hard to trace back to the cause when they are set too low.
// Quasar has a copy of this file in Shared/Diagnostics/SystemLimits.cs, keep them in step.
public static class SystemLimits
{
    // One warning for each limit below what we expect, empty if all is fine
    public static IEnumerable<string> Check()
    {
        if (Tools.IsWindows() && !Tools.IsProton())
        {
            string warning = CheckLongPaths();
            if (warning is not null)
                yield return warning;
        }

        // Linux limits (open files, inotify, processes) will go here
    }

    // .NET Framework builds are longPathAware, which works only if long paths are enabled.
    // The .NET builds handle long paths in managed code, but native code they load does not.
#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    private static string CheckLongPaths()
    {
        object value;
        try
        {
            using RegistryKey key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\FileSystem"
            );
            value = key?.GetValue("LongPathsEnabled");
        }
        catch (Exception e)
        {
            return $"Could not read the Windows long path setting: {e.Message}";
        }

        if (value is int enabled && enabled != 0)
            return null;

        return $"Windows long path support is off (LongPathsEnabled is {value ?? "not set"}, expected 1). "
            + "Paths of 260 or more characters will fail, deep save and plugin folders can reach that. "
            + "To turn it on, enable \"Enable Win32 long paths\" in the Local Group Policy Editor "
            + "or run this as administrator, then restart: "
            + @"reg add HKLM\SYSTEM\CurrentControlSet\Control\FileSystem /v LongPathsEnabled /t REG_DWORD /d 1 /f";
    }
}
