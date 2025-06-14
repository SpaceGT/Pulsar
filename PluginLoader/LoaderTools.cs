using HarmonyLib;
using Sandbox;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRage.FileSystem;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using VRage;
using VRage.Audio;
using Sandbox.Game.Gui;
using System.Collections.Generic;

namespace avaness.PluginLoader
{
    public static class LoaderTools
    {
        public static string PluginsDir => Path.GetFullPath(Path.Combine(MyFileSystem.ExePath, "Plugins"));

        public const string ContinueArg = "-continue";

        public static DialogResult ShowMessageBox(string msg, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        {
            if (Application.OpenForms.Count > 0)
            {
                Form form = Application.OpenForms[0];
                if (form.InvokeRequired)
                {
                    // Form is on a different thread
                    try
                    {
                        object result = form.Invoke(() => MessageBox.Show(form, msg, "Plugin Loader", buttons, icon, defaultButton));
                        if (result is DialogResult dialogResult)
                            return dialogResult;
                    }
                    catch (Exception) { }
                }
                else
                {
                    // Form is on the same thread
                    return MessageBox.Show(form, msg, "Plugin Loader", buttons, icon, defaultButton);
                }
            }

            // No form
            return MessageBox.Show(msg, "Plugin Loader", buttons, icon, defaultButton, System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly);
        }

        public static void AskToRestart()
        {
            if (MyGuiScreenGamePlay.Static != null)
                AskSave(delegate {
                    Unload();
                    Restart();
                });
            else
            {
                Unload();
                Restart();
            }
        }

        /// <summary>
        /// From WesternGamer/InGameWorldLoading
        /// </summary>
        /// <param name="afterMenu">Action after code is executed.</param>
        private static void AskSave(Action afterMenu)
        {
            // Sync.IsServer is backwards
            if (!Sync.IsServer)
            {
                afterMenu();
                return;
            }

            string message = "";
            bool isCampaign = false;
            MyMessageBoxButtonsType buttonsType = MyMessageBoxButtonsType.YES_NO_CANCEL;

            // Sync.IsServer is backwards
            if (Sync.IsServer && !MySession.Static.Settings.EnableSaving)
            {
                message += "Are you sure that you want to restart the game? All progress from the last checkpoint will be lost.";
                isCampaign = true;
                buttonsType = MyMessageBoxButtonsType.YES_NO;
            }
            else
            {
                message += "Save changes before restarting game?";
            }

            MyGuiScreenMessageBox saveMenu = MyGuiSandbox.CreateMessageBox(buttonType: buttonsType, messageText: new StringBuilder(message), messageCaption: MyTexts.Get(MyCommonTexts.MessageBoxCaptionPleaseConfirm), callback: ShowSaveMenuCallback, cancelButtonText: MyStringId.GetOrCompute("Don't Restart"));
            saveMenu.InstantClose = false;
            MyGuiSandbox.AddScreen(saveMenu);

            void ShowSaveMenuCallback(MyGuiScreenMessageBox.ResultEnum callbackReturn)
            {
                if (isCampaign)
                {
                    if (callbackReturn == MyGuiScreenMessageBox.ResultEnum.YES)
                        afterMenu();

                    return;
                }

                switch (callbackReturn)
                {
                    case MyGuiScreenMessageBox.ResultEnum.YES:
                        MyAsyncSaving.Start(delegate { MySandboxGame.Static.OnScreenshotTaken += UnloadAndExitAfterScreenshotWasTaken; });
                        break;

                    case MyGuiScreenMessageBox.ResultEnum.NO:
                        MyAudio.Static.Mute = true;
                        MyAudio.Static.StopMusic();
                        afterMenu();
                        break;
                }
            }

            void UnloadAndExitAfterScreenshotWasTaken(object sender, EventArgs e)
            {
                MySandboxGame.Static.OnScreenshotTaken -= UnloadAndExitAfterScreenshotWasTaken;
                afterMenu();
            }
        }

        public static void Unload()
        {
            LogFile.Dispose();
            MySessionLoader.Unload();
            MySandboxGame.Config.ControllerDefaultOnStart = MyInput.Static.IsJoystickLastUsed;
            MySandboxGame.Config.Save();
            MyScreenManager.CloseAllScreensNowExcept(null);
            MyPlugins.Unload();
        }

        public static void Restart(bool autoRejoin = false)
        {
            Start(autoRejoin);
            Process.GetCurrentProcess().Kill();
        }

        private static void Start(bool autoRejoin)
        {
            // Regular app case 
            StringBuilder sb = new StringBuilder();
            IEnumerable<string> args = Environment.GetCommandLineArgs().Skip(1).Where(x => x != ContinueArg);
            if(autoRejoin)
                args = args.Append(ContinueArg);
            foreach (string arg in args)
            {
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append('"');
                sb.Append(arg);
                sb.Append('"');
            }

            ProcessStartInfo currentStartInfo = Process.GetCurrentProcess().StartInfo;
            currentStartInfo.FileName = Application.ExecutablePath;
            if (sb.Length > 0)
                currentStartInfo.Arguments = sb.ToString();

            Process.Start(currentStartInfo);
            Process.GetCurrentProcess().Kill();
        }

        public static string GetHash256(string file)
        {
            using (SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider())
            {
                using (FileStream fileStream = new FileStream(file, FileMode.Open))
                {
                    using (BufferedStream bufferedStream = new BufferedStream(fileStream))
                    {
                        return GetHash(bufferedStream, sha);
                    }
                }
            }
        }

        public static string GetHashString256(string text)
        {
            using (SHA256CryptoServiceProvider sha = new SHA256CryptoServiceProvider())
            {
                using (MemoryStream memory = new MemoryStream(Encoding.UTF8.GetBytes(text)))
                {
                    return GetHash(memory, sha);
                }
            }
        }

        public static string GetHash(Stream input, HashAlgorithm hash)
        {
            byte[] data = hash.ComputeHash(input);
            StringBuilder sb = new StringBuilder(2 * data.Length);
            foreach (byte b in data)
                sb.AppendFormat("{0:x2}", b);
            return sb.ToString();
        }

        /// <summary>
        /// This method attempts to disable JIT compiling for the assembly.
        /// This method will force any member access exceptions by methods to be thrown now instead of later.
        /// </summary>
        public static void Precompile(Assembly a)
        {
            Type[] types;
            try
            {
                types = a.GetTypes();
            }
            catch(ReflectionTypeLoadException e)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("LoaderExceptions: ");
                foreach (Exception e2 in e.LoaderExceptions)
                    sb.Append(e2).AppendLine();
                LogFile.WriteLine(sb.ToString());
                throw;
            }

            foreach (Type t in types)
            {
                // Static constructors allow for early code execution which can cause issues later in the game
                if (HasStaticConstructor(t))
                    continue;

                foreach (MethodInfo m in t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    if (m.HasAttribute<HarmonyReversePatch>())
                        throw new Exception("Harmony attribute 'HarmonyReversePatch' found on the method '" + m.Name + "' is not compatible with Plugin Loader!");
                    Precompile(m);
                }
            }
        }

        private static void Precompile(MethodInfo m)
        {
            if (!m.IsAbstract && !m.ContainsGenericParameters)
                RuntimeHelpers.PrepareMethod(m.MethodHandle);
        }

        private static bool HasStaticConstructor(Type t)
        {
            return t.GetConstructors(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Instance).Any(c => c.IsStatic);
        }


        public static void OpenFileDialog(string title, string directory, string filter, Action<string> onOk)
        {
            Thread t = new Thread(new ThreadStart(() => OpenFileDialogThread(title, directory, filter, onOk)));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
        private static void OpenFileDialogThread(string title, string directory, string filter, Action<string> onOk)
        {
            try
            {
                // Get the file path via prompt
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    if(Directory.Exists(directory))
                        openFileDialog.InitialDirectory = directory;
                    openFileDialog.Title = title;
                    openFileDialog.Filter = filter;
                    openFileDialog.RestoreDirectory = true;

                    Form form = new Form
                    {
                        TopMost = true,
                        TopLevel = true,
                    };

                    DialogResult dialogResult = openFileDialog.ShowDialog(form);
                    string fileName = openFileDialog.FileName;

                    form.Close();

                    if (dialogResult == DialogResult.OK && !string.IsNullOrWhiteSpace(fileName))
                    {
                        // Move back to the main thread so that we can interact with keen code again
                        MySandboxGame.Static.Invoke(
                            () => onOk(fileName),
                            "PluginLoader");
                    }
                }
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening file dialog: " + e);
            }
        }

        public static void OpenFolderDialog(string title, Action<string> onOk)
        {
            Thread t = new Thread(new ThreadStart(() => OpenFolderDialogThread(title, onOk)));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }
        private static void OpenFolderDialogThread(string title, Action<string> onOk)
        {
            try
            {
                // Get the file path via prompt
                using (FolderBrowserDialog openFileDialog = new FolderBrowserDialog())
                {
                    openFileDialog.Description = title;

                    Form form = new Form
                    {
                        TopMost = true,
                        TopLevel = true,
                    };

                    DialogResult dialogResult = openFileDialog.ShowDialog(form);
                    string selectedPath = openFileDialog.SelectedPath;

                    form.Close();

                    if (dialogResult == DialogResult.OK && !string.IsNullOrWhiteSpace(selectedPath))
                    {
                        // Move back to the main thread so that we can interact with keen code again
                        MySandboxGame.Static.Invoke(
                            () =>
                            {
                                onOk(selectedPath);
                            },
                            "PluginLoader");
                    }
                }
            }
            catch (Exception e)
            {
                LogFile.Error("Error while opening file dialog: " + e);
            }
        }
    }
}
