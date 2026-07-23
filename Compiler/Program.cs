using System;
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
            if (!ipc.TryRead(out InitializeCompilerRequest request))
                return 0;

            InitializeCompilerResponse response = Initialize(request);
            ipc.Write(response);

            if (response.Error is not null)
                return 1;

            while (ipc.TryRead(out CompileRequest compileRequest))
            {
                CompileResponse result = Compile(compileRequest);
                ipc.Write(result);
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
        InitializeCompilerResponse response = new();

        try
        {
            LogFile.Init(request.LogFile);

            RoslynReferences.Instance.SetSearchDirectories(request.ProbeDirectories ?? []);
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
