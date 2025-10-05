using System;

namespace krrTools.Beatmaps;

public interface IBeatmap
{
    string FilePath { get; set; }
    string OutputFilePath { get; set; }
    double BPM { get; set; }
    String BPMDisplay { get; set; }
    double xxyStarRating { get; set; }
    double KRR_LV { get; set; }
    double YLS_LV { get; set; }
    double TotalTime { get; set; }
    int KeyCount { get; set; }
    int NoteCount { get; }
    int HoldNoteCount { get; }
    double LNPercent { get; set; }
}