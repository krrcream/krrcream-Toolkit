using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Data;
using krrTools.Tools.Preview;
using OsuParsers.Beatmaps;

namespace krrTools.Utilities
{
    public class ConverterProcessor : IPreviewProcessor
    {
        public string? CurrentTool { get; set; }
        public int ColumnOverride { get; set; } // 0表示不覆盖，使用实际列数

        public int LastStartMs { get; private set; }

        // 选项提供器
        public IModuleManager? ToolScheduler { get; init; }

        public Func<object>? ConverterOptionsProvider { get; init; }

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
                            Console.WriteLine("[INFO] 通过 ConverterOptionsProvider 获取到工具选项，传递给工具调度器。");
                            return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap, toolOptions);
                        }
                    }
                    Console.WriteLine("[INFO] ConverterOptionsProvider为空或未实现 IToolOptions，无设置传入工具调度器。");
                    return ToolScheduler.ProcessBeatmap(toolName, maniaBeatmap);
                }

                return maniaBeatmap;
            };
        }

        public FrameworkElement BuildVisual(ManiaBeatmap input, bool converted)
        {
            const int maxRows = 10;
            
            if (converted)
            {
                if (ConversionProvider == null || CurrentTool == null)
                    return new TextBlock { Text = "转换器传递失败，或工具获取为空" };

                var rawData = ConversionProvider(CurrentTool, input.FilePath);
                
                if (rawData is Beatmap beatmap)
                {
                    var processed = rawData is ManiaBeatmap mb ? mb : beatmap.GetManiaBeatmap();
                    var (columns, notes, quarterMs) = PreviewTransformation.BuildFromBeatmap(processed, maxRows);
                    return BuildManiaTimeRowsFromNotes(notes, columns, maxRows, quarterMs);
                }
            
                return new TextBlock { Text = "没有获取到Beatmap结果" };
            }
            
            var (c, n, q) = PreviewTransformation.BuildFromBeatmap(input, maxRows);
            return BuildManiaTimeRowsFromNotes(n, c, maxRows, q);
        }
        
        // 根据时间行构建动态预览控件（按时间分组、限制行数）
        private FrameworkElement BuildManiaTimeRowsFromNotes(List<ManiaBeatmap.PreViewManiaNote> allNotes, int columns, int maxRows, double quarterMs = 0, Func<int, ManiaBeatmap.PreViewManiaNote, ManiaBeatmap.PreViewManiaNote>? noteTransform = null)
        {
            if (allNotes.Count == 0) return new TextBlock { Text = "allNotes.Count == 0" };
            var timeGroups = allNotes.GroupBy(n => n.StartTime).OrderBy(g => g.Key).Take(maxRows).ToList();
            if (timeGroups.Count == 0) return new TextBlock { Text = "timeGroups.Count == 0" };

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
