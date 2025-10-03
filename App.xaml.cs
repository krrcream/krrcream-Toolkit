using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.N2NC;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace krrTools;

public partial class App
{
    [DllImport("kernel32.dll")]
    private static extern bool AllocConsole();

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        AllocConsole();

        try
        {
            // 设置控制台编码为UTF-8以支持中文输出
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.InputEncoding = Encoding.UTF8;

            Console.WriteLine("应用启动开始");

            var services = new ServiceCollection();

            // 注册日志服务
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // 注册选项服务
            services.AddSingleton(BaseOptionsManager.LoadOptions<N2NCOptions>(ConverterEnum.N2NC) ?? new N2NCOptions());
            services.AddSingleton(BaseOptionsManager.LoadOptions<DPToolOptions>(ConverterEnum.DP) ?? new DPToolOptions());
            services.AddSingleton(BaseOptionsManager.LoadOptions<KRRLNTransformerOptions>(ConverterEnum.KRRLN) ?? new KRRLNTransformerOptions());

            // 注册模块管理器
            services.AddSingleton<IModuleManager, ModuleManager>();

            // 注册模块
            services.AddSingleton<IToolModule, N2NCModule>();
            services.AddSingleton<IToolModule, DPToolModule>();
            services.AddSingleton<IToolModule, KRRLNTransformerModule>();

            Services = services.BuildServiceProvider();

            // 初始化全局Logger
            var logger = Services.GetRequiredService<ILogger<App>>();
            Logger.Initialize(logger);

            Console.WriteLine("[INFO] 应用启动完成");

            base.OnStartup(e);

            var main = new MainWindow();
            main.Show();
            Console.WriteLine("[INFO] 主窗口显示完成");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"应用启动失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            MessageBox.Show($"应用启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}