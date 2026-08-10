using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pulsar.Compiler;
using Pulsar.Interface;
using Pulsar.Protocol.Interface;
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

    public static void OpenFolderDialog(Action<string> onOk)
    {
        Task.Run(() =>
        {
            try
            {
                string folder = Interface.OpenFolder();
                if (!string.IsNullOrWhiteSpace(folder))
                    External.OnMainThread(() => onOk(folder));
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening folder dialog: " + e);
            }
        });
    }

    public static PromptResult ShowMessageBox(
        string message,
        PromptButtons buttons = PromptButtons.Ok,
        PromptIcon icon = PromptIcon.None
    )
    {
        try
        {
            return Interface.ShowPrompt(message, buttons, icon);
        }
        catch (Exception e)
        {
            LogFile.Error("Error while opening message box: " + e);
            return buttons == PromptButtons.Ok ? PromptResult.Ok : PromptResult.Cancel;
        }
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

    public static string RemoveAll(string text, IEnumerable<string> tokens)
    {
        foreach (string t in tokens)
            text = text.Replace(t, "");
        return text;
    }

#if NETCOREAPP
    [SupportedOSPlatformGuard("windows")]
#endif
    public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public static bool IsSupportedEnvironment(string runtimes, string platforms)
    {
        string runtime = Type.GetType("Mono.Runtime") is not null ? "Mono"
#if NETFRAMEWORK
            : "CLR";
#else
            : "CoreCLR";
#endif
        string platform =
            IsProton() ? "Proton"
            : IsWindows() ? "Windows"
            : "Linux";

        return IsSupportedValue(runtimes, runtime) && IsSupportedValue(platforms, platform);
    }

    public static bool IsSupportedValue(string supportedValues, string value) =>
        string.IsNullOrWhiteSpace(supportedValues)
        || supportedValues
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries)
            .Any(x => x.Trim().Equals(value, StringComparison.OrdinalIgnoreCase));

    public static bool IsProton() =>
        IsWindows() && Environment.GetEnvironmentVariable("STEAM_COMPAT_PROTON") is not null;

    public static string ExecutableExtension => IsWindows() ? ".exe" : ".bin";
}
