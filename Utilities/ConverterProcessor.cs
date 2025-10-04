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
using System.IO;
using System.Text;
using ManiaHitObject = krrTools.Beatmaps.ManiaHitObject;

namespace krrTools.Utilities;

public class ConverterProcessor : IPreviewProcessor
{
    public string? CurrentTool { get; set; } // 当前使用的转换工具名称，在主程序中获取
    public int ColumnOverride { get; set; } // 0表示不覆盖，使用实际列数

    public int LastStartMs { get; private set; }

    // 选项提供器，从主程序传入模块设置
    public IModuleManager? TPScheduler { get; set; }

    public Func<object>? ConverterOptionsProvider { get; init; }

    private Func<string, Beatmap, object?>? ConversionProvider { get; set; }

    public ConverterProcessor()
    {
        // 分配活动工具、输入路径开始转换
        ConversionProvider = (toolName, beatmap) =>
        {
            if (TPScheduler != null)
            {
                // 克隆谱面以避免修改原始谱面
                Beatmap clonedBeatmap = BeatmapDecoder.Decode(beatmap.OriginalFilePath);
                

                if (ConverterOptionsProvider != null)
                {
                    var options = ConverterOptionsProvider();
                    if (options is IToolOptions toolOptions)
                    {
                        Console.WriteLine(
                            $"[INFO] ConverterOptionsProvider 获取到{toolName}，{beatmap.MetadataSection.Title} // {beatmap.MetadataSection.Creator} // {beatmap.MetadataSection.Version}，传递给ToolScheduler。");
                        return TPScheduler.ProcessBeatmap(toolName, clonedBeatmap, toolOptions);
                    }
                }

                Console.WriteLine("[INFO] ConverterOptionsProvider为空或未实现 IToolOptions，无设置传入工具调度器。");
                return TPScheduler.ProcessBeatmap(toolName, clonedBeatmap);
            }

            return beatmap;
        };
    }

    public FrameworkElement BuildOriginalVisual(Beatmap input)
    {
        var maniaBeatmap = input;
        if (maniaBeatmap.HitObjects.Count > 0)
            LastStartMs = maniaBeatmap.HitObjects.Min(n => n.StartTime);

        return BuildManiaTimeRowsFromNotes(maniaBeatmap);
    }

    public FrameworkElement BuildConvertedVisual(Beatmap input)
    {
        if (ConversionProvider == null || CurrentTool == null)
            return new TextBlock { Text = "转换器传递失败，或工具获取为空" };

        var maniaBeatmap = input;
        // 对于内置谱面，直接使用输入的 beatmap，不需要从路径解码
        if (maniaBeatmap.MetadataSection.Title == "Built-in Sample")
        {
            LastStartMs = maniaBeatmap.HitObjects.Min(n => n.StartTime);

            return BuildManiaTimeRowsFromNotes(maniaBeatmap);
        }

        if (TPScheduler == null)
            return new TextBlock { Text = "ToolScheduler 未设置，无法进行转换" };

        var rawData = ConversionProvider(CurrentTool, maniaBeatmap);

        if (rawData is Beatmap beatmap)
        {
            if (beatmap.HitObjects.Count > 0)
                LastStartMs = beatmap.HitObjects.Min(n => n.StartTime);

            return BuildManiaTimeRowsFromNotes(beatmap);
        }

        return new TextBlock { Text = "没有获取到Beatmap结果" };
    }

    // 根据时间行构建动态预览控件
    private FrameworkElement BuildManiaTimeRowsFromNotes(Beatmap beatmap, Func<int, ManiaHitObject, ManiaHitObject>? noteTransform = null)
    {
        var columns = (int)beatmap.DifficultySection.CircleSize;
        var quarterMs = beatmap.GetBPM(true);
        var notes = new List<ManiaHitObject>();
        
        foreach (var hit in beatmap.HitObjects.OrderBy(h => h.StartTime))
            notes.Add(new ManiaHitObject
            {
                Index = (int)hit.Position.X,
                StartTime = hit.StartTime,
                EndTime = hit.EndTime,
                IsHold = hit.StartTime != hit.EndTime
            });

        var timeGroups = notes.GroupBy(n => n.StartTime)
            .OrderBy(g => g.Key)
            .ToList(); // 移除Take限制，显示所有时间组
        
        if (timeGroups.Count == 0) return new TextBlock { Text = "timeGroups.Count == 0" };

        List<(int time, List<ManiaHitObject> notes)> grouped;
        
        if (noteTransform != null)
        {
            grouped = new List<(int time, List<ManiaHitObject> notes)>(timeGroups.Count);
            foreach (var g in timeGroups)
            {
                var list = new List<ManiaHitObject>(g.Count());
                foreach (var n in g) list.Add(noteTransform(columns, n));
                grouped.Add((g.Key, list));
            }
        }
        else
        {
            grouped = timeGroups.Select(g => (g.Key, g.ToList())).ToList();
        }

        // 使用动态控件显示；控件自适应父容器大小
        var displayColumns = columns;

        if (displayColumns <= 0) displayColumns = 1;

        var dyn = new PreviewDynamicControl(grouped, displayColumns, quarterMs)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        return dyn;
    }
}