// The harness compiles the production preloader without the game or Steam dependencies.
namespace Pulsar.Shared.Arguments
{
    internal sealed class Flags
    {
        public static Flags Current { get; } = new();
        public bool LazyPreload { get; set; }
    }
}

namespace Pulsar.Protocol.Interface
{
    internal enum PromptButtons { Ok }
    internal enum PromptIcon { Error }
}

namespace Pulsar.Shared
{
    internal static class LogFile
    {
        public static void Error(string text) => Console.Error.WriteLine(text);
        public static void Warn(string text) => Console.Error.WriteLine(text);
    }

    internal static class Tools
    {
        public static void ShowMessageBox(string text, Protocol.Interface.PromptButtons buttons,
            Protocol.Interface.PromptIcon icon) { }
    }
}
