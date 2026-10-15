#nullable enable
using System;
using System.Collections.Generic;
using System.IO;

namespace Pulsar.Shared;

// Linux limits that make our programs fail in ways hard to trace back to the cause once
// they run out. They count per user, so many servers or clients under one user share them.
// The checks only warn, a limit that is too low for a big setup may be fine for a small one.
// Identical copies, apart from the namespace, are in Pulsar Shared/LinuxLimits.cs,
// Quasar Shared/Diagnostics/LinuxLimits.cs and cluster ClusterRegistry/LinuxLimits.cs.
// Keep them in step.
public static class LinuxLimits
{
    // .NET raises the soft open file limit to the hard one at startup, so this is the hard
    // limit in effect. The busiest process measured on a loaded cluster had about 400 open.
    public const long MinOpenFiles = 65536;

    // Counts the threads of all processes of the user, a server has 60 to 90.
    public const long MinProcesses = 16384;

    public const long MinInotifyInstances = 1024;
    public const long MinInotifyWatches = 524288;

    // One warning for each limit below the expected minimum, empty if all is fine
    public static List<string> Check() => Check("/proc", Environment.UserName);

    // procRoot is /proc, or a folder with the same layout in tests
    public static List<string> Check(string procRoot, string user)
    {
        var warnings = new List<string>();
        string limits = Read(Path.Combine(procRoot, "self", "limits")) ?? "";

        Warn(
            warnings,
            "Open file limit (ulimit -n, RLIMIT_NOFILE)",
            ParseRlimit(limits, "Max open files"),
            MinOpenFiles,
            "Opening files and network connections fails once it runs out.",
            $"add the line \"{user} - nofile {MinOpenFiles}\" to /etc/security/limits.conf and log in again, "
                + $"or set LimitNOFILE={MinOpenFiles} in the systemd unit that starts this program"
        );

        Warn(
            warnings,
            "Process limit (ulimit -u, RLIMIT_NPROC)",
            ParseRlimit(limits, "Max processes"),
            MinProcesses,
            "It counts every thread of the user, starting threads fails once it runs out.",
            $"add the line \"{user} - nproc {MinProcesses}\" to /etc/security/limits.conf and log in again, "
                + $"or set LimitNPROC={MinProcesses} in the systemd unit that starts this program"
        );

        Warn(
            warnings,
            "inotify instance limit (fs.inotify.max_user_instances)",
            ParseNumber(Read(Path.Combine(procRoot, "sys", "fs", "inotify", "max_user_instances"))),
            MinInotifyInstances,
            "File watchers fail once it runs out, and the programs that hit it need a restart.",
            Sysctl("fs.inotify.max_user_instances", MinInotifyInstances)
        );

        Warn(
            warnings,
            "inotify watch limit (fs.inotify.max_user_watches)",
            ParseNumber(Read(Path.Combine(procRoot, "sys", "fs", "inotify", "max_user_watches"))),
            MinInotifyWatches,
            "Watching more folders fails once it runs out.",
            Sysctl("fs.inotify.max_user_watches", MinInotifyWatches)
        );

        return warnings;
    }

    // Soft limit of one row of /proc/<pid>/limits, null if the row is missing.
    // Row example: "Max open files            1024                 524288               files"
    public static long? ParseRlimit(string limits, string name)
    {
        foreach (string line in limits.Split('\n'))
        {
            if (!line.StartsWith(name + " ", StringComparison.Ordinal))
                continue;

            string[] columns = line.Substring(name.Length)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return columns.Length == 0 ? null
                : columns[0] == "unlimited" ? long.MaxValue
                : ParseNumber(columns[0]);
        }
        return null;
    }

    public static long? ParseNumber(string? text) =>
        long.TryParse(text?.Trim(), out long value) ? value : null;

    private static void Warn(
        List<string> warnings,
        string limit,
        long? value,
        long expected,
        string effect,
        string fix
    )
    {
        if (value < expected)
            warnings.Add(
                $"{limit} is {value}, expected at least {expected}. {effect} To raise it, {fix}."
            );
    }

    private static string Sysctl(string key, long value) =>
        $"run: echo {key}={value} | sudo tee -a /etc/sysctl.d/90-inotify.conf && sudo sysctl -p /etc/sysctl.d/90-inotify.conf";

    // Missing or unreadable files skip the check, some containers hide parts of /proc
    private static string? Read(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
