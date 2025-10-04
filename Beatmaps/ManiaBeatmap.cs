
using System;
using System.Globalization;
using System.Linq;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Beatmaps;
/// <summary>
/// Mania模式的Beatmap封装类，未来的统一主体接口
/// 包括路径实现和对象实现
/// </summary>
public class ManiaBeatmap : Beatmap
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
        
        // 设置文件路径
        FilePath = filePath;
    }

    public ManiaBeatmap(Beatmap beatmap)
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

        FilePath = beatmap.OriginalFilePath;

        var bpms = beatmap.TimingPoints.Select(tp => 60000.0 / tp.BeatLength);
        var enumerable = bpms as double[] ?? bpms.ToArray();
        MinBPM = enumerable.Min();
        MaxBPM = enumerable.Max();
        BPM = beatmap.GetBPM();
        BPMDisplay = !(Math.Abs(MinBPM - MaxBPM) < 0) ?
            $"{BPM}({MinBPM} - {MaxBPM})" :
            BPM.ToString(CultureInfo.CurrentCulture);

        LNPercent = beatmap.GetLNPercent();
    }

    public int KeyCount => (int)DifficultySection.CircleSize;
    
    public string FilePath { get; set; } = string.Empty;
    public string OuputFilePath { get; set; } = string.Empty;
    
    public double BPM { get; set; }
    public String BPMDisplay { get; set; } = string.Empty;
    public double xxyStarRating { get; set; }
    public double KRR_LV { get; set; }
    public double YLS_LV { get; set; }
    public double TotalTime { get; set; }
    public int NoteCount => GeneralSection.CirclesCount;
    public int LNCount => GeneralSection.SlidersCount;
    public double LNPercent { get; set; }

    // public List<ManiaHitObject> ManiaHitObjects { get; set; } = new List<ManiaHitObject>();
    // public List<PreViewManiaNote> note = new List<PreViewManiaNote>();
    //Note封装 以后在搞拓展归类，先隐藏以防滥用
}