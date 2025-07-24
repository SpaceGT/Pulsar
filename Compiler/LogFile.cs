using System.IO;
using NLog;
using NLog.Config;
using NLog.Layouts;

namespace Pulsar.Compiler
{
    internal static class LogFile
    {
        private const string fileName = "compiler.log";
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
                if (level == null)
                    level = LogLevel.Info;
                logger?.Log(level, text);
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
