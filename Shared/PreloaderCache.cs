using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;

namespace Pulsar.Shared;

internal sealed class PreloaderCache
{
    private const string LeaseName = ".lease";
    private static readonly ConcurrentBag<FileStream> RunLeases = new();
    private readonly string cacheDirectory;

    public string RunDirectory { get; }

    public PreloaderCache(string cacheDirectory)
    {
        this.cacheDirectory = cacheDirectory;
        string runs = Path.Combine(cacheDirectory, ".runs");
        Directory.CreateDirectory(runs);
        RemoveInactiveRuns(runs);

        RunDirectory = Path.Combine(runs, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RunDirectory);

        // Assembly.LoadFrom may hold these files until the process exits.
        RunLeases.Add(File.Open(Path.Combine(RunDirectory, LeaseName), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None));
    }

    public bool TryLoad(string assemblyName)
    {
        string runPath = Path.Combine(RunDirectory, assemblyName);
        try
        {
            // Allow publishers to replace the snapshot while this handle still reads it.
            using (var source = File.Open(Path.Combine(cacheDirectory, assemblyName), FileMode.Open,
                FileAccess.Read, FileShare.Read | FileShare.Delete))
            using (var destination = File.Create(runPath))
                source.CopyTo(destination);

            Assembly.LoadFrom(runPath);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            // A missing, locked or invalid snapshot can be rebuilt from the game assembly.
            return false;
        }
    }

    public void Publish(string assemblyName)
    {
        string stage = Path.Combine(RunDirectory, Path.GetRandomFileName());
        string snapshot = Path.Combine(cacheDirectory, assemblyName);
        try
        {
            File.Copy(Path.Combine(RunDirectory, assemblyName), stage, overwrite: true);
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(snapshot))
                        File.Replace(stage, snapshot, null);
                    else
                        File.Move(stage, snapshot);
                    return;
                }
                catch (IOException) when (attempt < 2)
                {
                    // A competing publisher can change whether the destination exists.
                    Thread.Sleep(50);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // An older launcher may still have the shared snapshot mapped. This
            // process has already loaded its own copy, so it can keep starting.
            LogFile.Warn($"Could not update the preloader cache for '{assemblyName}' ({error.HResult:X8}).");
        }
        finally
        {
            TryDeleteFile(stage);
        }
    }

    public void Prune(ICollection<string> targets)
    {
        foreach (string file in Directory.GetFiles(cacheDirectory))
            if (!targets.Contains(Path.GetFileName(file)))
                TryDeleteFile(file);
    }

    private static void RemoveInactiveRuns(string runs)
    {
        foreach (string directory in Directory.GetDirectories(runs))
        {
            try
            {
                // A missing lease means the launcher may still be creating the directory.
                // A released lease can only belong to a process that has exited.
                using (File.Open(Path.Combine(directory, LeaseName), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }

                Directory.Delete(directory, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A live launcher or another cleanup can still own the directory.
            }
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Cache cleanup can wait until a later launch.
        }
    }
}
