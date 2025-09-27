using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using krrTools.tools.Shared;
using krrTools.Tools.OsuParser;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.tools.KRRLNTransformer
{
    public class KRRLN
    {
        public Beatmap ProcessFiles(string filepath, KRRLNTransformerOptions parameters)
        {
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException($"文件未找到: {filepath}");
            }

            if (Path.GetExtension(filepath).ToLower() != ".osu")
            {
                throw new ArgumentException("文件扩展名必须为.osu");
            }

            Beatmap beatmap = BeatmapDecoder.Decode(filepath);

            if (beatmap.GeneralSection.ModeId != 3)
                throw new ArgumentException("不是mania模式");

            var ANA = new OsuAnalyzer();
            double BPM = beatmap.GetBPM();
            if (BPM <= 0)
                throw new ArgumentException("无法获取有效的BPM");
            var beatLengthDict = ANA.GetBeatLengthList(beatmap);
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var beatLengthAxis = ANA.GetbeatLengthAxis(beatLengthDict, BPM, timeAxis);
            var AvailableTime = CalculateAvailableTime(matrix, timeAxis);

            int[,] longMTX = new int[matrix.GetLength(0), matrix.GetLength(1)];
            int[,] shortMTX = new int[matrix.GetLength(0), matrix.GetLength(1)];

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    longMTX[i, j] = -1;
                    shortMTX[i, j] = -1;
                }
            }

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (AvailableTime[i, j] >= 0) // 只处理有效位置
                    {
                        double threshold = beatLengthAxis[i] * 2;
                        if (AvailableTime[i, j] > threshold)
                        {
                            longMTX[i, j] = AvailableTime[i, j];
                        }
                        else
                        {
                            shortMTX[i, j] = AvailableTime[i, j];
                        }
                    }
                }
            }

            ProcessMatrix(shortMTX, (int)parameters.ShortPercentageValue, (int)parameters.ShortLimitValue);
            ProcessMatrix(longMTX, (int)parameters.LongPercentageValue, (int)parameters.LongLimitValue);
            GenerateTailLength(shortMTX, (int)parameters.ShortLevelValue);
            GenerateTailLength(longMTX, (int)parameters.LongLevelValue);


            int[,] mergeALBMTX = new int[matrix.GetLength(0), matrix.GetLength(1)];

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    mergeALBMTX[i, j] = -1;
                }
            }

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (shortMTX[i, j] >= 0)
                    {
                        mergeALBMTX[i, j] = shortMTX[i, j];
                    }
                    else if (longMTX[i, j] >= 0)
                    {
                        mergeALBMTX[i, j] = longMTX[i, j];
                    }
                }
            }
            // 生成面尾长度


            // 修改面尾
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (mergeALBMTX[i, j] > 0)
                    {
                        if (beatmap.HitObjects[matrix[i, j]].EndTime - beatmap.HitObjects[matrix[i, j]].StartTime > 9
                            && !parameters.ProcessOriginalIsChecked)
                        {
                            continue;
                        }

                        beatmap.HitObjects[matrix[i, j]].EndTime = mergeALBMTX[i, j];
                    }
                }
            }


            return beatmap;



        }

        private int[,] CalculateAvailableTime(int[,] matrix, List<int> timeAxis)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            // 创建同样大小的矩阵，默认值为-1
            int[,] availableTime = new int[rows, cols];

            // 初始化为-1
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    availableTime[i, j] = -1;
                }
            }

            // 按列遍历
            for (int col = 0; col < cols; col++)
            {
                for (int row = 0; row < rows; row++)
                {
                    // 如果当前元素大于等于0
                    if (matrix[row, col] >= 0)
                    {
                        int nextRowIndex = -1;

                        // 寻找同一列中下一个大于等于0的元素
                        for (int nextRow = row + 1; nextRow < rows; nextRow++)
                        {
                            if (matrix[nextRow, col] >= 0)
                            {
                                nextRowIndex = nextRow;
                                break;
                            }
                        }

                        // 计算可用时间
                        if (nextRowIndex == -1)
                        {
                            // 如果没有下一个元素，使用最后时间轴的时间
                            availableTime[row, col] = timeAxis[timeAxis.Count - 1] - timeAxis[row];
                        }
                        else
                        {
                            // 计算与下一个时间轴的时间差
                            availableTime[row, col] = timeAxis[nextRowIndex] - timeAxis[row];
                        }
                    }
                }
            }

            return availableTime;
        }

        private void ProcessMatrix(int[,] mtx, int p, int x)
        {
            int rows = mtx.GetLength(0);
            int cols = mtx.GetLength(1);

            // 计算需要保留的位置数量比例
            double keepRatio = p / 100.0;

            Random random = new Random();

            // 按行处理
            for (int i = 0; i < rows; i++)
            {
                // 统计当前行中大于等于0的位置
                List<int> validPositions = new List<int>();
                for (int j = 0; j < cols; j++)
                {
                    if (mtx[i, j] >= 0)
                    {
                        validPositions.Add(j);
                    }
                }

                // 如果当前行的非负数数量已经超过限制X
                if (validPositions.Count > x)
                {
                    // 必须减少到X个，计算需要移除的数量
                    int removeCount = validPositions.Count - x;

                    // 使用权重法随机选择要移除的位置
                    List<int> positionsToRemove = SelectPositionsByWeight(mtx, i, validPositions, removeCount, random);

                    // 将选中的位置设置为-1
                    foreach (int col in positionsToRemove)
                    {
                        mtx[i, col] = -1;
                    }
                }
                else if (validPositions.Count > 0)
                {
                    // 根据百分比计算需要保留的数量
                    int keepCount = (int)Math.Round(validPositions.Count * keepRatio);
                    int removeCount = validPositions.Count - keepCount;

                    if (removeCount > 0)
                    {
                        // 使用权重法随机选择要移除的位置
                        List<int> positionsToRemove =
                            SelectPositionsByWeight(mtx, i, validPositions, removeCount, random);

                        // 将选中的位置设置为-1
                        foreach (int col in positionsToRemove)
                        {
                            mtx[i, col] = -1;
                        }
                    }
                }
            }
        }

        private List<int> SelectPositionsByWeight(int[,] mtx, int row, List<int> validPositions, int removeCount,
            Random random)
        {
            List<int> positionsToRemove = new List<int>();
            List<int> availablePositions = new List<int>(validPositions);

            for (int i = 0; i < removeCount; i++)
            {
                // 计算每个位置的权重（这里使用简单的随机权重，可以根据需要调整）
                Dictionary<int, double> weights = new Dictionary<int, double>();
                double totalWeight = 0;

                foreach (int col in availablePositions)
                {
                    // 权重可以根据位置的重要性来计算，这里使用简单的随机权重
                    double weight = random.NextDouble() + 0.1; // 避免权重为0
                    weights[col] = weight;
                    totalWeight += weight;
                }

                // 根据权重选择一个位置
                double randomValue = random.NextDouble() * totalWeight;
                double currentWeight = 0;
                int selectedCol = availablePositions[0];

                foreach (int col in availablePositions)
                {
                    currentWeight += weights[col];
                    if (randomValue <= currentWeight)
                    {
                        selectedCol = col;
                        break;
                    }
                }

                positionsToRemove.Add(selectedCol);
                availablePositions.Remove(selectedCol);
            }

            return positionsToRemove;
        }

        private void GenerateTailLength(int[,] mtx, int p)
        {
            int rows = mtx.GetLength(0);
            int cols = mtx.GetLength(1);
            double percentage = p / 100.0;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    // 只处理大于等于0的位置
                    if (mtx[i, j] >= 0)
                    {
                        // 计算当前数字乘以百分比
                        double calculatedValue = mtx[i, j] * percentage;

                        // 计算阈值：原本数字减50
                        int threshold = mtx[i, j] - 50;

                        // 如果计算值大于阈值，则设置为阈值
                        if (calculatedValue > threshold)
                        {
                            mtx[i, j] = threshold;
                        }
                        else
                        {
                            // 否则保持计算值（需要转换为整数）
                            mtx[i, j] = (int)calculatedValue;
                        }
                    }
                }
            }
        }

        public Beatmap ProcessBeatmapToData(Beatmap beatmap, KRRLNTransformerOptions parameters)
        {
            if (beatmap.GeneralSection.ModeId != 3)
                throw new ArgumentException("不是mania模式");

            var ANA = new OsuAnalyzer();
            double BPM = beatmap.GetBPM();
            if (BPM <= 0)
                throw new ArgumentException("无法获取有效的BPM");
            var beatLengthDict = ANA.GetBeatLengthList(beatmap);
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var beatLengthAxis = ANA.GetbeatLengthAxis(beatLengthDict, BPM, timeAxis);
            var AvailableTime = CalculateAvailableTime(matrix, timeAxis);

            int[,] longMTX = new int[matrix.GetLength(0), matrix.GetLength(1)];
            int[,] shortMTX = new int[matrix.GetLength(0), matrix.GetLength(1)];

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    longMTX[i, j] = -1;
                    shortMTX[i, j] = -1;
                }
            }

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (AvailableTime[i, j] >= 0) // 只处理有效位置
                    {
                        double threshold = beatLengthAxis[i] * 2;
                        if (AvailableTime[i, j] > threshold)
                        {
                            longMTX[i, j] = AvailableTime[i, j];
                        }
                        else
                        {
                            shortMTX[i, j] = AvailableTime[i, j];
                        }
                    }
                }
            }

            ProcessMatrix(shortMTX, (int)parameters.ShortPercentageValue, (int)parameters.ShortLimitValue);
            ProcessMatrix(longMTX, (int)parameters.LongPercentageValue, (int)parameters.LongLimitValue);
            GenerateTailLength(shortMTX, (int)parameters.ShortLevelValue);
            GenerateTailLength(longMTX, (int)parameters.LongLevelValue);


            int[,] mergeALBMTX = new int[matrix.GetLength(0), matrix.GetLength(1)];

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    mergeALBMTX[i, j] = -1;
                }
            }

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (shortMTX[i, j] >= 0)
                    {
                        mergeALBMTX[i, j] = shortMTX[i, j];
                    }
                    else if (longMTX[i, j] >= 0)
                    {
                        mergeALBMTX[i, j] = longMTX[i, j];
                    }
                }
            }
            // 生成面尾长度


            // 修改面尾
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (mergeALBMTX[i, j] > 0)
                    {
                        if (beatmap.HitObjects[matrix[i, j]].EndTime - beatmap.HitObjects[matrix[i, j]].StartTime > 9
                            && !parameters.ProcessOriginalIsChecked)
                        {
                            continue;
                        }

                        beatmap.HitObjects[matrix[i, j]].EndTime = mergeALBMTX[i, j];
                    }
                }
            }

            return beatmap;
        }
    }
}
