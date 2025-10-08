using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void ConvertFiles(string[] paths, ConverterEnum activeTabTag)
        {
            ConvertWithResults(paths, activeTabTag);
        }

        private void ConvertWithResults(string[] paths, ConverterEnum activeTabTag)
        {
            var startTime = DateTime.Now;
            Console.WriteLine($"[INFO] 开始转换 - 调用模块: {activeTabTag}, 使用活动设置, 文件数量: {paths.Length}");

            // 使用ModuleManager获取工具实例
            var moduleManager = App.Services.GetService(typeof(IModuleManager)) as IModuleManager;
            if (moduleManager == null)
            {
                Console.WriteLine($"[ERROR] ModuleManager未找到");
                return;
            }

            var tool = moduleManager.GetToolName(nameof(activeTabTag));
            if (tool == null)
            {
                Console.WriteLine($"[ERROR] 未找到工具: {activeTabTag}");
                return;
            }

            var created = new ConcurrentBag<string>();
            var failed = new ConcurrentBag<string>();

            // 并行处理每个文件
            Parallel.ForEach(paths.Where(p => !string.IsNullOrEmpty(p)), p =>
            {
                try
                {
                    var outputPath = tool.ProcessFileSave(p);
                    if (!string.IsNullOrEmpty(outputPath))
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
            });

            if (created.Count > 0)
            {
                try
                {
                    // DualPreviewControl.BroadcastStagedPaths(null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] 广播已转换文件失败: {ex.Message}");
                }
            }

            Console.WriteLine($"[INFO] 转换器: {activeTabTag}, 生成文件数量: {created.Count}");
            var duration = DateTime.Now - startTime;
            Console.WriteLine($"[INFO] 转换器: {activeTabTag}, 成功: {created.Count}, 失败: {failed.Count}, 用时: {duration.TotalSeconds:F4}s");

            ShowConversionResult(created.ToList(), failed.ToList());
        }

        private void ShowConversionResult(List<string> created, List<string> failed)
        {
            string message;
            string title;
            MessageBoxImage icon;

            if (created.Count > 0)
            {
                // 转换成功
                title = "转换成功";
                icon = MessageBoxImage.Information;

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
                icon = MessageBoxImage.Warning;
                message = failed.Count > 0
                    ? Strings.ConversionFailedAllFiles.Localize()
                    : Strings.ConversionNoOutput.Localize();
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }
}