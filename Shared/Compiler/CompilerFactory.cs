using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pulsar.Protocol;
using Pulsar.Protocol.Compiler;

namespace Pulsar.Compiler;

public class CompilerFactory(
    string compilerPath,
    string[] references,
    string[] probeDirectories,
    string logDirectory,
    string runtimeFlag
) : ICompilerFactory
{
    private const int ExitTimeout = 2000;
    private const int RequestTimeout = 5 * 60 * 1000;

    private readonly object processLock = new();

    private Process process;
    private IpcStream ipc;
    private Task<string> errorOutput;

    public void Init()
    {
        lock (processLock)
        {
            if (process is not null)
            {
                if (!process.HasExited)
                    return;

                Stop();
            }

            if (!File.Exists(compilerPath))
                throw new FileNotFoundException("Unable to find the Pulsar compiler.", compilerPath);

            ProcessStartInfo startInfo = new()
            {
                WorkingDirectory = Path.GetDirectoryName(compilerPath),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                startInfo.FileName = compilerPath;
            else
            {
                startInfo.FileName = "mono";
                startInfo.Arguments = $"\"{compilerPath.Replace("\"", "\\\"")}\"";
            }

            try
            {
                process = new Process { StartInfo = startInfo };
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start the Pulsar compiler.");

                errorOutput = process.StandardError.ReadToEndAsync();
                ipc = new IpcStream(
                    process.StandardOutput.BaseStream,
                    process.StandardInput.BaseStream
                );

                InitializeCompilerRequest request = new()
                {
                    Version = CompilerProtocol.Version,
                    References = references,
                    ProbeDirectories = probeDirectories,
                    LogDirectory = logDirectory,
                    UseFrameworkReferences = runtimeFlag == "NETFRAMEWORK",
                };

                IpcMessage message = Exchange(CompilerProtocol.Initialize, request);
                if (message.Type != CompilerProtocol.Ready)
                    throw new InvalidDataException("The compiler returned an unexpected response.");

                InitializeCompilerResponse response =
                    message.GetData<InitializeCompilerResponse>();

                if (response.Version != CompilerProtocol.Version)
                    throw new InvalidDataException(
                        $"Unsupported compiler protocol version: {response.Version}"
                    );

                if (!string.IsNullOrWhiteSpace(response.Error))
                    throw new InvalidOperationException(response.Error);
            }
            catch (Exception e)
            {
                string error = Stop();
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException(
                        $"The compiler process failed:\n{error}",
                        e
                    );

                throw;
            }
        }
    }

    public ICompiler Create(bool debugBuild = false)
    {
        Init();
        string[] flags = debugBuild
            ? [runtimeFlag, "TRACE", "DEBUG"]
            : [runtimeFlag, "TRACE"];

        return new CompilerClient(this, debugBuild, flags);
    }

    public static string[] GetRuntimeDirectories()
    {
        string assemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(assemblies))
            return [RuntimeEnvironment.GetRuntimeDirectory()];

        return
        [
            .. assemblies
                .Split(Path.PathSeparator)
                .Select(Path.GetDirectoryName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(),
        ];
    }

    internal CompileResponse Compile(CompileRequest request)
    {
        lock (processLock)
        {
            try
            {
                if (process is null || process.HasExited)
                    throw new InvalidOperationException("The compiler process is not running.");

                IpcMessage message = Exchange(CompilerProtocol.Compile, request);
                if (message.Type != CompilerProtocol.Result)
                    throw new InvalidDataException("The compiler returned an unexpected response.");

                return message.GetData<CompileResponse>();
            }
            catch (Exception e)
            {
                string error = Stop();
                if (!string.IsNullOrWhiteSpace(error))
                    throw new InvalidOperationException(
                        $"The compiler process failed:\n{error}",
                        e
                    );

                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (processLock)
            Stop();
    }

    private string Stop()
    {
        if (process is null)
            return null;

        try
        {
            process.StandardInput.Close();
        }
        catch { }

        bool exited = false;
        try
        {
            exited = process.HasExited || process.WaitForExit(ExitTimeout);
            if (!exited)
            {
                process.Kill();
                exited = process.WaitForExit(ExitTimeout);
            }
        }
        catch { }

        string error = null;
        try
        {
            if (
                errorOutput is not null
                && (errorOutput.IsCompleted || (exited && errorOutput.Wait(ExitTimeout)))
            )
                error = errorOutput.GetAwaiter().GetResult();
        }
        catch { }

        process.Dispose();
        process = null;
        ipc = null;
        errorOutput = null;

        return error;
    }

    private IpcMessage Exchange<T>(int type, T value)
    {
        IpcStream connection = ipc;
        Task<IpcMessage> exchange = Task.Run(() =>
        {
            connection.Write(type, value);
            return connection.Read();
        });

        Task completed = Task.WhenAny(exchange, Task.Delay(RequestTimeout))
            .GetAwaiter()
            .GetResult();

        if (completed != exchange)
        {
            string error = Stop();
            string message = "The compiler process timed out.";
            if (!string.IsNullOrWhiteSpace(error))
                message += "\n" + error;

            throw new TimeoutException(message);
        }

        return exchange.GetAwaiter().GetResult();
    }
}

file class CompilerClient(CompilerFactory factory, bool debugBuild, string[] flags) : ICompiler
{
    private readonly List<SourceFile> source = [];
    private readonly List<string> references = [];

    public void Load(Stream s, string name, string embedFile = null)
    {
        using MemoryStream stream = new();
        s.CopyTo(stream);

        source.Add(
            new SourceFile
            {
                Name = name,
                Data = stream.ToArray(),
                EmbedFile = embedFile,
            }
        );
    }

    public byte[] Compile(string assemblyName, out byte[] symbols)
    {
        CompileRequest request = new()
        {
            AssemblyName = assemblyName,
            DebugBuild = debugBuild,
            Flags = flags,
            Sources = [.. source],
            References = [.. references],
        };

        CompileResponse response = factory.Compile(request);
        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new InvalidOperationException(response.Error);

        if (!response.Success)
        {
            IEnumerable<Exception> errors = response.Diagnostics.Select(diagnostic =>
            {
                string message = $"{diagnostic.Id}: {diagnostic.Message}";
                if (diagnostic.Source is not null)
                    message +=
                        $" in file: {diagnostic.Source} ({diagnostic.Line},{diagnostic.Column})";

                return new Exception(message);
            });

            throw new AggregateException("Compilation failed!", errors);
        }

        if (response.Assembly is null)
            throw new InvalidDataException("The compiler returned no assembly.");

        symbols = response.Symbols;
        return response.Assembly;
    }

    public void TryAddDependency(string dll)
    {
        if (
            Path.HasExtension(dll)
            && Path.GetExtension(dll).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && File.Exists(dll)
        )
            references.Add(dll);
    }
}
