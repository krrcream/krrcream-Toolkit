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

namespace krrTools.Utilities;

public class ConverterProcessor : IPreviewProcessor
{
    public ConverterEnum? ModuleTool { get; set; } // 当前使用的转换工具名称，在主程序中获取

    public int StartMs { get; private set; } // 谱面起始时间，用于预览控件定位
    
    public IModuleManager? ModuleScheduler { get; set; } // 模块调度器，从主程序传入

    public Func<IToolOptions>? OptionsProvider { get; init; } // 选项提供器，从主程序传入模块设置

    private Func<ConverterEnum, Beatmap, Beatmap?> ConversionProvider { get; set; } // 转换提供器，分配活动工具、输入路径开始转换

    public ConverterProcessor()
    {
        // 分配活动工具、输入路径开始转换
        ConversionProvider = (toolName, beatmap) =>
        {
            if (ModuleScheduler != null)
            {
                var clonedBeatmap = beatmap;
                if (OptionsProvider != null)
                {
                    var options = OptionsProvider();
                    return ModuleScheduler.ProcessBeatmap(toolName, clonedBeatmap, options);
                }

                Console.WriteLine("[ConversionProvider] TPScheduler未获得IToolOptions。");
                return ModuleScheduler.ProcessBeatmap(toolName, clonedBeatmap);
            }

            Console.WriteLine("[ConversionProvider] ModuleScheduler为空，未转换，返回了原始beatmap。");
            return beatmap;
        };
    }

    public FrameworkElement BuildOriginalVisual(Beatmap input)
    {
        var maniaBeatmap = input;
        if (maniaBeatmap.HitObjects.Count > 0)
            StartMs = maniaBeatmap.HitObjects.Min(n => n.StartTime);
        else
            return new TextBlock { Text = "note == 0" };

        return BuildManiaTimeRowsFromNotes(maniaBeatmap);
    }

    public FrameworkElement BuildConvertedVisual(Beatmap input)
    {
        if (ModuleTool == null)
            return new TextBlock { Text = "lamda && ModuleTool == null" };

        var maniaBeatmap = ConversionProvider(ModuleTool.Value, input);
        if (maniaBeatmap == null)
            return new TextBlock { Text = "Conv. ManiaBeatmap == null" };

        return BuildManiaTimeRowsFromNotes(maniaBeatmap);
    } // 通过委托获得转换结果，传递绘制

    private FrameworkElement BuildManiaTimeRowsFromNotes(Beatmap beatmap)
    {
        var columns = (int)beatmap.DifficultySection.CircleSize;
        if (columns == 0) return new TextBlock { Text = "BuildMania columns == 0" };

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

        var columnGroups = notes.GroupBy(n => n.Index)
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.OrderBy(n => n.StartTime).ToList()))
            .ToList();

        var dyn = new PreviewDynamicControl(columnGroups, columns, quarterMs)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        return dyn;
    }
}