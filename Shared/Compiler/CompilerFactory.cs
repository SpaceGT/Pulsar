using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pulsar.Protocol;
using Pulsar.Protocol.Compiler;
using Pulsar.Shared;

namespace Pulsar.Compiler;

public class CompilerFactory(
    string compilerPath,
    string[] globalReferenceNames,
    string[] probeDirectories,
    string logFile,
    string[] globalFlags = null
) : ICompilerFactory
{
    private const int ExitTimeout = 2000;
    private const int RequestTimeout = 10000;

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
                throw new FileNotFoundException(
                    "Unable to find the Pulsar compiler.",
                    compilerPath
                );

            ProcessStartInfo startInfo = new()
            {
                FileName = compilerPath,
                WorkingDirectory = Path.GetDirectoryName(compilerPath),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

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
                    References = globalReferenceNames,
                    ProbeDirectories = probeDirectories,
                    LogFile = logFile,
                };

                InitializeCompilerResponse response = Exchange<
                    InitializeCompilerRequest,
                    InitializeCompilerResponse
                >(request);

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

    public ICompiler Create(bool debugBuild = false, string[] flags = null)
    {
        Init();

        IEnumerable<string> compilerFlags = ["TRACE"];
        compilerFlags = compilerFlags.Concat(globalFlags ?? []).Concat(flags ?? []);

        if (debugBuild)
            compilerFlags = compilerFlags.Append("DEBUG");

        return new CompilerClient(this, debugBuild, [.. compilerFlags]);
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

                return Exchange<CompileRequest, CompileResponse>(request);
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
        catch (Exception e)
        {
            LogFile.Warn($"Failed to close compiler input: {e}");
        }

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
        catch (Exception e)
        {
            LogFile.Warn($"Failed to stop compiler process: {e}");
        }

        string error = null;
        try
        {
            if (
                errorOutput is not null
                && (errorOutput.IsCompleted || (exited && errorOutput.Wait(ExitTimeout)))
            )
                error = errorOutput.GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            LogFile.Warn($"Failed to read compiler error output: {e}");
        }

        process.Dispose();
        process = null;
        ipc = null;
        errorOutput = null;

        return error;
    }

    private TResponse Exchange<TRequest, TResponse>(TRequest value)
    {
        IpcStream connection = ipc;
        Task<TResponse> exchange = Task.Run(() =>
        {
            connection.Write(value);
            return connection.Read<TResponse>();
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
    private readonly List<SourceFile> sourceFiles = [];
    private readonly List<string> privateReferenceFiles = [];

    public void Load(Stream s, string name, string embedFile = null)
    {
        using MemoryStream stream = new();
        s.CopyTo(stream);

        sourceFiles.Add(
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
            Sources = [.. sourceFiles],
            References = [.. privateReferenceFiles],
        };

        CompileResponse response = factory.Compile(request);
        if (!string.IsNullOrWhiteSpace(response.Error))
            throw new InvalidOperationException(response.Error);

        if (!response.Success)
        {
            IEnumerable<Exception> errors = response.Diagnostics.Select(CreateException);
            throw new AggregateException("Compilation failed!", errors);
        }

        if (response.Assembly is null)
            throw new InvalidDataException("The compiler returned no assembly.");

        symbols = response.Symbols;
        return response.Assembly;
    }

    private static Exception CreateException(CompilerDiagnostic diagnostic)
    {
        string message = $"{diagnostic.Id}: {diagnostic.Message}";
        if (diagnostic.Source is not null)
            message += $" in file: {diagnostic.Source} ({diagnostic.Line},{diagnostic.Column})";

        return new Exception(message);
    }

    public void TryAddDependency(string dll)
    {
        if (
            Path.HasExtension(dll)
            && Path.GetExtension(dll).Equals(".dll", StringComparison.OrdinalIgnoreCase)
            && File.Exists(dll)
        )
            privateReferenceFiles.Add(dll);
    }
}
