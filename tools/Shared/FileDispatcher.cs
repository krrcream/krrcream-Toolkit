using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.tools.KRRLNTransformer;
using krrTools.tools.Preview;
using krrTools.tools.Shared;
using krrTools.tools.DPtool;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using System.IO;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.Shared
{
    public class FileDispatcher(Dictionary<string, DualPreviewControl> previewControls, TabView mainTabControl)
    {
        private static readonly ILogger<FileDispatcher> _logger = LoggerFactoryHolder.CreateLogger<FileDispatcher>();

        private readonly Dictionary<string, IConverter> _converters = new()
        {
            { OptionsManager.N2NCToolName, new N2NCConverterWrapper() },
            { OptionsManager.LNToolName, new LNConverterWrapper() },
            { OptionsManager.DPToolName, new DPConverterWrapper() },
            { OptionsManager.KRRLNToolName, new KrrLNConverterWrapper() }
        };

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
            if (activeTabTag == OptionsManager.N2NCToolName)
            {
                // Special handling for Converter with result reporting
                ConvertWithResults(paths);
            }
            else if (_converters.TryGetValue(activeTabTag, out var converter))
            {
                var created = new List<string>();
                var failed = new List<string>();

                foreach (var path in paths.Where(p => !string.IsNullOrEmpty(p)))
                {
                    try
                    {
                        var result = converter.ProcessSingleFile(path);
                        if (!string.IsNullOrEmpty(result))
                            created.Add(result);
                        else
                            failed.Add(path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "转换文件时出错: {Path}", path);
                        failed.Add(path);
                    }
                }

                _logger.LogInformation("转换器: {Converter}, 生成文件数量: {CreatedCount}", activeTabTag, created.Count);
                ShowConversionResult(created, failed, paths.Length);
            }
        }

        private void ConvertWithResults(string[] paths)
        {
            // Try to find existing N2NCControl instance from MainWindow
            var mainWindow = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault();
            var conv = mainWindow?.ConvWindowInstance;
            if (conv == null)
            {
                // Fallback: create new instance if not found
                conv = new N2NCControl();
                _logger.LogWarning("使用备用N2NCControl实例 - 选项可能未正确加载");
            }

            var created = new List<string>();
            var failed = new List<string>();

            foreach (var p in paths.Where(p => !string.IsNullOrEmpty(p)))
            {
                try
                {
                    var result = conv.ProcessSingleFile(p);
                    if (!string.IsNullOrEmpty(result))
                        created.Add(result);
                    else
                        failed.Add(p);
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

            _logger.LogInformation("转换器: {Converter}, 生成文件数量: {CreatedCount}", OptionsManager.N2NCToolName, created.Count);
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
                        sb.AppendLine($"• {System.IO.Path.GetFileName(file)}");
                    
                    if (failed.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"失败 {failed.Count} 个文件：");
                        foreach (var file in failed)
                            sb.AppendLine($"• {System.IO.Path.GetFileName(file)}");
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

        // Wrapper classes to implement IConverter
        private class N2NCConverterWrapper : IConverter
        {
            public string? ProcessSingleFile(string path)
            {
                var options = OptionsManager.LoadOptions<N2NCOptions>(OptionsManager.N2NCToolName, OptionsManager.ConfigFileName) ?? new N2NCOptions();
                return N2NCService.ProcessSingleFile(path, options, openOsz: false);
            }
        }

        private class LNConverterWrapper : IConverter
        {
            public string? ProcessSingleFile(string path)
            {
                var options = OptionsManager.LoadOptions<LNTransformerOptions>(OptionsManager.LNToolName, OptionsManager.ConfigFileName) ?? new LNTransformerOptions();
                TransformService.ProcessFiles(new List<string> { path }, options);
                return path; // Output is the same as input since it overwrites
            }
        }

        private class DPConverterWrapper : IConverter
        {
            public string? ProcessSingleFile(string path)
            {
                var options = OptionsManager.LoadOptions<DPToolOptions>(OptionsManager.DPToolName, OptionsManager.ConfigFileName) ?? new DPToolOptions();
                var dp = new DP();
                return dp.ProcessFile(path, options);
            }
        }

        private class KrrLNConverterWrapper : IConverter
        {
            public string? ProcessSingleFile(string path)
            {
                // 使用默认参数进行转换
                var parameters = OptionsManager.LoadOptions<KRRLNTransformerOptions>(OptionsManager.KRRLNToolName, OptionsManager.ConfigFileName) ?? new KRRLNTransformerOptions
                {
                    ShortPercentageValue = 50,
                    ShortLevelValue = 5,
                    ShortLimitValue = 20,
                    ShortRandomValue = 50,
                    LongPercentageValue = 50,
                    LongLevelValue = 5,
                    LongLimitValue = 20,
                    LongRandomValue = 50,
                    AlignIsChecked = false,
                    AlignValue = 4,
                    ProcessOriginalIsChecked = false,
                    ODValue = 8,
                    SeedText = "114514"
                };
                var LN = new KRRLN();
                var beatmap = LN.ProcessFiles(path, parameters);
                string? dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) dir = ".";
                string outputPath = Path.Combine(dir, Path.GetFileNameWithoutExtension(path) + "_KRRLN.osu");
                File.WriteAllText(outputPath, beatmap.ToString());
                return outputPath;
            }
        }
    }
}