using System;
using Microsoft.Extensions.Logging;

namespace krrTools
{
    public static class Logger
    {
        private static ILogger? _logger;

        public static void Initialize(ILogger? logger)
        {
            _logger = logger;
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

            // Output to console with color in UI thread
            ConsoleColor color = level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.Gray,
            };

            string formatted = args.Length > 0 ? string.Format(message, args) : message;

            Console.ForegroundColor = color;
            Console.WriteLine($"[{level}] {formatted}");
            Console.ResetColor();
        }
    }
}