// Only host/game services are stubbed. PluginInstance and Patch_Rewriter are linked verbatim.
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp;

namespace Pulsar.Shared
{
    public static class LogFile
    {
        public static void Error(string text) => Console.WriteLine(text);

        public static void WriteLine(string text, NLog.LogLevel level = null) { }
    }

    public static class Tools
    {
        public static bool IsProton() => false;
    }
}

namespace Pulsar.Shared.Assets
{
    public static class PluginAsset
    {
        public const string ReservedAssetFolder = "AssetFolder";
    }
}

namespace Pulsar.Shared.Data
{
    public enum PluginStatus
    {
        None,
        Error,
    }

    public class PluginData
    {
        public string Id => "probe";
        public string FriendlyName => "Probe";
        public string Author => "Tests";
        public PluginStatus Status { get; set; }

        public void Error() => Status = PluginStatus.Error;

        public void InvalidateCache() { }

        public string GetConfigPath(string name, string extension) => "unused";

        public IReadOnlyDictionary<string, string> GetNamedAssets() =>
            new Dictionary<string, string>();
    }
}

namespace VRage.Plugins
{
    public interface IPlugin : IDisposable
    {
        void Init(object game);
        void Update();
    }

    public interface IHandleInputPlugin : IPlugin
    {
        void HandleInput();
    }
}

namespace VRage.Scripting
{
    public enum MyApiTarget
    {
        None,
        Mod,
        Ingame,
    }

    public class MyScriptCompiler
    {
        public void Compile() { }
    }
}

namespace VRage.Game.VisualScripting.ScriptBuilder
{
    public class MyVSCompiler
    {
        public bool Compile() => true;
    }
}

namespace VRage.ObjectBuilders
{
    public class MyObjectBuilder_Base { }
}

namespace VRage.Game
{
    public class MyModContext
    {
        public static readonly MyModContext UnknownContext = new();
    }

    public static class Extensions
    {
        public static bool IsNullOrEmpty<T>(this T[] values) =>
            values == null || values.Length == 0;
    }
}

namespace VRage.Game.Components
{
    public class MySessionComponentDescriptor : Attribute { }

    public class MySessionComponentBase
    {
        public int UpdateOrder;
        public int Priority;
    }

    public class MyEntityComponentDescriptor : Attribute
    {
        public Type EntityBuilderType;
        public string[] EntityBuilderSubTypeNames;
    }

    public class MyGameLogicComponent { }
}

namespace Sandbox.Game.World
{
    public class MySession
    {
        public void RegisterComponent(
            VRage.Game.Components.MySessionComponentBase component,
            int order,
            int priority
        ) { }
    }

    public class MyScriptManager
    {
        public readonly Dictionary<Type, VRage.Game.MyModContext> TypeToModMap = new();
        public readonly Dictionary<Type, HashSet<Type>> EntityScripts = new();
        public readonly Dictionary<Tuple<Type, string>, HashSet<Type>> SubEntityScripts = new();
    }
}
