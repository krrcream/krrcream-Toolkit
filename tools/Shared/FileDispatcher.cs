using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.tools.KRRLNTransformer;
using krrTools.tools.Preview;
using krrTools.tools.Shared;
using System.Diagnostics;
using krrTools.tools.DPtool;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using System.IO;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;

namespace krrTools.Tools.Shared
{
    public class FileDispatcher(Dictionary<string, DualPreviewControl> previewControls, TabView mainTabControl)
    {
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
            MessageBox.Show($"Converting {paths.Length} files for {activeTabTag}");
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
                        Debug.WriteLine($"Error converting {path}: {ex.Message}");
                        failed.Add(path);
                    }
                }

                // Notify user on UI thread that processing finished
                if (created.Count > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("Conversion finished. Created files:");
                    foreach (var c in created) sb.AppendLine(c);
                    if (failed.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("The following source files failed to convert:");
                        foreach (var f in failed) sb.AppendLine(f);
                    }
                    MessageBox.Show(sb.ToString(), "Conversion Result", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var msg = failed.Count > 0
                        ? "Conversion failed for the selected files."
                        : "Conversion did not produce any output.";
                    MessageBox.Show(msg, "Conversion Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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
                Debug.WriteLine("Warning: Using fallback N2NCControl instance - options may not be loaded correctly");
            }

            var created = new List<string>();
            var failed = new List<string>();

            foreach (var p in paths.Where(p => !string.IsNullOrEmpty(p)))
            {
                try
                {
                    Debug.WriteLine($"Processing file: {p}");
                    var result = conv.ProcessSingleFile(p);
                    Debug.WriteLine($"ProcessSingleFile result: {result}");
                    if (!string.IsNullOrEmpty(result))
                        created.Add(result);
                    else
                        failed.Add(p);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Converter processing error for {p}: {ex.Message}");
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
                    Debug.WriteLine($"BroadcastStagedPaths error: {ex.Message}");
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Conversion finished. Created files:");
                foreach (var c in created) sb.AppendLine(c);
                if (failed.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("The following source files failed to convert:");
                    foreach (var f in failed) sb.AppendLine(f);
                }
                MessageBox.Show(sb.ToString(), "Conversion Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                var msg = failed.Count > 0
                    ? "Conversion failed for the selected files. The staged files remain so you can retry."
                    : "Conversion did not produce any output.";
                MessageBox.Show(msg, "Conversion Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
                string outputPath = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_KRRLN.osu");
                File.WriteAllText(outputPath, beatmap.ToString());
                return outputPath;
            }
        }
    }
}