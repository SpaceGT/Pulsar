using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Pulsar.Compiler;

namespace Pulsar.Shared;

public interface IExternalTools
{
    void OnMainThread(Action action);
}

public static class Tools
{
    public const string XmlDataType = "Xml files (*.xml)|*.xml|All files (*.*)|*.*";
    public static IExternalTools External { get; private set; }
    public static ICompilerFactory Compiler { get; private set; }

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
        string cliptext = string.Empty;
        return cliptext;
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
        string filter,
        Action<string> onOk
    )
    {
        _ = Task.Run(async () =>
        {
            try
            {
                string path = await LinuxFileDialog.OpenFileAsync(title, directory, filter);
                if (!string.IsNullOrEmpty(path))
                    External.OnMainThread(() => onOk(path));
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening file dialog: " + e);
            }
        });
    }

    public static void OpenFolderDialog(Action<string> onOk)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                string path = await LinuxFileDialog.SelectFolderAsync("Select a folder", home);
                if (!string.IsNullOrEmpty(path))
                    External.OnMainThread(() => onOk(path));
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening folder dialog: " + e);
            }
        });
    }

    public static DialogResult ShowMessageBox(
        string msg,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1
    )
    {
        Console.WriteLine("Showing message box: " + msg);
        return DialogResult.No;
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

    public static bool IsNative() =>
        Environment.GetEnvironmentVariable("STEAM_COMPAT_PROTON") is null;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}

public enum MessageBoxDefaultButton
{
    Button1=1,
}

public enum MessageBoxIcon
{
    None=0,
    Error=1,
    Warning=2,
}

public enum MessageBoxButtons
{
    OK=0,
    YesNo=1,
}

public enum DialogResult
{
    No=0,
    Yes=1,
}
