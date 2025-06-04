using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace avaness.PluginLoader.Compiler
{
    internal class CompilerProxy
    {
        internal static Stack<Assembly> assemblies = new Stack<Assembly>();
        private static readonly HashSet<string> blacklist = ["System.ValueTuple"];

        private bool debugBuild;
        private List<string> dependencies;
        private Dictionary<string, Stream> sourceStreams;

        public CompilerProxy(bool debugBuild = false)
        {
            this.debugBuild = debugBuild;
            dependencies = new List<string>();
            sourceStreams = new Dictionary<string, Stream>();
        }

        public static void GenerateAssemblyList()
        {
            assemblies = new Stack<Assembly>(
                AppDomain
                    .CurrentDomain.GetAssemblies()
                    .Where(
                        (a) =>
                            !a.IsDynamic
                            && !string.IsNullOrWhiteSpace(a.Location)
                            && !blacklist.Contains(a.GetName().Name)
                    )
            );
        }

        private static void compileInAppDomain()
        {
            Func<string, object> GetData = AppDomain.CurrentDomain.GetData;
            Action<string, object> SetData = AppDomain.CurrentDomain.SetData;

            string loaderBase = AppDomain.CurrentDomain.BaseDirectory;

            var assemblyName = (string)GetData("AssemblyName");
            var dependencies = (List<string>)GetData("Dependencies");
            var sourceStreams = (Dictionary<string, Stream>)GetData("SourceStreams");
            var debugBuild = (bool)GetData("DebugBuild");
            var assemblies = (Stack<Assembly>)GetData("Assemblies");

            RoslynReferences.GenerateAssemblyList(assemblies);

            var compiler = new RoslynCompiler(debugBuild);

            foreach (string dependency in dependencies)
                compiler.TryAddDependency(dependency);

            foreach (var (name, stream) in sourceStreams)
                compiler.Load(stream, name);

            byte[] compilation = compiler.Compile(assemblyName, out byte[] symbols);

            SetData("Symbols", symbols);
            SetData("Compilation", compilation);
        }

        public byte[] Compile(string assemblyName, out byte[] symbols)
        {
            AppDomainSetup config = new AppDomainSetup
            {
                ApplicationBase = LoaderTools.PluginsDir,
                PrivateBinPath = Path.Combine(LoaderTools.PluginsDir, "Libraries"),
                ConfigurationFile = Path.Combine(LoaderTools.PluginsDir, "loader.dll.config"),
            };

            AppDomain domain = AppDomain.CreateDomain("Compiler", null, config);

            // Load Space Engineers references from the Bin64 directory
            domain.AssemblyResolve += (object sender, ResolveEventArgs args) =>
            {
                string loaderBase = AppDomain.CurrentDomain.BaseDirectory;
                string targetName = new AssemblyName(args.Name).Name;
                string targetPath = Path.Combine(loaderBase, "..", targetName);

                if (File.Exists(targetPath + ".dll"))
                    return Assembly.LoadFrom(targetPath + ".dll");

                if (File.Exists(targetPath + ".exe"))
                    return Assembly.LoadFrom(targetPath + ".exe");

                return null;
            };

            domain.SetData("AssemblyName", assemblyName);
            domain.SetData("Assemblies", assemblies);
            domain.SetData("Dependencies", dependencies);
            domain.SetData("DebugBuild", debugBuild);
            domain.SetData("SourceStreams", sourceStreams);

            domain.DoCallBack(compileInAppDomain);

            byte[] compilation = (byte[])domain.GetData("Compilation");
            symbols = (byte[])domain.GetData("Symbols");

            AppDomain.Unload(domain);

            return compilation;
        }

        public void TryAddDependency(string dll)
        {
            dependencies.Add(dll);
        }

        public void Load(Stream s, string name)
        {
            MemoryStream mem = new MemoryStream();
            s.CopyTo(mem);
            mem.Position = 0;
            sourceStreams[name] = mem;
        }
    }
}
