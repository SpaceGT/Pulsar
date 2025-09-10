using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Pulsar.Shared;
using Sandbox;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using SpaceEngineers;
using VRage.FileSystem;
using VRage.Plugins;
using VRage.Utils;

namespace Pulsar.Legacy.Launcher;

internal class GameLog : IGameLog
{
    public bool Exists()
    {
        string file = MyLog.Default?.GetFilePath();
        return File.Exists(file) && file.EndsWith(".log");
    }

    public bool Open()
    {
        MyLog.Default.Flush();
        string file = MyLog.Default?.GetFilePath();

        if (!File.Exists(file) || !file.EndsWith(".log"))
            return false;

        Process.Start(file);
        return true;
    }

    public void Write(string line) => MyLog.Default.WriteLine(line);
}

internal static class Game
{
    public static void RegisterPlugin(IPlugin plugin)
    {
        FieldInfo m_pluginsField = typeof(MyPlugins).GetField(
            "m_plugins",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        List<IPlugin> m_plugins = (List<IPlugin>)m_pluginsField.GetValue(null);
        m_plugins.Add(plugin);
    }

    public static void SetMainAssembly(Assembly assembly)
    {
        FieldInfo mainAssemblyField = typeof(MyFileSystem).GetField(
            "m_mainAssembly",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        mainAssemblyField.SetValue(null, assembly);

        FieldInfo mainAssemblyNameField = typeof(MyFileSystem).GetField(
            "MainAssemblyName",
            BindingFlags.Static | BindingFlags.Public
        );
        mainAssemblyNameField.SetValue(null, assembly.GetName().Name);

        var asmFolder = new FileInfo(assembly.Location).DirectoryName;
        var seRoot = new FileInfo(asmFolder).Directory?.FullName ?? Path.GetFullPath(asmFolder);

        MyFileSystem.ExePath = asmFolder;
        MyFileSystem.RootPath = seRoot;

        Environment.CurrentDirectory = asmFolder;
    }

    public static ResolveEventHandler GameAssemblyResolver(string bin64Dir)
    {
        return (sender, args) =>
        {
            string targetName = new AssemblyName(args.Name).Name;
            string loaderBase = AppDomain.CurrentDomain.BaseDirectory;
            string targetPath = Path.Combine(bin64Dir, targetName);

            if (File.Exists(targetPath + ".dll"))
                return Assembly.LoadFrom(targetPath + ".dll");

            if (File.Exists(targetPath + ".exe"))
                return Assembly.LoadFrom(targetPath + ".exe");

            return null;
        };
    }

    public static Version GetGameVersion(string bin64Dir)
    {
        const string Assembly = "SpaceEngineers.Game.dll";
        const string Method = "SetupBasicGameInfo";
        const string ReferencedField = "GameVersion";

        // We read the version directly from the DLL to avoid loading Keen assemblies.
        // Originally an AppDomain was used but Mono could not unload them properly.
        using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(bin64Dir, Assembly));

        Instruction storeField = assembly
            .MainModule.Types.SelectMany(type => type.Methods)
            .Where(method => method.HasBody && method.Name == Method)
            .SelectMany(method => method.Body.Instructions)
            .FirstOrDefault(instruction =>
                instruction.OpCode == OpCodes.Stfld
                && instruction.Operand is FieldReference fr
                && fr.Name == ReferencedField
            );

        Instruction versionValue = storeField?.Previous?.Previous;
        if (versionValue is null || versionValue.OpCode.Code != Code.Ldc_I4)
            return null;

        // 1234567 => "1.234.567"
        string vStr = ((int)versionValue.Operand).ToString();
        string version = string.Join(".", [vStr[0], vStr.Substring(1, 3), vStr.Substring(4, 3)]);
        return new Version(version);
    }

    public static void SetupMyFakes()
    {
        typeof(MyFakes).TypeInitializer.Invoke(null, null);

        // Note SpaceEngineers internally prioritises -nosplash over ENABLE_SPLASHSCREEN
        MyFakes.ENABLE_SPLASHSCREEN = Tools.HasCommandArg("-sesplash");

        if (Tools.HasCommandArg("-f12menu"))
            MyFakes.ENABLE_F12_MENU = true;
    }

    public static void CorrectExitText()
    {
        FieldInfo exitTextField = typeof(MyCommonTexts).GetField(
            "ScreenMenuButtonExitToWindows",
            BindingFlags.Static | BindingFlags.Public
        );

        string message = $"Exit to {(Tools.IsNative() ? "Windows" : "Linux")}";
        exitTextField.SetValue(null, MyStringId.GetOrCompute(message));
    }

    public static float GetLoadProgress()
    {
        // No native function in Space Engineers does this but we can estimate
        // FIXME: Does not work well with Preloaders or under Proton
        float expectedGrowth = 1100f * 1024 * 1024;

        Process process = Process.GetCurrentProcess();
        process.Refresh();

        float ratio = process.PrivateMemorySize64 / expectedGrowth;

        return Math.Min(1f, Math.Max(0f, ratio));
    }

    // Inlining causes Keen references to load prematurely
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void StartSpaceEngineers(string[] args) => MyProgram.Main(args);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShowIntroVideo(bool enabled) =>
        MyPlatformGameSettings.ENABLE_LOGOS = enabled;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RunOnGameThread(Action action) =>
        MySandboxGame.Static.Invoke(action, "Pulsar");
}
