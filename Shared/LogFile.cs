using System.IO;
using NLog;
using NLog.Config;
using NLog.Layouts;

namespace Pulsar.Shared
{
    public interface IGameLog
    {
        bool Open();
        bool Exists();
        void Write(string line);
    }

    public static class LogFile
    {
        public static IGameLog GameLog = null;

        private const string fileName = "pulsar.log";
        private static Logger logger;
        private static LogFactory logFactory;

        public static void Init(string mainPath)
        {
            string file = Path.Combine(mainPath, fileName);
            LoggingConfiguration config = new();
            config.AddRuleForAllLevels(
                new NLog.Targets.FileTarget()
                {
                    DeleteOldFileOnStartup = true,
                    FileName = file,
                    Layout = new SimpleLayout(
                        "${longdate} [${level:uppercase=true}] (${threadid}) ${message:withexception=true}"
                    ),
                }
            );
            logFactory = new LogFactory(config) { ThrowExceptions = false };

            try
            {
                logger = logFactory.GetLogger("Pulsar");
            }
            catch
            {
                logger = null;
            }
        }

        public static void Error(string text, bool gameLog = false)
        {
            WriteLine(text, LogLevel.Error, gameLog);
        }

        public static void Warn(string text, bool gameLog = false)
        {
            WriteLine(text, LogLevel.Warn, gameLog);
        }

        public static void WriteLine(string text, LogLevel level = null, bool gameLog = false)
        {
            try
            {
                if (level == null)
                    level = LogLevel.Info;

                logger?.Log(level, text);
                if (gameLog)
                    GameLog.Write($"[Pulsar] [{level.Name}] {text}");
            }
            catch
            {
                Dispose();
            }
        }

        public static void Dispose()
        {
            if (logger == null)
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
}
