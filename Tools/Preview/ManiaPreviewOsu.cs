using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using krrTools.Beatmaps;

namespace krrTools.Tools.Preview;

public class ManiaPreviewOsu : IPreviewProcessor
{
    private const string ManiaData = @"
osu file format v14

[General]
SampleSet: Normal
StackLeniency: 0.7
Mode: 3

[Events]
//Background and Video events
0,0,""bg.jpg"",0,0

[Difficulty]
HPDrainRate:3
CircleSize:5
OverallDifficulty:8
ApproachRate:8
SliderMultiplier:3.59999990463257
SliderTickRate:2

[TimingPoints]
24,352.941176470588,4,1,1,100,1,0
6376,-50,4,1,1,100,0,0

[HitObjects]
51,192,24,1,0,0:0:0:0:
153,192,200,1,0,0:0:0:0:
358,192,376,1,0,0:0:0:0:
460,192,553,1,0,0:0:0:0:
460,192,729,128,0,1435:0:0:0:0:
358,192,906,128,0,1612:0:0:0:0:
256,192,1082,128,0,1788:0:0:0:0:
153,192,1259,128,0,1965:0:0:0:0:
51,192,1435,128,0,2141:0:0:0:0:
51,192,2318,1,12,0:0:0:0:
153,192,2318,1,4,0:0:0:0:
256,192,2318,1,6,0:0:0:0:
358,192,2318,1,14,0:0:0:0:
460,192,2318,1,0,0:0:0:0:
";

    public string? CurrentTool { get; set; }

    public static Beatmap GetBeatmapStream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ManiaData));
        return BeatmapDecoder.Decode(stream);
    }

    public FrameworkElement BuildVisual(ManiaBeatmap input, bool converted)
    {
        Console.WriteLine($"[INFO] ManiaPreviewOsu.BuildVisual called with converted={converted}");
        const int maxRows = 10;
        var beatmap = GetBeatmapStream();
        var (columns, notes, quarterMs) = PreviewTransformation.BuildFromBeatmap(beatmap, maxRows);
        return BuildManiaTimeRowsFromNotes(notes, columns, maxRows, quarterMs);
    }

    // 根据时间行构建动态预览控件（按时间分组、限制行数）
    private FrameworkElement BuildManiaTimeRowsFromNotes(List<ManiaBeatmap.PreViewManiaNote> allNotes, int columns, int maxRows, double quarterMs = 0, System.Func<int, ManiaBeatmap.PreViewManiaNote, ManiaBeatmap.PreViewManiaNote>? noteTransform = null)
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

        return new DynamicPreviewControl(grouped, columns, quarterMs);
    }
}