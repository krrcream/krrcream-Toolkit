using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Data;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.Preview
{
    public class PreviewProcessor : IPreviewProcessor
    {
        private static readonly ILogger<PreviewProcessor> _logger = LoggerFactoryHolder.CreateLogger<PreviewProcessor>();

        public string ToolKey => "Preview";
        public string? CurrentTool { get; set; }
        public int ColumnOverride { get; set; } // 0表示不覆盖，使用实际列数

        public int LastStartMs { get; private set; }

        // 选项提供器
        public ToolScheduler? ToolScheduler { get; init; }

        public Func<object>? ConverterOptionsProvider { get; set; }

        private Func<string, string, int, int, object?>? ConversionProvider { get; set; }

        public PreviewProcessor()
        {
            ConversionProvider = (toolName, path, start, end) =>
            {
                var maniaBeatmap = FilesHelper.GetManiaBeatmap(path);

                if (ToolScheduler != null)
                {
                    if (ConverterOptionsProvider != null)
                    {
                        var options = ConverterOptionsProvider();
                        if (options is IToolOptions toolOptions)
                        {
                            return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, toolOptions);
                        }
                    }
                    return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap);
                }

                return maniaBeatmap;
            };
        }

        public FrameworkElement BuildVisual(string[] filePaths, bool converted)
        {
            if (converted)
            {
                return BuildNotesPreview(filePaths, true, ConversionProvider, CurrentTool);
            }
            
            return BuildNotesPreview(filePaths, false, null, null);
        }
        
        private FrameworkElement BuildNotesPreview(string[] filePaths, bool converted,
            Func<string, string, int, int, object?>? conversionProvider, string? toolName)
        {
            // 仅预览第一个文件
            var previewOnePath = filePaths is { Length: > 0 } ? filePaths[0] : string.Empty;
            
            _logger.LogInformation("预览器读取转换: {Path}, 转换: {Converted}", previewOnePath, converted);

            int? first = PreviewTransformation.GetFirstNonEmptyTime(previewOnePath);
            if (!first.HasValue)
            {
                var full = PreviewTransformation.BuildOriginal(previewOnePath, 1);
                if (full.notes.Count > 0) first = full.notes.Min(n => n.StartTime);
            }
            if (!first.HasValue) return new TextBlock { Text = "(无可用音符)" };

            ManiaBeatmap maniaBeatmap = FilesHelper.GetManiaBeatmap(previewOnePath);
            
            var quarterMs = maniaBeatmap.GetBPM(true);
            int startMs = first.Value;
            int windowMs = Math.Max(PreviewConstants.MinWindowLengthMs,
                (int)Math.Round(quarterMs * PreviewConstants.PreviewWindowUnitCount / PreviewConstants.PreviewWindowUnitBeatDenominator));
            int endMs = startMs + windowMs;
            
            LastStartMs = startMs;

            (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) data;
            if (converted)
            {
                if (conversionProvider == null)
                    return new TextBlock { Text = "(无转换提供器)" };
                var rawData = conversionProvider(toolName ?? "", previewOnePath, startMs, endMs);
                if (rawData is Beatmap beatmap)
                {
                    // 如果是ManiaBeatmap直接使用，否则尝试转换为ManiaBeatmap
                    var processedManiaBeatmap = rawData is ManiaBeatmap mb ? mb : beatmap.GetManiaBeatmap();
                    data = PreviewTransformation.BuildFromBeatmapWindow(processedManiaBeatmap, startMs, endMs);
                }
                else
                {
                    data = (0, new List<ManiaBeatmap.PreViewManiaNote>(), 0.0);
                }
            }
            else
            {
                data = PreviewTransformation.BuildOriginalWindow(previewOnePath, startMs, endMs);
            }

            var previewElement = BuildNotes(data);
            return previewElement;
        }

        // 从实际音符构建显示
        private FrameworkElement BuildNotes((int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) data)
        {
            if (data.columns <= 0 || data.notes.Count == 0)
                return new TextBlock { Text = "(无可用数据)" };

            var displayColumns = data.columns;
            if (ColumnOverride > 0)
                displayColumns = ColumnOverride;

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
            int displayColumns = columns; 
            
            if (displayColumns <= 0) displayColumns = 1;
            
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
