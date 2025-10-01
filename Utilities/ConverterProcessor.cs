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
using krrTools.Tools.Preview;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Utilities
{
    public class ConverterProcessor : IPreviewProcessor
    {
        public string ToolKey => "Preview";
        public string? CurrentTool { get; set; }
        public int ColumnOverride { get; set; } // 0表示不覆盖，使用实际列数

        public int LastStartMs { get; private set; }

        // 选项提供器
        public ToolScheduler? ToolScheduler { get; init; }

        public Func<object>? ConverterOptionsProvider { get; set; }

        private Func<string, string, object?>? ConversionProvider { get; set; }

        public ConverterProcessor()
        {
            // 分配活动工具、输入路径开始转换
            ConversionProvider = (toolName, path) =>
            {
                var maniaBeatmap = FilesHelper.GetManiaBeatmap(path);

                if (ToolScheduler != null)
                {
                    // 事件触发开始转换
                    if (ConverterOptionsProvider != null)
                    {
                        var options = ConverterOptionsProvider();
                        if (options is IToolOptions toolOptions)
                        {
                            Logger.Log(LogLevel.Information, "使用实时设置进行转换");
                            return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, toolOptions);
                        }
                    }
                    Logger.Log(LogLevel.Information, "使用默认设置进行转换");
                    return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap);
                }

                return maniaBeatmap;
            };
        }

        public FrameworkElement BuildVisual(ManiaBeatmap beatmap, bool converted)
        {
            if (converted)
            {
                return BuildConvertedPreview(beatmap);
            }
            
            return BuildOriginalPreview(beatmap);
        }
        
        private FrameworkElement BuildOriginalPreview(ManiaBeatmap beatmap)
        {
            var (columns, notes, quarterMs) = PreviewTransformation.BuildFromBeatmap(beatmap, 10);
            return BuildManiaTimeRowsFromNotes(notes, columns, 10, quarterMs);
        }
        
        private FrameworkElement BuildConvertedPreview(ManiaBeatmap originalBeatmap)
        {
            if (ConversionProvider == null || CurrentTool == null)
                return new TextBlock { Text = "No conversion available" };

            // 使用转换提供器处理谱面
            var rawData = ConversionProvider(CurrentTool, originalBeatmap.FilePath);
            if (rawData is Beatmap beatmap)
            {
                // 如果是ManiaBeatmap直接使用，否则尝试转换为ManiaBeatmap
                var processedManiaBeatmap = rawData is ManiaBeatmap mb ? mb : beatmap.GetManiaBeatmap();
                var (columns, notes, quarterMs) = PreviewTransformation.BuildFromBeatmap(processedManiaBeatmap, 10);
                return BuildManiaTimeRowsFromNotes(notes, columns, 10, quarterMs);
            }
            
            return new TextBlock { Text = "Conversion failed" };
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
                VerticalAlignment = VerticalAlignment.Stretch
            };
            return dyn;
        }
    }
}
