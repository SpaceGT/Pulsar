namespace Pulsar.Protocol.Compiler;

public static class CompilerProtocol
{
    public const int Version = 1;
    public const int Initialize = 1;
    public const int Ready = 2;
    public const int Compile = 3;
    public const int Result = 4;
}

public class InitializeCompilerRequest
{
    public int Version { get; set; }

    public string[] References { get; set; } = [];

    public string[] ProbeDirectories { get; set; } = [];

    public string LogDirectory { get; set; }

    public bool UseFrameworkReferences { get; set; }
}

public class InitializeCompilerResponse
{
    public int Version { get; set; }

    public string Error { get; set; }
}

public class CompileRequest
{
    public string AssemblyName { get; set; }

    public bool DebugBuild { get; set; }

    public string[] Flags { get; set; } = [];

    public SourceFile[] Sources { get; set; } = [];

    public string[] References { get; set; } = [];
}

public class SourceFile
{
    public string Name { get; set; }

    public byte[] Data { get; set; } = [];

    public string EmbedFile { get; set; }
}

public class CompileResponse
{
    public bool Success { get; set; }

    public byte[] Assembly { get; set; }

    public byte[] Symbols { get; set; }

    public CompilerDiagnostic[] Diagnostics { get; set; } = [];

    public string Error { get; set; }
}

public class CompilerDiagnostic
{
    public string Id { get; set; }

    public string Message { get; set; }

    public string Source { get; set; }

    public int Line { get; set; }

    public int Column { get; set; }
}
