using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Pulsar.Shared;
using Pulsar.Shared.Data;
using Sandbox.Game.World;
using VRage.Game.Components;
using VRage.Plugins;

namespace Pulsar.Legacy.Loader
{
    public class PluginInstance
    {
        private readonly Type mainType;
        private readonly PluginData data;
        private readonly Assembly mainAssembly;
        private MethodInfo openConfigDialog;
        private IPlugin plugin;
        private IHandleInputPlugin inputPlugin;

        public string Id => data.Id;
        public string FriendlyName => data.FriendlyName;
        public string Author => data.Author;
        public bool HasConfigDialog => openConfigDialog != null;

        private PluginInstance(PluginData data, Assembly mainAssembly, Type mainType)
        {
            this.data = data;
            this.mainAssembly = mainAssembly;
            this.mainType = mainType;
        }

        /// <summary>
        /// To be called when a <see langword="MemberAccessException"/> is thrown. Returns true if the exception was thrown from this plugin.
        /// </summary>
        public bool ContainsExceptionSite(MemberAccessException exception)
        {
            // Note: this wont find exceptions thrown within transpiled methods or some kinds of patches
            Assembly a = exception.TargetSite?.DeclaringType?.Assembly;
            if (a != null && a == mainAssembly)
            {
                data.InvalidateCache();
                ThrowError($"ERROR: Plugin {data} threw an exception: {exception}");
                return true;
            }
            return false;
        }

        public bool Instantiate()
        {
            try
            {
                plugin = (IPlugin)Activator.CreateInstance(mainType);
                inputPlugin = plugin as IHandleInputPlugin;
                LoadAssets();
            }
            catch (Exception e)
            {
                ThrowError($"Failed to instantiate {data} because of an error: {e}");
                return false;
            }

            try
            {
                openConfigDialog = AccessTools.DeclaredMethod(mainType, "OpenConfigDialog", []);
            }
            catch (Exception e)
            {
                LogFile.Error($"Unable to find OpenConfigDialog() in {data} due to an error: {e}");
                openConfigDialog = null;
            }
            return true;
        }

        private void LoadAssets()
        {
            string assetFolder = data.GetAssetPath();
            if (string.IsNullOrEmpty(assetFolder) || !Directory.Exists(assetFolder))
                return;

            LogFile.WriteLine($"Loading assets for {data} from {assetFolder}");
            MethodInfo loadAssets = AccessTools.DeclaredMethod(
                mainType,
                "LoadAssets",
                [typeof(string)]
            );
            loadAssets?.Invoke(plugin, [assetFolder]);
        }

        public void OpenConfig()
        {
            if (plugin == null || openConfigDialog == null)
                return;

            try
            {
                openConfigDialog.Invoke(plugin, []);
            }
            catch (Exception e)
            {
                ThrowError($"Failed to open plugin config for {data} because of an error: {e}");
            }
        }

        public bool Init(object gameInstance)
        {
            if (plugin == null)
                return false;

            try
            {
                plugin.Init(gameInstance);
                return true;
            }
            catch (Exception e)
            {
                ThrowError($"Failed to initialize {data} because of an error: {e}");
                return false;
            }
        }

        public void RegisterSession(MySession session)
        {
            if (plugin != null)
            {
                try
                {
                    Type descType = typeof(MySessionComponentDescriptor);
                    int count = 0;
                    foreach (
                        Type t in mainAssembly
                            .GetTypes()
                            .Where(t => Attribute.IsDefined(t, descType))
                    )
                    {
                        MySessionComponentBase comp = (MySessionComponentBase)
                            Activator.CreateInstance(t);
                        session.RegisterComponent(comp, comp.UpdateOrder, comp.Priority);
                        count++;
                    }
                    if (count > 0)
                        LogFile.WriteLine(
                            $"Registered {count} session components from: {mainAssembly.FullName}"
                        );
                }
                catch (Exception e)
                {
                    ThrowError($"Failed to register {data} because of an error: {e}");
                }
            }
        }

        public bool Update()
        {
            if (plugin == null)
                return false;

            plugin.Update();
            return true;
        }

        public bool HandleInput()
        {
            if (plugin == null)
                return false;

            inputPlugin?.HandleInput();
            return true;
        }

        public void Dispose()
        {
            if (plugin != null)
            {
                try
                {
                    plugin.Dispose();
                    plugin = null;
                    inputPlugin = null;
                }
                catch (Exception e)
                {
                    data.Status = PluginStatus.Error;
                    LogFile.Error($"Failed to dispose {data} because of an error: {e}");
                }
            }
        }

        private void ThrowError(string error)
        {
            LogFile.Error(error);
            data.Error();
            Dispose();
        }

        public static bool TryGet(PluginData data, Assembly assembly, out PluginInstance instance)
        {
            instance = null;

            try
            {
                Type pluginType = assembly
                    .GetTypes()
                    .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t));

                if (pluginType == null)
                {
                    LogFile.Error($"Failed to load {data} because it does not contain an IPlugin");
                    data.Error();
                    return false;
                }

                instance = new PluginInstance(data, assembly, pluginType);
                return true;
            }
            catch (Exception e)
            {
                StringBuilder sb = new();
                sb.Append("Failed to load ")
                    .Append(data)
                    .Append(" because of an exception: ")
                    .Append(e)
                    .AppendLine();
                if (
                    e is ReflectionTypeLoadException typeLoadEx
                    && typeLoadEx.LoaderExceptions != null
                )
                {
                    sb.Append("Exception details: ").AppendLine();
                    foreach (Exception loaderException in typeLoadEx.LoaderExceptions)
                        sb.Append(loaderException).AppendLine();
                }
                LogFile.Error(sb.ToString());
                data.Error();
                return false;
            }
        }

        public override string ToString()
        {
            return data.ToString();
        }
    }
}
