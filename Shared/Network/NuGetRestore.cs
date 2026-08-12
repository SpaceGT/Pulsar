using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.RuntimeModel;
using NuGet.Versioning;
using Pulsar.Shared.Config;

namespace Pulsar.Shared.Network;

internal static class NuGetRestore
{
    private const string Source = "https://api.nuget.org/v3/index.json";
    internal const string PluginFileName = "plugin.dll";

#if NETFRAMEWORK
    private static readonly NuGetFramework framework = NuGetFramework.Parse("net48");
#else
    private static readonly NuGetFramework framework = NuGetFramework.Parse(
        Tools.IsWindows() ? "net10.0-windows7.0" : "net10.0"
    );
#endif

    private static readonly ILogger logger = NullLogger.Instance;

    public static NuGetRestoreResult Run(NuGetPackageList packageList)
    {
        return Task.Run(() => RunAsync(packageList)).GetAwaiter().GetResult();
    }

    private static async Task<NuGetRestoreResult> RunAsync(NuGetPackageList packageList)
    {
        Dictionary<string, PackageIdentity> packages = GetPackages(packageList);
        if (packages.Count == 0)
            return new NuGetRestoreResult { CompileFiles = [], RuntimeFiles = [] };

        ISettings settings = Settings.LoadDefaultSettings(root: null);
        string dataDir = Path.GetDirectoryName(ConfigManager.Instance.PulsarDir);
        string packageFolder = Path.Combine(dataDir, "NuGet");
        PackageSpec project = CreateProject(packages.Values);
        LockFile lockFile = await RestoreAsync(project, settings, packageFolder);

        return CreateResult(lockFile, packageFolder);
    }

    private static Dictionary<string, PackageIdentity> GetPackages(NuGetPackageList packageList)
    {
        Dictionary<string, PackageIdentity> packages = new(StringComparer.OrdinalIgnoreCase);

        foreach (NuGetPackageId package in packageList.PackageIds ?? [])
        {
            if (!package.TryGetIdentity(out PackageIdentity identity))
                continue;

            if (identity.Id.Equals("Lib.Harmony", StringComparison.OrdinalIgnoreCase))
                continue;

            if (
                packages.TryGetValue(identity.Id, out PackageIdentity existing)
                && existing.Version != identity.Version
            )
                throw new InvalidDataException(
                    $"Package {identity.Id} is declared with both {existing.Version} and {identity.Version}."
                );

            packages[identity.Id] = identity;
        }

        return packages;
    }

    private static PackageSpec CreateProject(IEnumerable<PackageIdentity> packages)
    {
        string projectPath = Path.Combine(ConfigManager.Instance.PulsarDir, "Pulsar.Plugin.csproj");
        LibraryDependency[] deps =
        [
            .. packages.Select(CreateDependency),
            CreateHarmonyDependency(),
        ];

        TargetFrameworkInformation target = new()
        {
            FrameworkName = framework,
            Dependencies = deps,
        };

        // Dummy metadata is used to keep NuGet happy
        PackageSpec project = new([target])
        {
            Name = "Pulsar.Plugin",
            FilePath = projectPath,
            RuntimeGraph = CreateRuntimeGraph(),
            RestoreMetadata = new(),
        };

        return project;
    }

    private static async Task<LockFile> RestoreAsync(
        PackageSpec project,
        ISettings settings,
        string packageFolder
    )
    {
        using SourceCacheContext cache = new();
        RestoreRequest request = CreateRestoreRequest(project, settings, packageFolder, cache);
        RestoreResult result = await new RestoreCommand(request).ExecuteAsync();

        if (result.Success)
            return result.LockFile;

        foreach (IAssetsLogMessage message in result.LockFile.LogMessages)
            LogFile.WriteLine($"[NuGet] {message.Message}");

        throw new InvalidOperationException("NuGet restore failed.");
    }

    private static RestoreRequest CreateRestoreRequest(
        PackageSpec project,
        ISettings settings,
        string packageFolder,
        SourceCacheContext cache
    )
    {
        SourceRepository repository = Repository.Factory.GetCoreV3(Source);
        LocalPackageFileCache fileCache = new();
        var providers = RestoreCommandProviders.Create(
            packageFolder,
            [],
            [repository],
            cache,
            fileCache,
            logger
        );

        ClientPolicyContext policy = ClientPolicyContext.GetClientPolicy(settings, logger);
        Dictionary<string, IReadOnlyList<string>> mappings = [];
        PackageSourceMapping mapping = new(mappings);
        LockFileBuilderCache builderCache = new();

        RestoreRequest request = new(
            project,
            providers,
            cache,
            policy,
            mapping,
            logger,
            builderCache
        )
        {
            ProjectStyle = ProjectStyle.Standalone,
            RequestedRuntimes = { Tools.RuntimeIdentifier },
        };

        return request;
    }

    private static NuGetRestoreResult CreateResult(LockFile lockFile, string packageFolder)
    {
        LockFileTarget compileTarget = lockFile.GetTarget(framework, null);
        LockFileTarget runtimeTarget = lockFile.GetTarget(framework, Tools.RuntimeIdentifier);

        if (compileTarget is null || runtimeTarget is null)
            throw new InvalidDataException(
                $"NuGet restore did not produce {framework}/{Tools.RuntimeIdentifier} targets."
            );

        return new NuGetRestoreResult
        {
            CompileFiles = GetCompileFiles(lockFile, compileTarget, packageFolder),
            RuntimeFiles = GetRuntimeFiles(lockFile, runtimeTarget, packageFolder),
        };
    }

    private static string[] GetCompileFiles(
        LockFile lockFile,
        LockFileTarget target,
        string packageFolder
    )
    {
        List<string> files = [];
        foreach (LockFileTargetLibrary library in target.Libraries)
        {
            string root = GetPackageRoot(lockFile, library, packageFolder);
            foreach (LockFileItem item in library.CompileTimeAssemblies)
            {
                string file = FindPackageFile(root, item.Path);
                if (file is not null)
                    files.Add(file);
            }
        }
        return [.. files];
    }

    private static NuGetRestoreFile[] GetRuntimeFiles(
        LockFile lockFile,
        LockFileTarget target,
        string packageFolder
    )
    {
        List<NuGetRestoreFile> runtimeFiles = [];
        foreach (LockFileTargetLibrary library in target.Libraries)
        {
            string root = GetPackageRoot(lockFile, library, packageFolder);

            foreach (LockFileItem item in library.RuntimeAssemblies)
            {
                AddRuntimeFile(runtimeFiles, root, item.Path, Path.GetFileName(item.Path));
                if (item.Properties.TryGetValue("related", out string related))
                {
                    var ext = related.Split([';'], StringSplitOptions.RemoveEmptyEntries);
                    foreach (string extension in ext)
                    {
                        string path = Path.ChangeExtension(item.Path, extension.Trim());
                        AddRuntimeFile(runtimeFiles, root, path, Path.GetFileName(path));
                    }
                }
            }

            foreach (LockFileItem item in library.ResourceAssemblies)
            {
                item.Properties.TryGetValue("locale", out string locale);
                locale ??= Path.GetFileName(Path.GetDirectoryName(item.Path));
                AddRuntimeFile(
                    runtimeFiles,
                    root,
                    item.Path,
                    Path.Combine(locale, Path.GetFileName(item.Path))
                );
            }

            foreach (LockFileItem item in library.NativeLibraries)
                AddRuntimeFile(runtimeFiles, root, item.Path, Path.GetFileName(item.Path));
        }

        StringComparer pathComparer = Tools.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        return [.. runtimeFiles.GroupBy(x => x.OutputPath, pathComparer).Select(x => x.First())];
    }

    private static string GetPackageRoot(
        LockFile lockFile,
        LockFileTargetLibrary targetLibrary,
        string packageFolder
    )
    {
        LockFileLibrary library = lockFile.GetLibrary(targetLibrary.Name, targetLibrary.Version);
        if (library is null)
        {
            string package = $"{targetLibrary.Name} {targetLibrary.Version}";
            throw new DirectoryNotFoundException($"Restored package {package} was not found.");
        }

        string packagePath = library.Path.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(packageFolder, packagePath));
    }

    private static string FindPackageFile(string packageRoot, string packagePath)
    {
        if (Path.GetFileName(packagePath) == PackagingCoreConstants.EmptyFolder)
            return null;

        string relativePath = packagePath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(packageRoot, relativePath));

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static void AddRuntimeFile(
        List<NuGetRestoreFile> runtimeFiles,
        string root,
        string packagePath,
        string outputPath
    )
    {
        if (FindPackageFile(root, packagePath) is not string sourcePath)
            return;

        if (!IsValidOutputPath(outputPath))
            throw new InvalidDataException(
                $"Package contains an invalid output path: {outputPath}"
            );

        outputPath = outputPath.Replace(
            Path.AltDirectorySeparatorChar,
            Path.DirectorySeparatorChar
        );

        if (outputPath.Equals(PluginFileName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Package output path {PluginFileName} is reserved.");

        runtimeFiles.Add(new NuGetRestoreFile { OutputPath = outputPath, SourcePath = sourcePath });
    }

    private static bool IsValidOutputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
            return false;

        string[] parts = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);
        return !parts.Contains("..");
    }

    private static LibraryDependency CreateDependency(PackageIdentity package)
    {
        VersionRange versionRange = new(package.Version, true, package.Version, true);
        LibraryRange libraryRange = new(package.Id, versionRange, LibraryDependencyTarget.Package);
        return new LibraryDependency { LibraryRange = libraryRange };
    }

    private static LibraryDependency CreateHarmonyDependency()
    {
        Version version = Version.Parse(ConfigManager.HarmonyVersion);
        NuGetVersion packageVersion = new(version.Major, version.Minor, version.Build);
        PackageIdentity package = new("Lib.Harmony", packageVersion);
        LibraryDependency dependency = CreateDependency(package);

        dependency.IncludeType = LibraryIncludeFlags.None;
        return dependency;
    }

    private static RuntimeGraph CreateRuntimeGraph()
    {
        string runtime = Tools.RuntimeIdentifier;
        string architecture = runtime.Substring(runtime.IndexOf('-') + 1);
        string[] fallbacks = Tools.IsWindows()
            ? ["win", "any"]
            : ["linux", "unix-" + architecture, "unix", "any"];

        RuntimeDescription description = new(runtime, fallbacks);
        return new RuntimeGraph([description]);
    }
}

internal sealed class NuGetRestoreResult
{
    public string[] CompileFiles { get; set; }
    public NuGetRestoreFile[] RuntimeFiles { get; set; }
}

internal sealed class NuGetRestoreFile
{
    public string OutputPath { get; set; }
    public string SourcePath { get; set; }
}
