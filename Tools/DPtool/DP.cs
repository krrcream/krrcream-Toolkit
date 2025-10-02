using System;
using System.Collections.Generic;
using krrTools.Core;
using krrTools.Data;
using krrTools.Tools.N2NC;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Tools.DPtool
{
    public class DP : AbstractBeatmapTransformer<DPToolOptions>
    {
        // 常量定义
        private const int RANDOM_SEED = 114514;
        private const double TRANSFORM_SPEED = 4.0;
        private const double BEAT_LENGTH_MULTIPLIER = 4.0;

        private int _newKeyCount;

        protected override int[,] ProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, DPToolOptions options)
        {
            var Conv = new N2NC.N2NC();
            var random = new Random(RANDOM_SEED);
            int[,] orgMTX;
            int CS = (int)beatmap.DifficultySection.CircleSize;
            var convOptions = new N2NCOptions
            {
                TargetKeys = options.SingleSideKeyCount,
                TransformSpeed = TRANSFORM_SPEED
            };
            double BPM = beatmap.MainBPM;
            double beatLength = 60000 / BPM * BEAT_LENGTH_MULTIPLIER;
            double convertTime = Math.Max(1, convOptions.TransformSpeed * beatLength - 10);

            if (options.ModifySingleSideKeyCount && options.SingleSideKeyCount > beatmap.DifficultySection.CircleSize)
            {
                int targetKeys = options.SingleSideKeyCount;
                var (oldMTX, insertMTX) = Conv.convertMTX(targetKeys - CS, timeAxis, convertTime, CS, random);
                int[,] newMatrix = Conv.convert(matrix, oldMTX, insertMTX, timeAxis, targetKeys, beatLength, random);
                orgMTX = newMatrix;
            }
            else if (options.ModifySingleSideKeyCount &&
                     options.SingleSideKeyCount < beatmap.DifficultySection.CircleSize)
            {
                int targetKeys = options.SingleSideKeyCount;
                var newMatrix = Conv.SmartReduceColumns(matrix, timeAxis, CS - targetKeys, convertTime, beatLength);
                orgMTX = newMatrix;
            }
            else
            {
                orgMTX = matrix;
            }

            // Apply DP processing
            var processedMatrix = ProcessMatrixStatic(orgMTX, options);
            _newKeyCount = processedMatrix.GetLength(1);
            return processedMatrix;
        }

        protected override void ApplyChangesToHitObjects(Beatmap beatmap, int[,] processedMatrix, DPToolOptions options)
        {
            newHitObjects(beatmap, processedMatrix);
        }

        protected override void ModifyMetadata(Beatmap beatmap, DPToolOptions options)
        {
            // Update CS based on new matrix columns
            beatmap.DifficultySection.CircleSize = _newKeyCount;
            beatmap.MetadataSection.Creator = "DP Tool & " + beatmap.MetadataSection.Creator;
            beatmap.MetadataSection.Version = "[DP] " + beatmap.MetadataSection.Version;
        }

        protected override string SaveBeatmap(Beatmap beatmap, string originalPath)
        {
            throw new NotImplementedException();
        }

        // 静态方法：处理矩阵，应用DP转换选项
        private static int[,] ProcessMatrixStatic(int[,] matrix, DPToolOptions options)
        {
            int[,] orgMTX = matrix;

            //克隆两个矩阵分别叫做orgL和orgR
            int[,] orgL = (int[,])orgMTX.Clone();
            int[,] orgR = (int[,])orgMTX.Clone();
            if (options.LMirror)
            {
                orgL = Mirror(orgL);
            }

            if (options.RMirror)
            {
                orgR = Mirror(orgR);
            }

            if (options.LDensity)
            {
                var randomL = new Random(RANDOM_SEED);
                LimitDensity(orgL, options.LMaxKeys, randomL);
            }

            if (options.RDensity)
            {
                var randomR = new Random(RANDOM_SEED);
                LimitDensity(orgR, options.RMaxKeys, randomR);
            }

            // Apply remove functionality
            if (options.LRemove)
            {
                RemoveHalf(orgL, true); // Remove left half
            }

            if (options.RRemove)
            {
                RemoveHalf(orgR, false); // Remove right half
            }

            // 合并两个矩阵
            int[,] result = ConcatenateMatrices(orgL, orgR);
            return result;
        }

        public Beatmap DPBeatmapToData(Beatmap beatmap, DPToolOptions options)
        {
            return ProcessBeatmapToData(beatmap, options);
        }

        /// <summary>
        /// 根据处理后的矩阵重建谱面的HitObjects
        /// </summary>
        /// <param name="beatmap">原始谱面对象</param>
        /// <param name="newMatrix">处理后的矩阵</param>
        private void newHitObjects(Beatmap beatmap, int[,] newMatrix)
        {
            int rows = newMatrix.GetLength(0);
            int cols = newMatrix.GetLength(1);

            // 预估容量以减少List的重新分配
            var newObjects = new List<HitObject>(rows * cols / 4); // 假设平均密度为25%
            var conv = new N2NC.N2NC();

            // 遍历矩阵重建HitObjects
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int oldIndex = newMatrix[i, j];
                    if (oldIndex >= 0 && oldIndex < beatmap.HitObjects.Count)
                    {
                        newObjects.Add(conv.CopyHitObjectByPX(
                            beatmap.HitObjects[oldIndex],
                            ColumnPositionMapper.ColumnToPositionX(cols, j)
                        ));
                    }
                }
            }

            // 批量更新HitObjects
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(newObjects);
            conv.HitObjectSort(beatmap);
        }
        
        private static int[,] Mirror(int[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            int[,] result = new int[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = matrix[i, cols - 1 - j];
                }
            }

            return result;
        }
        
        private static void RemoveHalf(int[,] matrix, bool isLeft)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            int half = cols / 2;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (isLeft && j < half)
                    {
                        matrix[i, j] = -1; // 标记为删除
                    }
                    else if (!isLeft && j >= half)
                    {
                        matrix[i, j] = -1; // 标记为删除
                    }
                }
            }
        }

        private static void LimitDensity(int[,] matrix, int maxKeys, Random random)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            for (int i = 0; i < rows; i++)
            {
                var rowRandom = new Random(random.Next() + i); // 每个线程使用不同的种子
                var activeNotes = new List<int>(cols); // 预分配容量

                // 收集活跃音符
                for (int j = 0; j < cols; j++)
                {
                    if (matrix[i, j] >= 0)
                    {
                        activeNotes.Add(j);
                    }
                }

                // 如果超过限制，随机移除多余的音符
                if (activeNotes.Count > maxKeys)
                {
                    int toRemove = activeNotes.Count - maxKeys;

                    // 使用更高效的随机选择算法
                    for (int r = 0; r < toRemove; r++)
                    {
                        int randomIndex = rowRandom.Next(activeNotes.Count - r);
                        int colToRemove = activeNotes[randomIndex];

                        // 标记为删除
                        matrix[i, colToRemove] = -1;

                        // 将选中的元素与列表末尾元素交换，然后移除末尾
                        activeNotes[randomIndex] = activeNotes[activeNotes.Count - 1 - r];
                    }
                }
            }
        }

        /// <summary>
        /// 将两个矩阵水平拼接成一个双人矩阵
        /// </summary>
        /// <param name="matrixA">左侧矩阵</param>
        /// <param name="matrixB">右侧矩阵</param>
        /// <returns>拼接后的矩阵</returns>
        /// <exception cref="ArgumentException">当矩阵行数不匹配时抛出</exception>
        private static int[,] ConcatenateMatrices(int[,] matrixA, int[,] matrixB)
        {
            // 获取矩阵维度
            int rowsA = matrixA.GetLength(0);
            int colsA = matrixA.GetLength(1);
            int rowsB = matrixB.GetLength(0);
            int colsB = matrixB.GetLength(1);

            // 检查行数是否一致
            if (rowsA != rowsB)
            {
                throw new ArgumentException($"矩阵行数不匹配: A有{rowsA}行, B有{rowsB}行");
            }

            // 创建结果矩阵
            int rows = rowsA;
            int cols = colsA + colsB;
            int[,] result = new int[rows, cols];

            // 处理每一行
            for (int i = 0; i < rows; i++)
            {
                // 复制左侧矩阵A
                for (int j = 0; j < colsA; j++)
                {
                    result[i, j] = matrixA[i, j];
                }
                // 复制右侧矩阵B
                for (int j = 0; j < colsB; j++)
                {
                    result[i, j + colsA] = matrixB[i, j];
                }
            }

            return result;
        }
    }
}