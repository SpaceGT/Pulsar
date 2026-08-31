using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using McMaster.Extensions.CommandLineUtils;

namespace Pulsar.Shared.Arguments;

public static class Parser
{
    private static CommandLineApplication<Flags> application;

    public static void Initialize(string[] args, bool se1)
    {
        StringWriter output = new();
        application = new() { HelpTextGenerator = new Help(), Out = output };
        application.Conventions.UseDefaultConventions();
        SelectHelpOptions(se1);
        Flags.Current = application.Model;

        try
        {
            application.Parse([.. args.Select(Normalize)]);
        }
        catch (CommandParsingException error)
        {
            output.WriteLine(error.Message);
        }

        string text = output.ToString();
        if (text.Length > 0)
        {
            Console.Write(text);
            Environment.Exit(0);
        }
    }

    public static void LogChanged()
    {
        List<string> changed = [];
        Flags defaults = new();

        foreach (PropertyInfo property in typeof(Flags).GetProperties())
        {
            if (!property.IsDefined(typeof(OptionAttribute)))
                continue;

            if (Equals(property.GetValue(Flags.Current), property.GetValue(defaults)))
                continue;

            changed.Add(property.Name);
        }

        if (changed.Count > 0)
            LogFile.WriteLine($"Enabled flags: {string.Join(" ", changed)}");
    }

    private static void SelectHelpOptions(bool se1)
    {
        // Simplified implementation due to a single consumer
        foreach (CommandOption option in application.Options)
            option.ShowInHelpText &= option.ShortName != (se1 ? "game2" : "bin64");
    }

    private static string Normalize(string arg)
    {
        // Only normalize option-style tokens
        if (string.IsNullOrEmpty(arg) || (arg[0] != '-' && arg[0] != '/'))
            return arg;

        string trimmed = arg.Replace("/", "").Replace("-", "");

        if (trimmed is "h" or "H" or "?")
            return $"-{application.OptionHelp.ShortName}";

        if (trimmed is "v" or "V")
            return $"-{application.OptionVersion.ShortName}";

        foreach (CommandOption option in application.Options)
            if (trimmed.Equals(option.ShortName, StringComparison.OrdinalIgnoreCase))
                return $"-{option.ShortName}";

        return arg;
    }
}
