using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OsuParsers.Beatmaps;

namespace krrTools.Beatmaps;
/// <summary>
/// Mania模式的Beatmap封装类，未来的统一主体接口
/// 包括路径实现和对象实现
/// </summary>
public class ManiaBeatmap : Beatmap, IBeatmap
{
    private ManiaBeatmap() { }

    public static ManiaBeatmap FromBeatmap(Beatmap beatmap)
    {
        if (beatmap.GeneralSection.ModeId != 3)
            throw new ArgumentException("Not a mania beatmap.");

        var mania = new ManiaBeatmap
        {
            FilePath = beatmap.OriginalFilePath,
            TotalTime = beatmap.GeneralSection.Length,
            MetadataSection = beatmap.MetadataSection,
            DifficultySection = beatmap.DifficultySection,
            GeneralSection = beatmap.GeneralSection,
            TimingPoints = beatmap.TimingPoints,
            HitObjects = beatmap.HitObjects,
            ManiaHitObjects = beatmap.HitObjects
                .Select(ho => {
                    var obj = new ManiaHitObject();
                    obj.InitFrom(ho);
                    return obj;
                })
                .ToList(),
        };

        var bpmArray = beatmap.TimingPoints.Select(tp => 60000.0 / tp.BeatLength).ToArray();
        mania.MinBPM = bpmArray.Min();
        mania.MaxBPM = bpmArray.Max();
        mania.BPM = beatmap.GetBPM();
        mania.BPMDisplay = !(Math.Abs(mania.MinBPM - mania.MaxBPM) < 0)
            ? $"{mania.BPM}({mania.MinBPM} - {mania.MaxBPM})"
            : mania.BPM.ToString(CultureInfo.CurrentCulture);
        mania.LNPercent = beatmap.GetLNPercent();
        
        mania.KeyCount = (int)beatmap.DifficultySection.CircleSize;
        mania.NoteCount = beatmap.GeneralSection.CirclesCount;
        mania.HoldNoteCount = beatmap.GeneralSection.SlidersCount;
        
        return mania;
    }
    
    public string BPMDisplay { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public double BPM { get; set; }

    public double xxyStarRating { get; set; }
    public double KRR_LV { get; set; }
    public double YLS_LV { get; set; }
    public double TotalTime { get; set; }
    public int KeyCount { get; set; }
    public int NoteCount { get; private set; }
    public int HoldNoteCount { get; private set; }
    public double LNPercent { get; set; }
    
    //封装 以后在搞拓展归类，先隐藏以防滥用
    public List<ManiaHitObject> ManiaHitObjects { get; set; } = new List<ManiaHitObject>();
    public static Beatmap OriginalBeatmap { get; set; } = new Beatmap();
}