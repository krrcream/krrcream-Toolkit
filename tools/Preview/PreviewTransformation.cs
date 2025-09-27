using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using OsuParsers.Beatmaps.Objects;
using krrTools.tools.DPtool;
using krrTools.tools.LNTransformer;
using krrTools.tools.N2NC;
using krrTools.Tools.OsuParser;

namespace krrTools.tools.Preview;

// 提供用于预览的转换辅助方法（仅在内存中操作，不写出文件）
public static class PreviewTransformation
{
    // 从 osu 文件构建原始 mania 音符列表（按时间行取前 maxRows 行）
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildOriginal(
        string osuPath, int maxRows)
    {
        if (!TryBuildMatrix(osuPath, null, out var beatmap, out _, out var matrix, out var timeAxis, out var cs))
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var bpm = GetBpmDouble(beatmap);
        var quarterMs = 60000.0 / Math.Max(1.0, bpm);
        var res = MatrixToNotes(matrix, timeAxis, cs, maxRows);
        return (res.columns, res.notes, quarterMs);
    }

    // 返回第一个非空时间点（毫秒），找不到返回 null
    public static int? GetFirstNonEmptyTime(string osuPath)
    {
        if (!TryBuildMatrix(osuPath, null, out _, out _, out var matrix, out var timeAxis, out _)) return null;
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        for (var r = 0; r < rows; r++)
        for (var c = 0; c < cols; c++)
            if (matrix[r, c] >= 0)
                return timeAxis[r];
        return null;
    }

    // 获取一拍的毫秒长度（使用解析到的 BPM，失败时回退到 180 BPM）
    public static double GetQuarterMs(string osuPath)
    {
        if (!TryDecodeMania(osuPath, out var beatmap, out _)) return 60000.0 / 180.0;
        var bpm = GetBpmDouble(beatmap);
        return 60000.0 / Math.Max(1.0, bpm);
    }

    // 在有序时间轴上用二分查找找到 startMs..endMs 对应的起止行索引
    private static void FindTimeWindowIndices(List<int>? timeAxis, int startMs, int endMs, out int startIdx, out int endIdx)
    {
        startIdx = -1;
        endIdx = -1;
        if (timeAxis == null || timeAxis.Count == 0) return;
        // find first index with value >= startMs
        int lo = 0, hi = timeAxis.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (timeAxis[mid] >= startMs) hi = mid - 1; else lo = mid + 1;
        }
        startIdx = lo < timeAxis.Count ? lo : -1;
        // find last index with value <= endMs
        lo = 0; hi = timeAxis.Count - 1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (timeAxis[mid] <= endMs) lo = mid + 1; else hi = mid - 1;
        }
        endIdx = hi >= 0 ? hi : -1;
    }

    // 按时间窗口切片矩阵和时间轴，返回行区间内的子矩阵及对应时间轴
    private static (int[,] matrix, List<int> timeAxis) SliceMatrixByTime(int[,] matrix, List<int>? timeAxis, int startMs,
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

    // 构建一个时间窗口内的原始音符（通过矩阵切片实现）
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildOriginalWindow(
        string osuPath, int startMs, int endMs)
    {
        if (!TryBuildMatrix(osuPath, null, out var beatmap, out _, out var matrix, out var timeAxis, out var cs))
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        return BuildWindowFromMatrix(matrix, timeAxis, cs, beatmap, startMs, endMs);
    }

    // 从 Beatmap 构建 mania 音符列表（用于预览转换后的数据）
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildFromBeatmap(Beatmap beatmap, int maxRows)
    {
        if (beatmap.GeneralSection.ModeId != 3) return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var cs = (int)beatmap.DifficultySection.CircleSize;
        var (matrix, timeAxis) = BuildMatrixFromBeatmap(beatmap);
        var bpm = GetBpmDouble(beatmap);
        var quarterMs = 60000.0 / Math.Max(1.0, bpm);
        var res = MatrixToNotes(matrix, timeAxis, cs, maxRows);
        return (res.columns, res.notes, quarterMs);
    }

    // 从 Beatmap 构建时间窗口内的音符
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildFromBeatmapWindow(Beatmap beatmap, int startMs, int endMs)
    {
        if (beatmap.GeneralSection.ModeId != 3) return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var cs = (int)beatmap.DifficultySection.CircleSize;
        var (matrix, timeAxis) = BuildMatrixFromBeatmap(beatmap);
        var bpm = GetBpmDouble(beatmap);
        var quarterMs = 60000.0 / Math.Max(1.0, bpm);
        var (slicedMatrix, slicedTimeAxis) = SliceMatrixByTime(matrix, timeAxis, startMs, endMs);
        var res = MatrixToNotes(slicedMatrix, slicedTimeAxis, cs, int.MaxValue);
        return (res.columns, res.notes, quarterMs);
    }

    // 从 OsuFileV14 构建 mania 音符列表（用于预览转换后的数据）
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildFromOsuFileV14(OsuFileV14 osu, int maxRows)
    {
        if (!TryBuildMatrixFromOsu(osu, out var matrix, out var timeAxis, out var cs))
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var bpm = GetBpmDoubleFromOsu(osu);
        var quarterMs = 60000.0 / Math.Max(1.0, bpm);
        var res = MatrixToNotes(matrix, timeAxis, cs, maxRows);
        return (res.columns, res.notes, quarterMs);
    }

    // 从 OsuFileV14 构建时间窗口内的音符
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildFromOsuFileV14Window(OsuFileV14 osu, int startMs, int endMs)
    {
        if (!TryBuildMatrixFromOsu(osu, out var matrix, out var timeAxis, out var cs))
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var bpm = GetBpmDoubleFromOsu(osu);
        var quarterMs = 60000.0 / Math.Max(1.0, bpm);
        var (slicedMatrix, slicedTimeAxis) = SliceMatrixByTime(matrix, timeAxis, startMs, endMs);
        var res = MatrixToNotes(slicedMatrix, slicedTimeAxis, cs, int.MaxValue);
        return (res.columns, res.notes, quarterMs);
    }

    private static (int[,], List<int>) BuildMatrixFromBeatmap(Beatmap beatmap)
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

        // 初始化二维矩阵，所有元素默认为-1（代表空）
        int[,] matrix = new int[h, a];
        for (int i = 0; i < h; i++)
        {
            for (int j = 0; j < a; j++)
            {
                matrix[i, j] = -1;
            }
        }

        Dictionary<int, int> timeToRow = new Dictionary<int, int>();
        for (int i = 0; i < timeAxis.Count; i++)
        {
            timeToRow[timeAxis[i]] = i;
        }

        for (int i = 0; i < beatmap.HitObjects.Count; i++)
        {
            var hitObject = beatmap.HitObjects[i];
            int column = positionXToColumn(cs, (int)hitObject.Position.X);
            int startRow = timeToRow[hitObject.StartTime];

            matrix[startRow, column] = i;

            if (hitObject.EndTime > 0)
            {
                int endRow = timeToRow[hitObject.EndTime];
                for (int r = startRow; r <= endRow; r++)
                {
                    matrix[r, column] = i;
                }
            }
        }
        return (matrix, timeAxis);
    }

    private static int positionXToColumn(int cs, int positionX)
    {
        // 假设列从0到cs-1，positionX从0到511
        return positionX * cs / 512;
    }





    // 直接从内存 OsuFileV14 构建矩阵（无文件 I/O）
    private static bool TryBuildMatrixFromOsu(OsuFileV14? osu, out int[,] matrix, out List<int> timeAxis, out int cs)
    {
        matrix = new int[0, 0];
        timeAxis = new List<int>();
        cs = 0;
        if (osu == null || osu.HitObjects.Count == 0) return false;
        cs = (int)osu.General.CircleSize;

        var timeSet = new SortedSet<int>();
        foreach (var h in osu.HitObjects)
        {
            timeSet.Add(h.StartTime);
            var end = h.EndTime;
            if (end > h.StartTime) timeSet.Add(end);
        }

        timeAxis = timeSet.ToList();
        if (timeAxis.Count == 0) return false;

        var rows = timeAxis.Count;
        var cols = Math.Max(1, cs);
        matrix = new int[rows, cols];
        for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) matrix[r, c] = -1;

        var timeToRow = new Dictionary<int, int>();
        for (int i = 0; i < timeAxis.Count; i++) timeToRow[timeAxis[i]] = i;

        for (int i = 0; i < osu.HitObjects.Count; i++)
        {
            var h = osu.HitObjects[i];
            int column = -1;

            if (h.Column >= 0 && h.Column < cols) column = h.Column;

            // 尝试 KeyX 映射
            if (column == -1)
            {
                if (OsuFileV14.KeyX.TryGetValue(cs, out var keyXs) && keyXs.Count == cols)
                {
                    var best = 0; var bestDist = Math.Abs(h.X - keyXs[0]);
                    for (int k = 1; k < keyXs.Count; k++) { var d = Math.Abs(h.X - keyXs[k]); if (d < bestDist) { bestDist = d; best = k; } }
                    column = best;
                }
            }

            // 回退到 floor 映射
            if (column == -1) column = Math.Clamp((int)Math.Floor(h.X * cs / 512.0), 0, cols - 1);

            if (!timeToRow.TryGetValue(h.StartTime, out var startRow))
            {
                // 如果没有完全匹配的行，尝试在 2ms 以内匹配最近行
                int nearest = -1;
                int lo = 0, hi = timeAxis.Count - 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (timeAxis[mid] == h.StartTime) { nearest = mid; break; }
                    if (timeAxis[mid] < h.StartTime) lo = mid + 1; else hi = mid - 1;
                }
                if (nearest == -1)
                {
                    int cand1 = (lo < timeAxis.Count) ? lo : -1;
                    int cand0 = (hi >= 0) ? hi : -1;
                    int bestIdx = -1; int bestDist = int.MaxValue;
                    if (cand0 != -1)
                    {
                        var d = Math.Abs(timeAxis[cand0] - h.StartTime);
                        if (d < bestDist) { bestDist = d; bestIdx = cand0; }
                    }
                    if (cand1 != -1)
                    {
                        var d = Math.Abs(timeAxis[cand1] - h.StartTime);
                        if (d < bestDist)
                        {
                            bestIdx = cand1;
                        }
                    }
                    nearest = bestIdx;
                }

                if (nearest != -1 && Math.Abs(timeAxis[nearest] - h.StartTime) <= 2) startRow = nearest; else continue;
            }

            matrix[startRow, column] = i;
            var end = h.EndTime;
            if (end > h.StartTime && timeToRow.TryGetValue(end, out var endRow))
            {
                for (int r = startRow + 1; r <= endRow && r < rows; r++) matrix[r, column] = -7;
            }
        }

        return true;
    }





    // 将矩阵和时间轴转换为预览音符列表（只取前 maxRows 个时间行）
    private static (int columns, List<BasePreviewProcessor.ManiaNote> notes) MatrixToNotes(int[,] matrix,
        List<int> timeAxis, int columns, int maxRows)
    {
        var notes = new List<BasePreviewProcessor.ManiaNote>();
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
                    notes.Add(new BasePreviewProcessor.ManiaNote
                    {
                        X = (int)((c + 0.5) * (512.0 / cols)),
                        Time = t,
                        IsHold = endTime.HasValue,
                        EndTime = endTime
                    });
                }
            }

            takenRows++;
        }

        return (columns, notes);
    }

    // 尝试解码 mania 格式并返回 cs
    private static bool TryDecodeMania(string osuPath, out Beatmap? beatmap, out int cs)
    {
        beatmap = null;
        cs = 0;
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
        cs = (int)beatmap.DifficultySection.CircleSize;
        return true;
    }

    // 集中构建矩阵的辅助方法（可传入可选的 ConverterOptions）
    private static bool TryBuildMatrix(string osuPath, N2NCOptions? options, out Beatmap? beatmap,
        out N2NC.N2NC conv, out int[,] matrix, out List<int> timeAxis, out int cs)
    {
        beatmap = null;
        conv = new N2NC.N2NC();
        matrix = new int[0, 0];
        timeAxis = new List<int>();
        cs = 0;
        if (!TryDecodeMania(osuPath, out beatmap, out cs)) return false;
        conv = options != null ? new N2NC.N2NC { options = options } : new N2NC.N2NC();
        var tuple = conv.BuildMatrix(beatmap!);
        matrix = tuple.Item1;
        timeAxis = tuple.Item2;
        return true;
    }

    // 复用解析器实例以避免重复分配
    private static readonly OsuAnalyzer ana = new();

    // 将 OsuAnalyzer 输出的 BPM 字符串解析为 double，失败时使用回退值
    private static double GetBpmDouble(Beatmap? beatmap, double fallback = 180)
    {
        if (beatmap == null) return fallback;
        try
        {
            // Use numeric API for primary BPM. Keep fallback if unexpected value.
            double bpm = ana.GetBPM(beatmap);
            if (bpm <= 0 || double.IsNaN(bpm) || double.IsInfinity(bpm)) return fallback;
            return bpm;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    private static double GetBpmDoubleFromOsu(OsuFileV14 osu)
    {
        if (osu.TimingPoints.Count > 0)
        {
            var tp = osu.TimingPoints[0];
            return 60000.0 / Math.Max(1.0, tp.BeatLength);
        }
        return 180.0;
    }

    // 通用：从矩阵和时间轴构建预览窗口并返回拍子信息
    private static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildWindowFromMatrix(int[,] matrix, List<int> timeAxis, int columns, Beatmap? beatmap, int startMs, int endMs)
     {
        double BPM = GetBpmDouble(beatmap);
         double quarterMs = 60000.0 / Math.Max(1.0, BPM);
         var (mSlice, tSlice) = SliceMatrixByTime(matrix, timeAxis, startMs, endMs);
         if (mSlice.GetLength(0) == 0) return (columns, new List<BasePreviewProcessor.ManiaNote>(), quarterMs);
         var res = MatrixToNotes(mSlice, tSlice, columns, int.MaxValue);
         return (res.columns, res.notes, quarterMs);
     }

     // 获取osu文件的背景图路径
     public static string? GetBackgroundImagePath(string osuPath)
     {
         if (!File.Exists(osuPath)) return null;
         
         // 跳过内置测试文件，不加载背景图
         var fileName = Path.GetFileName(osuPath);
         if (fileName == "mania-last-object-not-latest.osu")
         {
             Debug.WriteLine($"Skipping background image for built-in test file: {osuPath}");
             return null;
         }
         
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
                     Debug.WriteLine($"Found background image: {fullPath}");
                     return fullPath;
                 }
             }
             else
             {
                 Debug.WriteLine($"No background image found in {osuPath}");
             }
         }
         catch (Exception ex)
         {
             Debug.WriteLine($"Failed to get background image from {osuPath}: {ex.Message}");
         }
         
         return null;
     }
}