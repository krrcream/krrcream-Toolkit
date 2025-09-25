using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Linq;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
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

    // 构建一个时间窗口内的转换器输出音符
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildConverterWindow(
        string osuPath, N2NCOptions options, int startMs, int endMs)
    {
        if (!TryBuildMatrix(osuPath, options, out var beatmap, out var conv, out var matrix, out var timeAxis,
                out var cs)) return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
            double BPM = GetBpmDouble(beatmap);
             double beatLength = 60000 / BPM * 4;
             double convertTime = Math.Max(1, options.TransformSpeed * beatLength - 10);
             int turn = (int)options.TargetKeys - cs;
             int[,] newMatrix;
             var rng = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();

        // 先在全矩阵上应用转换，再切片时间窗口
        if (turn < 0)
        {
            newMatrix = conv.SmartReduceColumns(matrix, timeAxis, -turn, convertTime, beatLength);
            conv.DensityReducer(newMatrix, (int)options.TargetKeys - (int)options.MaxKeys, (int)options.MinKeys, (int)options.TargetKeys, rng);
        }
        else if (turn > 0)
        {
            var (oldMTX, insertMTX) = conv.convertMTX(turn, timeAxis, convertTime, cs, rng);
            newMatrix = conv.convert(matrix, oldMTX, insertMTX, timeAxis, (int)options.TargetKeys, beatLength, rng);
            conv.DensityReducer(newMatrix, (int)options.TargetKeys - (int)options.MaxKeys, (int)options.MinKeys, (int)options.TargetKeys, rng);
        }
        else
        {
            newMatrix = matrix;
            if (options.MaxKeys < options.TargetKeys)
            {
                conv.DensityReducer(newMatrix, (int)options.TargetKeys - (int)options.MaxKeys, (int)options.MinKeys, (int)options.TargetKeys, rng);
            }
        }

        return BuildWindowFromMatrix(newMatrix, timeAxis, (int)options.TargetKeys, beatmap, startMs, endMs);
    }

    public struct LNPreviewParameters
    {
        public double LevelValue;
        public double PercentageValue;
        public double DivideValue;
        public double ColumnValue;
        public double GapValue;
        public bool OriginalLN;
        public bool FixError;
        public double OverallDifficulty;
    }

    // 复用解析器实例以避免重复分配
    private static readonly OsuAnalyzer ana = new();

    // LN 预览：在内存中做 LN 转换，然后生成窗口内的音符
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildLNWindow(
        string osuPath, LNPreviewParameters p, int startMs, int endMs)
    {
         var osu = OsuFileProcessor.ReadFile(osuPath);
         var transformed = LNTransformerCore.TransformFull(osu, p);

         // 尝试直接从内存 OsuFileV14 构建矩阵
        if (TryBuildMatrixFromOsu(transformed, out var matrix, out var timeAxis, out var cs))
        {
            // 如果转换后的矩阵在请求窗口内有音符，直接返回切片
            FindTimeWindowIndices(timeAxis, startMs, endMs, out var sIdx, out var eIdx);
            if (sIdx != -1 && eIdx != -1 && eIdx >= sIdx)
            {
                for (int r = sIdx; r <= eIdx; r++)
                {
                    for (int c = 0; c < matrix.GetLength(1); c++) if (matrix[r, c] >= 0) return BuildWindowFromMatrix(matrix, timeAxis, cs, null, startMs, endMs);
                }
            }

            // 如果转换放置了音符但窗口内为空，则把窗口移动到第一个放置音符处（保留长度）
            var totalPlaced = 0;
            int firstRow = -1;
            for (int r = 0; r < matrix.GetLength(0); r++)
            {
                for (int c = 0; c < matrix.GetLength(1); c++)
                {
                    if (matrix[r, c] >= 0)
                    {
                        totalPlaced++;
                        if (firstRow == -1) firstRow = r;
                    }
                }
            }

            if (totalPlaced > 0 && firstRow != -1)
            {
                var firstTime = timeAxis[firstRow];
                var windowLen = Math.Max(1, endMs - startMs);
                var newStart = firstTime;
                var newEnd = newStart + windowLen;
                Debug.WriteLine($"[Preview] shifting LN preview window to transformed first note: start={newStart} end={newEnd}");
                return BuildWindowFromMatrix(matrix, timeAxis, cs, null, newStart, newEnd);
            }

            // 没有放置任何音符 -> 返回空
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        }

        // 如果内存路径失败，回退到写文件再解析（最后手段）
        var tmp = Path.GetTempFileName();
        var tmpOsu = Path.ChangeExtension(tmp, ".osu");
        try
        {
            OsuFileProcessor.WriteOsuFile(transformed, tmpOsu);
            if (!TryBuildMatrix(tmpOsu, null, out var b2, out _, out var m2, out var t2, out var cs2))
                return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
            return BuildWindowFromMatrix(m2, t2, cs2, b2, startMs, endMs);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            if (File.Exists(tmpOsu)) File.Delete(tmpOsu);
        }
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

    // DP 专用预览转换（左右分离、镜像、密度调节等） - 返回前 maxRows 时间行
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildDP(string osuPath,
        DPToolOptions options, int maxRows)
    {
        if (!TryBuildMatrix(osuPath, null, out var beatmap, out var conv, out var matrix, out var timeAxis, out var CS))
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var BPM = GetBpmDouble(beatmap);
        var quarterMs = 60000.0 / Math.Max(1.0, BPM);
        var rng = new Random(114514);
        int[,] orgMTX;
        if (options.ModifySingleSideKeyCount && options.SingleSideKeyCount > CS)
        {
            var target = options.SingleSideKeyCount;
            var beatLength = 60000 / BPM * 4;
            var convertTime = Math.Max(1, 4 * beatLength - 10);
            var (oldMTX, insertMTX) = conv.convertMTX(target - CS, timeAxis, convertTime, CS, rng);
            orgMTX = conv.convert(matrix, oldMTX, insertMTX, timeAxis, target, beatLength, rng);
        }
        else
        {
            orgMTX = matrix;
        }

        var orgL = (int[,])orgMTX.Clone();
        var orgR = (int[,])orgMTX.Clone();
        if (options.LMirror) orgL = Mirror(orgL);
        if (options.RMirror) orgR = Mirror(orgR);
        if (options.LDensity && orgL.GetLength(1) > options.LMaxKeys)
        {
            var rL = new Random(114514);
            conv.DensityReducer(orgL, orgL.GetLength(1) - options.LMaxKeys, options.LMinKeys, orgL.GetLength(1), rL);
        }

        if (options.RDensity && orgR.GetLength(1) > options.RMaxKeys)
        {
            var rR = new Random(114514);
            conv.DensityReducer(orgR, orgR.GetLength(1) - options.RMaxKeys, options.RMinKeys, orgR.GetLength(1), rR);
        }

        var result = Concatenate(orgL, orgR);
        var res = MatrixToNotes(result, timeAxis, result.GetLength(1), maxRows);
        return (res.columns, res.notes, quarterMs);
    }

    // DP 专用预览窗口版本（按时间切片）
    public static (int columns, List<BasePreviewProcessor.ManiaNote> notes, double quarterMs) BuildDPWindow(
        string osuPath, DPToolOptions options, int startMs, int endMs)
    {
        if (!TryBuildMatrix(
                osuPath, 
                null, 
                out var beatmap, 
                out var conv, 
                out var matrix, 
                out var timeAxis, 
                out var CS))
            return (0, new List<BasePreviewProcessor.ManiaNote>(), 0);
        var BPM = GetBpmDouble(beatmap);
        var rng = new Random(114514);
        int[,] orgMTX;
        if (options.ModifySingleSideKeyCount && options.SingleSideKeyCount > CS)
        {
            var target = options.SingleSideKeyCount;
            var beatLength = 60000 / BPM * 4;
            var convertTime = Math.Max(1, 4 * beatLength - 10);
            var (oldMTX, insertMTX) = conv.convertMTX(target - CS, timeAxis, convertTime, CS, rng);
            orgMTX = conv.convert(matrix, oldMTX, insertMTX, timeAxis, target, beatLength, rng);
        }
        else
        {
            orgMTX = matrix;
        }

        var orgL = (int[,])orgMTX.Clone();
        var orgR = (int[,])orgMTX.Clone();
        if (options.LMirror) orgL = Mirror(orgL);
        if (options.RMirror) orgR = Mirror(orgR);
        if (options.LDensity && orgL.GetLength(1) > options.LMaxKeys)
        {
            var rL = new Random(114514);
            conv.DensityReducer(orgL, orgL.GetLength(1) - options.LMaxKeys, options.LMinKeys, orgL.GetLength(1), rL);
        }

        if (options.RDensity && orgR.GetLength(1) > options.RMaxKeys)
        {
            var rR = new Random(114514);
            conv.DensityReducer(orgR, orgR.GetLength(1) - options.RMaxKeys, options.RMinKeys, orgR.GetLength(1), rR);
        }

        var result2 = Concatenate(orgL, orgR);
        return BuildWindowFromMatrix(result2, timeAxis, result2.GetLength(1), beatmap, startMs, endMs);
    }

    // 反转矩阵（左右镜像）
    private static int[,] Mirror(int[,] matrix)
    {
        var rows = matrix.GetLength(0);
        var cols = matrix.GetLength(1);
        var res = new int[rows, cols];
        for (var i = 0; i < rows; i++)
        for (var j = 0; j < cols; j++)
            res[i, j] = matrix[i, cols - 1 - j];
        return res;
    }

    // 水平拼接两个矩阵
    private static int[,] Concatenate(int[,] A, int[,] B)
    {
        var rows = A.GetLength(0);
        var colsA = A.GetLength(1);
        var colsB = B.GetLength(1);
        var res = new int[rows, colsA + colsB];
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < colsA; j++) res[i, j] = A[i, j];
            for (var j = 0; j < colsB; j++) res[i, j + colsA] = B[i, j];
        }

        return res;
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
}