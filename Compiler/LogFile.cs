using System.IO;
using NLog;
using NLog.Config;
using NLog.Layouts;

namespace Pulsar.Compiler;

public static class LogFile
{
    private const string fileName = "info.log";
    private static Logger logger;
    private static LogFactory logFactory;

    public static void Init(string mainPath)
    {
        string file = Path.Combine(mainPath, fileName);
        LoggingConfiguration config = new();
        config.AddRuleForAllLevels(
            new NLog.Targets.FileTarget()
            {
                DeleteOldFileOnStartup = false,
                ReplaceFileContentsOnEachWrite = false,
                KeepFileOpen = false,
                FileName = file,
                Layout = new SimpleLayout(
                    "${longdate} [${level:uppercase=true}] (${threadid}) ${message:withexception=true}"
                ),
            }
        );
        logFactory = new LogFactory() { ThrowExceptions = false, Configuration = config };

        try
        {
            logger = logFactory.GetLogger("Pulsar");
        }
        catch
        {
            logger = null;
        }
    }

    public static void Error(string text)
    {
        WriteLine(text, LogLevel.Error);
    }

    public static void Warn(string text)
    {
        WriteLine(text, LogLevel.Warn);
    }

    public static void WriteLine(string text, LogLevel level = null)
    {
        try
        {
            level ??= LogLevel.Info;
            logger?.Log(level, text);
        }
        catch
        {
            Dispose();
        }
    }

    public static void Dispose()
    {
        if (logger is null)
            return;

        try
        {
            logFactory.Flush();
            logFactory.Dispose();
        }
        catch { }
        logger = null;
        logFactory = null;
    }
}
