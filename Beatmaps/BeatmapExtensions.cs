using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Beatmaps;

public static class BeatmapExtensions
{
    public static double GetBPM(this Beatmap? beatmap, bool asMs = false)
    {
        if (beatmap == null)
            return 180;

        var tp = beatmap.TimingPoints
            .Where(p => p.BeatLength > 0)
            .OrderBy(p => p.Offset)
            .ToList();
        if (tp.Count == 0)
            return 0;

        double maxDuration = -1;
        var longestPoint = tp[0];
        for (int i = 0; i < tp.Count - 1; i++)
        {
            double duration = tp[i + 1].Offset - tp[i].Offset;
            if (duration > maxDuration)
            {
                maxDuration = duration;
                longestPoint = tp[i];
            }
        }

        double bpm = Math.Round(60000 / longestPoint.BeatLength, 2);
        return asMs ? 60000.0 / Math.Max(1.0, bpm) : bpm;
    }

    public static (int[,], List<int>) BuildMatrix(this Beatmap beatmap)
    {
        int cs = (int)beatmap.DifficultySection.CircleSize;
        var timePoints = new SortedSet<int>();
        foreach (var hitObject in beatmap.HitObjects)
        {
            timePoints.Add(hitObject.StartTime);
            if (hitObject.EndTime > 0)
            {
                timePoints.Add(hitObject.EndTime);
            }
        }

        var timeAxis = timePoints.ToList();
        int h = timeAxis.Count;
        int a = cs;

        int[,] matrix = new int[h, a];
        for (int i = 0; i < h; i++)
        {
            for (int j = 0; j < a; j++)
            {
                matrix[i, j] = -1;
            }
        }

        Dictionary<int, int> timeToRow = timeAxis
            .Select((time, index) => new { time, index })
            .ToDictionary(x => x.time, x => x.index);

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            var hitObject = beatmap.HitObjects[i];
            int column = positionXToColumn(cs, (int)hitObject.Position.X);
            int startRow = timeToRow[hitObject.StartTime];

            matrix[startRow, column] = i;

            if (hitObject.EndTime > 0)
            {
                int endRow = timeToRow[hitObject.EndTime];

                for (int row = startRow + 1; row <= endRow; row++)
                {
                    matrix[row, column] = -7;
                }
            }
        }

        return (matrix, timeAxis);
    }

    private static int positionXToColumn(int CS, int X)
    {
        int column = (int)Math.Floor(X * (double)CS / 512);
        return column;
    }

    private static int columnToPositionX(int CS, int column)
    {
        // int set_x = ((column - 1) * 512 / CS) + (256 / CS); // 不要删
        int x = (int)Math.Floor((column + 0.5) * (512.0 / CS));
        return x;
    }

    public static double GetLNPercent(this Beatmap beatmap)
    {
        double noteCount = beatmap.HitObjects.Count;
        if (noteCount == 0) return 0;
        double LNCountCount = beatmap.HitObjects.Count(hitObject => hitObject.EndTime > hitObject.StartTime);
        return LNCountCount / noteCount * 100;
    }

    public static string GetOutputOsuFileName(this Beatmap beatmap)
    {
        // 清理文件名中的非法字符
        string artist = beatmap.MetadataSection.Artist ?? "";
        string title = beatmap.MetadataSection.Title ?? "";
        string creator = beatmap.MetadataSection.Creator ?? "";
        string version = beatmap.MetadataSection.Version ?? "";

        // 使用正则表达式移除所有非法字符
        string invalidCharsPattern = $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]";
        artist = Regex.Replace(artist, invalidCharsPattern, "");
        title = Regex.Replace(title, invalidCharsPattern, "");
        creator = Regex.Replace(creator, invalidCharsPattern, "");
        version = Regex.Replace(version, invalidCharsPattern, "");
        var FileName = $"{artist} - {title} ({creator}) [{version}]";
        var remainder = 250 - beatmap.OriginalFilePath.Length;
        
        if (FileName.Length > remainder)
        {
            FileName = FileName.Substring(0, remainder) + "...";
        }
        
        return FileName;
    }

    public static Dictionary<double, double> GetBeatLengthList(this Beatmap beatmap)
    {
        var tp = beatmap.TimingPoints
            .Where(p => p.BeatLength > 0)
            .OrderBy(p => p.Offset)
            .ToList();
        if (tp.Count == 0)
            return new Dictionary<double, double>();

        var beatLengthDict = new Dictionary<double, double>();
        foreach (var timingPoint in tp)
        {
            beatLengthDict[timingPoint.Offset] = timingPoint.BeatLength;
        }

        return beatLengthDict;
    }

    public static ManiaBeatmap GetManiaBeatmap(this Beatmap beatmap, string? path = null)
    {
        if (beatmap == null)
        {
            Logger.WriteLine(LogLevel.Error, "GetManiaBeatmap为空");
            throw new InvalidDataException("GetManiaBeatmap为空");
        }
        if (path != null)
            beatmap.OriginalFilePath = path;

        return new ManiaBeatmap(beatmap);
    }
}