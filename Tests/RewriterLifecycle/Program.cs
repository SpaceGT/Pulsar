using System;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Pulsar.Legacy.Loader;
using Pulsar.Legacy.Patch;
using Pulsar.Shared.Data;
using VRage.Plugins;
using VRage.Scripting;

internal static class Program
{
    private static int checks;
    private static readonly MethodInfo Rewrite = typeof(Patch_Rewriter).GetMethod(
        "Rewrite",
        BindingFlags.NonPublic | BindingFlags.Static
    );

    private static void Main()
    {
        var data = new PluginData();
        var owner = Discover(data);
        Check(Probe.Constructions == 0, "discovery does not construct the plugin");
        Check(Patch_Rewriter.Methods.ContainsKey(owner), "discovery registers the owner");
        RunRewrite();
        Check(Probe.Rewrites == 1, "rewriter runs before plugin construction");
        Check(owner.Instantiate(), "normal construction succeeds");
        Check(owner.Init(null), "normal initialization succeeds");
        Check(Probe.Constructions == 1 && Probe.Initializations == 1, "plugin lifecycle runs once");
        Check(Patch_Rewriter.Methods.Count == 1, "normal initialization retains one entry");
        RunRewrite();
        Check(Probe.Rewrites == 2, "one rewrite invocation per compilation");
        owner.Dispose();
        Check(Patch_Rewriter.Methods.IsEmpty, "disposal unregisters the owner");

        foreach (var invalidReturn in new[] { false, true })
        {
            Probe.Reset();
            data = new PluginData();
            owner = Discover(data);
            Probe.ThrowRewrite = !invalidReturn;
            Probe.ReturnNull = invalidReturn;
            RunRewrite();
            Check(data.Status == PluginStatus.Error, "early failure marks owner disabled");
            Check(Patch_Rewriter.Methods.IsEmpty, "early failure unregisters the owner");
            Check(!owner.Instantiate(), "disabled owner cannot be constructed later");
            Check(Probe.Constructions == 0, "disabled plugin constructor never runs");
            Check(
                Patch_Rewriter.Methods.IsEmpty,
                "late initialization does not resurrect rewriter"
            );
            RunRewrite();
            Check(Probe.Rewrites == 1, "disabled rewriter is not called again");
            owner.Dispose();
        }

        Probe.Reset();
        owner = Discover(new PluginData());
        Probe.ThrowConstructor = true;
        Check(!owner.Instantiate(), "constructor failure is reported");
        Check(Patch_Rewriter.Methods.IsEmpty, "constructor failure removes early registration");
        Console.WriteLine($"{checks}/{checks} checks passed");
    }

    private static PluginInstance Discover(PluginData data)
    {
        Check(
            PluginInstance.TryGet(data, typeof(Probe).Assembly, out var owner),
            "discovery succeeds"
        );
        return owner;
    }

    private static void RunRewrite()
    {
        var compilation = CSharpCompilation.Create("probe");
        Check(
            ReferenceEquals(
                compilation,
                Rewrite.Invoke(null, new object[] { compilation, MyApiTarget.Mod })
            ),
            "compilation survives successful or disabled rewrite"
        );
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
        checks++;
        Console.WriteLine("PASS: " + message);
    }
}

public sealed class Probe : IPlugin
{
    public static int Constructions,
        Initializations,
        Rewrites;
    public static bool ThrowRewrite,
        ReturnNull,
        ThrowConstructor;

    public Probe()
    {
        Constructions++;
        if (ThrowConstructor)
            throw new InvalidOperationException("expected constructor failure");
    }

    public static CSharpCompilation Rewrite(CSharpCompilation compilation, MyApiTarget target)
    {
        Rewrites++;
        if (ThrowRewrite)
            throw new InvalidOperationException("expected rewrite failure");
        return ReturnNull ? null : compilation;
    }

    public void Init(object game) => Initializations++;

    public void Update() { }

    public void Dispose() { }

    public static void Reset()
    {
        Constructions = Initializations = Rewrites = 0;
        ThrowRewrite = ReturnNull = ThrowConstructor = false;
    }
}
