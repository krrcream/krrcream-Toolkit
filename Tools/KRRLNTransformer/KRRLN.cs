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
using krrTools.Localization;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.KRRLNTransformer
{
    /// <summary>
    /// KRRLN 转换算法实现
    /// </summary>
    public class KRRLN
    {
        /// <summary>
        /// 修改metadeta,放在每个转谱器开头
        /// </summary>
        private void MetadetaChange(Beatmap beatmap, KRRLNTransformerOptions options)
        {
            // 修改作者 保持叠加转谱后的标签按顺序唯一
            beatmap.MetadataSection.Creator = CreatorManager.AddTagToCreator(beatmap.MetadataSection.Creator, Strings.KRRLNTag);

            // 替换Version （允许叠加转谱）
            beatmap.MetadataSection.Version = $"[{Strings.KRRLNTag}] {beatmap.MetadataSection.Version}";
            
            // 替换标签，保证唯一
            var existingTags = new HashSet<string>(beatmap.MetadataSection.Tags ?? Enumerable.Empty<string>());
            var requiredTags = new[] { Strings.ConverterTag, Strings.KRRLNTag , "Krr"};

            var newTags = requiredTags
                .Where(tag => !existingTags.Contains(tag))
                .Concat(beatmap.MetadataSection.Tags ?? Enumerable.Empty<string>())
                .ToArray();
            
            beatmap.MetadataSection.Tags = newTags;
            // 修改ID 但是维持BeatmapSetID
            beatmap.MetadataSection.BeatmapID = 0;
        }
        
        /// <summary>
        /// 执行谱面转换
        /// </summary>
        public void TransformBeatmap(Beatmap beatmap, KRRLNTransformerOptions options)
        {
            // TODO: Matrix构建方法重复？ 初始化一次，然后传递更好吧
            var (matrix, timeAxis) = beatmap.BuildMatrix(); // 可能是过时的方法
            var processedMatrix = BuildAndProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, timeAxis, options);
            MetadetaChange(beatmap, options);
        }

        private Matrix BuildAndProcessMatrix(NoteMatrix matrix , List<int> timeAxis, Beatmap beatmap,
            KRRLNTransformerOptions options)
        {
            // 创建带种子的随机数生成器
            var RG = options.Seed.Value.HasValue
                ? new Random(options.Seed.Value.Value)
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
            if (!options.ProcessOriginalIsChecked.Value)
            {
                MarkOriginalLNAsSkipped(matrix1, orgIsLNMatrix);
            }
            
            //生成长短面标记
            var borderKey = (int)options.LengthThreshold.Value;
            var borderdrict = new BeatNumberGenerator(64, 1.0 / 4);
            var shortLNdrict = new BeatNumberGenerator(256, 1.0 / 16);
            
            var (shortLNFlag, longLNFlag) = GenerateLNFlags(matrix1, ManiaObjects, availableTimeMtx, beatLengthMtx, borderdrict ,borderKey);
            
            longLNFlag = MarkByPercentage(longLNFlag, options.LongPercentage.Value, RG);
            shortLNFlag = MarkByPercentage(shortLNFlag, options.ShortPercentage.Value, RG);
            longLNFlag = LimitTruePerRow(longLNFlag, (int)options.LongLimit.Value, RG);
            shortLNFlag = LimitTruePerRow(shortLNFlag, (int)options.ShortLimit.Value, RG);

            double LongLevel = options.LongLevel.Value; // 滑块是0到100，代码中用Level/100表示百分率
            double ShortLevel = shortLNdrict.GetValue((int)options.ShortLevel.Value); // 滑块是整数，要对应到字典里 
            //正式生成longLN矩阵
            GenerateLongLNMatrix(matrix1, longLnWaitModify, longLNFlag,  
                availableTimeMtx, beatLengthMtx, borderKey, 
                LongLevel ,ShortLevel, (int)options.LongRandom.Value, borderdrict,RG);

            GenerateShortLNMatrix(matrix1, shortLnWaitModify, shortLNFlag, 
                availableTimeMtx, beatLengthMtx, borderKey,
                ShortLevel, (int)options.ShortRandom.Value,borderdrict, RG);

            var result = MergeMatrices(longLnWaitModify, shortLnWaitModify);
            var resultSpan = result.AsSpan();
            if (options.Alignment.Value.HasValue)
            {
                PerformLengthAlignment(result, beatLengthMtx, options);
            }
            return result;
        }
        
        private void ApplyChangesToHitObjects(Beatmap beatmap, Matrix mergeMTX , List<int> timeAxis,
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

            if (options.ODValue.Value.HasValue)
            {
                float OD = (float)options.ODValue.Value.Value;
                beatmap.DifficultySection.OverallDifficulty = OD;
            }
            // 修改元数据
            // return beatmap;
        }
        
        /*// 面尾对齐作废，但是代码暂时留着万一哪天想出来了
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
        }*/
        
        // 长度对齐
        private void PerformLengthAlignment(Matrix result, DoubleMatrix beatLengthMtx, KRRLNTransformerOptions parameters)
        {
            //不用容差法在double==double中判断，用分子加上EPSILON截断法，这里是调整过的EPSILON,注意AI过的时候不要换成容差来比较double的相等
            double EPSILON = 0.99;
            double denominator = 0;
            if (parameters.Alignment.Value != null)
            {
                double aligValue = alignList[(int)parameters.Alignment.Value.Value];
                var resultSpan = result.AsSpan();
                var beatLengthMtxSpan = beatLengthMtx.AsSpan();
                for (int i = 0; i < resultSpan.Length; i++)
                {
                    if (resultSpan[i] > 0)
                    {
                        denominator = (beatLengthMtxSpan[i] * aligValue);
                        resultSpan[i] =
                            (int)((int)((resultSpan[i]+EPSILON) / denominator) * denominator); //请AI不要优化这一行,这个就是最精度最快的写法，这是能够放几个分母的意思
                    
                    }
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
            BeatNumberGenerator BG,
            int borderKey)
        {
            var shortLNFlag = new BoolMatrix(matrix1.Rows, matrix1.Cols);
            var longLNFlag = new BoolMatrix(matrix1.Rows, matrix1.Cols);
    
            var matrixSpan = matrix1.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var shortLNSpan = shortLNFlag.AsSpan();
            var longLNSpan = longLNFlag.AsSpan();
            
            double borderValue = BG.GetValue(borderKey);

            for (int i = 0; i < matrixSpan.Length; i++)
            {
                int index = matrixSpan[i];
                if (index >= 0 && index < maniaObjects.Count)
                {
                    if (availableTimeSpan[i] > borderValue * beatLengthSpan[i] )
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
             Matrix availableTimeMtx, DoubleMatrix beatLengthMtx,
            int borderKey, double LLNLevel,double SLNLevel, int longRandom, BeatNumberGenerator BG, Random random)
        {
            var matrixSpan = matrix1.AsSpan();
            var longFlagSpan = longLNFlag.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var resultSpan = longLnWaitModify.AsSpan();
            
            double borderValue = BG.GetValue(borderKey);
            
            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (longFlagSpan[i])
                {
                    double mean = availableTimeSpan[i] * LLNLevel / 100; //长面用百分比
                    double di = borderValue * beatLengthSpan[i];
                    int newLength = 0;
                    if (mean < di)
                    {
                        newLength = GenerateRandom(
                            0,
                            di,
                            SLNLevel * beatLengthSpan[i],
                            longRandom, random
                        );
                    }
                    else
                    {
                        newLength = GenerateRandom(
                            di,
                            availableTimeSpan[i],
                            mean,
                            longRandom, random
                        );
                    }
                    if (newLength > availableTimeSpan[i] - 34)
                        newLength = availableTimeSpan[i] - 34;
                    resultSpan[i] = newLength;
                }
            }
        }

        // 正式生成shortLN矩阵 - 优化版本
        private void GenerateShortLNMatrix(Matrix matrix1, Matrix shortLnWaitModify, BoolMatrix shortLNFlag,
             Matrix availableTimeMtx, DoubleMatrix beatLengthMtx,
            int borderKey, double SLNLevel, int shortRandom, BeatNumberGenerator BG, Random random)
        {
            var matrixSpan = matrix1.AsSpan();
            var shortFlagSpan = shortLNFlag.AsSpan();
            var availableTimeSpan = availableTimeMtx.AsSpan();
            var beatLengthSpan = beatLengthMtx.AsSpan();
            var resultSpan = shortLnWaitModify.AsSpan();
            
            double borderValue = BG.GetValue(borderKey);
                
            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (shortFlagSpan[i])
                {
                    var indexObj = matrixSpan[i];
                    double up = SLNLevel * beatLengthSpan[i];
                    var newLength = GenerateRandom(
                        0,
                        borderValue * beatLengthSpan[i],  
                        SLNLevel * beatLengthSpan[i],  //短面直接指定长度
                        shortRandom, random
                    );
                    if (newLength > availableTimeSpan[i] - 34)
                        newLength = availableTimeSpan[i] - 34;
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
            if (P <= 0)
            {
                return (int)M;
            }
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
            { 6, 1.0 / 3 },
            { 7, 1.0 / 2 },
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
    
    /// <summary>
    /// 可配置的数字生成器类
    /// 根据中间值和系数生成数字序列
    /// 仅仅在KRRLN中使用
    /// </summary>
    internal class BeatNumberGenerator
    {
        private readonly double[] _values;
        private readonly int _middleIndex;
        private readonly double _coefficient;
        private readonly double _lastValue;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="middleIndex">中间索引值</param>
        /// <param name="coefficient">系数，决定中间数字的大小</param>
        /// <param name="lastValue">最后一个值，默认为999.0</param>
        public BeatNumberGenerator(int middleIndex, double coefficient, double lastValue = 999.0)
        {
            _middleIndex = middleIndex;
            _coefficient = coefficient;
            _lastValue = lastValue;
            // 创建数组，大小为middleIndex + 2 (索引0到middleIndex+1)
            _values = new double[middleIndex + 2];
            // 初始化数组
            _values[0] = 0.0; // 第一个值始终为0
            // 中间的值按照 i * coefficient 计算
            for (int i = 1; i <= middleIndex; i++)
            {
                _values[i] = i * coefficient;
            }
            // 最后一个值为指定值
            _values[middleIndex + 1] = lastValue;
        }
        /// <summary>
        /// 获取指定索引的值
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns>对应的double值</returns>
        public double GetValue(int index)
        {
            if (index <= 0)
                return 0;
            if (index >= _values.Length - 1)
                return _lastValue;
            return _values[index];
        }
        /// <summary>
        /// 获取中间索引
        /// </summary>
        public int MiddleIndex => _middleIndex;
        /// <summary>
        /// 获取系数
        /// </summary>
        public double Coefficient => _coefficient;
        /// <summary>
        /// 获取数组长度
        /// </summary>
        public int Length => _values.Length;
    }
    
}