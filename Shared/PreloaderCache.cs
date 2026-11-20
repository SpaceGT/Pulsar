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
        var lease = File.Open(Path.Combine(RunDirectory, LeaseName), FileMode.CreateNew,
            FileAccess.ReadWrite, FileShare.None);
        try
        {
            LockLease(lease);
            // Publish the lease only after acquiring it. Until then the file is empty.
            lease.WriteByte(0);
            lease.Flush();
            RunLeases.Add(lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
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
                string path = Path.Combine(directory, LeaseName);
                if (new FileInfo(path).Length == 0)
                    continue;

                using (var lease = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    LockLease(lease);

                Directory.Delete(directory, recursive: true);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A live launcher or another cleanup can still own the directory.
            }
        }
    }

    private static void LockLease(FileStream lease)
    {
        // Mono on Unix uses byte-range locks for cross-process exclusion. Both
        // runtimes must honor those locks when they share a Linux cache.
#if !NETFRAMEWORK
        if (!OperatingSystem.IsLinux())
            return;
#endif
        lease.Lock(0, 1);
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
