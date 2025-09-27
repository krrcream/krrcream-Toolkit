using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.tools.DPtool;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using krrTools.tools.Preview;
using krrTools.tools.Shared;
using System.Diagnostics;

namespace krrTools.Tools.Shared
{
    public class FileDispatcher
    {
        private readonly Dictionary<string, DualPreviewControl> _previewControls;
        private readonly Dictionary<string, IConverter> _converters;
        private readonly TabControl _mainTabControl;

        public FileDispatcher(Dictionary<string, DualPreviewControl> previewControls, TabControl mainTabControl)
        {
            _previewControls = previewControls;
            _mainTabControl = mainTabControl;
            _converters = new Dictionary<string, IConverter>
            {
                { OptionsManager.ConverterToolName, new N2NCConverterWrapper() },
                { OptionsManager.LNToolName, new LNConverterWrapper() },
                { OptionsManager.DPToolName, new DPConverterWrapper() }
            };
        }

        public void LoadFiles(string[]? paths, string? activeTabTag = null)
        {
            activeTabTag ??= GetActiveTabTag();
            if (_previewControls.TryGetValue(activeTabTag, out var control))
            {
                control.LoadFiles(paths);
                control.StageFiles(paths);
            }
        }

        public void ConvertFiles(string[] paths, string? activeTabTag = null)
        {
            activeTabTag ??= GetActiveTabTag();
            if (activeTabTag == OptionsManager.ConverterToolName)
            {
                // Special handling for Converter with result reporting
                ConvertWithResults(paths);
            }
            else if (_converters.TryGetValue(activeTabTag, out var converter))
            {
                foreach (var path in paths.Where(p => !string.IsNullOrEmpty(p)))
                {
                    try
                    {
                        converter.ProcessSingleFile(path);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error converting {path}: {ex.Message}", "Conversion Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ConvertWithResults(string[] paths)
        {
            var conv = Application.Current?.Windows.OfType<N2NCControl>().FirstOrDefault() ?? new N2NCControl();
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
            return (_mainTabControl.SelectedItem as TabItem)?.Tag as string ?? OptionsManager.ConverterToolName;
        }

        // Wrapper classes to implement IConverter
        private class N2NCConverterWrapper : IConverter
        {
            public void ProcessSingleFile(string path)
            {
                var conv = Application.Current?.Windows.OfType<N2NCControl>().FirstOrDefault() ?? new N2NCControl();
                conv.ProcessSingleFile(path, openOsz: false);
            }
        }

        private class LNConverterWrapper : IConverter
        {
            public void ProcessSingleFile(string path)
            {
                var ln = Application.Current?.Windows.OfType<LNTransformerControl>().FirstOrDefault() ?? new LNTransformerControl();
                ln.ProcessSingleFile(path);
            }
        }

        private class DPConverterWrapper : IConverter
        {
            public void ProcessSingleFile(string path)
            {
                var dp = Application.Current?.Windows.OfType<DPToolControl>().FirstOrDefault() ?? new DPToolControl();
                dp.ProcessSingleFile(path);
            }
        }
    }
}