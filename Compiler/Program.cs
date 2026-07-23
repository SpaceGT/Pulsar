using System;
using System.IO;
using Pulsar.Protocol;
using Pulsar.Protocol.Compiler;

namespace Pulsar.Compiler;

static class Program
{
    static int Main()
    {
        try
        {
            IpcStream ipc = new(Console.OpenStandardInput(), Console.OpenStandardOutput());
            if (!ipc.TryRead(out IpcMessage message))
                return 0;

            if (message.Type != CompilerProtocol.Initialize)
                throw new InvalidDataException("The compiler was not initialized.");

            InitializeCompilerRequest request = message.GetData<InitializeCompilerRequest>();
            InitializeCompilerResponse response = Initialize(request);
            ipc.Write(CompilerProtocol.Ready, response);

            if (response.Error is not null)
                return 1;

            while (ipc.TryRead(out message))
            {
                CompileResponse result;
                if (message.Type != CompilerProtocol.Compile)
                {
                    result = new CompileResponse
                    {
                        Error = $"Unknown compiler message: {message.Type}",
                    };
                }
                else
                {
                    result = Compile(message.GetData<CompileRequest>());
                }

                ipc.Write(CompilerProtocol.Result, result);
            }

            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
        finally
        {
            LogFile.Dispose();
        }
    }

    private static InitializeCompilerResponse Initialize(InitializeCompilerRequest request)
    {
        InitializeCompilerResponse response = new() { Version = CompilerProtocol.Version };

        try
        {
            if (request.Version != CompilerProtocol.Version)
                throw new InvalidDataException(
                    $"Unsupported compiler protocol version: {request.Version}"
                );

            LogFile.Init(request.LogDirectory);

            RoslynReferences.Instance.SetSearchDirectories(
                request.ProbeDirectories ?? [],
                request.UseFrameworkReferences
            );
            RoslynReferences.Instance.GenerateAssemblyList(request.References ?? []);
        }
        catch (Exception e)
        {
            response.Error = e.ToString();
        }

        return response;
    }

    private static CompileResponse Compile(CompileRequest request)
    {
        try
        {
            return new RoslynCompiler(request).Compile();
        }
        catch (Exception e)
        {
            return new CompileResponse { Error = e.ToString() };
        }
    }
}
