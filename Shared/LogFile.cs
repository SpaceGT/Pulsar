using System.IO;
using NLog;
using NLog.Config;
using NLog.Layouts;
using Pulsar.Shared.Config;

namespace Pulsar.Shared
{
    public static class LogFile
    {
        private const string fileName = "puslar.log";
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
            var writeGameLog = ConfigManager.Instance.Dependencies.WriteGameLog;

            try
            {
                if (level == null)
                    level = LogLevel.Info;
                logger?.Log(level, text);
                if (gameLog)
                    writeGameLog($"[Pulsar] [{level.Name}] {text}");
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
