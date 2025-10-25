using System.Collections.Generic;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Beatmaps.Sections;

namespace krrTools.Beatmaps
{
    public interface IBeatmap
    {
        string FilePath { get; set; }
        string OutputFilePath { get; set; }
        double BPM { get; set; }
        string BPMDisplay { get; set; }
        double xxyStarRating { get; set; }
        double KRR_LV { get; set; }
        double YLS_LV { get; set; }
        double TotalTime { get; set; }
        int KeyCount { get; set; }
        int NoteCount { get; }
        int HoldNoteCount { get; }
        double LNPercent { get; set; }

        // 参考 osu.Game.Beatmaps.IBeatmap，添加兼容属性
        BeatmapMetadataSection? Metadata { get; }
        BeatmapDifficultySection? Difficulty { get; }
        BeatmapGeneralSection? General { get; }
        BeatmapEditorSection? Editor { get; }
        BeatmapEventsSection? Events { get; }
        BeatmapColoursSection? Colours { get; }
        List<HitObject>? HitObjects { get; }
        double AudioLeadIn { get; }
        float StackLeniency { get; }
        bool SpecialStyle { get; }
        bool LetterboxInBreaks { get; }
        bool WidescreenStoryboard { get; }
        bool EpilepsyWarning { get; }
        bool SamplesMatchPlaybackRate { get; }
    }
}
