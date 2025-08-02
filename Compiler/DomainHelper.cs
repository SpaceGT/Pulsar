using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Pulsar.Compiler
{
    public static class DomainHelper
    {
        public static AppDomain AppDomain = null;
        private static readonly AssemblyName name = typeof(DomainHelper).Assembly.GetName();

        private static void SetupAppDomain()
        {
            var assemblies = (HashSet<string>)AppDomain.CurrentDomain.GetData("assemblies");
            RoslynReferences.GenerateAssemblyList(assemblies);
        }

        public static void CreateAppDomain(string pulsarDir, HashSet<string> assemblyList)
        {
            string libraries = Path.Combine(pulsarDir, "Libraries");

            AppDomainSetup config = new()
            {
                ApplicationBase = libraries,
                PrivateBinPath = null,
                ConfigurationFile = Path.Combine(libraries, name.Name + ".dll.config"),
            };

            AppDomain domain = AppDomain.CreateDomain("Compiler", null, config);

            // Load Space Engineers references from the Bin64 directory
            domain.AssemblyResolve += (sender, args) =>
            {
                string loaderBase = AppDomain.CurrentDomain.BaseDirectory;
                string targetName = new AssemblyName(args.Name).Name;
                string targetPath = Path.Combine(loaderBase, "..", "..", targetName);

                if (File.Exists(targetPath + ".dll"))
                    return Assembly.LoadFrom(targetPath + ".dll");

                if (File.Exists(targetPath + ".exe"))
                    return Assembly.LoadFrom(targetPath + ".exe");

                return null;
            };
            domain.SetData("assemblies", assemblyList);
            domain.DoCallBack(SetupAppDomain);

            AppDomain = domain;
        }

        public static void UnloadAppDomain()
        {
            AppDomain.Unload(AppDomain);
            AppDomain = null;
        }
    }
}
