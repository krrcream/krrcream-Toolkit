using System.Windows;
using Microsoft.Extensions.Logging;

namespace krrTools;

public static class LoggerFactoryHolder
{
    public static ILoggerFactory Factory { get; } = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    public static ILogger<T> CreateLogger<T>() => Factory.CreateLogger<T>();
}

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var main = new MainWindow();
        main.Show();
    }
}