using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using krrTools.Tools.Preview;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Utilities
{
    public class ConverterProcessor : IPreviewProcessor
    {
        public string? CurrentTool { get; set; } // 当前使用的转换工具名称，在主程序中获取
        public int ColumnOverride { get; set; } // 0表示不覆盖，使用实际列数

        public int LastStartMs { get; private set; }

        // 选项提供器，从主程序传入模块设置
        public IModuleManager? ToolScheduler { get; init; }

        public Func<object>? ConverterOptionsProvider { get; init; }

        private Func<string, Beatmap, object?>? ConversionProvider { get; set; }

        public ConverterProcessor()
        {
            // 分配活动工具、输入路径开始转换
            ConversionProvider = (toolName, beatmap) =>
                {
                if (ToolScheduler != null)
                {
                    if (ConverterOptionsProvider != null)
                    {
                        var options = ConverterOptionsProvider();
                        if (options is IToolOptions toolOptions)
                        {
                            Console.WriteLine($"[INFO] ConverterOptionsProvider 获取到{toolName}，{beatmap.MetadataSection.Title} // {beatmap.MetadataSection.Creator} // {beatmap.MetadataSection.Version}，传递给ToolScheduler。");
                            return ToolScheduler.ProcessBeatmap(toolName, beatmap, toolOptions);
                        }
                    }
                    Console.WriteLine("[INFO] ConverterOptionsProvider为空或未实现 IToolOptions，无设置传入工具调度器。");
                    return ToolScheduler.ProcessBeatmap(toolName, beatmap);
                }

                return beatmap;
            };
        }

        public FrameworkElement BuildOriginalVisual(ManiaBeatmap maniaBeatmap)
        {
            // var maniaBeatmap = input.GetManiaBeatmap();
            if (maniaBeatmap.HitObjects.Count > 0)
                LastStartMs = maniaBeatmap.HitObjects.Min(n => n.StartTime);
            
            var (columns, notes, quarterMs) = BuildNotesList(maniaBeatmap);
            return BuildManiaTimeRowsFromNotes(notes, columns, quarterMs);
        }
        
        public FrameworkElement BuildConvertedVisual(ManiaBeatmap input)
        {
            if (ConversionProvider == null || CurrentTool == null)
                return new TextBlock { Text = "转换器传递失败，或工具获取为空" };

            // 对于内置谱面，直接使用输入的 beatmap，不需要从路径解码
            if (input.MetadataSection.Title == "Built-in Sample")
            {
                var maniaBeatmap = input;
                if (maniaBeatmap.NoteCount > 0)
                    LastStartMs = maniaBeatmap.HitObjects.Min(n => n.StartTime);

                var (columns, notes, quarterMs) = BuildNotesList(input);
                return BuildManiaTimeRowsFromNotes(notes, columns, quarterMs);
            }

            var rawData = ConversionProvider(CurrentTool, input);

            if (rawData is ManiaBeatmap beatmap)
            {
                if (beatmap.NoteCount > 0)
                    LastStartMs = beatmap.HitObjects.Min(n => n.StartTime);

                var (columns, notes, quarterMs) = BuildNotesList(beatmap);
                return BuildManiaTimeRowsFromNotes(notes, columns, quarterMs);
            
            }

            return new TextBlock { Text = "没有获取到Beatmap结果" };
        }
        
        private static (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) BuildNotesList(
            ManiaBeatmap beatmap)
        {
            var columns = beatmap.GetManiaBeatmap().KeyCount;
            var quarterMs = beatmap.GetBPM(true);
            var notes = new List<ManiaBeatmap.PreViewManiaNote>();
            foreach (var hit in beatmap.HitObjects.OrderBy(h => h.StartTime))
            {
                notes.Add(new ManiaBeatmap.SimpleManiaNote
                {
                    Index = (int)hit.Position.X,
                    StartTime = hit.StartTime,
                    EndTime = hit.EndTime > 0 ? hit.EndTime : null,
                    IsHold = hit.EndTime > 0
                });
            }
            return (columns, notes, quarterMs);
        }
        
        // 根据时间行构建动态预览控件
        private FrameworkElement BuildManiaTimeRowsFromNotes(List<ManiaBeatmap.PreViewManiaNote> allNotes, int columns, double quarterMs = 0, Func<int, ManiaBeatmap.PreViewManiaNote, ManiaBeatmap.PreViewManiaNote>? noteTransform = null)
        {
            if (allNotes.Count == 0) return new TextBlock { Text = "allNotes.Count == 0" };
            var timeGroups = allNotes.GroupBy(n => n.StartTime).OrderBy(g => g.Key).ToList(); // 移除Take限制，显示所有时间组
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

            var dyn = new PreviewDynamicControl(grouped, displayColumns, quarterMs)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            return dyn;
        }
    }
}
