using System.Diagnostics;
using System.IO;
using NLog;
using NLog.Config;
using NLog.Layouts;

namespace Pulsar.Shared;

public interface IGameLog
{
    bool Open();
    bool Exists();
    void Write(string line);
}

public static class LogFile
{
    public static IGameLog GameLog = null;

    private const string fileName = "info.log";
    private static Logger logger;
    private static LogFactory logFactory;
    private static string file;

    public static void Init(string mainPath)
    {
        file = Path.Combine(mainPath, fileName);
        LoggingConfiguration config = new();
        config.AddRuleForAllLevels(
            new NLog.Targets.FileTarget()
            {
                DeleteOldFileOnStartup = true,
                ReplaceFileContentsOnEachWrite = false,
                FileName = file,
                KeepFileOpen = false,
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

    public static void Open()
    {
        if (file is not null)
            Process.Start(file);
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
