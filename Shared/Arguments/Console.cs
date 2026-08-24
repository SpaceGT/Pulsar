using System.IO;
using System.Runtime.InteropServices;
using SysConsole = System.Console;

namespace Pulsar.Shared.Arguments;

internal static class Console
{
    private const uint AttachParentProcess = unchecked((uint)-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    public static void Write(string text)
    {
        if (Tools.IsWindows() && AttachConsole(AttachParentProcess))
        {
            StreamWriter writer = new(SysConsole.OpenStandardOutput()) { AutoFlush = true };
            SysConsole.SetOut(writer);
            SysConsole.WriteLine();
        }

        SysConsole.Write(text);
    }
}
