using System.Diagnostics;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Pulsar.Shared.Arguments;
using GamePreloader = Pulsar.Shared.Preloader;

const string target = "SyntheticGame.dll";

if (args.Length != 0)
{
    string root = args[0];
    string name = args[1];
    int patchVersion = int.Parse(args[2]);
    int expectedVersion = int.Parse(args[3]);
    Flags.Current.LazyPreload = args.Length > 4;
    Preloader.Version = patchVersion;
    Preloader.FailWrite = name == "failed-write";
    Preloader.BeforePatch = () =>
    {
        File.WriteAllText(Path.Combine(root, name + ".patching"), "");
        if (name.StartsWith("writer", StringComparison.Ordinal))
            WaitForFile(Path.Combine(root, "start-writers"));
    };

    try
    {
        new GamePreloader([typeof(Preloader).Assembly]).Patch(Path.Combine(root, "game"),
            Path.Combine(root, "cache"));
    }
    catch (ArgumentException error) when (Preloader.FailWrite
        && error.TargetSite?.DeclaringType?.Assembly == typeof(AssemblyDefinition).Assembly)
    {
        return;
    }
    Check(!Preloader.FailWrite, "Cecil accepted a reference from an unrelated module");
    var loaded = AppDomain.CurrentDomain.GetAssemblies().Single(x => x.GetName().Name == "SyntheticGame");
    Check(loaded.GetName().Version.Major == expectedVersion, "wrong assembly loaded");
    using (File.Open(loaded.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        string marker = Path.Combine(root, name + ".loaded");
        File.WriteAllText(marker + ".tmp", loaded.Location);
        File.Move(marker + ".tmp", marker);
        if (name == "writer-a")
            WaitForFile(Path.Combine(root, "release-reader"));
    }
    return;
}

string fixture = Path.Combine(Path.GetTempPath(), "pulsar-preloader-" + Guid.NewGuid().ToString("N"));
string game = Directory.CreateDirectory(Path.Combine(fixture, "game")).FullName;
string cache = Directory.CreateDirectory(Path.Combine(fixture, "cache")).FullName;
string snapshot = Path.Combine(cache, target);
var children = new List<Process>();
try
{
    using (var source = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("SyntheticGame",
        new Version(1, 0)), "SyntheticGame", ModuleKind.Dll))
        source.Write(Path.Combine(game, target));

    WaitForExit(Start("seed", 2, 2));

    // Simulate an older launcher keeping the old flat DLL open on Windows.
    byte[] previousSnapshot = File.ReadAllBytes(snapshot);
    using (File.Open(snapshot, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        WaitForExit(Start("blocked", 3, 3));
        if (Path.DirectorySeparatorChar == '\\')
            Check(File.ReadAllBytes(snapshot).SequenceEqual(previousSnapshot), "blocked update changed the snapshot");
        Check(ReadVersion(snapshot) is 2 or 3, "snapshot became unreadable");
    }

    previousSnapshot = File.ReadAllBytes(snapshot);
    WaitForExit(Start("failed-write", 3, 3));
    Check(File.ReadAllBytes(snapshot).SequenceEqual(previousSnapshot), "failed serialization damaged the snapshot");

    // Both writers start patching together. One keeps its private DLL open while the next launcher starts.
    var writerA = Start("writer-a", 4, 4);
    var writerB = Start("writer-b", 5, 5);
    WaitForFile(Path.Combine(fixture, "writer-a.patching"));
    WaitForFile(Path.Combine(fixture, "writer-b.patching"));
    File.WriteAllText(Path.Combine(fixture, "start-writers"), "");
    WaitForExit(writerB);
    string pathA = ReadLoadedPath("writer-a");
    string pathB = ReadLoadedPath("writer-b");
    Check(pathA != pathB && pathA != snapshot && pathB != snapshot, "launchers shared their load path");
    int cachedVersion = ReadVersion(snapshot);
    Check(cachedVersion is 4 or 5, "writers published an invalid snapshot");

    WaitForExit(Start("lazy", 6, cachedVersion, lazy: true));
    Check(!File.Exists(Path.Combine(fixture, "lazy.patching")), "lazy launch ran the patch");
    Check(File.Exists(pathA) && ReadVersion(pathA) == 4, "a later launch touched the live reader's DLL");
    Check(!Directory.Exists(Path.GetDirectoryName(pathB)), "a later launch left a finished run behind");
    Check(ReadLoadedPath("lazy") != snapshot, "lazy launch bound the shared snapshot");
    File.WriteAllText(Path.Combine(fixture, "release-reader"), "");
    WaitForExit(writerA);

    File.WriteAllText(snapshot, "incomplete old snapshot");
    WaitForExit(Start("recover", 7, 7, lazy: true));
    Check(ReadVersion(snapshot) == 7, "recovery did not leave a usable snapshot");
    Check(ReadVersion(Path.Combine(game, target)) == 1, "preloading modified the game DLL");
    Console.WriteLine("Preloader checks passed");
}
finally
{
    foreach (var child in children)
    {
        if (!child.HasExited)
        {
            child.Kill();
            child.WaitForExit();
        }
        child.Dispose();
    }
    Directory.Delete(fixture, recursive: true);
}

Process Start(string name, int patchVersion, int expectedVersion, bool lazy = false)
{
    using var current = Process.GetCurrentProcess();
    string host = current.MainModule.FileName;
    string hostName = Path.GetFileNameWithoutExtension(host);
    string command = hostName == "dotnet" || hostName.StartsWith("mono", StringComparison.Ordinal)
        ? Quote(Assembly.GetExecutingAssembly().Location) + " "
        : "";
    command += $"{Quote(fixture)} {name} {patchVersion} {expectedVersion}" + (lazy ? " lazy" : "");
    var child = Process.Start(new ProcessStartInfo(host, command) { UseShellExecute = false });
    children.Add(child);
    return child;
}

string ReadLoadedPath(string name)
{
    string marker = Path.Combine(fixture, name + ".loaded");
    WaitForFile(marker);
    return File.ReadAllText(marker);
}

static void WaitForExit(Process child)
{
    Check(child.WaitForExit(10_000), "preloader worker timed out");
    Check(child.ExitCode == 0, $"preloader worker exited with code {child.ExitCode}");
}

static void WaitForFile(string path)
{
    var wait = Stopwatch.StartNew();
    while (!File.Exists(path))
    {
        Check(wait.ElapsedMilliseconds < 10_000, $"timed out waiting for {Path.GetFileName(path)}");
        Thread.Sleep(10);
    }
}

static int ReadVersion(string path)
{
    using var assembly = AssemblyDefinition.ReadAssembly(path);
    return assembly.Name.Version.Major;
}

static string Quote(string argument) => "\"" + argument + "\"";

static void Check(bool condition, string failure)
{
    if (!condition)
        throw new InvalidOperationException(failure);
}

public static class Preloader
{
    public static IEnumerable<string> TargetDLLs { get; } = ["SyntheticGame.dll"];
    public static int Version { get; set; }
    public static bool FailWrite { get; set; }
    public static Action BeforePatch { get; set; }

    public static void Patch(AssemblyDefinition assembly)
    {
        BeforePatch();
        assembly.Name.Version = new Version(Version, 0);
        if (FailWrite)
        {
            var foreignModule = ModuleDefinition.CreateModule("Foreign", ModuleKind.Dll);
            var foreignType = new TypeDefinition("", "ForeignBase", Mono.Cecil.TypeAttributes.Public);
            foreignModule.Types.Add(foreignType);
            // Cecil rejects an IL operand from another module when serializing the body.
            var method = new MethodDefinition("Invalid", Mono.Cecil.MethodAttributes.Static, assembly.MainModule.TypeSystem.Void);
            assembly.MainModule.Types[0].Methods.Add(method);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ldtoken, foreignType));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        }
    }
}
