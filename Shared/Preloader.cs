using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Mono.Cecil;

namespace Pulsar.Shared;

public class Preloader
{
    private const string ClassName = "Preloader";
    private const string TargetName = "TargetDLLs";
    private const string PatchName = "Patch";
    private const string HookName = "Hook";

    public bool HasPatches => patches.Keys.Count > 0;

    private readonly Dictionary<string, HashSet<Type>> patches = [];
    private readonly HashSet<Type> hooks = [];

    public Preloader(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
            AddPreloader(assembly);
    }

    public void Preload(string gameDir, string preloadDir, string[] probeDirs)
    {
        File.Delete(@"C:\Temp\ilverify.out.log");
        File.Delete(@"C:\Temp\ilverify.err.log");
        
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameDir);

        var readerParams = new ReaderParameters() { AssemblyResolver = resolver };

        if (!Directory.Exists(preloadDir))
            Directory.CreateDirectory(preloadDir);

        foreach (var kvp in patches)
        {
            string dll = kvp.Key;
            string seDll = Path.Combine(gameDir, dll);
            HashSet<Type> patchClasses = kvp.Value;

            if (IsAssemblyLoaded(dll))
            {
                string message = $"Cannot preloader patch loaded '{dll}'";
                LogFile.Error(message);
                Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
                continue;
            }

            AssemblyDefinition asmDef;

            try
            {
                asmDef = AssemblyDefinition.ReadAssembly(seDll, readerParams);
            }
            catch (FileNotFoundException)
            {
                string message =
                    $"Target '{dll}' for preloader plugin(s) "
                    + string.Join(
                        ", ",
                        patchClasses.Select(x => "'" + x.Assembly.GetName().Name + "'")
                    )
                    + " could not be found";

                LogFile.Error(message);
                Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
                continue;
            }

            foreach (Type patchClass in patchClasses)
                Patch(patchClass, ref asmDef);

            // CLR does not respect pure in-memory refrences when resolving
            string newDll = Path.Combine(preloadDir, dll);
            asmDef.Write(newDll);
            IlVerifyRunner.Verify(newDll, preloadDir, probeDirs[0], probeDirs[1], probeDirs[2]);
            Assembly.LoadFrom(newDll);
        }

        foreach (string file in Directory.GetFiles(preloadDir))
            if (!patches.ContainsKey(Path.GetFileName(file)))
                File.Delete(file);

        foreach (Type hook in hooks)
            RunHook(hook);
    }

    public void AddPatch(Type patch)
    {
        IEnumerable<string> targets = GetTargets(patch);

        if (targets is null)
        {
            string name = patch.Assembly.GetName().Name;
            string message = $"Preloader plugin '{name}' does not define targets";
            LogFile.Error(message);
            Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        foreach (string dll in targets)
        {
            if (patches.ContainsKey(dll))
                patches[dll].Add(patch);
            else
                patches[dll] = [patch];
        }
    }

    private void AddPreloader(Assembly assembly)
    {
        Type patch = assembly.GetType(ClassName);
        if (patch is null)
            return;

        AddPatch(patch);
        hooks.Add(patch);
    }

    private static bool IsAssemblyLoaded(string simpleName)
    {
        return AppDomain
            .CurrentDomain.GetAssemblies()
            .Any(a =>
                string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase)
            );
    }

    private static IEnumerable<string> GetTargets(Type patch)
    {
        PropertyInfo prop = patch.GetProperty(
            TargetName,
            BindingFlags.Public | BindingFlags.Static
        );

        if (prop is null || prop.GetValue(null) is not IEnumerable<string> targets)
            return null;

        return targets;
    }

    private static bool Patch(Type patch, ref AssemblyDefinition definition)
    {
        MethodInfo patchMethod = patch.GetMethod(
            PatchName,
            BindingFlags.Public | BindingFlags.Static
        );

        if (patchMethod is null)
        {
            string name = patch.Assembly.GetName().Name;
            string message = $"Preloader plugin '{name}' does not define a patch method";
            LogFile.Error(message);
            Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        bool reference = patchMethod.GetParameters()[0].ParameterType.IsByRef;
        object[] args = [definition];

#if DEBUG
        patchMethod.Invoke(null, args);
#else        
        try
        {
            patchMethod.Invoke(null, args);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            string name = patch.Assembly.GetName().Name;
            var message = $"Preloader plugin '{name}' had an exception:\n" + tie.InnerException;
            LogFile.Error(message);
            Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
#endif

        if (reference)
            definition = (AssemblyDefinition)args[0];

        return true;
    }

    private static bool RunHook(Type patch)
    {
        MethodInfo hookMethod = patch.GetMethod(
            HookName,
            BindingFlags.Public | BindingFlags.Static
        );

        if (hookMethod is null)
        {
            string name = patch.Assembly.GetName().Name;
            string message = $"Preloader plugin '{name}' does not define a hook method";
            LogFile.Error(message);
            return false;
        }

#if DEBUG
        hookMethod.Invoke(null, []);
#else
        try
        {
            hookMethod.Invoke(null, []);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            string name = patch.Assembly.GetName().Name;
            var message = $"Preloader plugin '{name}' had an exception:\n" + tie.InnerException;
            LogFile.Error(message);
            Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
#endif

        return true;
    }
}

public static class IlVerifyRunner
{
    public static int Verify(string dllPath, params string[] additionalReferenceFolders)
    {
        if (!File.Exists(dllPath))
            throw new FileNotFoundException(dllPath);

        var references = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 1. Add runtime TPA references first
        var tpa = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = Path.GetFileName(path);
            if (!references.ContainsKey(name))
                references.Add(name, path);
        }

        // 2. Add user-provided folders (first folder wins per DLL name)
        foreach (var folder in additionalReferenceFolders.Where(Directory.Exists))
        {
            foreach (var dll in Directory.EnumerateFiles(folder, "*.dll"))
            {
                var name = Path.GetFileName(dll);
                if (!references.ContainsKey(name))
                    references.Add(name, dll);
            }
        }

        var args = new StringBuilder();
        args.Append($"\"{dllPath}\" ");

        foreach (var reference in references.Values)
            args.Append($"--reference \"{reference}\" ");

        var psi = new ProcessStartInfo
        {
            FileName = "ilverify",
            Arguments = args.ToString(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = Process.Start(psi)!;

        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();

        process.WaitForExit();

        File.AppendAllText(@"C:\Temp\ilverify.out.log", stdout);
        File.AppendAllText(@"C:\Temp\ilverify.err.log", stderr);

        return process.ExitCode;
    }
}
