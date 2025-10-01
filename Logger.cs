using Microsoft.Extensions.Logging;

namespace krrTools
{
    public static class Logger
    {
        private static ILogger? _logger;

        public static void Initialize(ILogger logger)
        {
            _logger = logger;
        }

        public static void Log(LogLevel level, string message, params object[] args)
        {
            _logger?.Log(level, message, args);
        }
    }
}