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
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Utilities
{
    public class FileDispatcher
    {
        private readonly BeatmapTransformationService _transformationService;

        // 进度更新委托
        public Action<int, int, string>? UpdateProgress { get; set; }

        // 消息显示委托
        public Action<string, string>? ShowMessage { get; set; }

        public FileDispatcher()
        {
            // 初始化转换服务，传入活动模块接口
            if (App.Services.GetService(typeof(IModuleManager)) is IModuleManager moduleManager)
            {
                _transformationService = new BeatmapTransformationService(moduleManager);
            }
            else
            {
                throw new InvalidOperationException("IModuleManager not found");
            }
        }

        public ConverterEnum ActiveTabTag { get; set; }

        public void ConvertFiles(string[] paths)
        {
            ConvertWithResults(paths, ActiveTabTag);
        }

        private void ConvertWithResults(string[] paths, ConverterEnum activeTabTag)
        {
            var startTime = DateTime.Now;
            Console.WriteLine($"[INFO] 开始转换 - 调用模块: {activeTabTag}, 使用活动设置, 文件数量: {paths.Length}");

            var created = new ConcurrentBag<string>();
            var failed = new ConcurrentBag<string>();

            int processedCount = 0;

            // 并行处理每个文件，记录数量
            Parallel.ForEach(paths.Where(p => !string.IsNullOrEmpty(p)), p =>
            {
                try
                {
                    var outputPath = _transformationService.TransformAndSaveBeatmap(p, activeTabTag);
                    if (outputPath != null)
                    {
                        created.Add(outputPath);
                    }
                    else
                    {
                        failed.Add(p);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 并行转换文件失败: {p}\n{ex}");
                    failed.Add(p);
                }
                finally
                {
                    // 更新进度
                    int current = Interlocked.Increment(ref processedCount);
                    UpdateProgress?.Invoke(current, paths.Length, string.Empty);
                }
            });

            if (created.Count > 0)
            {
                try
                {
                    Console.WriteLine($"[INFO] 转换器: {activeTabTag}, 生成文件数量: {created.Count}");

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 广播已转换文件失败: {ex.Message}");
                }
            }

            var duration = DateTime.Now - startTime;
            Console.WriteLine($"[INFO] 转换器: {activeTabTag}, 成功: {created.Count}, 失败: {failed.Count}, 用时: {duration.TotalSeconds:F4}s");

            ShowConversionResult(created.ToList(), failed.ToList());
        }

        private void ShowConversionResult(List<string> created, List<string> failed)
        {
            string message;
            string title;

            if (created.Count > 0)
            {
                // 转换成功
                title = "转换成功";

                if (created.Count == 1)
                {
                    message = $"转换成功！\n\n生成的文件：{created[0]}";
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"成功转换 {created.Count} 个文件：");
                    foreach (var file in created)
                        sb.AppendLine($"• {Path.GetFileName(file)}");

                    if (failed.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"失败 {failed.Count} 个文件：");
                        foreach (var file in failed)
                            sb.AppendLine($"• {Path.GetFileName(file)}");
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
            {
                ShowMessage(title, message);
            }
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