using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using krrTools.Beatmaps;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Tools.Preview;

// 提供用于预览的转换辅助方法（仅在内存中操作，不写出文件）
public static class PreviewTransformation
{
    // 返回第一个非空时间点（毫秒），找不到返回 null
    public static int? GetFirstNonEmptyTime(string osuPath)
    {
        if (!TryDecodeMania(osuPath, out var beatmap)) return null;
        if (beatmap == null) return null;
        var (matrix, timeAxis) = BuildMatrixFromBeatmap(beatmap);
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
            if (matrix[r, c] >= 0)
                return timeAxis[r];
        return null;
    }

    // 在有序时间轴上用二分查找找到 startMs..endMs 对应的起止行索引
    private static void FindTimeWindowIndices(List<int>? timeAxis, int startMs, int endMs, out int startIdx,
        out int endIdx)
    {
        startIdx = -1;
        endIdx = -1;
        if (timeAxis == null || timeAxis.Count == 0) return;
        // find first index with value >= startMs
        int lo = 0, hi = timeAxis.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (timeAxis[mid] >= startMs) hi = mid - 1;
            else lo = mid + 1;
        }

        startIdx = lo < timeAxis.Count ? lo : -1;
        // find last index with value <= endMs
        lo = 0;
        hi = timeAxis.Count - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >> 1;
            if (timeAxis[mid] <= endMs) lo = mid + 1;
            else hi = mid - 1;
        }

        endIdx = hi >= 0 ? hi : -1;
    }

    // 按时间窗口切片矩阵和时间轴，返回行区间内的子矩阵及对应时间轴
    private static (int[,] matrix, List<int> timeAxis) SliceMatrixByTime(int[,] matrix, List<int>? timeAxis,
        int startMs,
        int endMs)
    {
        // matrix is non-nullable (int[,]) so no null check needed
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        if (rows == 0 || cols == 0 || timeAxis == null || timeAxis.Count == 0) return (new int[0, 0], new List<int>());

        FindTimeWindowIndices(timeAxis, startMs, endMs, out var startIdx, out var endIdx);
        if (startIdx == -1 || endIdx == -1 || endIdx < startIdx) return (new int[0, 0], new List<int>());

        var outRows = endIdx - startIdx + 1;
        var outMat = new int[outRows, cols];
        var outTime = new List<int>(outRows);
        for (var r = 0; r < outRows; r++)
        {
            outTime.Add(timeAxis[startIdx + r]);
            for (var c = 0; c < cols; c++) outMat[r, c] = matrix[startIdx + r, c];
        }

        return (outMat, outTime);
    }

    // 通用构建方法
    private static (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) BuildCommon(Beatmap beatmap, int maxRows)
    {
        if (beatmap.GeneralSection.ModeId != 3) return (0, new List<ManiaBeatmap.PreViewManiaNote>(), 0);
        var cs = (int)beatmap.DifficultySection.CircleSize;
        var (matrix, timeAxis) = BuildMatrixFromBeatmap(beatmap);
        var bpm = beatmap.GetBPM();
        var quarterMs = 60000.0 / Math.Max(1.0, bpm);
        var res = MatrixToNotes(matrix, timeAxis, cs, maxRows);
        return (res.columns, res.notes, quarterMs);
    }

    // 从 Beatmap 构建 mania 音符列表（用于预览转换后的数据）
    public static (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) BuildFromBeatmap(
        Beatmap beatmap, int maxRows)
    {
        return BuildCommon(beatmap, maxRows);
    }

    // 从 Beatmap 构建时间窗口内的音符
    public static (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) BuildFromBeatmapWindow(
        Beatmap beatmap, int startMs, int endMs)
    {
        if (beatmap.GeneralSection.ModeId != 3) return (0, new List<ManiaBeatmap.PreViewManiaNote>(), 0);
        var cs = (int)beatmap.DifficultySection.CircleSize;
        var (matrix, timeAxis) = BuildMatrixFromBeatmap(beatmap);
        return BuildWindowFromMatrix(matrix, timeAxis, cs, beatmap, startMs, endMs);
    }

    private static (int[,], List<int>) BuildMatrixFromBeatmap(Beatmap beatmap)
    {
        var cs = (int)beatmap.DifficultySection.CircleSize;
        var timePoints = new SortedSet<int>();
        foreach (var hitObject in beatmap.HitObjects)
        {
            timePoints.Add(hitObject.StartTime);
            if (hitObject.EndTime > 0) timePoints.Add(hitObject.EndTime);
        }

        var timeAxis = timePoints.ToList();
        var h = timeAxis.Count;
        var a = cs;

        // 初始化二维矩阵，所有元素默认为-1（代表空）
        var matrix = new int[h, a];
        for (var i = 0; i < h; i++)
        for (var j = 0; j < a; j++)
            matrix[i, j] = -1;

        var timeToRow = new Dictionary<int, int>();
        for (var i = 0; i < timeAxis.Count; i++) timeToRow[timeAxis[i]] = i;

        for (var i = 0; i < beatmap.HitObjects.Count; i++)
        {
            var hitObject = beatmap.HitObjects[i];
            var column = positionXToColumn(cs, (int)hitObject.Position.X);
            var startRow = timeToRow[hitObject.StartTime];

            matrix[startRow, column] = i;

            if (hitObject.EndTime > 0)
            {
                var endRow = timeToRow[hitObject.EndTime];
                for (var r = startRow; r <= endRow; r++) matrix[r, column] = i;
            }
        }

        return (matrix, timeAxis);
    }

    private static int positionXToColumn(int cs, int positionX)
    {
        // 假设列从0到cs-1，positionX从0到511
        return positionX * cs / 512;
    }

    // 将矩阵和时间轴转换为预览音符列表（只取前 maxRows 个时间行）
    private static (int columns, List<ManiaBeatmap.PreViewManiaNote> notes) MatrixToNotes(int[,] matrix,
        List<int> timeAxis, int columns, int maxRows)
    {
        var notes = new List<ManiaBeatmap.PreViewManiaNote>();
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        var takenRows = 0;
        for (var r = 0; r < rows && takenRows < maxRows; r++)
        {
            var t = timeAxis[r];
            for (var c = 0; c < cols; c++)
            {
                var val = matrix[r, c];
                if (val >= 0) // 音符头
                {
                    // 判断是否为连音：扫描后续是否为 -7
                    var endRow = r;
                    for (var k = r + 1; k < rows; k++)
                        if (matrix[k, c] == -7) endRow = k;
                        else break;
                    int? endTime = endRow > r ? timeAxis[endRow] : null;
                    notes.Add(new ManiaBeatmap.SimpleManiaNote
                    {
                        Index = (int)((c + 0.5) * (512.0 / cols)),
                        StartTime = t,
                        EndTime = endTime,
                        IsHold = endTime.HasValue
                    });
                }
            }

            takenRows++;
        }

        return (columns, notes);
    }

    // 尝试解码 mania 格式并返回 cs
    private static bool TryDecodeMania(string osuPath, out Beatmap? beatmap)
    {
        beatmap = null;
        if (!File.Exists(osuPath)) return false;
        try
        {
            beatmap = BeatmapDecoder.Decode(osuPath);
        }
        catch (Exception)
        {
            return false;
        }

        if (beatmap == null || beatmap.GeneralSection.ModeId != 3) return false;
        return true;
    }

    // 通用：从矩阵和时间轴构建预览窗口并返回拍子信息
    private static (int columns, List<ManiaBeatmap.PreViewManiaNote> notes, double quarterMs) BuildWindowFromMatrix(
        int[,] matrix,
        List<int> timeAxis,
        int columns,
        Beatmap? beatmap,
        int startMs,
        int endMs)
    {
        var BPM = beatmap.GetBPM();
        var quarterMs = 60000.0 / Math.Max(1.0, BPM);
        var (mSlice, tSlice) = SliceMatrixByTime(matrix, timeAxis, startMs, endMs);
        if (mSlice.GetLength(0) == 0) return (columns, new List<ManiaBeatmap.PreViewManiaNote>(), quarterMs);
        var res = MatrixToNotes(mSlice, tSlice, columns, int.MaxValue);
        return (res.columns, res.notes, quarterMs);
    }

    // 获取osu文件的背景图路径
    public static string? GetBackgroundImagePath(string osuPath)
    {
        if (!File.Exists(osuPath)) return null;

        try
        {
            var beatmap = BeatmapDecoder.Decode(osuPath);
            var bgImage = beatmap.EventsSection.BackgroundImage;
            if (!string.IsNullOrWhiteSpace(bgImage))
            {
                // 背景图路径相对于osu文件所在目录
                var osuDir = Path.GetDirectoryName(osuPath);
                if (osuDir != null)
                {
                    var fullPath = Path.Combine(osuDir, bgImage);
                    return fullPath;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to get background image from {osuPath}: {ex.Message}");
        }

        return null;
    }
}