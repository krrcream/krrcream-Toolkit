using System;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.OsuParser;

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
    }

    public int KeyCount => (int)DifficultySection.CircleSize;
}