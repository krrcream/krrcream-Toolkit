using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Data;
using krrTools.Tools.DPtool;
using krrTools.Tools.KRRLNTransformer;
// using krrTools.Tools.LNTransformer;
using krrTools.Tools.N2NC;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using Microsoft.Extensions.Logging;
using krrTools.Configuration;
using krrTools.Core;

namespace krrTools.Tools.Preview
{
    public class PreviewProcessor : IPreviewProcessor
    {
        private static readonly ILogger<PreviewProcessor> _logger = LoggerFactoryHolder.CreateLogger<PreviewProcessor>();

        public string ToolKey => "Preview";
        public string? CurrentTool { get; set; }
        public int? ColumnOverride { get; set; }

        public int? LastOriginalStartMs { get; private set; }
        public int? LastConvertedStartMs { get; private set; }

        // 选项提供器
        public ToolScheduler? ToolScheduler { get; set; }
        public Func<N2NCOptions?>? ConverterOptionsProvider { get; set; }
        public Func<DPToolOptions?>? DPOptionsProvider { get; set; }
        // public Func<YLsLNTransformerOptions?>? LNOptionsProvider { get; set; }
        public Func<KRRLNTransformerOptions?>? KRRLNOptionsProvider { get; set; }

        private Func<string, string, int, int, object?>? ConversionProvider { get; set; }

        public PreviewProcessor()
        {
            ConversionProvider = (toolName, path, start, end) =>
            {
                var maniaBeatmap = FilesHelper.GetManiaBeatmap(path);

                if (ToolScheduler != null)
                {
                    // 使用ToolScheduler处理
                    if (toolName == BaseOptionsManager.N2NCToolName)
                    {
                        var opt = ConverterOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, opt);
                    }
                    
                    if (toolName == BaseOptionsManager.DPToolName)
                    {
                        var opt = DPOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, opt);
                    }
                    
                    // if (toolName == OptionsManager.YLsLNToolName)
                    // {
                    //     var opt = LNOptionsProvider?.Invoke();
                    //     if (opt == null) return null;
                    //     return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, opt);
                    // }

                    if (toolName == BaseOptionsManager.KRRsLNToolName)
                    {
                        var opt = KRRLNOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, opt);
                    }
                }
                else
                {
                    // 回退到直接调用工具（保持兼容性）
                    if (toolName == BaseOptionsManager.N2NCToolName)
                    {
                        var tool = new N2NCTool();
                        var opt = ConverterOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return tool.ProcessBeatmapToDataWithOptions(maniaBeatmap, opt);
                    }
                    
                    if (toolName == BaseOptionsManager.DPToolName)
                    {
                        var tool = new DPTool();
                        var opt = DPOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return tool.ProcessBeatmapToDataWithOptions(maniaBeatmap, opt);
                    }
                    
                    // if (toolName == OptionsManager.YLsLNToolName)
                    // {
                    //     var tool = new YLsLNTransformerTool();
                    //     var opt = LNOptionsProvider?.Invoke();
                    //     if (opt == null) return null;
                    //     return tool.ProcessBeatmapToDataWithOptions(maniaBeatmap, opt);
                    // }

                    if (toolName == BaseOptionsManager.KRRsLNToolName)
                    {
                        var tool = new KRRLNTool();
                        var opt = KRRLNOptionsProvider?.Invoke();
                        if (opt == null) return null;
                        return tool.ProcessBeatmapToDataWithOptions(maniaBeatmap, opt);
                    }
                }

                return maniaBeatmap;
            };
        }

        public FrameworkElement BuildOriginalVisual(string[] filePaths)
        {
            return BuildPreview(filePaths, false, null, null);
        }

        public FrameworkElement BuildConvertedVisual(string[] filePaths)
        {
            return BuildPreview(filePaths, true, ConversionProvider, CurrentTool);
        }
        
        private FrameworkElement BuildPreview(string[] filePaths, bool converted,
            Func<string, string, int, int, object?>? conversionProvider, string? toolName)
        {
            var path = filePaths is { Length: > 0 } ? filePaths[0] : string.Empty;
            _logger.LogInformation("预览器读取转换: {Path}, 转换: {Converted}", path, converted);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new TextBlock { Text = "(无文件)" };

            int? first = PreviewTransformation.GetFirstNonEmptyTime(path);
            if (!first.HasValue)
            {
                var full = PreviewTransformation.BuildOriginal(path, 1);
                if (full.notes.Count > 0) first = full.notes.Min(n => n.StartTime);
            }
            if (!first.HasValue) return new TextBlock { Text = "(无可用音符)" };

            ManiaBeatmap maniaBeatmap = FilesHelper.GetManiaBeatmap(path);
            
            var quarterMs = maniaBeatmap.GetBPM(true);
            int startMs = first.Value;
            int windowMs = Math.Max(PreviewConstants.MinWindowLengthMs,
                (int)Math.Round(quarterMs * PreviewConstants.PreviewWindowUnitCount / PreviewConstants.PreviewWindowUnitBeatDenominator));
            int endMs = startMs + windowMs;

            if (converted)
                LastConvertedStartMs = startMs;
            else
                LastOriginalStartMs = startMs;

            (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) data;
            if (converted)
            {
                if (conversionProvider == null)
                    return new TextBlock { Text = "(无转换提供器)" };
                var rawData = conversionProvider(toolName ?? "", path, startMs, endMs);
                if (rawData is Beatmap beatmap)
                {
                    data = PreviewTransformation.BuildFromBeatmapWindow(beatmap, startMs, endMs);
                }
                else
                {
                    data = (0, new List<ManiaBeatmap.PreViewManiaNote>(), 0.0);
                }
            }
            else
            {
                data = PreviewTransformation.BuildOriginalWindow(path, startMs, endMs);
            }

            var previewElement = BuildFromRealNotes(data);
            return previewElement;
        }

        // 从实际音符构建显示
        private FrameworkElement BuildFromRealNotes((int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) data)
        {
            if (data.columns <= 0 || data.notes.Count == 0)
                return new TextBlock { Text = "(无可用数据)" };

            var displayColumns = data.columns;
            if (ColumnOverride is > 0)
                displayColumns = ColumnOverride.Value;

            return BuildManiaTimeRowsFromNotes(data.notes, displayColumns, 10, data.quarterMs);
        }

        // 根据时间行构建动态预览控件（按时间分组、限制行数）
        private FrameworkElement BuildManiaTimeRowsFromNotes(List<ManiaBeatmap.PreViewManiaNote> allNotes, int columns, int maxRows, double quarterMs = 0, Func<int, ManiaBeatmap.PreViewManiaNote, ManiaBeatmap.PreViewManiaNote>? noteTransform = null)
        {
            if (allNotes.Count == 0) return new TextBlock { Text = "(无数据)" };
            var timeGroups = allNotes.GroupBy(n => n.StartTime).OrderBy(g => g.Key).Take(maxRows).ToList();
            if (timeGroups.Count == 0) return new TextBlock { Text = "(无数据)" };

            List<(int time, List<ManiaBeatmap.PreViewManiaNote> notes)> grouped;
            if (noteTransform != null)
            {
                grouped = new List<(int time, List<ManiaBeatmap.PreViewManiaNote> notes)>(timeGroups.Count);
                foreach (var g in timeGroups)
                {
                    var list = new List<ManiaBeatmap.PreViewManiaNote>(g.Count());
                    foreach (var n in g) list.Add(noteTransform(columns, n));
                    grouped.Add((g.Key, list));
                }
            }
            else
            {
                grouped = timeGroups.Select(g => (g.Key, g.ToList())).ToList();
            }

            // 使用动态控件显示；控件自适应父容器大小
            int displayColumns = columns; if (displayColumns <= 0) displayColumns = 1;
            var dyn = new DynamicPreviewControl(grouped, displayColumns, quarterMs)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = Brushes.Transparent
            };
            return dyn;
        }
    }
}
