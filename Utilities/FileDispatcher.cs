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
using krrTools.Tools.Preview;
using Microsoft.Extensions.Logging;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace krrTools.Utilities
{
    public class FileDispatcher(Dictionary<string, DualPreviewControl> previewControls, TabView mainTabControl)
    {
        private static readonly ILogger<FileDispatcher> _logger = LoggerFactoryHolder.CreateLogger<FileDispatcher>();

        public void LoadFiles(string[]? paths, string? activeTabTag = null)
        {
            activeTabTag ??= GetActiveTabTag();
            if (previewControls.TryGetValue("Global", out var control))
            {
                control.CurrentTool = activeTabTag;
                control.LoadPreview(paths);
                control.StageFiles(paths);
            }
        }

        public void ConvertFiles(string[] paths, string? activeTabTag = null)
        {
            activeTabTag ??= GetActiveTabTag();
            ConvertWithResults(paths, activeTabTag);
        }

        private void ConvertWithResults(string[] paths, string activeTabTag)
        {
            // Try to find existing Control instance from MainWindow
            var mainWindow = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            object? conv = null;

            if (mainWindow != null)
            {
                conv = activeTabTag switch
                {
                    "N2NC" => mainWindow.ConvWindowInstance,
                    "DP" => mainWindow.DPWindowInstance,
                    "KRRLN" => mainWindow.KRRLNTransformerInstance,
                    _ => null
                };
            }

            if (conv == null)
            {
                // Fallback: create new instance using reflection
                var controlType = BaseOptionsManager.GetControlType(activeTabTag);
                if (controlType != null)
                {
                    try
                    {
                        conv = Activator.CreateInstance(controlType);
                        _logger.LogWarning("使用反射创建{Converter}实例 - 选项可能未正确加载", activeTabTag);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "创建控件实例失败: {ToolName}", activeTabTag);
                    }
                }
                else
                {
                    _logger.LogWarning("未找到工具{Converter}对应的控件类型", activeTabTag);
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "转换器处理错误: {Path}", p);
                    failed.Add(p);
                }
            });

            if (created.Count > 0)
            {
                try
                {
                    DualPreviewControl.BroadcastStagedPaths(null);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "BroadcastStagedPaths错误");
                }
            }

            _logger.LogInformation("转换器: {Converter}, 生成文件数量: {CreatedCount}", activeTabTag, created.Count);
            ShowConversionResult(created.ToList(), failed.ToList(), paths.Length);
        }

        private void ShowConversionResult(List<string> created, List<string> failed, int totalFiles)
        {
            string message;
            string title;
            MessageBoxImage icon;

            if (created.Count > 0)
            {
                // 转换成功
                title = "转换成功";
                icon = MessageBoxImage.Information;
                
                if (totalFiles == 1 && created.Count == 1)
                {
                    // 只转换了一个文件且成功
                    message = $"转换成功！\n\n生成的文件：{created[0]}";
                }
                else
                {
                    // 转换了多个文件
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
                // 转换失败
                title = "转换失败";
                icon = MessageBoxImage.Warning;
                message = failed.Count > 0 
                    ? Strings.ConversionFailedAllFiles.Localize()
                    : Strings.ConversionNoOutput.Localize();
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        private string GetActiveTabTag()
        {
            return (mainTabControl.SelectedItem as TabViewItem)?.Tag as string ?? "N2NC";
        }
    }
}