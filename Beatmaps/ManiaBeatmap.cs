using System;
using System.Collections.Generic;
using OsuFileIO.HitObject.Mania;
using OsuParsers.Decoders;

namespace krrTools.Beatmaps;

/// <summary>
/// Mania模式的Beatmap封装类，未来的统一主体接口
/// 包括路径实现和对象实现
/// </summary>
public class ManiaBeatmap : OsuParsers.Beatmaps.Beatmap
{
    public ManiaBeatmap(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            throw new ArgumentException("File path is null or empty.");
        
        var beatmap = BeatmapDecoder.Decode(filePath);
        if (beatmap.GeneralSection.ModeId != 3)
            throw new ArgumentException("Beatmap is not in Mania mode.");
        
        GeneralSection = beatmap.GeneralSection;
        EditorSection = beatmap.EditorSection;
        MetadataSection = beatmap.MetadataSection;
        DifficultySection = beatmap.DifficultySection;
        EventsSection = beatmap.EventsSection;
        TimingPoints = beatmap.TimingPoints;
        HitObjects = beatmap.HitObjects;
        ColoursSection = beatmap.ColoursSection;
    }

    public ManiaBeatmap(OsuParsers.Beatmaps.Beatmap beatmap)
    {
        if (beatmap.GeneralSection.ModeId != 3)
            throw new ArgumentException("Not a mania beatmap.");
        
        GeneralSection = beatmap.GeneralSection;
        EditorSection = beatmap.EditorSection;
        MetadataSection = beatmap.MetadataSection;
        DifficultySection = beatmap.DifficultySection;
        EventsSection = beatmap.EventsSection;
        TimingPoints = beatmap.TimingPoints;
        HitObjects = beatmap.HitObjects;
        ColoursSection = beatmap.ColoursSection;
    }

    public int KeyCount => (int)DifficultySection.CircleSize;
    
    public double MinBPM { get; set; }
    public double MaxBPM { get; set; }
    public double BPM { get; set; }
    public double xxyStarRating { get; set; }
    public double KRR_LV { get; set; }
    public double YLS_LV { get; set; }
    public double TotalTime { get; set; }
    public int NoteCount { get; set; }
    public int LNCount { get; set; }
    
    public List<ManiaHitObject> ManiaHitObjects { get; set; } = new List<ManiaHitObject>();
    public List<PreViewManiaNote> note = new List<PreViewManiaNote>();
    //Note封装 以后在搞拓展归类
    
    public abstract class PreViewManiaNote
    {
        public int Index;
        public int StartTime;
        public int? EndTime;
        public bool IsHold;
    }
    
    public class SimpleManiaNote : PreViewManiaNote
    {
        // 如果 ManiaNote 有抽象成员，必须实现它们
    }

}