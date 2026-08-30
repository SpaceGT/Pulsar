using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Newtonsoft.Json;
using Pulsar.Compiler;
using Pulsar.Interface;
using Pulsar.Protocol.Interface;
using Pulsar.Shared.Arguments;
#if NETCOREAPP
using System.Runtime.Versioning;
#endif

namespace Pulsar.Shared;

public interface IExternalTools
{
    void OnMainThread(Action action);
}

public static class Tools
{
    public static IExternalTools External { get; private set; }
    public static ICompilerFactory Compiler { get; private set; }
    public static InterfaceClient Interface { get; private set; }

    public static void EarlyInit(InterfaceClient interfaceClient)
    {
        Interface = interfaceClient;
    }

    public static void Init(IExternalTools external, ICompilerFactory compiler)
    {
        External = external;
        Compiler = compiler;
    }

    public static bool IsManagedDll(string file)
    {
        bool isDll = Path.GetExtension(file).Equals(".dll", StringComparison.OrdinalIgnoreCase);
        if (!File.Exists(file) || !isDll)
            return false;

        try
        {
            using var _ = AssemblyDefinition.ReadAssembly(file);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    public static string GetFileHash(string file)
    {
        using var sha = SHA256.Create();
        using FileStream fileStream = new(file, FileMode.Open, FileAccess.Read);
        return GetHash(fileStream, sha);
    }

    public static string GetStringHash(string text)
    {
        using var sha = SHA256.Create();
        using MemoryStream memory = new(Encoding.UTF8.GetBytes(text));
        return GetHash(memory, sha);
    }

    public static string GetHash(Stream input, HashAlgorithm hash)
    {
        byte[] data = hash.ComputeHash(input);
        StringBuilder sb = new(2 * data.Length);
        foreach (byte b in data)
            sb.AppendFormat("{0:x2}", b);
        return sb.ToString();
    }

    public static string GetFolderHash(string folderPath, string glob = "*")
    {
        if (!Directory.Exists(folderPath))
            throw new DirectoryNotFoundException($"Cannot hash non-existent folder: {folderPath}");

        IEnumerable<string> files = Directory
            .GetFiles(folderPath, glob, SearchOption.AllDirectories)
            .OrderBy(Path.GetFileName);

        StringBuilder hashBuilder = new();
        foreach (string path in files)
            hashBuilder.Append(GetFileHash(path));

        return GetStringHash(hashBuilder.ToString());
    }

    public static string GetClipboard()
    {
        try
        {
            return Interface.GetClipboard();
        }
        catch (Exception e)
        {
            LogFile.Error("Error while reading clipboard: " + e);
            return string.Empty;
        }
    }

    public static string DateToString(DateTime? lastCheck)
    {
        if (lastCheck is null)
            return "Never";

        TimeSpan time = DateTime.UtcNow - lastCheck.Value;

        if (time.TotalMinutes < 5)
            return "Just Now";

        if (time.TotalHours < 1)
            return $"{time.Minutes} minutes ago";

        if (time.Hours == 1)
            return $"{time.Hours} hour ago";

        if (time.TotalDays < 1)
            return $"{time.Hours} hours ago";

        if (time.Days == 1)
            return $"{time.Days} day ago";

        return $"{time.Days} days ago";
    }

    public static void OpenFileDialog(
        string title,
        string directory,
        FilePickerFilter[] filters,
        Action<string> onOk
    )
    {
        Task.Run(() =>
        {
            try
            {
                string file = Interface.OpenFile(title, directory, filters);
                if (!string.IsNullOrWhiteSpace(file))
                    External.OnMainThread(() => onOk(file));
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening file dialog: " + e);
            }
        });
    }

    public static void OpenFolderDialog(string title, Action<string> onOk)
    {
        Task.Run(() =>
        {
            try
            {
                string folder = Interface.OpenFolder(title);
                if (!string.IsNullOrWhiteSpace(folder))
                    External.OnMainThread(() => onOk(folder));
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening folder dialog: " + e);
            }
        });
    }

    /// <param name="unattendedResult">
    /// The result to report when the prompt cannot be shown (suppressed by
    /// -noPrompt, or the interface process is unavailable). Defaults to Ok
    /// for Ok-only prompts and Cancel otherwise. The message is logged so
    /// unattended machines keep a trace of what was auto-answered.
    /// </param>
    public static PromptResult ShowMessageBox(
        string message,
        PromptButtons buttons,
        PromptIcon icon,
        PromptResult? unattendedResult = null
    )
    {
        PromptResult fallback =
            unattendedResult
            ?? (buttons == PromptButtons.Ok ? PromptResult.Ok : PromptResult.Cancel);

        if (Flags.Current?.NoPrompt ?? false)
        {
            LogPrompt(message, icon, fallback);
            return fallback;
        }

        try
        {
            return Interface.ShowPrompt(message, buttons, icon);
        }
        catch (Exception e)
        {
            LogFile.Error("Error while opening message box: " + e);
            LogPrompt(message, icon, fallback);
            return fallback;
        }
    }

    private static void LogPrompt(string message, PromptIcon icon, PromptResult result)
    {
        string line = $"Prompt auto-answered '{result}': {message}";

        if (icon == PromptIcon.Error)
            LogFile.Error(line);
        else if (icon == PromptIcon.Warning)
            LogFile.Warn(line);
        else
            LogFile.WriteLine(line);
    }

    public static IEnumerable<string> GetFiles(
        string path,
        string[] includeGlobs,
        string[] excludeGlobs
    )
    {
        IEnumerable<string> included = includeGlobs.SelectMany(pattern =>
            Directory.EnumerateFiles(path, pattern)
        );

        IEnumerable<string> excluded = excludeGlobs.SelectMany(pattern =>
            Directory.EnumerateFiles(path, pattern)
        );

        return included
            .Except(excluded, StringComparer.OrdinalIgnoreCase)
            .Select(Path.GetFileNameWithoutExtension);
    }

    public static string CleanFileName(string name)
    {
        HashSet<char> invalid = [.. Path.GetInvalidFileNameChars()];
        StringBuilder newName = new();

        foreach (char character in name)
        {
            if (invalid.Contains(character))
                newName.Append('-');
            else
                newName.Append(character);
        }

        return newName.ToString();
    }

    public static string GetRelativePath(string folder, string path)
    {
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(path))
            return null;

#if NETFRAMEWORK
        string fullFolder = Path.GetFullPath(folder).TrimEnd('\\', '/');
        string fullPath = Path.GetFullPath(path);
        if (fullPath.Equals(fullFolder, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        fullFolder += '\\';
        if (!fullPath.StartsWith(fullFolder, StringComparison.OrdinalIgnoreCase))
            return null;

        return fullPath.Substring(fullFolder.Length);
#else
        string relativePath = Path.GetRelativePath(folder, path);
        if (relativePath == ".")
            return string.Empty;

        string parent = ".." + Path.DirectorySeparatorChar;
        if (
            Path.IsPathRooted(relativePath)
            || relativePath == ".."
            || relativePath.StartsWith(parent)
        )
            return null;

        return relativePath;
#endif
    }

    public static bool PathsEqual(string first, string second)
    {
        StringComparer comparer = IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        return comparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
    }

    public static void ShowInFileManager(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) && !Directory.Exists(path))
            return;

        if (IsWindows())
        {
            string arguments = File.Exists(path) ? $"/select, \"{path}\"" : $"\"{path}\"";
            Process.Start(
                new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = false }
            );
        }
        else
        {
            if (File.Exists(path))
                path = Path.GetDirectoryName(path);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }

    public static T DeepCopy<T>(T obj)
    {
        string json = JsonConvert.SerializeObject(obj);
        return JsonConvert.DeserializeObject<T>(json);
    }

    public static void Shuffle<T>(IList<T> items)
    {
        T[] shuffled = [.. items.OrderBy(_ => Guid.NewGuid())];
        for (int i = 0; i < items.Count; i++)
            items[i] = shuffled[i];
    }

    public static string RemoveAll(string text, IEnumerable<string> tokens)
    {
        foreach (string t in tokens)
            text = text.Replace(t, "");
        return text;
    }

    public static List<string> GetRestartArgs(string executable)
    {
        List<string> args = [.. Environment.GetCommandLineArgs()];
        string originalName = Path.GetFileNameWithoutExtension(args[0]);
        string executableName = Path.GetFileNameWithoutExtension(executable);

        // First "argument" is the invoked executable
        // Preserve if invoked via `dotnet` or drop if invoking directly.
        if (originalName.Equals(executableName, StringComparison.OrdinalIgnoreCase))
            args.RemoveAt(0);

        return args;
    }

#if NETCOREAPP
    [SupportedOSPlatformGuard("windows")]
#endif
    public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsMono() => Type.GetType("Mono.Runtime") is not null;

    public static string RuntimeIdentifier =>
        (IsWindows() ? "win-" : "linux-")
        + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    public static string Platform =>
        IsProton() ? "Proton"
        : IsWindows() ? "Windows"
        : "Linux";

    public static string Runtime => IsMono() ? "Mono"
#if NETFRAMEWORK
            : "CLR";
#else
            : "CoreCLR";
#endif

    public static IEnumerable<string> GetCompilationSymbols(bool trusted)
    {
#if NETCOREAPP
        yield return "NETCOREAPP";
#endif

        if (!IsWindows())
            yield return "LINUX";

        if (!trusted)
            yield break;

#if NETFRAMEWORK
        yield return "NETFRAMEWORK";
#endif

        yield return "PULSAR";
    }

    public static bool IsSupportedEnvironment(string runtimes, string platforms) =>
        IsSupportedValue(runtimes, Runtime) && IsSupportedValue(platforms, Platform);

    private static bool IsSupportedValue(string supportedValues, string value) =>
        string.IsNullOrWhiteSpace(supportedValues)
        || supportedValues
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Any(x => x.Trim().Equals(value, StringComparison.OrdinalIgnoreCase));

    public static bool IsProton() =>
        IsWindows() && Environment.GetEnvironmentVariable("STEAM_COMPAT_PROTON") is not null;

    public static string ExecutableExtension => IsWindows() ? ".exe" : ".bin";
}
