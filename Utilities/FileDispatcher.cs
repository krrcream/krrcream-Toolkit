using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Utilities
{
    public class FileDispatcher
    {
        private readonly BeatmapTransformationService _transformationService;
        private readonly SemaphoreSlim _ioSemaphore = new SemaphoreSlim(4); // 限制并发I/O操作数量

        private const int max_show = 5;

        // 进度更新委托
        public Action<int, int, string>? UpdateProgress { get; set; }

        // 消息显示委托
        public Action<string, string>? ShowMessage { get; set; }

        public FileDispatcher()
        {
            // 初始化转换服务，传入活动模块接口
            if (App.Services.GetService(typeof(IModuleManager)) is IModuleManager moduleManager)
                _transformationService = new BeatmapTransformationService(moduleManager);
            else
                throw new InvalidOperationException("IModuleManager not found");
        }

        // 构造函数重载，用于测试
        public FileDispatcher(IModuleManager moduleManager)
        {
            _transformationService = new BeatmapTransformationService(moduleManager);
        }

        public ConverterEnum ActiveTabTag { get; set; }

        public void ConvertFiles(string[] paths)
        {
            // 传入的文件路径列表已经过预处理，确保都是有效的.osu文件路径
            // 解析后跳过 0 note 文件
            ConvertWithResults(paths, ActiveTabTag);
        }

        private void ConvertWithResults(string[] paths, ConverterEnum activeTabTag)
        {
            DateTime startTime = DateTime.Now;
            Logger.WriteLine(LogLevel.Debug, "[FileDispatcher] 开始转换 - 调用模块: {0}, 使用活动设置, 文件数量: {1}", activeTabTag, paths.Length);

            var created = new ConcurrentBag<string>();
            var failed = new ConcurrentBag<string>();

            int processedCount = 0;

            // 并行处理每个文件，记录数量，限制并行度为4（I/O密集型）
            Parallel.ForEach(paths, new ParallelOptions { MaxDegreeOfParallelism = 4 },
                             p =>
                             {
                                 _ioSemaphore.Wait();

                                 try
                                 {
                                     string? outputPath = _transformationService.TransformAndSaveBeatmap(p, activeTabTag);
                                     if (outputPath != null)
                                         created.Add(outputPath);
                                     else
                                         failed.Add(p);
                                 }
                                 catch (Exception ex)
                                 {
                                     Logger.WriteLine(LogLevel.Error, "[FileDispatcher] File: {0} convert fail: {1}", p, ex);
                                     failed.Add(p);
                                 }
                                 finally
                                 {
                                     _ioSemaphore.Release();
                                     // 更新进度
                                     int current = Interlocked.Increment(ref processedCount);
                                     UpdateProgress?.Invoke(current, paths.Length, string.Empty);
                                 }
                             });

            if (created.Count > 0)
            {
                try
                {
                    Logger.WriteLine(LogLevel.Information, "[FileDispatcher] Tool: {0}, created: {1}", activeTabTag, created.Count);
                }
                catch (Exception ex)
                {
                    Logger.WriteLine(LogLevel.Error, "[FileDispatcher] 广播已转换文件失败: {0}", ex.Message);
                }
            }

            TimeSpan duration = DateTime.Now - startTime;
            Logger.WriteLine(LogLevel.Information, "[FileDispatcher] Tool: {0}, success: {1}, fail: {2}, 用时: {3:F4}s", activeTabTag, created.Count, failed.Count, duration.TotalSeconds);

            ShowConversionResult(created.ToList(), failed.ToList());
        }

        private void ShowConversionResult(List<string> created, List<string> failed)
        {
            string message;
            string title;

            if (created.Count > 0)
            {
                // 转换成功
                title = "The conversion was successful";

                if (created.Count == 1)
                    message = $"\n\nCreated File: {created[0]}";
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"Successfully converted {created.Count} files:");

                    for (int i = 0; i < Math.Min(created.Count, max_show); i++)
                        sb.AppendLine($"• {Path.GetFileName(created[i])}");
                    if (created.Count > max_show)
                        sb.AppendLine($"... and {created.Count - max_show} more files");

                    if (failed.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"Failed {failed.Count} files:");
                        for (int i = 0; i < Math.Min(failed.Count, max_show); i++)
                            sb.AppendLine($"• {Path.GetFileName(failed[i])}");
                        if (failed.Count > max_show)
                            sb.AppendLine($"... and {failed.Count - max_show} more files");
                    }

                    message = sb.ToString();
                }
            }
            else
            {
                title = "转换失败";
                message = failed.Count > 0
                              ? Strings.ConversionFailedAllFiles.Localize()
                              : Strings.ConversionNoOutput.Localize();
            }

            // 使用Snackbar显示消息
            if (ShowMessage != null)
                ShowMessage(title, message);
            else
            {
                // 降级到MessageBox
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 重置进度状态
            UpdateProgress?.Invoke(0, 100, string.Empty);
        }
    }
}
