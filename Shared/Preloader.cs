using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Mono.Cecil;
using NLog;

namespace Pulsar.Shared
{
    public class Preloader
    {
        private const string ClassName = "Preloader";
        private const string TargetName = "TargetDLLs";
        private const string PatchName = "Patch";

        public bool HasPatches => patches.Keys.Count > 0;

        private readonly Dictionary<string, HashSet<Type>> patches = [];

        public Preloader(IEnumerable<Assembly> assemblies)
        {
            foreach (Assembly assembly in assemblies)
                AddPatch(assembly);
        }

        public void Preload(string gameDir, string preloadDir)
        {
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
                Assembly.LoadFrom(newDll);
            }

            foreach (string file in Directory.GetFiles(preloadDir))
                if (!patches.ContainsKey(Path.GetFileName(file)))
                    File.Delete(file);
        }

        public void AddPatch(Type patch)
        {
            IEnumerable<string> targets = GetTargets(patch);

            if (targets == null)
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

        private void AddPatch(Assembly assembly)
        {
            Type patch = assembly.GetType(ClassName);

            if (patch != null)
                AddPatch(patch);
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

            if (prop == null || prop.GetValue(null) is not IEnumerable<string> targets)
                return null;

            return targets;
        }

        private static bool Patch(Type patch, ref AssemblyDefinition definition)
        {
            MethodInfo patchMethod = patch.GetMethod(
                PatchName,
                BindingFlags.Public | BindingFlags.Static
            );

            if (patchMethod == null)
            {
                string name = patch.Assembly.GetName().Name;
                string message = $"Preloader plugin '{name}' does not define a patch method";
                LogFile.Error(message);
                Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            bool reference = patchMethod.GetParameters()[0].ParameterType.IsByRef;
            object[] args = [definition];

            try
            {
                patchMethod.Invoke(null, args);
            }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                string name = patch.Assembly.GetName().Name;
                var message = $"Preloader plugin '{name}' had an exception:\n" + tie.InnerException;
                LogFile.Error(message);
                Tools.ShowMessageBox(message, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (reference)
                definition = (AssemblyDefinition)args[0];

            return true;
        }
    }
}
