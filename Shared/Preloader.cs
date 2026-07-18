using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mono.Cecil;
using Pulsar.Interface.Protocol;

namespace Pulsar.Shared;

public class Preloader
{
    private const string ClassName = "Preloader";
    private const string TargetName = "TargetDLLs";
    private const string PatchName = "Patch";
    private const string PreHookName = "Initialize";
    private const string PostHookName = "Finish";

    public bool HasPatches => patches.Keys.Count + preHooks.Count + postHooks.Count > 0;

    private readonly HashSet<MethodInfo> preHooks = [];
    private readonly HashSet<MethodInfo> postHooks = [];
    private readonly Dictionary<string, HashSet<MethodInfo>> patches = [];

    public Preloader(IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            Type preloader = assembly.GetType(ClassName);
            if (preloader is not null)
                AddPreloader(preloader);
        }
    }

    public void PreHooks()
    {
        foreach (MethodInfo hook in preHooks)
            SafeInvoke(hook);
    }

    public void Patch(string gameDir, string cacheDir)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameDir);

        var readerParams = new ReaderParameters() { AssemblyResolver = resolver };

        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);

        foreach (var kvp in patches)
        {
            string dll = kvp.Key;
            string seDll = Path.Combine(gameDir, dll);
            HashSet<MethodInfo> patchMethods = kvp.Value;

            if (EnsureNotLoaded(dll))
                continue;

            if (TryReadAssembly(seDll, readerParams, patchMethods, out var asmDef))
                continue;

            foreach (MethodInfo patchMethod in patchMethods)
                ApplyPatch(patchMethod, ref asmDef);

            // Mono.Cecil does not support writing mixed mode assemblies (used by SE2)
            // Forcing ILOnly is safe because R2R preserves the original IL as a fallback.
            asmDef.MainModule.Attributes |= ModuleAttributes.ILOnly;

            // CLR does not respect pure in-memory references when resolving
            string newDll = Path.Combine(cacheDir, dll);
            asmDef.Write(newDll);
            Assembly.LoadFrom(newDll);
        }

        foreach (string file in Directory.GetFiles(cacheDir))
            if (!patches.ContainsKey(Path.GetFileName(file)))
                File.Delete(file);
    }

    public void PostHooks()
    {
        foreach (MethodInfo hook in postHooks)
            SafeInvoke(hook);
    }

    private static bool EnsureNotLoaded(string simpleName)
    {
        bool IsEqual(Assembly asm)
        {
            string name = asm.GetName().Name;
            return string.Equals(name, simpleName, StringComparison.OrdinalIgnoreCase);
        }

        if (!AppDomain.CurrentDomain.GetAssemblies().Any(IsEqual))
            return false;

        string message = $"Failed to patch '{simpleName}' as it is loaded into memory!";
        LogFile.Error(message);
        Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);

        return true;
    }

    private static bool TryReadAssembly(
        string path,
        ReaderParameters reader,
        IEnumerable<MethodInfo> users,
        out AssemblyDefinition assemblyDefinition
    )
    {
        try
        {
            assemblyDefinition = AssemblyDefinition.ReadAssembly(path, reader);
        }
        catch (FileNotFoundException)
        {
            string dll = Path.GetFileName(path);
            string message =
                $"Target '{dll}' for preloader plugin(s) "
                + string.Join(", ", users.Select(GetAssemblyName))
                + " could not be found";

            LogFile.Error(message);
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);

            assemblyDefinition = null;
            return true;
        }

        return false;
    }

    public void AddPreloader(Type type)
    {
        MethodInfo preHookMethod = GetMethod(type, PreHookName, []);
        if (preHookMethod is not null)
            preHooks.Add(preHookMethod);

        MethodInfo postHookMethod = GetMethod(type, PostHookName, []);
        if (postHookMethod is not null)
            postHooks.Add(postHookMethod);

        IEnumerable<string> targets = GetSequence<string>(type, TargetName);
        if (targets is null)
            return;

        MethodInfo patchMethod =
            GetMethod(type, PatchName, [typeof(AssemblyDefinition)])
            ?? GetMethod(type, PatchName, [typeof(AssemblyDefinition).MakeByRefType()]);

        if (patchMethod is null)
        {
            string name = type.Assembly.GetName().Name;
            string message = $"Preloader plugin '{name}' does not define a Patch method";
            LogFile.Error(message);
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            return;
        }

        foreach (string dll in targets)
        {
            if (patches.TryGetValue(dll, out HashSet<MethodInfo> methods))
                methods.Add(patchMethod);
            else
                patches.Add(dll, [patchMethod]);
        }
    }

    private static MethodInfo GetMethod(Type type, string name, Type[] signature)
    {
        MethodInfo mi = AccessTools.Method(type, name, signature);
        bool valid = mi is not null && mi.IsStatic && mi.IsPublic && mi.ReturnType == typeof(void);
        return valid ? mi : null;
    }

    private static IEnumerable<T> GetSequence<T>(Type type, string name)
    {
        var targetsProp = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (targetsProp?.GetValue(null) is IEnumerable<T> targets)
            return targets;
        return null;
    }

    private static string GetAssemblyName(MethodInfo method) =>
        $"'{method.DeclaringType.Assembly.GetName().Name}'";

    private static bool ApplyPatch(MethodInfo patchMethod, ref AssemblyDefinition definition)
    {
        bool reference = patchMethod.GetParameters()[0].ParameterType.IsByRef;
        object[] args = [definition];

        if (!SafeInvoke(patchMethod, args))
            return false;

        if (reference)
            definition = (AssemblyDefinition)args[0];

        return true;
    }

    private static bool SafeInvoke(MethodInfo method, object[] args = null)
    {
        try
        {
            method.Invoke(null, args ?? []);
        }
        catch (TargetInvocationException tie) when (tie.InnerException is not null)
        {
            string name = GetAssemblyName(method);
            var message = $"Preloader plugin {name} had an exception:\n" + tie.InnerException;
            LogFile.Error(message);
            Tools.ShowMessageBox(message, PromptButtons.Ok, PromptIcon.Error);
            return false;
        }

        return true;
    }
}
