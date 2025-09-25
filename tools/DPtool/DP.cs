using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OsuParsers.Beatmaps;
using krrTools.tools.N2NC;
using OsuParsers.Beatmaps.Objects;
using krrTools.Tools.OsuParser;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;

namespace krrTools.tools.DPtool
{
    public class DP
    {
        public string ProcessFile(string filePath, DPToolOptions options)
        {
            options.Left.Validate();
            options.Right.Validate();

            var beatmap = FileProcessingHelper.LoadValidatedBeatmap(filePath);

            var Conv = new N2NC.N2NC();
            var random = new Random(114514);
            int[,] orgMTX;
            int CS = (int)beatmap.DifficultySection.CircleSize;
            var convOptions = new N2NCOptions
            {
                MaxKeys = options.SingleSideKeyCount,
                MinKeys = 2,
                TargetKeys = options.SingleSideKeyCount,
                TransformSpeed = 4
            };
            Conv.options = convOptions;
            var ANA = new OsuAnalyzer();
            double BPM = ANA.GetBPM(beatmap);
            double beatLength = 60000 / BPM * 4;
            double convertTime = Math.Max(1, convOptions.TransformSpeed * beatLength - 10);
            var (matrix, timeAxis) = Conv.BuildMatrix(beatmap);

            if (options.ModifySingleSideKeyCount && options.SingleSideKeyCount > beatmap.DifficultySection.CircleSize)
            {
                int targetKeys = options.SingleSideKeyCount;
                // 变换时间

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
                // We already built (matrix, timeAxis) above; reuse it when no conversion is needed
                orgMTX = matrix;
            }

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


            if (options.LDensity && orgL.GetLength(1) > options.LMaxKeys)
            {
                var randomL = new Random(114514);
                Conv.DensityReducer(orgL, orgL.GetLength(1) - options.LMaxKeys,
                    options.LMinKeys, orgL.GetLength(1), randomL);
            }

            if (options.RDensity && orgR.GetLength(1) > options.RMaxKeys)
            {
                var randomR = new Random(114514);
                Conv.DensityReducer(orgR, orgR.GetLength(1) - options.RMaxKeys,
                    options.RMinKeys, orgR.GetLength(1), randomR);
            }

            // 合并两个矩阵
            int[,] result = ConcatenateMatrices(orgL, orgR);
            // 合成新HitObjects
            newHitObjects(beatmap, result);
            return BeatmapSave();


            string BeatmapSave()
            {
                // 添加作者前缀并更新 CircleSize 与版本说明
                beatmap.MetadataSection.Creator = OptionsConstants.DPCreatorPrefix + beatmap.MetadataSection.Creator;
                beatmap.DifficultySection.CircleSize = result.GetLength(1);
                beatmap.MetadataSection.Version = "[" + CS + "to" + result.GetLength(1) + "DP] " + beatmap.MetadataSection.Version;

                // 处理 tags：确保不是 null，然后添加默认 DP tag（避免重复）
                var currentTags = beatmap.MetadataSection.Tags ?? [];
                var tagToAdd = OptionsConstants.DPDefaultTag;
                if (!currentTags.Contains(tagToAdd))
                {
                    var newTags = currentTags.Concat([tagToAdd]).ToArray();
                    beatmap.MetadataSection.Tags = newTags;
                }

                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                string baseFilename = Conv.getfilename(beatmap);
                string filename = baseFilename + ".osu";
                string fullPath = Path.Combine(directory, filename);
                if (fullPath.Length > 255)
                {
                    int excessLength = fullPath.Length - 255;
                    int charsToTrim = excessLength + 3; // 多截掉3个字符用于添加"..."

                    if (charsToTrim < baseFilename.Length)
                    {
                        baseFilename = baseFilename.Substring(0, baseFilename.Length - charsToTrim) + "...";
                        filename = baseFilename + ".osu";
                        fullPath = Path.Combine(directory, filename);
                    }
                }

                beatmap.Save(fullPath);
                return fullPath;
            }
        }

        private void newHitObjects(Beatmap beatmap, int[,] newMatrix)
        {
            // 创建临时列表存储对象
            List<HitObject> newObjects = new List<HitObject>();
            var Conv = new N2NC.N2NC();
            //遍历newMatrix
            for (int i = 0; i < newMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < newMatrix.GetLength(1); j++)
                {
                    int oldIndex = newMatrix[i, j];
                    if (oldIndex >= 0)
                    {
                        newObjects.Add(Conv.CopyHitObjectByPX(beatmap.HitObjects[oldIndex],
                            ColumnPositionMapper.ColumnToPositionX(newMatrix.GetLength(1), j)
                        ));
                    }
                }
            }

            beatmap.HitObjects.Clear();
            // 在遍历完成后添加所有新对象
            beatmap.HitObjects.AddRange(newObjects);
            Conv.HitObjectSort(beatmap);
        }

        private int[,] Mirror(int[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            int[,] result = new int[rows, cols];

            Parallel.For(0, rows, i =>
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = matrix[i, cols - 1 - j];
                }
            });

            return result;
        }

        private int[,] ConcatenateMatrices(int[,] matrixA, int[,] matrixB)
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

            // 复制矩阵A到结果矩阵的左侧
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < colsA; j++)
                {
                    result[i, j] = matrixA[i, j];
                }
            }

            // 复制矩阵B到结果矩阵的右侧
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    result[i, j + colsA] = matrixB[i, j];
                }
            }

            return result;
        }
    }
}