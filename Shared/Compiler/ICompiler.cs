using System;
using System.IO;

namespace Pulsar.Compiler;

public interface ICompilerFactory : IDisposable
{
    void Init();
    ICompiler Create(bool debugBuild = false);
}

public interface ICompiler
{
    void Load(Stream s, string name, string embedFile = null);
    byte[] Compile(string assemblyName, out byte[] symbols);
    void TryAddDependency(string dll);
}
