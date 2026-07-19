using Microsoft.Extensions.Logging;
using ZLogger;
using ZLogger.Providers;

namespace TRPG.Client;

internal static class ClientLogging
{
    public static ILoggerFactory CreateLoggerFactory(string logDirectory = "logs")
    {
        Directory.CreateDirectory(logDirectory);

        return LoggerFactory.Create(builder =>
        {
            builder.AddZLoggerRollingFile(options =>
            {
                options.FilePathSelector = (timestamp, sequence) =>
                    Path.Combine(
                        logDirectory,
                        $"trpg-client_{timestamp.LocalDateTime:yyyyMMdd}_{sequence:000}.log"
                    );
                options.RollingInterval = RollingInterval.Day;
                options.UsePlainTextFormatter(formatter =>
                {
                    formatter.SetPrefixFormatter(
                        $"{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}: ",
                        (in template, in info) =>
                            template.Format(info.Timestamp.Local, info.LogLevel, info.Category)
                    );
                });
            });
        });
    }
}
