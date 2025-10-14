using System;
using System.Collections.Generic;
using krrTools.Beatmaps;
using krrTools.Tools.N2NC;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP 转换算法实现
    /// </summary>
    public class DP : IApplyToBeatmap
    {
        // 常量定义
        private const int RANDOM_SEED = 114514;
        private const double TRANSFORM_SPEED = 4.0;
        private const double BEAT_LENGTH_MULTIPLIER = 4.0;

        // private int _newKeyCount;

        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            throw new NotImplementedException();
        }
    
        /// <summary>
        /// 执行谱面转换
        /// </summary>
        public void TransformBeatmap(Beatmap beatmap, DPToolOptions options)
        {
            var originalCircleSize = beatmap.DifficultySection.CircleSize;
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options, originalCircleSize);
        }

        /// <summary>
        /// 处理音符矩阵
        /// </summary>
        private NoteMatrix ProcessMatrix(NoteMatrix matrix, List<int> timeAxis, Beatmap beatmap, DPToolOptions options)
        {
            // 如果 SingleSideKeyCount 为 null，不进行转换
            if (!options.SingleSideKeyCount.Value.HasValue)
            {
                return matrix;
            }

            var Conv = new N2NC.N2NC();
            var random = new Random(RANDOM_SEED);
            NoteMatrix orgMTX;
            var CS = (int)beatmap.DifficultySection.CircleSize;
            var convOptions = new N2NCOptions();
            convOptions.TargetKeys.Value = options.SingleSideKeyCount.Value.Value;
            convOptions.TransformSpeed.Value = TRANSFORM_SPEED;
            var BPM = beatmap.MainBPM;
            var beatLength = 60000 / BPM * BEAT_LENGTH_MULTIPLIER;
            var convertTime = Math.Max(1, convOptions.TransformSpeed.Value * beatLength - 10);

            var targetKeys = (int)options.SingleSideKeyCount.Value.Value;
            if (targetKeys > beatmap.DifficultySection.CircleSize)
            {
            
                var (oldMTX, insertMTX) = Conv.convertMTX(targetKeys - CS, timeAxis, convertTime, CS, random);
                var newMatrix = Conv.convert(matrix, oldMTX, insertMTX, timeAxis, targetKeys, beatLength, random);
                orgMTX = newMatrix;
            }
            else if (targetKeys < beatmap.DifficultySection.CircleSize)
            {
                var newMatrix = Conv.SmartReduceColumns(matrix, timeAxis, CS - targetKeys, convertTime, beatLength, random);
                orgMTX = newMatrix;
            }
            else
            {
                orgMTX = matrix;
            }

            // Apply DP processing
            var processedData = ProcessMatrixStatic(orgMTX.GetData(), options);
            // _newKeyCount = processedData.GetLength(1);
            return new NoteMatrix(processedData);
        }

        /// <summary>
        /// 将处理后的矩阵应用到谱面对象
        /// </summary>
        private void ApplyChangesToHitObjects(Beatmap beatmap, NoteMatrix processedMatrix, DPToolOptions options, double originalCircleSize)
        {
            newHitObjects(beatmap, processedMatrix);

            // 修改元数据
            if (beatmap.DifficultySection == null)
                throw new InvalidOperationException("Beatmap.DifficultySection cannot be null");

            // 只有当 SingleSideKeyCount 有值时，才修改 CircleSize
            if (options.SingleSideKeyCount.Value.HasValue)
            {
                beatmap.DifficultySection.CircleSize = (int)options.SingleSideKeyCount.Value.Value * 2;
            }
            else
            {
                beatmap.DifficultySection.CircleSize = (float)(originalCircleSize * 2);
            }

            // 避免重复拼接 Creator
            if (beatmap.MetadataSection == null)
                throw new InvalidOperationException("Beatmap.MetadataSection cannot be null");
            if (beatmap.MetadataSection.Creator == null)
                beatmap.MetadataSection.Creator = "DP Tool";
            else if (!beatmap.MetadataSection.Creator.StartsWith("DP Tool"))
                beatmap.MetadataSection.Creator = "DP Tool & " + beatmap.MetadataSection.Creator;

            // 避免重复拼接 Version
            if (beatmap.MetadataSection.Version == null)
                beatmap.MetadataSection.Version = "[DP]";
            else if (!beatmap.MetadataSection.Version.StartsWith("[DP]"))
                beatmap.MetadataSection.Version = "[DP] " + beatmap.MetadataSection.Version;
        }

        // 静态方法：处理矩阵，应用DP转换选项
        private static int[,] ProcessMatrixStatic(int[,] orgMTXData, DPToolOptions options)
        {
            // 确保使用深拷贝，避免引用共享
            var rows = orgMTXData.GetLength(0);
            var cols = orgMTXData.GetLength(1);
        
            // 创建完全独立的左右矩阵副本
            var orgL = new int[rows, cols];
            var orgR = new int[rows, cols];
        
            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
            {
                orgL[i, j] = orgMTXData[i, j];
                orgR[i, j] = orgMTXData[i, j];
            }
        
            // 分别处理左侧和右侧矩阵
            // 左侧处理：镜像 -> 密度限制 -> 去除
            if (options.LMirror.Value) orgL = Mirror(orgL);
            if (options.LDensity.Value)
            {
                var randomL = new Random(RANDOM_SEED);
                LimitDensity(orgL, (int)options.LMaxKeys.Value, randomL);
            }
            if (options.LRemove.Value) ClearMatrix(orgL); // 去除左侧结果：清空整个左侧矩阵

            // 右侧处理：镜像 -> 密度限制 -> 去除 
            if (options.RMirror.Value) orgR = Mirror(orgR);
            if (options.RDensity.Value)
            {
                var randomR = new Random(RANDOM_SEED + 1000); // 使用完全不同的种子确保左右两侧独立
                LimitDensity(orgR, (int)options.RMaxKeys.Value, randomR);
            }
            if (options.RRemove.Value) ClearMatrix(orgR); // 去除右侧结果：清空整个右侧矩阵

            // 合并两个矩阵
            var result = ConcatenateMatrices(orgL, orgR);
        
            return result;
        }

        /// <summary>
        /// 根据处理后的矩阵重建谱面的HitObjects
        /// </summary>
        /// <param name="beatmap">原始谱面对象</param>
        /// <param name="newMatrix">处理后的矩阵</param>
        private void newHitObjects(Beatmap beatmap, NoteMatrix newMatrix)
        {
            if (beatmap == null)
                throw new ArgumentNullException(nameof(beatmap), "Beatmap cannot be null in newHitObjects");
            if (newMatrix == null)
                throw new ArgumentNullException(nameof(newMatrix), "NewMatrix cannot be null in newHitObjects");
            if (beatmap.HitObjects == null)
                throw new InvalidOperationException("Beatmap.HitObjects cannot be null in newHitObjects");

            var rows = newMatrix.Rows;
            var cols = newMatrix.Cols;

            // 预估容量以减少List的重新分配
            var newObjects = new List<HitObject>(rows * cols / 4); // 假设平均密度为25%

            // 遍历矩阵重建HitObjects
            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
            {
                var oldIndex = newMatrix[i, j];
                if (oldIndex >= 0 && oldIndex < beatmap.HitObjects.Count)
                {
                    var originalHitObject = beatmap.HitObjects[oldIndex];
                    if (originalHitObject == null)
                        throw new InvalidOperationException($"HitObject at index {oldIndex} is null in beatmap.HitObjects");

                    var copiedHitObject = BeatmapExtensions.CopyHitObjectByPositionX(
                        originalHitObject,
                        ColumnPositionMapper.ColumnToPositionX(cols, j)
                    );

                    if (copiedHitObject == null)
                        throw new InvalidOperationException(
                            $"CopyHitObjectByPositionX returned null for HitObject at index {oldIndex}");

                    newObjects.Add(copiedHitObject);
                }
            }

            // 批量更新HitObjects
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(newObjects);
            beatmap.SortHitObjects();
        }

        private static int[,] Mirror(int[,] matrix)
        {
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);
            var result = new int[rows, cols];

            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                result[i, j] = matrix[i, cols - 1 - j];

            return result;
        }

        /// <summary>
        /// 清空矩阵 - 将所有元素标记为删除
        /// </summary>
        /// <param name="matrix">要清空的矩阵</param>
        private static void ClearMatrix(int[,] matrix)
        {
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);

            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                matrix[i, j] = -1; // 标记为删除
        }

        private static void LimitDensity(int[,] matrix, int maxKeys, Random random)
        {
            var rows = matrix.GetLength(0);
            var cols = matrix.GetLength(1);

            for (var i = 0; i < rows; i++)
            {
                var rowRandom = new Random(random.Next() + i); // 每个线程使用不同的种子
                var activeNotes = new List<int>(cols); // 预分配容量

                // 收集活跃音符
                for (var j = 0; j < cols; j++)
                    if (matrix[i, j] >= 0)
                        activeNotes.Add(j);

                // 如果超过限制，随机移除多余的音符
                if (activeNotes.Count > maxKeys)
                {
                    var toRemove = activeNotes.Count - maxKeys;

                    // 使用更高效的随机选择算法
                    for (var r = 0; r < toRemove; r++)
                    {
                        var randomIndex = rowRandom.Next(activeNotes.Count - r);
                        var colToRemove = activeNotes[randomIndex];

                        // 标记为删除
                        matrix[i, colToRemove] = -1;

                        // 将选中的元素与列表末尾元素交换，然后移除末尾
                        activeNotes[randomIndex] = activeNotes[activeNotes.Count - 1 - r];
                    }
                }
            }
        }

        private static int[,] ConcatenateMatrices(int[,] matrixA, int[,] matrixB)
        {
            // 获取矩阵维度
            var rowsA = matrixA.GetLength(0);
            var colsA = matrixA.GetLength(1);
            var rowsB = matrixB.GetLength(0);
            var colsB = matrixB.GetLength(1);

            // 检查行数是否一致
            if (rowsA != rowsB) throw new ArgumentException($"矩阵行数不匹配: A有{rowsA}行, B有{rowsB}行");

            // 创建结果矩阵
            var rows = rowsA;
            var cols = colsA + colsB;
            var result = new int[rows, cols];

            // 处理每一行
            for (var i = 0; i < rows; i++)
            {
                // 复制左侧矩阵A
                for (var j = 0; j < colsA; j++) result[i, j] = matrixA[i, j];
                // 复制右侧矩阵B
                for (var j = 0; j < colsB; j++) result[i, j + colsA] = matrixB[i, j];
            }

            return result;
        }
    }
}