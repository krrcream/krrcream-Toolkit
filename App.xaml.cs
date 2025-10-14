using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
using krrTools.Tools.N2NC;
using krrTools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace krrTools
{
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

                var services = new ServiceCollection();

                // 注册日志服务
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });

                // 注册事件总线
                services.AddSingleton<IEventBus, EventBus>();

                // 注册状态栏管理器
                services.AddSingleton<StateBarManager>();

                // 注册选项服务 - 已迁移到 ReactiveOptions
                services.AddSingleton(sp => new ReactiveOptions<N2NCOptions>(ConverterEnum.N2NC, sp.GetRequiredService<IEventBus>()));
                services.AddSingleton(sp => new ReactiveOptions<DPToolOptions>(ConverterEnum.DP, sp.GetRequiredService<IEventBus>()));
                services.AddSingleton(sp => new ReactiveOptions<KRRLNTransformerOptions>(ConverterEnum.KRRLN, sp.GetRequiredService<IEventBus>()));

                // 注册模块管理器
                services.AddSingleton<IModuleManager, ModuleManager>();

                // 注册模块
                services.AddSingleton<IToolModule, N2NCModule>();
                services.AddSingleton<IToolModule, DPToolModule>();
                services.AddSingleton<IToolModule, KRRLNTransformerModule>();

                // 注册主窗口
                services.AddTransient(sp => new MainWindow(sp.GetRequiredService<IModuleManager>()));

                Services = services.BuildServiceProvider();

                // 初始化全局Logger
                var logger = Services.GetRequiredService<ILogger<App>>();
                Logger.Initialize(logger);

                // 设置BaseOptionsManager的EventBus引用
                var eventBus = Services.GetRequiredService<IEventBus>();
                BaseOptionsManager.SetEventBus(eventBus);

                base.OnStartup(e);

                var main = Services.GetRequiredService<MainWindow>();
                main.Show();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"应用启动失败: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                MessageBox.Show($"应用启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}