using Microsoft.CodeAnalysis;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace avaness.PluginLoader.Compiler
{
    public static class RoslynReferences
    {
        internal static readonly Dictionary<string, MetadataReference> AllReferences = new Dictionary<string, MetadataReference>();
        private static readonly HashSet<string> referenceBlacklist = new HashSet<string>(new[] { "System.ValueTuple" });

        public static void GenerateAssemblyList()
        {
            if (AllReferences.Count > 0)
                return;

            AssemblyName harmonyInfo = typeof(HarmonyLib.Harmony).Assembly.GetName();

            Stack<Assembly> loadedAssemblies = new Stack<Assembly>(AppDomain.CurrentDomain.GetAssemblies().Where(IsValidReference));

            StringBuilder sb = new StringBuilder();

            sb.AppendLine();
            string line = "===================================";
            sb.AppendLine(line);
            sb.AppendLine("Assembly References");
            sb.AppendLine(line);

            LogLevel level = LogLevel.Info;
            try
            {
                foreach (Assembly a in loadedAssemblies)
                {
                    // Prevent other Harmony versions from being loaded
                    AssemblyName name = a.GetName();
                    if (name.Name == harmonyInfo.Name && name.Version != harmonyInfo.Version)
                    {
                        LogFile.Warn($"Multiple Harmony assemblies are loaded. Plugin Loader is using {harmonyInfo} but found {name}");
                        continue;
                    }

                    AddAssemblyReference(a);
                    sb.AppendLine(a.FullName);
                }

                foreach (Assembly a in GetOtherReferences())
                {
                    AddAssemblyReference(a);
                    sb.AppendLine(a.FullName);
                }

                sb.AppendLine(line);
                while (loadedAssemblies.Count > 0)
                {
                    Assembly a = loadedAssemblies.Pop();

                    foreach (AssemblyName name in a.GetReferencedAssemblies())
                    {
                        // Prevent other Harmony versions from being loaded
                        if (name.Name == harmonyInfo.Name && name.Version != harmonyInfo.Version)
                        {
                            LogFile.Warn($"Multiple Harmony assemblies are loaded. Plugin Loader is using {harmonyInfo} but found {name}");
                            continue;
                        }

                        if (!ContainsReference(name) && TryLoadAssembly(name, out Assembly aRef) && IsValidReference(aRef))
                        {
                            AddAssemblyReference(aRef);
                            sb.AppendLine(name.FullName);
                            loadedAssemblies.Push(aRef);
                        }
                    }
                }

                sb.AppendLine(line);
            }
            catch (Exception e)
            {
                sb.Append("Error: ").Append(e).AppendLine();
                level = LogLevel.Error;
            }

            LogFile.WriteLine(sb.ToString(), level, gameLog: false);
        }

        /// <summary>
        /// This method is used to load references that otherwise would not exist or be optimized out
        /// </summary>
        private static IEnumerable<Assembly> GetOtherReferences()
        {
            yield return typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly;
            yield return typeof(System.Windows.Forms.DataVisualization.Charting.Chart).Assembly;
        }

        private static bool ContainsReference(AssemblyName name)
        {
            return AllReferences.ContainsKey(name.Name);
        }

        private static bool TryLoadAssembly(AssemblyName name, out Assembly aRef)
        {
            try
            {
                aRef = Assembly.Load(name);
                return true;
            }
            catch (IOException)
            {
                aRef = null;
                return false;
            }
        }

        private static void AddAssemblyReference(Assembly a)
        {
            string name = a.GetName().Name;
            if (!AllReferences.ContainsKey(name))
                AllReferences.Add(name, MetadataReference.CreateFromFile(a.Location));
        }

        private static bool IsValidReference(Assembly a)
        {
            return !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location) && !referenceBlacklist.Contains(a.GetName().Name);
        }

        public static void LoadReference(string name)
        {
            try
            {
                AssemblyName aName = new AssemblyName(name);
                if (!AllReferences.ContainsKey(aName.Name))
                {
                    Assembly a = Assembly.Load(aName);
                    LogFile.WriteLine("Reference added at runtime: " + a.FullName);
                    MetadataReference aRef = MetadataReference.CreateFromFile(a.Location);
                    AllReferences[a.GetName().Name] = aRef;
                }
            }
            catch (IOException)
            {
                LogFile.Warn("Unable to find the assembly '" + name + "'!");
            }
        }

        public static bool Contains(string id)
        {
            return AllReferences.ContainsKey(id);
        }
    }
}