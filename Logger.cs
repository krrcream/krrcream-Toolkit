using Microsoft.Extensions.Logging;

namespace krrTools
{
    public static class Logger
    {
        private static ILogger? _logger;
        private static bool _consoleOutputEnabled = true;

        public static void Initialize(ILogger? logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 启用或禁用控制台输出。在单元测试中可以禁用以减少日志噪音。
        /// </summary>
        public static void SetConsoleOutputEnabled(bool enabled)
        {
            _consoleOutputEnabled = enabled;
        }
        
        /// <summary>
        /// Logger.WriteLine(LogLevel.Information, "xxxx{0}, {1}", a, b);
        /// </summary>
        /// <param name="level">日志等级</param>
        /// <param name="message">信息</param>
        /// <param name="args">lamda变量字段</param>
        public static void WriteLine(LogLevel level, string message, params object[] args)
        {
            _logger?.Log(level, message, args);

            // 如果有外部logger，就不输出到控制台，避免双重输出
            // if (_logger != null || !_consoleOutputEnabled)
            //     return;

            ConsoleColor color = level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.Gray,
            };

            string formatted = args.Length > 0 ? string.Format(message, args) : message;

            string levelString = level switch
            {
                LogLevel.Debug => "Debug",
                LogLevel.Information => "Info",
                LogLevel.Warning => "Warning",
                LogLevel.Error => "Error",
                LogLevel.Critical => "Critical",
                _ => "unkn",
            };

            Console.ForegroundColor = color;
            Console.WriteLine($"{levelString}: {formatted}");
            Console.ResetColor();
        }
    }
}