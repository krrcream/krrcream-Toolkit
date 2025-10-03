using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using krrTools.Configuration;
using krrTools.Data;
using krrTools.Localization;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Utilities
{
    public class FileDispatcher
    {
        public void ConvertFiles(string[] paths, string activeTabTag) //上游进行了空路径过滤
        {
            var startTime = DateTime.Now;
            Console.WriteLine($"[INFO] 开始转换 - 调用模块: {activeTabTag}, 使用活动设置, 文件数量: {paths.Length}");
            // 尝试获取主窗口中的转换器实例
            var mainWindow = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            object? conv = null;

            if (mainWindow != null)
            {
                conv = activeTabTag switch
                {
                    nameof(ConverterEnum.N2NC) => mainWindow.ConvWindowInstance,
                    nameof(ConverterEnum.DP) => mainWindow.DpToolWindowInstance,
                    nameof(ConverterEnum.KRRLN) => mainWindow.KrrlnTransformerInstance,
                    _ => null
                };
            }

            if (conv == null)
            {
                // 如果主窗口不可用或未找到实例，则使用反射创建实例
                var controlType = BaseOptionsManager.GetControlType(activeTabTag);
                if (controlType != null)
                {
                    try
                    {
                        conv = Activator.CreateInstance(controlType);
                        Console.WriteLine($"[ERROR] 使用反射创建{activeTabTag}实例 - 选项可能未正确加载");
                    }
                    catch (Exception)
                    {
                        Console.WriteLine($"[ERROR] 创建控件实例失败: {activeTabTag}");
                    }
                }
                else
                {
                    Console.WriteLine($"[ERROR] 未找到工具{activeTabTag}对应的控件类型");
                }
            }

            var created = new ConcurrentBag<string>();
            var failed = new ConcurrentBag<string>();

            // 并行处理每个文件
            Parallel.ForEach(paths.Where(p => !string.IsNullOrEmpty(p)), p =>
            {
                try
                {
                    var beatmap = (conv as dynamic)?.ProcessSingleFile(p);
                    if (beatmap != null)
                    {
                        var outputFileName = (conv as dynamic)?.GetOutputFileName(p, beatmap);
                        var outputPath = Path.Combine(Path.GetDirectoryName(p) ?? "", outputFileName);

                        if (BeatmapOutputHelper.SaveBeatmapToFile(beatmap, outputPath))
                        {
                            created.Add(outputPath);
                        }
                        else
                        {
                            failed.Add(p);
                        }
                    }
                    else
                    {
                        failed.Add(p);
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"[ERROR] 并行转换文件失败: {p}");
                    failed.Add(p);
                }
            });

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