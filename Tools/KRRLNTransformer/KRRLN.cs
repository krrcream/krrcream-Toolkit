using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using krrTools.Beatmaps;
using krrTools.Configuration;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects.Mania;

namespace krrTools.Tools.KRRLNTransformer
{
    /// <summary>
    /// KRRLN 转换算法实现
    /// </summary>
    public class KRRLN
    {
        /// <summary>
        /// 执行谱面转换
        /// </summary>
        public void TransformBeatmap(Beatmap beatmap, KRRLNTransformerOptions options)
        {
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var processedMatrix = BuildAndProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, timeAxis, options);
        }

        private NoteMatrix BuildAndProcessMatrix(NoteMatrix matrix, List<int> timeAxis, Beatmap beatmap,
            KRRLNTransformerOptions parameters)
        {
            // 创建带种子的随机数生成器
            var RG = parameters.Seed.Value.HasValue
                ? new Random(parameters.Seed.Value.Value)
                : new Random();

            var ManiaObjects = beatmap.HitObjects.OfType<ManiaNote>().ToList();
            var MainBPM = beatmap.MainBPM;
            // var timeAxis1 = new List<int>();

            // 使用传入的矩阵和时间轴，而不是重新构建
            var matrix1 = matrix;
            var timeAxis1 = timeAxis;

            // 初始化原始长度矩阵(用于不处理Org时的面条对齐，也要和原面条对齐)和可用时间矩阵，以及长短面等待修改的矩阵
            var lnLength = DeepCopyMatrix(matrix1);
            var availableTimeMtx = DeepCopyMatrix(matrix1);
            var longLnWaitModify = DeepCopyMatrix(matrix1);
            var shortLnWaitModify = DeepCopyMatrix(matrix1);

            // 完成坐标矩阵，长度矩阵，可用时间矩阵初始化
            for (var i = 0; i < ManiaObjects.Count; i++)
            {
                var obj = ManiaObjects[i];
                var rowindex = obj.RowIndex;
                var colindex = obj.ColIndex;
                if (rowindex.HasValue && colindex.HasValue &&
                    rowindex.Value >= 0 && rowindex.Value < matrix1.Rows &&
                    colindex.Value >= 0 && colindex.Value < matrix1.Cols)
                {
                    matrix1[rowindex.Value, colindex.Value] = i;
                    lnLength[rowindex.Value, colindex.Value] = obj.HoldLength;
                }
            }

            // availableTimeMtx生成
            for (var j = 0; j < matrix1.Cols; j++)
            {
                var currentRow = -1;
                for (var i = 0; i < matrix1.Rows; i++)
                    if (matrix1[i, j] >= 0)
                    {
                        if (currentRow >= 0)
                        {
                            var nextRow = i;
                            var availableTime = timeAxis1[nextRow] - timeAxis1[currentRow];
                            availableTimeMtx[currentRow, j] = availableTime;
                        }

                        currentRow = i;
                    }

                if (currentRow >= 0)
                {
                    var lastTime = timeAxis1.Last();
                    var availableTime = lastTime - timeAxis1[currentRow];
                    availableTimeMtx[currentRow, j] = availableTime;
                }
            }

            // 完成是否是原LN矩阵
            var orgIsLN = new bool[matrix1.Rows, matrix1.Cols];
            foreach (var obj in ManiaObjects)
                if (obj.GetType() == typeof(ManiaHoldNote))
                {
                    var rowindex = obj.RowIndex;
                    var colindex = obj.ColIndex;
                    if (rowindex.HasValue && colindex.HasValue &&
                        rowindex.Value >= 0 && rowindex.Value < orgIsLN.GetLength(0) &&
                        colindex.Value >= 0 && colindex.Value < orgIsLN.GetLength(1))
                        orgIsLN[rowindex.Value, colindex.Value] = true;
                }

            //是否处理原始面条初步判定
            if (!parameters.ProcessOriginalIsChecked.Value)
                for (var i = 0; i < matrix1.Rows; i++)
                for (var j = 0; j < matrix1.Cols; j++)
                    if (orgIsLN[i, j])
                        matrix1[i, j] = -1;

            //通过百分比标记处理位置
            var borderKey = parameters.LengthThreshold.Value.HasValue ? (int)parameters.LengthThreshold.Value.Value : 4; // 默认值4
            if (!borderlist.ContainsKey(borderKey)) borderKey = 0; // 默认值

            var shortLNFlag = new bool[matrix1.Rows, matrix1.Cols];
            var longLNFlag = new bool[matrix1.Rows, matrix1.Cols];
            for (var i = 0; i < matrix1.Rows; i++)
            for (var j = 0; j < matrix1.Cols; j++)
                if (matrix1[i, j] >= 0 && matrix1[i, j] < ManiaObjects.Count)
                {
                    if (availableTimeMtx[i, j] >
                        borderlist[borderKey] * ManiaObjects[matrix1[i, j]].BeatLengthOfThisNote)
                        longLNFlag[i, j] = true;
                    else
                        shortLNFlag[i, j] = true;
                }

            longLNFlag = MarkByPercentage(longLNFlag, parameters.LongPercentage.Value, RG);
            shortLNFlag = MarkByPercentage(shortLNFlag, parameters.ShortPercentage.Value, RG);
            longLNFlag = LimitTruePerRow(longLNFlag, (int)parameters.LongLimit.Value, RG);
            shortLNFlag = LimitTruePerRow(shortLNFlag, (int)parameters.ShortLimit.Value, RG);

            //正式生成longLN矩阵
            for (var i = 0; i < matrix1.Rows; i++)
            for (var j = 0; j < matrix1.Cols; j++)
                if (longLNFlag[i, j])
                {
                    var indexObj = matrix1[i, j];
                    var NewLength = GenerateTriangularRandom(
                        borderlist[borderKey] * ManiaObjects[indexObj].BeatLengthOfThisNote,
                        availableTimeMtx[i, j] - 600000 / MainBPM / 8,
                        availableTimeMtx[i, j] * parameters.LongPercentage.Value / 100,
                        (int)parameters.LongRandom.Value, RG
                    );
                    longLnWaitModify[i, j] = NewLength;
                }

            //正式生成shortLN矩阵
            for (var i = 0; i < matrix1.Rows; i++)
            for (var j = 0; j < matrix1.Cols; j++)
                if (shortLNFlag[i, j])
                {
                    var indexObj = matrix1[i, j];
                    var NewLength = GenerateTriangularRandom(
                        600000 / MainBPM / 8,
                        borderlist[borderKey] * ManiaObjects[indexObj].BeatLengthOfThisNote,
                        availableTimeMtx[i, j] * parameters.ShortPercentage.Value / 100,
                        (int)parameters.ShortRandom.Value, RG
                    );
                    shortLnWaitModify[i, j] = NewLength;
                }

            var result = MergeMatrices(longLnWaitModify, shortLnWaitModify);


            return new NoteMatrix(result);
        }

        private Beatmap ApplyChangesToHitObjects(Beatmap beatmap, NoteMatrix mergeMTX, List<int> timeAxis,
            KRRLNTransformerOptions options)
        {
            var matrix2 = mergeMTX.GetData();
            var timeAxisTemp = timeAxis; // 使用传入的 timeAxis

            var ManiaObjects = beatmap.HitObjects.OfType<ManiaNote>().ToList();

            for (var i = 0; i < ManiaObjects.Count; i++)
            {
                var obj = ManiaObjects[i];
                var rowindex = obj.RowIndex;
                var colindex = obj.ColIndex;
                if (rowindex.HasValue && colindex.HasValue &&
                    rowindex.Value >= 0 && rowindex.Value < matrix2.GetLength(0) &&
                    colindex.Value >= 0 && colindex.Value < matrix2.GetLength(1))
                    matrix2[rowindex.Value, colindex.Value] = beatmap.HitObjects.IndexOf(obj);
            }

            for (var i = 0; i < mergeMTX.Rows; i++)
            for (var j = 0; j < mergeMTX.Cols; j++)
                if (mergeMTX[i, j] > 0 && matrix2[i, j] >= 0)
                {
                    var index = (int)matrix2[i, j];
                    if (index >= 0 && index < beatmap.HitObjects.Count && beatmap.HitObjects[index] is ManiaNote note)
                        beatmap.HitObjects[index] = ConvertToHoldNote(note, mergeMTX[i, j] + note.StartTime);
                }

            // 修改元数据
            // 避免重复添加 Version 前缀
            if (beatmap.MetadataSection.Version != null && !beatmap.MetadataSection.Version.Contains("[KRR LN.]"))
                beatmap.MetadataSection.Version = $"[KRR LN.]{beatmap.MetadataSection.Version}";

            // 避免重复拼接 Creator
            if (beatmap.MetadataSection.Creator != null && !beatmap.MetadataSection.Creator.Contains("Krr LN."))
                beatmap.MetadataSection.Creator = "Krr LN. & " + beatmap.MetadataSection.Creator;

            // 避免重复添加 Tag
            var currentTags = beatmap.MetadataSection.Tags ?? [];
            var tagToAdd = "krrcream's transformer LN";
            if (!currentTags.Contains(tagToAdd))
            {
                var newTags = currentTags.Concat([tagToAdd]).ToArray();
                beatmap.MetadataSection.Tags = newTags;
            }

            return beatmap;
        }

        private bool[,] MarkByPercentage(bool[,] MTX, double P, Random random)
        {
            // 边界情况处理
            if (P >= 100)
                // 返回原始矩阵
                return MTX;

            if (P <= 0)
                // 返回全false矩阵
                return new bool[MTX.GetLength(0), MTX.GetLength(1)];

            // 收集所有true的位置
            List<(int row, int col)> truePositions = new();
            for (var i = 0; i < MTX.GetLength(0); i++)
            for (var j = 0; j < MTX.GetLength(1); j++)
                if (MTX[i, j])
                    truePositions.Add((i, j));

            // 如果没有true位置，直接返回全false矩阵
            if (truePositions.Count == 0) return new bool[MTX.GetLength(0), MTX.GetLength(1)];

            var ratio = 1.0 - P / 100.0;
            // 计算需要设置为false的数量
            var countToSetFalse = (int)Math.Round(truePositions.Count * ratio);

            // 按行分组，实现分层抽样
            var groupedByRow = truePositions.GroupBy(pos => pos.row)
                .ToDictionary(g => g.Key, g => g.ToList());

            var positionsToSetFalse = new HashSet<(int, int)>();

            foreach (var group in groupedByRow)
            {
                var row = group.Key;
                var positionsInRow = group.Value;
                var countInRow = (int)Math.Round(positionsInRow.Count * ratio);
                countInRow = Math.Min(countInRow, positionsInRow.Count);
                var selectedInRow = positionsInRow.OrderBy(x => random.Next())
                    .Take(countInRow);
                foreach (var pos in selectedInRow) positionsToSetFalse.Add(pos);
            }

            // 如果还有剩余配额未满足，补充随机选择
            if (positionsToSetFalse.Count < countToSetFalse)
            {
                var remaining = truePositions.Where(pos => !positionsToSetFalse.Contains(pos))
                    .OrderBy(pos => random.Next())
                    .Take(countToSetFalse - positionsToSetFalse.Count);

                foreach (var pos in remaining) positionsToSetFalse.Add(pos);
            }

            // 构建结果矩阵
            var result = new bool[MTX.GetLength(0), MTX.GetLength(1)];
            for (var i = 0; i < MTX.GetLength(0); i++)
            for (var j = 0; j < MTX.GetLength(1); j++)
                if (MTX[i, j] && !positionsToSetFalse.Contains((i, j)))
                    result[i, j] = true;

            return result;
        }

        private bool[,] LimitTruePerRow(bool[,] MTX, int limit, Random random)
        {
            var rows = MTX.GetLength(0);
            var cols = MTX.GetLength(1);

            var result = new bool[rows, cols];

            for (var i = 0; i < rows; i++)
            {
                var truePositions = new List<int>();
                for (var j = 0; j < cols; j++)
                    if (MTX[i, j])
                        truePositions.Add(j);

                if (truePositions.Count > limit)
                {
                    var shuffledPositions = truePositions.OrderBy(x => random.Next()).ToList();
                    for (var k = 0; k < limit; k++) result[i, shuffledPositions[k]] = true;
                }
                else
                {
                    for (var j = 0; j < cols; j++) result[i, j] = MTX[i, j];
                }
            }

            return result;
        }

        private int GenerateTriangularRandom(double D, double U, double M, int P, Random r)
        {
            if (P <= 0 || D >= U || M > U)
                return M > U ? (int)U : (int)M;
            if (M < D)
                return (int)D;
            if (P >= 100)
                P = 100;
            // 计算实际百分比
            var p = P / 100.0;

            // 计算新的下界限和上界限
            var d = M - (M - D) * p; // (D,M)距离M的p位置
            var u = M + (U - M) * p; // (M,U)距离M的p位置

            // 确保新范围在原范围内
            d = Math.Max(d, D);
            u = Math.Min(u, U);

            // 如果范围无效，则返回中心值
            if (d >= u)
                return (int)M;

            // 使用三角分布生成随机数
            // 三角分布公式：F(x) = (x-a)²/((b-a)(c-a))  when a≤x≤c
            //              F(x) = 1-(b-x)²/((b-a)(b-c))  when c≤x≤b
            var a = d; // 下限
            var b = u; // 上限
            var c = M; // 众数(期望最大值)

            // 确保众数在范围内
            c = Math.Max(a, Math.Min(b, c));

            var random = r;
            var u_random = random.NextDouble(); // 生成[0,1)的随机数

            double result;
            if (u_random <= (c - a) / (b - a))
                result = a + Math.Sqrt(u_random * (b - a) * (c - a));
            else
                result = b - Math.Sqrt((1 - u_random) * (b - a) * (b - c));

            return (int)result;
        }

        private int[,] MergeMatrices(int[,] matrix1, int[,] matrix2)
        {
            var rows = matrix1.GetLength(0);
            var cols = matrix1.GetLength(1);

            var result = new int[rows, cols];

            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
            {
                var val1 = matrix1[i, j];
                var val2 = matrix2[i, j];

                if (val1 >= -1 && val2 >= -1)
                    result[i, j] = Math.Max(val1, val2);
                else if (val1 >= -1)
                    result[i, j] = val1;
                else if (val2 >= -1)
                    result[i, j] = val2;
                else
                    result[i, j] = -1; // 或者保持不变，根据需求决定
            }

            return result;
        }

        private Dictionary<int, double> borderlist = new()
        {
            { 0, 0 },
            { 1, 1.0 / 8 },
            { 2, 1.0 / 6 },
            { 3, 1.0 / 4 },
            { 4, 1.0 / 3 },
            { 5, 1.0 / 2 },
            { 6, 1.0 / 1 },
            { 7, 3.0 / 2 },
            { 8, 2.0 / 1 },
            { 9, 4.0 / 1 },
            { 10, 999 }
        };

        //修改难度名，tag，和标签等
        private void changeMeta(Beatmap beatmap)
        {
            if (beatmap.MetadataSection.Version != null)
                beatmap.MetadataSection.Version = $"[KRR LN.]{beatmap.MetadataSection.Version}";
            if (beatmap.MetadataSection.Creator != null)
                beatmap.MetadataSection.Creator = "Krr LN. & " + beatmap.MetadataSection.Creator;
            var currentTags = beatmap.MetadataSection.Tags ?? [];
            var tagToAdd = BaseOptionsManager.KRRLNDefaultTag;
            if (!currentTags.Contains(tagToAdd))
            {
                var newTags = currentTags.Concat([tagToAdd]).ToArray();
                beatmap.MetadataSection.Tags = newTags;
            }
        }

        private int[,] DeepCopyMatrix(NoteMatrix source)
        {
            var rows = source.Rows;
            var cols = source.Cols;
            var destination = new int[rows, cols];

            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                destination[i, j] = source[i, j];

            return destination;
        }

        private static ManiaHoldNote ConvertToHoldNote(ManiaNote note, int newEndTime) //临时使用，深拷贝
        {
            // 创建新的 ManiaHoldNote 实例
            var holdNote = new ManiaHoldNote(
                new Vector2(note.Position.X, note.Position.Y),
                note.StartTime,
                newEndTime,
                note.HitSound,
                note.Extras,
                note.IsNewCombo,
                note.ComboOffset
            )
            {
                // 复制 ManiaNote 特有的属性
                BeatLengthOfThisNote = note.BeatLengthOfThisNote,
                RowIndex = note.RowIndex
            };

            // 如果 NoteCircleSize 已设置，则复制它（这会自动处理 ColIndex）
            if (note.NoteCircleSize.HasValue)
                holdNote.NoteCircleSize = note.NoteCircleSize;
            else if (note.ColIndex.HasValue)
                // 如果只有 ColIndex，则手动设置
                holdNote.ColIndex = note.ColIndex;

            return holdNote;
        }
    }
}