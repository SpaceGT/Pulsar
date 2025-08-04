using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using NLog;

namespace Pulsar.Compiler
{
    public static class RoslynReferences
    {
        private static readonly HashSet<string> referenceBlacklist = ["System.ValueTuple"];
        internal static readonly Dictionary<string, MetadataReference> AllReferences = [];

        public static void GenerateAssemblyList(HashSet<string> assemblies)
        {
            if (AllReferences.Count > 0)
                return;

            Stack<Assembly> loadedAssemblies = new();
            foreach (string name in assemblies)
                loadedAssemblies.Push(Assembly.Load(name));

            StringBuilder sb = new();

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
                        if (
                            !ContainsReference(name)
                            && TryLoadAssembly(name, out Assembly aRef)
                            && IsValidReference(aRef)
                        )
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

            LogFile.WriteLine(sb.ToString(), level);
        }

        /// <summary>
        /// This method is used to load references that otherwise would not exist or be optimized out
        /// </summary>
        private static IEnumerable<Assembly> GetOtherReferences()
        {
            yield return typeof(Microsoft.CSharp.RuntimeBinder.Binder).Assembly;
            yield return typeof(System.Windows.Forms.DataVisualization.Charting.Chart).Assembly;

            // WPF assemblies
            yield return typeof(System.Windows.Media.TextEffect).Assembly;
            yield return typeof(System.Windows.Controls.Button).Assembly;
            yield return typeof(System.Windows.Controls.Ribbon.Ribbon).Assembly;
            yield return typeof(System.Windows.Point).Assembly;
            yield return typeof(System.Xaml.XamlType).Assembly;

            // Patching assemblies
            yield return typeof(HarmonyLib.Harmony).Assembly;
            yield return typeof(Mono.Cecil.AssemblyDefinition).Assembly;
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
            string name = a.GetName().Name;
            return !a.IsDynamic
                && !string.IsNullOrWhiteSpace(a.Location)
                && !referenceBlacklist.Contains(name);
        }

        public static void LoadReference(string name)
        {
            try
            {
                AssemblyName aName = new(name);
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
