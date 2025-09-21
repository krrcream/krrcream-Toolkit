using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using krrTools.Tools.Converter;
using OsuParsers.Beatmaps.Objects;
using krrTools.Tools.OsuParser;

namespace krrTools.tools.DPtool
{
    public class DP
    {
        /// <summary>
        /// DP工具的配置选项
        /// </summary>
        static public DPToolOptions Options { get; set; }

        /// <summary>
        /// 处理.osu文件的DP操作
        /// </summary>
        /// <param name="filePath">需要处理的.osu文件路径</param>
        public string ProcessFile(string filePath)
        {
            // 检查文件是否存在
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"文件未找到: {filePath}");
            }
            
            // 检查文件扩展名是否为.osu
            if (Path.GetExtension(filePath).ToLower() != ".osu")
            {
                throw new ArgumentException("文件扩展名必须为.osu");
            }

            // 加载Beatmap
            Beatmap beatmap = BeatmapDecoder.Decode(filePath);
            // 检查是否为mania模式
            if (beatmap.GeneralSection.ModeId != 3)
                throw new ArgumentException("不是mania模式");
            
            var Conv = new Converter();
            var random = new Random(114514);
            int[,] orgMTX;           
            int CS = (int)beatmap.DifficultySection.CircleSize;
            var OPorg = new ConversionOptions();
            OPorg.MaxKeys = Options.SingleSideKeyCount;
            OPorg.MinKeys = 2;
            OPorg.TargetKeys = Options.SingleSideKeyCount;
            OPorg.TransformSpeed = 4;
            Conv.options = OPorg;
            var ANA = new OsuAnalyzer();
            double BPM = double.Parse(ANA.GetBPM(beatmap).Split('(')[0]);
            double beatLength = 60000 / BPM * 4;
            double convertTime = Math.Max(1, OPorg.TransformSpeed * beatLength - 10);
            var (matrix, timeAxis) = Conv.BuildMatrix(beatmap);
            
            if (Options.ModifySingleSideKeyCount && Options.SingleSideKeyCount > beatmap.DifficultySection.CircleSize)
            {
                
                int targetKeys = Options.SingleSideKeyCount;
                // 变换时间
                
                var (oldMTX, insertMTX) = Conv.convertMTX(targetKeys-CS, timeAxis, convertTime, CS, random);
                int[,] newMatrix = Conv.convert(matrix, oldMTX, insertMTX, timeAxis, targetKeys,beatLength,random);
                orgMTX = newMatrix;
            }
            else if (Options.ModifySingleSideKeyCount &&
                     Options.SingleSideKeyCount < beatmap.DifficultySection.CircleSize)
            {
                int targetKeys = Options.SingleSideKeyCount;
                var newMatrix = Conv.SmartReduceColumns(matrix, timeAxis, CS - targetKeys, convertTime,beatLength);
                orgMTX = newMatrix;
            }
            else
            {
                (orgMTX,timeAxis) = Conv.BuildMatrix(beatmap);
            }
             
            //克隆两个矩阵分别叫做orgL和orgR
            int[,] orgL = (int[,])orgMTX.Clone();
            int[,] orgR = (int[,])orgMTX.Clone();
            if (Options.LMirror)
            {
                orgL = Mirror(orgL);
            }
            if (Options.RMirror)
            {
                orgR = Mirror(orgR);
            }
            
            
            if (Options.LDensity && orgL.GetLength(1)>Options.LMaxKeys )
            {
                var randomL = new Random(114514);
                Conv.DensityReducer(orgL, (int)orgL.GetLength(1) - (int)Options.LMaxKeys, 
                    (int)Options.LMinKeys, orgL.GetLength(1), randomL);
            }
            if (Options.RDensity && orgR.GetLength(1)>Options.RMaxKeys )
            {
                var randomR = new Random(114514);
                Conv.DensityReducer(orgR, (int)orgR.GetLength(1) - (int)Options.RMaxKeys, 
                    (int)Options.RMinKeys, orgR.GetLength(1), randomR);
            }
            
            // 合并两个矩阵
            int[,] result = ConcatenateMatrices(orgL, orgR);
            // 合成新HitObjects
            newHitObjects(beatmap, result);
            return BeatmapSave();
            
            
            string BeatmapSave()
            {
                
                beatmap.MetadataSection.Creator = "Krr DP. & " + beatmap.MetadataSection.Creator;
                beatmap.DifficultySection.CircleSize = (float)result.GetLength(1) ;
                beatmap.MetadataSection.Version =
                    "[" + CS + "to" + result.GetLength(1) + "DP] " + beatmap.MetadataSection.Version;

                var currentTags = beatmap.MetadataSection.Tags ?? new string[0];
                var newTags = currentTags.Concat(new[] { "krrcream's converter DP" }).ToArray();
                beatmap.MetadataSection.Tags = newTags;

                string directory = Path.GetDirectoryName(filePath);
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
                beatmap = null;
                return fullPath;
            }
            
        }
        
        public void newHitObjects(Beatmap beatmap ,int[,] newMatrix)
        {

            // 创建临时列表存储对象
            List<HitObject> newObjects = new List<HitObject>();
            var Conv = new Converter(); 
            //遍历newMatrix
            for (int i = 0; i < newMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < newMatrix.GetLength(1); j++)
                {
                    int oldindex = newMatrix[i, j];
                    if (oldindex >= 0)
                    {
                        newObjects.Add(Conv.CopyHitObjectbyPX(beatmap.HitObjects[oldindex],
                            Converter.ColumnPositionMapper.ColumnToPositionX((int)newMatrix.GetLength(1), j)
                        ));
                    }
                }
            }

            beatmap.HitObjects.Clear();
            // 在遍历完成后添加所有新对象
            beatmap.HitObjects.AddRange(newObjects);
            Conv.HitObjectSort(beatmap);
        }
        
        public int[,] Mirror(int[,] matrix)
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
        
        public int[,] ConcatenateMatrices(int[,] matrixA, int[,] matrixB)
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
        
 

        /// <summary>
        /// 物件信息类
        /// </summary>
        private class MatrixObject
        {
            public int Row { get; set; }
            public int Col { get; set; }
            public int Value { get; set; }
            public int Order { get; set; }
            public bool IsLongNote { get; set; }
            public int LongNoteLength { get; set; }
        }

        /// <summary>
        /// 提取矩阵中的所有物件信息
        /// </summary>
        /// <param name="matrix">原始矩阵</param>
        /// <returns>物件列表</returns>
        private static List<MatrixObject> ExtractObjects(int[,] matrix)
        {
            var objects = new List<MatrixObject>();
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            
            // 用于跟踪已处理的长条身体部分
            var processedLN = new bool[rows, cols];
            
            int order = 0;
            
            for (int j = 0; j < cols; j++)
            {
                for (int i = 0; i < rows; i++)
                {
                    if (processedLN[i, j]) continue;
                    
                    int value = matrix[i, j];
                    
                    if (value >= 0)
                    {
                        // 普通物件或长条头部
                        var obj = new MatrixObject
                        {
                            Row = i,
                            Col = j,
                            Value = value,
                            Order = order++
                        };
                        
                        // 检查是否为长条
                        if (IsLongNoteHead(matrix, i, j, out int length))
                        {
                            obj.IsLongNote = true;
                            obj.LongNoteLength = length;
                        }
                        
                        objects.Add(obj);
                    }
                    else if (value == -7)
                    {
                        // 长条身体部分，标记为已处理
                        processedLN[i, j] = true;
                    }
                }
            }
            
            return objects;
        }

        /// <summary>
        /// 检查指定位置是否为长条头部
        /// </summary>
        /// <param name="matrix">矩阵</param>
        /// <param name="row">行索引</param>
        /// <param name="col">列索引</param>
        /// <param name="length">长条长度</param>
        /// <returns>是否为长条头部</returns>
        private static bool IsLongNoteHead(int[,] matrix, int row, int col, out int length)
        {
            length = 0;
            
            // 检查是否为非负数（物件）
            if (matrix[row, col] < 0) return false;
            
            // 向下检查是否为连续的-7
            int currentRow = row + 1;
            int rows = matrix.GetLength(0);
            
            while (currentRow < rows && matrix[currentRow, col] == -7)
            {
                length++;
                currentRow++;
            }
            
            return length > 0;
        }

        /// <summary>
        /// 寻找可用的列位置
        /// </summary>
        /// <param name="result">结果矩阵</param>
        /// <param name="obj">物件信息</param>
        /// <param name="lastCol">上一个物件所在的列</param>
        /// <param name="targetCols">目标列数</param>
        /// <returns>可用的列索引，-1表示未找到</returns>
        private static int FindAvailableColumn(int[,] result, MatrixObject obj, int lastCol, int targetCols)
        {
            // 从lastCol+1开始寻找可用位置
            for (int col = lastCol + 1; col < targetCols; col++)
            {
                // 检查该位置是否可用
                if (result[obj.Row, col] == -1)
                {
                    // 如果是长条，检查身体部分是否有足够空间
                    if (obj.IsLongNote)
                    {
                        if (CanPlaceLongNoteBody(result, obj.Row, col, obj.LongNoteLength))
                        {
                            return col;
                        }
                    }
                    else
                    {
                        return col;
                    }
                }
            }
            
            return -1;
        }

        /// <summary>
        /// 检查是否可以放置长条身体部分
        /// </summary>
        /// <param name="result">结果矩阵</param>
        /// <param name="startRow">起始行</param>
        /// <param name="col">列索引</param>
        /// <param name="length">长条长度</param>
        /// <returns>是否可以放置</returns>
        private static bool CanPlaceLongNoteBody(int[,] result, int startRow, int col, int length)
        {
            int endRow = startRow + length;
            int rows = result.GetLength(0);
            
            // 检查是否会超出矩阵范围
            if (endRow >= rows) return false;
            
            // 检查身体部分是否都为空
            for (int i = startRow + 1; i <= endRow; i++)
            {
                if (result[i, col] != -1) return false;
            }
            
            return true;
        }

        /// <summary>
        /// 放置长条身体部分
        /// </summary>
        /// <param name="result">结果矩阵</param>
        /// <param name="obj">物件信息</param>
        /// <param name="col">新列索引</param>
        private static void PlaceLongNoteBody(int[,] result, MatrixObject obj, int col)
        {
            for (int i = 1; i <= obj.LongNoteLength; i++)
            {
                result[obj.Row + i, col] = -7;
            }
        }

        
    }
}
