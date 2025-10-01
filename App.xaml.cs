
using System;
using System.Windows;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.N2NC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace krrTools;
public static class LoggerFactoryHolder
{
    private static ILoggerFactory Factory { get; } = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    public static ILogger<T> CreateLogger<T>() => Factory.CreateLogger<T>();
}

public partial class App
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();

        // 注册选项服务
        services.AddSingleton(provider => BaseOptionsManager.LoadOptions<N2NCOptions>(ConverterEnum.N2NC) ?? new N2NCOptions());
        services.AddSingleton(provider => BaseOptionsManager.LoadOptions<DPToolOptions>(ConverterEnum.DP) ?? new DPToolOptions());
        services.AddSingleton(provider => BaseOptionsManager.LoadOptions<KRRLNTransformerOptions>(ConverterEnum.KRRLN) ?? new KRRLNTransformerOptions());

        // 注册模块管理器
        services.AddSingleton<IModuleManager, ModuleManager>();

        // 注册模块
        services.AddSingleton<IToolModule, N2NCModule>();
        services.AddSingleton<IToolModule, DPTool>();
        services.AddSingleton<IToolModule, KRRLNModule>();

        // 注册工具调度器
        services.AddSingleton<ToolScheduler>();

        Services = services.BuildServiceProvider();

        // 初始化兼容性层
        var moduleManager = Services.GetRequiredService<IModuleManager>();
        ToolModuleRegistry.Initialize(moduleManager);

        base.OnStartup(e);

        var main = new MainWindow();
        main.Show();
    }
}