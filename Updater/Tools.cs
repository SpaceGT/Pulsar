using System;

namespace Pulsar.Updater;

internal static class Tools
{
    public static string? GetCommandArg(string argument)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!args[i].Equals(argument, StringComparison.InvariantCultureIgnoreCase))
                continue;

            return args[i + 1];
        }

        return null;
    }

    public static bool HasCommandArg(string argument)
    {
        foreach (string arg in Environment.GetCommandLineArgs())
            if (arg.Equals(argument, StringComparison.InvariantCultureIgnoreCase))
                return true;

        return false;
    }
}
