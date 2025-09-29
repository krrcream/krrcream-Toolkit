using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using krrTools.tools.Preview;
using krrTools.tools.Shared;
using krrTools.tools.DPtool;
using krrTools.tools.KRRLNTransformer;
using System.IO;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;
using Microsoft.Extensions.Logging;
using krrTools.tools.N2NC;
using static krrTools.tools.LNTransformer.Setting;

namespace krrTools.Tools.Shared
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
            if (activeTabTag == OptionsManager.N2NCToolName)
                conv = mainWindow?.ConvWindowInstance;
            else if (activeTabTag == OptionsManager.YLsLNToolName)
                conv = mainWindow?.LNWindowInstance;
            else if (activeTabTag == OptionsManager.DPToolName)
                conv = mainWindow?.DPWindowInstance;
            else if (activeTabTag == OptionsManager.KRRsLNToolName)
                conv = mainWindow?.KRRLNTransformerInstance;

            if (conv == null)
            {
                // Fallback: create new instance if not found
                if (activeTabTag == OptionsManager.N2NCToolName)
                    conv = new N2NCControl();
                else if (activeTabTag == OptionsManager.YLsLNToolName)
                    conv = new YLsLNTransformerControl();
                else if (activeTabTag == OptionsManager.DPToolName)
                    conv = new DPToolControl();
                else if (activeTabTag == OptionsManager.KRRsLNToolName)
                    conv = new KRRLNTransformerControl();
                _logger.LogWarning("使用备用{Converter}实例 - 选项可能未正确加载", activeTabTag);
            }

            var created = new List<string>();
            var failed = new List<string>();

            foreach (var p in paths.Where(p => !string.IsNullOrEmpty(p)))
            {
                try
                {
                    var beatmap = (conv as dynamic)?.ProcessSingleFile(p);
                    if (beatmap != null)
                    {
                        var outputFileName = (conv as dynamic)?.GetOutputFileName(p, beatmap);
                        var outputPath = Path.Combine(Path.GetDirectoryName(p) ?? "", outputFileName);

                        // Handle file conflicts
                        if (File.Exists(outputPath))
                        {
                            var result = MessageBox.Show(
                                $"文件已存在：{Path.GetFileName(outputPath)}\n\n是否覆盖？",
                                "文件冲突",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);

                            if (result != MessageBoxResult.Yes)
                            {
                                failed.Add(p);
                                continue;
                            }
                        }

                        beatmap.Save(outputPath);
                        created.Add(outputPath);
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
            }

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
            ShowConversionResult(created, failed, paths.Length);
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
                    ? "转换失败，所有文件都未能成功转换。" 
                    : "转换未产生任何输出。";
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        private string GetActiveTabTag()
        {
            return (mainTabControl.SelectedItem as TabViewItem)?.Tag as string ?? OptionsManager.N2NCToolName;
        }
    }
}