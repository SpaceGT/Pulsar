using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Pulsar.Compiler;

namespace Pulsar.Shared
{
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
            using SHA256CryptoServiceProvider sha = new();
            using FileStream fileStream = new(file, FileMode.Open);
            using BufferedStream bufferedStream = new(fileStream);
            return GetHash(bufferedStream, sha);
        }

        public static string GetStringHash(string text)
        {
            using SHA256CryptoServiceProvider sha = new();
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

        public static string FormatDateIso8601(DateTime dt) => dt.ToString("s").Substring(0, 10);

        // FIXME: Replace this with the proper library call, I could not find one
        public static string FormatUriQueryString(Dictionary<string, string> parameters)
        {
            var query = new StringBuilder();
            foreach (var p in parameters)
            {
                if (query.Length > 0)
                    query.Append('&');
                query.Append($"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}");
            }
            return query.ToString();
        }

        public static string GetFolderHash(string folderPath, string glob = "*")
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");

            List<string> files =
            [
                .. Directory
                    .GetFiles(folderPath, glob, SearchOption.AllDirectories)
                    .OrderBy(Path.GetFileName),
            ];
            StringBuilder timestamps = new("");

            foreach (string path in files)
            {
                DateTime accessTimeUtc = File.GetLastWriteTimeUtc(path);
                timestamps.Append(accessTimeUtc.Ticks.ToString());
            }

            using var md5 = MD5.Create();
            byte[] inputBytes = new UTF8Encoding().GetBytes(timestamps.ToString());
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            string hash = BitConverter.ToString(hashBytes);
            return hash.Replace("-", "").ToLowerInvariant();
        }

        public static string GetClipboard()
        {
            string cliptext = string.Empty;

            Thread thread = new(new ThreadStart(() => cliptext = Clipboard.GetText()));
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

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

        public static bool HasCommandArg(string argument)
        {
            foreach (string arg in Environment.GetCommandLineArgs())
                if (arg.Equals(argument, StringComparison.InvariantCultureIgnoreCase))
                    return true;

            return false;
        }

        public static string GetCommandArg(string argument)
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

        public static void OpenFileDialog(
            string title,
            string directory,
            string filter,
            Action<string> onOk
        )
        {
            Thread t = new(
                new ThreadStart(() => OpenFileDialogThread(title, directory, filter, onOk))
            );
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private static void OpenFileDialogThread(
            string title,
            string directory,
            string filter,
            Action<string> onOk
        )
        {
            try
            {
                // Get the file path via prompt
                using OpenFileDialog openFileDialog = new();
                if (Directory.Exists(directory))
                    openFileDialog.InitialDirectory = directory;
                openFileDialog.Title = title;
                openFileDialog.Filter = filter;
                openFileDialog.RestoreDirectory = true;

                Form form = new() { TopMost = true, TopLevel = true };

                DialogResult dialogResult = openFileDialog.ShowDialog(form);
                string fileName = openFileDialog.FileName;

                form.Close();

                if (dialogResult == DialogResult.OK && !string.IsNullOrWhiteSpace(fileName))
                {
                    // Move back to the main thread so that we can interact with keen code again
                    External.OnMainThread(() => onOk(fileName));
                }
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening file dialog: " + e);
            }
        }

        public static void OpenFolderDialog(string title, Action<string> onOk)
        {
            Thread t = new(new ThreadStart(() => OpenFolderDialogThread(title, onOk)));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        private static void OpenFolderDialogThread(string title, Action<string> onOk)
        {
            try
            {
                // Get the file path via prompt
                using FolderBrowserDialog openFileDialog = new();
                openFileDialog.Description = title;

                Form form = new() { TopMost = true, TopLevel = true };

                DialogResult dialogResult = openFileDialog.ShowDialog(form);
                string selectedPath = openFileDialog.SelectedPath;

                form.Close();

                if (dialogResult == DialogResult.OK && !string.IsNullOrWhiteSpace(selectedPath))
                {
                    // Move back to the main thread so that we can interact with keen code again
                    External.OnMainThread(() => onOk(selectedPath));
                }
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening file dialog: " + e);
            }
        }

        public static DialogResult ShowMessageBox(
            string msg,
            MessageBoxButtons buttons = MessageBoxButtons.OK,
            MessageBoxIcon icon = MessageBoxIcon.None,
            MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1
        )
        {
            if (Application.OpenForms.Count > 0)
            {
                Form form = Application.OpenForms[0];
                if (form.InvokeRequired)
                {
                    // Form is on a different thread
                    try
                    {
                        object result = form.Invoke(() =>
                            MessageBox.Show(form, msg, "Pulsar", buttons, icon, defaultButton)
                        );
                        if (result is DialogResult dialogResult)
                            return dialogResult;
                    }
                    catch (Exception) { }
                }
                else
                {
                    // Form is on the same thread
                    return MessageBox.Show(form, msg, "Pulsar", buttons, icon, defaultButton);
                }
            }

            // No form
            return MessageBox.Show(
                msg,
                "Pulsar",
                buttons,
                icon,
                defaultButton,
                MessageBoxOptions.DefaultDesktopOnly
            );
        }

        public static bool FilesEqual(string file1, string file2)
        {
            FileInfo fileInfo1 = new(file1);
            FileInfo fileInfo2 = new(file2);
            return fileInfo1.Length == fileInfo2.Length && GetFileHash(file1) == GetFileHash(file2);
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

        public static string FriendlyPlatformName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return GetLinuxDistroName() ?? "Linux";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "MacOS";

            return null;
        }

        private static string GetLinuxDistroName()
        {
            const string path = @"Z:\etc\os-release";

            if (!File.Exists(path))
                return null;

            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("NAME="))
                {
                    var value = line.Split('=')[1].Trim('"');
                    return value;
                }
            }

            return null;
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

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public static bool EscapePressed() => (GetAsyncKeyState(0x1B) & 0x8000) != 0;
    }
}
