using System.Collections.Generic;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
using McMaster.Extensions.CommandLineUtils.HelpText;

namespace Pulsar.Shared.Arguments;

internal sealed class Help : IHelpTextGenerator
{
    public void Generate(CommandLineApplication application, TextWriter output)
    {
        output.WriteLine(
            $"""
            Usage: Pulsar [options] [Space Engineers arguments]

            Flags:
            {ListOptions(application.Options, false)}

            Overrides:
            {ListOptions(application.Options, true)}

            Options are case-insensitive. Linux form --no-splash and Windows form
            /NoSplash are also accepted. Help aliases include --help, -h, and /?.
            """
        );
    }

    private static string ListOptions(IEnumerable<CommandOption> options, bool showValue)
    {
        List<string> lines = [];
        foreach (CommandOption option in options)
        {
            bool hasValue = option.OptionType != CommandOptionType.NoValue;
            if (option.ShowInHelpText && hasValue == showValue)
                lines.Add(Format(option));
        }

        return string.Join("\n", lines);
    }

    private static string Format(CommandOption option)
    {
        string name = $"-{option.ShortName}";
        if (option.OptionType != CommandOptionType.NoValue)
            name += $" <{option.ValueName}>";
        return $"  {name,-18}{option.Description}";
    }
}
