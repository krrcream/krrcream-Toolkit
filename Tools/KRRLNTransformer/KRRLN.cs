using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using krrTools.Beatmaps;
using krrTools.Configuration;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects.Mania;
using OsuParsers.Extensions;
using OsuParsers.Enums;

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

        private Matrix BuildAndProcessMatrix(NoteMatrix matrix , List<int> timeAxis, Beatmap beatmap,
            KRRLNTransformerOptions parameters)
        {
            // 创建带种子的随机数生成器
            var RG = parameters.Seed.Value.HasValue
                ? new Random(parameters.Seed.Value.Value)
                : new Random();
            
            var ManiaObjects = beatmap.HitObjects.AsManiaNotes();
            int cs = (int)beatmap.DifficultySection.CircleSize;
            int rows = beatmap.Rows;
            
       
            //初始化坐标矩阵以及时间轴
            (Matrix matrix1 , List<int> timeAxis1) = beatmap.getMTXandTimeAxis();
            
          
            //初始化各种矩阵,减少对象访问提高速度（注意不要调整初始化的顺序，有先后顺序之分)
            Matrix availableTimeMtx = GenerateAvailableTimeMatrix(matrix1, timeAxis1);
            Matrix longLnWaitModify = new Matrix(rows, cs);
            Matrix shortLnWaitModify = new Matrix(rows, cs);
            DoubleMatrix beatLengthMtx = GenerateBeatLengthMatrix(matrix1, ManiaObjects);
            BoolMatrix orgIsLNMatrix = GenerateOrgIsLN(matrix1, ManiaObjects);
            
            //将原始LN标记为-1,跳过处理
            if (!parameters.ProcessOriginalIsChecked.Value)
            {
                MarkOriginalLNAsSkipped(matrix1, orgIsLNMatrix);
            }
            
            //生成长短面标记
            var borderKey = (int)(parameters.LengthThreshold.Value ?? 5);
            var (shortLNFlag, longLNFlag) = GenerateLNFlags(matrix1, ManiaObjects, availableTimeMtx, beatLengthMtx, borderKey);
            
            longLNFlag = MarkByPercentage(longLNFlag, parameters.LongPercentage.Value, RG);
            shortLNFlag = MarkByPercentage(shortLNFlag, parameters.ShortPercentage.Value, RG);
            longLNFlag = LimitTruePerRow(longLNFlag, (int)parameters.LongLimit.Value, RG);
            shortLNFlag = LimitTruePerRow(shortLNFlag, (int)parameters.ShortLimit.Value, RG);

            //正式生成longLN矩阵
            GenerateLongLNMatrix(matrix1, longLnWaitModify, longLNFlag, ManiaObjects, 
                availableTimeMtx, beatLengthMtx, borderKey, 
                parameters.LongLevel.Value, (int)parameters.LongRandom.Value, RG);

            GenerateShortLNMatrix(matrix1, shortLnWaitModify, shortLNFlag, ManiaObjects,
                availableTimeMtx, beatLengthMtx, borderKey,
                parameters.ShortLevel.Value, (int)parameters.ShortRandom.Value, RG);

            var result = MergeMatrices(longLnWaitModify, shortLnWaitModify);
            var resultSpan = result.AsSpan();
            if (parameters.Alignment.Value.HasValue)
            {
                PerformLengthAlignment(result, beatLengthMtx, parameters);
            }
            return result;
        }
        
        private Beatmap ApplyChangesToHitObjects(Beatmap beatmap, Matrix mergeMTX , List<int> timeAxis,
            KRRLNTransformerOptions options)
        {
            (Matrix matrix2, List<int> timeAxis2) = beatmap.getMTXandTimeAxis();
            int cs = (int)beatmap.DifficultySection.CircleSize;
            int rows = beatmap.Rows;
            var ManiaObjects = beatmap.HitObjects.AsManiaNotes();

            var mergeMTXspan = mergeMTX.AsSpan();
            var matrix2Span = matrix2.AsSpan();

            for (int i = 0; i < mergeMTXspan.Length; i++)
            {
                if (mergeMTXspan[i] >= 0) 
                {
                    int index = matrix2Span[i];
                    //使用更新法修改endtime，不能直接赋值，会导致note无法变成LN
                    beatmap.HitObjects.UpdateHitObject(index, beatmap.HitObjects[index].AsManiaNote()
                        .CloneNote(EndTime: mergeMTXspan[i] + beatmap.HitObjects[index].StartTime));
                }   
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

        
        // 面尾对齐作废，但是代码暂时留着万一哪天想出来了
        private Matrix AlignEndTimesByColumnAndGetHoldLengths(List<ManiaNote> maniaObjects, Matrix matrix, Matrix endTimeMtx, Matrix availableTimeMtx, List<int> timeAxis1, int timeX = 150)
        {
            int rows = matrix.Rows;
            int cols = matrix.Cols;
            
            var matrixSpan = matrix.AsSpan();
            var endTimeSpan = endTimeMtx.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            
            // 创建结果矩阵存储hold lengths
            var holdLengths = new Matrix(rows, cols);
            var holdLengthsSpan = holdLengths.AsSpan();
            
            // 初始化为-1（表示不处理）
            for (int i = 0; i < holdLengthsSpan.Length; i++) 
                holdLengthsSpan[i] = -1;
            
            // 按列处理
            for (int col = 0; col < cols; col++)
            {
                // 收集当前列的所有有效endtimes及其位置
                var endTimesInColumn = new List<(int row, int endTime, int availableTime, int index)>();
                
                for (int row = 0; row < rows; row++)
                {
                    int index = row * cols + col;
                    if (matrixSpan[index] >= 0 && endTimeSpan[index] > 0)
                    {
                        endTimesInColumn.Add((row, endTimeSpan[index], availableTimeSpan[index], index));
                    }
                }
                
                if (endTimesInColumn.Count <= 1) continue; // 只有一个或没有元素则无需对齐
                
                // 按endTime排序找到最大的endTime
                var sortedEndTimes = endTimesInColumn.OrderByDescending(et => et.endTime).ToList();
                int maxEndTime = sortedEndTimes.First().endTime;
                
                // 对每个需要调整的元素进行处理
                foreach (var item in sortedEndTimes)
                {
                    int diff = Math.Abs(maxEndTime - item.endTime);
                    
                    // 如果差异小于阈值，则尝试对齐
                    if (diff <= timeX)
                    {
                        int newEndTime = maxEndTime;
                        
                        // 获取该行的起始时间
                        int startTime = timeAxis1[item.row];
                        
                        // 检查是否会超过availableTime限制
                        if (newEndTime - startTime > item.availableTime)
                        {
                            // 如果超出，则调整为目标时间减去一定缓冲值
                            newEndTime = maxEndTime - timeX;
                            
                            // 再次检查是否仍然超出限制
                            if (newEndTime - startTime > item.availableTime)
                            {
                                // 如果还是超出，则使用availableTime内的最大值
                                newEndTime = startTime + item.availableTime;
                            }
                        }
                        
                        // 计算并存储hold length
                        int newHoldLength = newEndTime - startTime;
                        if (newHoldLength > 0)
                        {
                            holdLengthsSpan[item.index] = newHoldLength;
                        }
                    }
                }
            }
            return holdLengths;
        }
        
        // 长度对齐
        private void PerformLengthAlignment(Matrix result, DoubleMatrix beatLengthMtx, KRRLNTransformerOptions parameters)
        {
            double denominator = 0;
            double aligValue = alignList[(int)parameters.Alignment.Value.Value];
            var resultSpan = result.AsSpan();
            var beatLengthMtxSpan = beatLengthMtx.AsSpan();
            for (int i = 0; i < resultSpan.Length; i++)
            {
                if (resultSpan[i] > 0)
                {
                    denominator = beatLengthMtxSpan[i] * aligValue;
                    resultSpan[i] =
                        (int)((int)(resultSpan[i] / denominator) * denominator); //请AI不要优化这一行,这个就是最精度最快的写法，这是能够放几个分母的意思
                }
            }
        }
        
        // 生成EndTime矩阵
        private Matrix BuildEndTimeMatrix(Matrix matrix1, List<ManiaNote> maniaObjects, int rows, int cols)
        {
            Matrix endTimeMtx = new Matrix(rows, cols);
            
            var matrix1Span = matrix1.AsSpan();
            var entTimeMtxSpan = endTimeMtx.AsSpan();
            for (int i = 0; i < matrix1Span.Length; i++)
            {
                if (matrix1Span[i] < 0) continue;
                int ET = maniaObjects[matrix1Span[i]].EndTime;
                if (ET > 0)
                {
                    entTimeMtxSpan[i] = ET;
                }
            }
            return endTimeMtx;
        }
        
        
        // 生成是否是原始LN矩阵
        private void MarkOriginalLNAsSkipped(Matrix matrix, BoolMatrix orgIsLNMatrix)
        {
            var matrixSpan = matrix.AsSpan();
            var orgIsLNSpan = orgIsLNMatrix.AsSpan();
            for (var i = 0; i < matrixSpan.Length; i++)
            {
                if (orgIsLNSpan[i] && matrixSpan[i] >= 0)
                    matrixSpan[i] = -1;
            }
        }
        
        //生成长短面标记
        private (BoolMatrix shortLNFlag, BoolMatrix longLNFlag) GenerateLNFlags(
            Matrix matrix1, 
            List<ManiaNote> maniaObjects, 
            Matrix availableTimeMtx, 
            DoubleMatrix beatLengthMtx,
            int borderKey)
        {
            var shortLNFlag = new BoolMatrix(matrix1.Rows, matrix1.Cols);
            var longLNFlag = new BoolMatrix(matrix1.Rows, matrix1.Cols);
    
            var matrixSpan = matrix1.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var shortLNSpan = shortLNFlag.AsSpan();
            var longLNSpan = longLNFlag.AsSpan();
            
            double borderValue = borderList[borderKey];

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int index = matrixSpan[i];
                if (index >= 0 && index < maniaObjects.Count)
                {
                    if (availableTimeSpan[i] > borderValue * beatLengthSpan[i] * 4)
                        longLNSpan[i] = true;
                    else
                        shortLNSpan[i] = true;
                }
            }
            return (shortLNFlag, longLNFlag);
        }
        
        //是否是原始LN生成
        private BoolMatrix GenerateOrgIsLN(Matrix matrix1, List<ManiaNote> maniaObjects)
        {
            int cols = matrix1.Cols;
            var orgIsLN = new BoolMatrix(matrix1.Rows, cols);
            var orgIsLNSpan = orgIsLN.AsSpan();
            
    
            foreach (var obj in maniaObjects)
            {
                var rowIndex = obj.RowIndex;
                var colIndex = obj.ColIndex;
                if (rowIndex.HasValue && colIndex.HasValue &&
                    rowIndex.Value >= 0 && rowIndex.Value < matrix1.Rows &&
                    colIndex.Value >= 0 && colIndex.Value < cols)
                {
                    // 直接通过 Span 设置值，避免索引器开销
                    if (obj.EndTime > obj.StartTime)
                        orgIsLNSpan[rowIndex.Value * cols + colIndex.Value] = true;
                }
            }
    
            return orgIsLN;
        }
        
        // beatLengthMtx生成
        private DoubleMatrix GenerateBeatLengthMatrix(Matrix matrix1, List<ManiaNote> maniaObjects)
        {
            var beatLengthMtx = new DoubleMatrix(matrix1.Rows, matrix1.Cols);
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var matrixSpan = matrix1.AsSpan();
            int length = matrixSpan.Length;

            for (int i = 0; i < length; i++)
            {
                int index = matrixSpan[i];
                if (index >= 0 && index < maniaObjects.Count)
                {
                    beatLengthSpan[i] = maniaObjects[index].BeatLengthOfThisNote;
                }
            }
            return beatLengthMtx;
        }
        
        // 正式生成longLN矩阵 - 优化版本
        private void GenerateLongLNMatrix(Matrix matrix1, Matrix longLnWaitModify, BoolMatrix longLNFlag, 
            List<ManiaNote> maniaObjects, Matrix availableTimeMtx, DoubleMatrix beatLengthMtx,
            int borderKey, double LNLevel, int longRandom, Random random)
        {
            var matrixSpan = matrix1.AsSpan();
            var longFlagSpan = longLNFlag.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var resultSpan = longLnWaitModify.AsSpan();
            
            double borderValue = borderList[borderKey];
            
            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (longFlagSpan[i])
                {
                    var indexObj = matrixSpan[i];
                    var newLength = GenerateRandom(
                        borderValue * maniaObjects[indexObj].BeatLengthOfThisNote * 4,
                        availableTimeSpan[i] - beatLengthSpan[i] / 8,
                        availableTimeSpan[i] * LNLevel / 100,
                        longRandom, random
                    );
                    resultSpan[i] = newLength;
                }
            }
        }

        // 正式生成shortLN矩阵 - 优化版本
        private void GenerateShortLNMatrix(Matrix matrix1, Matrix shortLnWaitModify, BoolMatrix shortLNFlag,
            List<ManiaNote> maniaObjects, Matrix availableTimeMtx, DoubleMatrix beatLengthMtx,
            int borderKey, double LNLevel, int shortRandom, Random random)
        {
            var matrixSpan = matrix1.AsSpan();
            var shortFlagSpan = shortLNFlag.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var resultSpan = shortLnWaitModify.AsSpan();
            
            double borderValue = borderList[borderKey];
                
            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (shortFlagSpan[i])
                {
                    var indexObj = matrixSpan[i];
                    var newLength = GenerateRandom(
                        Math.Max(beatLengthSpan[i] / 4, 50),
                        borderValue * maniaObjects[indexObj].BeatLengthOfThisNote * 4,
                        borderValue,
                        shortRandom, random
                    );
                    resultSpan[i] = newLength;
                }
            }
        }
        
        // availableTimeMtx生成 
        private Matrix GenerateAvailableTimeMatrix(Matrix matrix1, List<int> timeAxis1)
        {
            var availableTimeMtx = new Matrix(matrix1.Rows, matrix1.Cols);
            
            var lastTime = timeAxis1.Last();
            
            Span<int> matrixSpan = matrix1.AsSpan();
            Span<int> timeMtxSpan = availableTimeMtx.AsSpan();

            int rows = matrix1.Rows;
            int cols = matrix1.Cols;

            for (var j = 0; j < cols; j++)
            {
                var currentRow = -1;
                var colOffset = j;

                for (var i = 0; i < rows; i++)
                {
                    int index = i * cols + colOffset;
                    if (matrixSpan[index] >= 0)
                    {
                        if (currentRow >= 0)
                        {
                            var nextRow = i;
                            var availableTime = timeAxis1[nextRow] - timeAxis1[currentRow];
                            timeMtxSpan[currentRow * cols + colOffset] = availableTime;
                        }
                        currentRow = i;
                    }
                }

                if (currentRow >= 0)
                {
                    var availableTime = lastTime - timeAxis1[currentRow];
                    timeMtxSpan[currentRow * cols + colOffset] = availableTime;
                }
            }
            
            return availableTimeMtx;
        }
        
        // 百分比标记方法
        private BoolMatrix MarkByPercentage(BoolMatrix MTX, double P, Random random)
        {
            // 边界情况处理
            if (P >= 100)
                // 返回原始矩阵
                return MTX;

            if (P <= 0)
                // 返回全false矩阵
                return new BoolMatrix(MTX.Rows, MTX.Cols);

            var mtxSpan = MTX.AsSpan();
            int rows = MTX.Rows;
            int cols = MTX.Cols;
            
            // 收集所有true的位置
            List<(int row, int col)> truePositions = new();
            for (int i = 0; i < mtxSpan.Length; i++)
            {
                if (mtxSpan[i])
                {
                    int row = i / cols;
                    int col = i % cols;
                    truePositions.Add((row, col));
                }
            }

            // 如果没有true位置，直接返回全false矩阵
            if (truePositions.Count == 0) 
                return new BoolMatrix(MTX.Rows, MTX.Cols);

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
            var result = new BoolMatrix(MTX.Rows, MTX.Cols);
            var resultSpan = result.AsSpan();
            
            // 复制原始矩阵数据
            mtxSpan.CopyTo(resultSpan);
            
            // 设置需要为false的位置
            foreach (var (row, col) in positionsToSetFalse)
            {
                resultSpan[row * cols + col] = false;
            }

            return result;
        }

        private BoolMatrix LimitTruePerRow(BoolMatrix MTX, int limit, Random random)
        {
            var rows = MTX.Rows;
            var cols = MTX.Cols;

            var result = new BoolMatrix(rows, cols);
            var mtxSpan = MTX.AsSpan();
            var resultSpan = result.AsSpan();

            for (var i = 0; i < rows; i++)
            {
                var truePositions = new List<int>();
                for (var j = 0; j < cols; j++)
                {
                    if (mtxSpan[i * cols + j])
                        truePositions.Add(j);
                }

                if (truePositions.Count > limit)
                {
                    var shuffledPositions = truePositions.OrderBy(x => random.Next()).ToList();
                    for (var k = 0; k < limit; k++) 
                        resultSpan[i * cols + shuffledPositions[k]] = true;
                }
                else
                {
                    for (var j = 0; j < cols; j++) 
                        resultSpan[i * cols + j] = mtxSpan[i * cols + j];
                }
            }

            return result;
        }

        private int GenerateRandom(double D, double U, double M, int P, Random r)
        {
            if (P <= 0 || D >= U || M >= U)
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
            
            if (d >= u)
                return (int)M;

            // 使用 Beta[2,2] 分布生成随机数
            // 然后 X = (U1 + U2) / 2 服从 Beta[2,2] 分布
            var u1 = r.NextDouble();
            var u2 = r.NextDouble();
            var betaRandom = (u1 + u2) / 2.0;

            // 将 Beta[2,2] 分布的随机数映射到 [d, u] 区间
            var range = u - d;
            var mRelative = (M - d) / range; // M 在 [d,u] 中的相对位置
            
            double result;
            if (betaRandom <= 0.5)
            {
                result = d + (mRelative * betaRandom / 0.5) * range;
            }
            else
            {
                result = d + (mRelative + (1 - mRelative) * (betaRandom - 0.5) / 0.5) * range;
            }

            return (int)result;
        }
        
        private Matrix MergeMatrices(Matrix matrix1, Matrix matrix2)
        {
            // 确保矩阵维度一致
            if (matrix1.Rows != matrix2.Rows || matrix1.Cols != matrix2.Cols)
                throw new ArgumentException("Matrix dimensions must match");

            var rows = matrix1.Rows;
            var cols = matrix1.Cols;
    
            // 创建结果矩阵
            var result = new Matrix(rows, cols);
    
            // 使用 Span 进行高效的批量操作
            var span1 = matrix1.AsSpan();
            var span2 = matrix2.AsSpan();
            var resultSpan = result.AsSpan();
    
            // 使用单层循环遍历所有元素
            for (int i = 0; i < span1.Length; i++)
            {
                var val1 = span1[i];
                var val2 = span2[i];
        
                if (val1 >= -1 && val2 >= -1)
                    resultSpan[i] = Math.Max(val1, val2);
                else if (val1 >= -1)
                    resultSpan[i] = val1;
                else if (val2 >= -1)
                    resultSpan[i] = val2;
                else
                    resultSpan[i] = -1; // 或者保持不变，根据需求决定
            }
    
            return result;
        }

        private Dictionary<int, double> borderList = new()
        {
            /*
            { 0, "AllIsLongLN" },
       
            { 65, "AllIsShortLN"}
            */
            
            { 0, 0 },
            { 1, 1.0/16},
            { 2, 2.0/16},
            { 3, 3.0/16},
            { 4, 4.0/16},
            { 5, 5.0/16},
            { 6, 6.0/16},
            { 7, 7.0/16},
            { 8, 8.0/16},
            { 9, 9.0/16},
            { 10, 10.0/16},
            { 11, 11.0/16},
            { 12, 12.0/16},
            { 13, 13.0/16},
            { 14, 14.0/16},
            { 15, 15.0/16},
            { 16, 16.0/16},
            { 17, 17.0/16},
            { 18, 18.0/16},
            { 19, 19.0/16},
            { 20, 20.0/16},
            { 21, 21.0/16},
            { 22, 22.0/16},
            { 23, 23.0/16},
            { 24, 24.0/16},
            { 25, 25.0/16},
            { 26, 26.0/16},
            { 27, 27.0/16},
            { 28, 28.0/16},
            { 29, 29.0/16},
            { 30, 30.0/16},
            { 31, 31.0/16},
            { 32, 32.0/16},
            { 33, 33.0/16},
            { 34, 34.0/16},
            { 35, 35.0/16},
            { 36, 36.0/16},
            { 37, 37.0/16},
            { 38, 38.0/16},
            { 39, 39.0/16},
            { 40, 40.0/16},
            { 41, 41.0/16},
            { 42, 42.0/16},
            { 43, 43.0/16},
            { 44, 44.0/16},
            { 45, 45.0/16},
            { 46, 46.0/16},
            { 47, 47.0/16},
            { 48, 48.0/16},
            { 49, 49.0/16},
            { 50, 50.0/16},
            { 51, 51.0/16},
            { 52, 52.0/16},
            { 53, 53.0/16},
            { 54, 54.0/16},
            { 55, 55.0/16},
            { 56, 56.0/16},
            { 57, 57.0/16},
            { 58, 58.0/16},
            { 59, 59.0/16},
            { 60, 60.0/16},
            { 61, 61.0/16},
            { 62, 62.0/16},
            { 63, 63.0/16},
            { 64, 64.0/16},
            { 65, 999 }
        };
        
        private Dictionary<int, double> alignList = new()
        {
            /*
            { 1, "1/8" },
            { 2, "1/7" },
            { 3, "1/6" },
            { 4, "1/5" },
            { 5, "1/4" },
            { 6, "1/3" },
            { 7, "1/2" },
            { 8, "1/1" }
            */
            { 1, 1.0 / 8 },
            { 2, 1.0 / 7 },
            { 3, 1.0 / 6 },
            { 4, 1.0 / 5 },
            { 5, 1.0 / 4 },
            { 6, 2.0 / 3 },
            { 7, 3.0 / 2 },
            { 8, 1.0 },
        };
        
        //修改难度名，tag，和标签等
        private void changeMeta(Beatmap beatmap)
        {
            if (beatmap.MetadataSection.Version != null)
                beatmap.MetadataSection.Version = $"[KRR LN.]{beatmap.MetadataSection.Version}";
            if (beatmap.MetadataSection.Creator == null)
                beatmap.MetadataSection.Creator = "KRR LN.";
            else if (!beatmap.MetadataSection.Creator.StartsWith("KRR LN. &"))
                beatmap.MetadataSection.Creator = "KRR LN. & " + beatmap.MetadataSection.Creator;
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
    }
}