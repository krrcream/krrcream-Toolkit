using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using krrTools.Tools.OsuParser;
using System.Text.RegularExpressions;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Enums.Beatmaps;

namespace krrTools.Tools.Converter
{
    public class Converter
    {
        
        /// <summary>
        /// 将.osu文件的键数转换为目标键数
        /// </summary>
        /// <param name="filepath">需要转换的.osu文件路径</param>
        static public ConversionOptions options { get; set; }

        /// <summary>

        public void NTONC(string filepath)
        {

            // 检查文件是否存在
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException($"文件未找到: {filepath}");
            }
            
            // 检查文件扩展名是否为.osu
            if (Path.GetExtension(filepath).ToLower() != ".osu")
            {
                throw new ArgumentException("文件扩展名必须为.osu");
            }

            Beatmap beatmap = BeatmapDecoder.Decode(filepath);
            
            if (beatmap.GeneralSection.ModeId != 3)
                throw new ArgumentException("不是mania模式");

            int CS = (int)beatmap.DifficultySection.CircleSize;
            int targetKeys = (int)options.TargetKeys;
            int turn = targetKeys - CS;
            var P = options.SelectedKeyTypes;
            
            // 创建带种子的随机数生成器
            Random RG;
            if (options.Seed.HasValue)
            {
                RG = new Random(options.Seed.Value);
            }
            else
            {
                RG = new Random(); // 使用系统时间作为种子
            }
            
            if (P.Count > 0 && !P.Contains(CS))
            {
                throw new ArgumentException("不在筛选的键位模式里");
            }
            
            var ANA = new OsuAnalyzer();
            double BPM = double.Parse(ANA.GetBPM(beatmap).Split('(')[0]);
            double beatLength = 60000 / BPM * 4;
            // 变换时间
            double convertTime = Math.Max(1, options.TransformSpeed * beatLength - 10);
            if (turn >= 0)
            {
                DOAddKeys(RG);
            }

            if (turn < 0)
            {
                DORemoveKeys(RG);//还没写
            }

            void DORemoveKeys(Random random)
            {
                //还没写
            }

            void DOAddKeys(Random random)
            {
                var (matrix, timeAxis) = BuildMatrix();
                var (oldMTX, insertMTX) = convertMTX(turn, timeAxis, convertTime, random);
                int[,] newMatrix = convert(matrix, oldMTX, insertMTX, timeAxis,random);
                DensityReducer(newMatrix, (int)options.TargetKeys - (int)options.MaxKeys, random);
                newHitObjects(newMatrix);
                
                // PrintMatrix(newMatrix, timeAxis);
                // PrintMatrix(oldMTX, timeAxis);
                // PrintMatrix(insertMTX, timeAxis);
                
                BeatmapSave();
            }

            // 转换操作
            (int[,], List<int>) BuildMatrix()
            {
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
                int a = CS;

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
                    int column = positionXtocolumn(CS, (int)hitObject.Position.X);
                    int startRow = timeToRow[hitObject.StartTime];

                    matrix[startRow, column] = i;

                    if (hitObject.EndTime > 0)
                    {
                        int endRow = timeToRow[hitObject.EndTime];

                        for (int row = startRow + 1; row <= endRow; row++)
                        {
                            matrix[row, column] = -7;
                        }
                    }
                }

                return (matrix, timeAxis);
            }

            // 打印二维矩阵到控制台
            void PrintMatrix(int[,] matrix, List<int> timeAxis)
            {
                int h = timeAxis.Count;
                int a = matrix.GetLength(1); // 获取矩阵的列数

                Console.WriteLine("二维矩阵:");
                // 从最后一行开始向前遍历到第一行
                for (int i = h - 1; i >= 0; i--)
                {
                    Console.Write($"[{timeAxis[i]:D6}] ");
                    for (int j = 0; j < a; j++)
                    {
                        if (matrix[i, j] == -1)
                            Console.Write(" . ");
                        else if (matrix[i, j] == -7)
                            Console.Write(" | ");
                        else
                            Console.Write($"{matrix[i, j],2:D} ");
                    }

                    Console.WriteLine();
                }
            }

            // 生成转换矩阵
            (int[,], int[,]) convertMTX(int turn, List<int> timeAxis, 
                double convertTime, Random random)
            {
                int rows = timeAxis.Count;
                int cols = turn; // 需要添加的列数

                // 初始化两个矩阵
                int[,] oldMTX = new int[rows, cols];
                int[,] insertMTX = new int[rows, cols];

                // 生成 oldMTX 矩阵
                for (int col = 0; col < cols; col++)
                {
                    // 为每一列创建一个震荡数字生成器，范围是 0 到 CS-1
                    OscillatorGenerator oldIndex = new OscillatorGenerator(CS - 1, random);
                    // 重置时间计数器
                    double timeCounter = 0;
                    int lastTime = timeAxis[0];

                    for (int row = 0; row < rows; row++)
                    {
                        oldMTX[row, col] = oldIndex.GetCurrent();

                        timeCounter += timeAxis[row] - lastTime;
                        lastTime = timeAxis[row];

                        if (timeCounter >= convertTime)
                        {
                            oldIndex.Next();
                            timeCounter = 0;
                        }
                    }
                    
                    int randomMoves = random.Next(0, CS - 1);
                    for (int i = 0; i < randomMoves; i++)
                    {
                        oldIndex.Next();
                    }
                }

                // 生成 insertMTX 矩阵
                for (int col = 0; col < cols; col++)
                {
                    // 为每一列创建一个震荡数字生成器，范围是 0 到 (CS + col)
                    // 随着列的增加，可插入位置也在增加
                    OscillatorGenerator insertIndex = new OscillatorGenerator(CS + col, random);
                    double timeCounter = 0;
                    int lastTime = timeAxis[0];

                    for (int row = 0; row < rows; row++)
                    {
                        insertMTX[row, col] = insertIndex.GetCurrent();

                        timeCounter += timeAxis[row] - lastTime;
                        lastTime = timeAxis[row];

                        if (timeCounter >= convertTime)
                        {
                            insertIndex.Next();
                            timeCounter = 0;
                        }
                    }
                    
                    int randomMoves = random.Next(0, CS - 1 + col); // 随机移动0-2次
                    for (int i = 0; i < randomMoves; i++)
                    {
                        insertIndex.Next();
                    }
                }

                return (oldMTX, insertMTX);
            }

            // 转换操作
            int[,] convert(int[,] matrix, int[,] oldMTX, int[,] insertMTX, List<int> timeAxis1,Random random)
            {
                try
                {
                    int rows = matrix.GetLength(0);
                    int originalCols = matrix.GetLength(1);
                    int newCols = targetKeys;
                    int turn1 = oldMTX.GetLength(1); // oldMTX的列数

                    // 创建一个新的矩阵，列数为目标键数，行数与原矩阵相同，初始化为-1
                    int[,] newMatrix = new int[rows, newCols];
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < newCols; j++)
                        {
                            newMatrix[i, j] = -1;
                        }
                    }

                    // 处理每一行
                    for (int i = 0; i < rows; i++)
                    {
                        // 创建临时数组
                        int[] tempRow = new int[newCols];

                        // 是否改变convert位置

                        bool[] flagChangeCol = new bool[turn1];
                        int changeRowIndex = -1;
                        if (i >= 1 && (AreRowsDifferent(oldMTX, i, i - 1) || AreRowsDifferent(insertMTX, i, i - 1)))
                        {
                            changeRowIndex = i;
                            for (int j = 0; j < turn1; j++)
                            {
                                flagChangeCol[j] = true;
                            }
                        }

                        // 初始化为-1
                        for (int k = 0; k < newCols; k++)
                        {
                            tempRow[k] = -1;
                        }

                        Span<int> orgCurrentRow = MemoryMarshal.CreateSpan(ref matrix[i, 0], originalCols);
                        // 先复制原始矩阵的这一行内容到临时数组的左侧
                        for (int j = 0; j < originalCols && j < newCols; j++)
                        {
                            tempRow[j] = orgCurrentRow[j];
                        }

                        //检查每根面条后续占用的行数
                        Dictionary<int, int> LNCount = new Dictionary<int, int>();
                        for (int j = 0; j < originalCols; j++)
                        {
                            if (matrix[i, j] >= 0)
                            {
                                int count = 0;
                                int k = 1;
                                while (i + k < rows && matrix[i + k, j] == -7)
                                {
                                    count++;
                                    k++;
                                }

                                LNCount[j] = count;
                            }
                        }
                        //插入复制的物件

                        for (int j = 0; j < turn1; j++)
                        {
                            int oldIndex = oldMTX[i, j];
                            int insertIndex = insertMTX[i, j];

                            //检查原本位置是否有物件需要复制
                            bool flagNeedcopy = matrix[i, oldIndex] >= 0;

                            //先shift物件
                            ShiftInsert(tempRow, insertIndex);
                            if (!flagChangeCol[j] && flagNeedcopy)
                            {
                                tempRow[insertIndex] = matrix[i, oldIndex];
                            }
                            else if (flagChangeCol[j] && flagNeedcopy)
                            {
                                if (timeAxis1[i] - timeAxis1[changeRowIndex] < beatLength / 16 * 3 + 10)
                                {
                                    continue;
                                }

                                tempRow[insertIndex] = matrix[i, oldIndex];
                                flagChangeCol[j] = false;
                            }
                        }

                        //填充newMatrix 第i行
                        for (int j = 0; j < newCols; j++)
                        {
                            if (tempRow[j] >= 0 && newMatrix[i, j] == -1)
                            {
                                newMatrix[i, j] = tempRow[j];
                            }
                        }

                        /*
                        填充面条身体
                        */
                        foreach (var kvp in LNCount)
                        {
                            int originalColumn = kvp.Key; // 原始列号
                            int lnLength = kvp.Value; // 长音符长度

                            int newValue = matrix[i, originalColumn]; // 要查找的值
                            List<int> newColumns = new List<int>(); // 存储所有匹配的列索引

                            // 查找newValue在newMatrix[i]行中的所有位置
                            for (int col = 0; col < newCols; col++)
                            {
                                if (newMatrix[i, col] == newValue)
                                {
                                    newColumns.Add(col);
                                }
                            }

                            // 为每个匹配的位置填充长音符身体部分
                            foreach (int newColumn in newColumns)
                            {
                                // 填充长音符的身体部分（-7表示长音符身体）
                                for (int k = 1; k <= lnLength && i + k < rows; k++)
                                {
                                    newMatrix[i + k, newColumn] = -7;
                                }
                            }
                        }

                    }
                    
                    //位置映射，拷贝newMatrix到colsMatrix;    
                    int[,] colsMatrix = new int[newMatrix.GetLength(0), newMatrix.GetLength(1)];
                    Array.Copy(newMatrix, colsMatrix, newMatrix.Length);
                    
                    for (int j = 0; j < newCols; j++)
                    {
                        for (int i = 1; i < rows; i++)
                        {
                            if (colsMatrix[i, j] == -7)
                            {
                                colsMatrix[i, j] = colsMatrix[i - 1, j];
                            }
                        }
                    }

                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < newCols; j++)
                        {
                            int targetValue = colsMatrix[i, j];
                            if (targetValue >= 0)
                            {
                                // 在matrix第i行中查找targetValue所在的列索引
                                for (int c = 0; c < originalCols; c++)
                                {
                                    if (matrix[i, c] == targetValue)
                                    {
                                        colsMatrix[i, j] = c;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    //删除矩阵
                    bool[,] needDeleteMTX = new bool[colsMatrix.GetLength(0), colsMatrix.GetLength(1)];
                    
                    for (int j = 0; j < newCols; j++)
                    {
                        for (int i = 0; i < rows - 1; i++)
                        {
                            // 如果当前值和下一个值不同，且都不为-1
                            if (colsMatrix[i, j] != -1 && colsMatrix[i + 1, j] != -1 && colsMatrix[i, j] != colsMatrix[i + 1, j])
                            {
                                int changeRow = i;        // 变化点
                                int nextValue = colsMatrix[i + 1, j];  // 变化后的值
            
                                // 从变化点之后开始检查
                                for (int k = 1; i + 1 + k < rows; k++)
                                {
                                    int checkRow = i + 1 + k;
                
                                    // 如果值变了，停止检查
                                    if (colsMatrix[checkRow, j] != nextValue)
                                        break;
                
                                    // 如果时间差小于阈值，标记为需要删除
                                    if (timeAxis1[checkRow] - timeAxis1[changeRow] < beatLength / 16 * 2 + 10)
                                    {
                                        needDeleteMTX[checkRow, j] = true;
                                    }
                                    else
                                    {
                                        break; // 时间差太大，停止
                                    }
                                }
                            }
                        }
                    }
                    
                    for (int i = 0; i < rows; i++)
                    {
                        for (int j = 0; j < newCols; j++)
                        {
                            if (needDeleteMTX[i, j])
                                newMatrix[i, j] = -1;
                        }
                    }
                    
                    return newMatrix;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"convert方法发生异常: {ex.Message}");
                    Console.WriteLine($"异常堆栈: {ex.StackTrace}");
                    throw;
                }
            }

            bool AreRowsDifferent(int[,] matrix, int row1, int row2)
            {
                int colCount = matrix.GetLength(1);
                for (int j = 0; j < colCount; j++)
                {
                    if (matrix[row1, j] != matrix[row2, j])
                        return true;
                }

                return false;
            }

            void ShiftInsert<T>(T nums, int insertIndex) where T : IList<int>
            {
                // 检查insertIndex是否为有效下标
                if (insertIndex >= 0 && insertIndex <= nums.Count - 1)
                {
                    // 从右向左移动元素，避免覆盖
                    for (int i = nums.Count - 1; i > insertIndex; i--)
                    {
                        nums[i] = nums[i - 1];
                    }

                    // 将insertIndex位置设为-1
                    nums[insertIndex] = -1;
                }
                else
                {
                    throw new IndexOutOfRangeException("insertIndex 超出有效范围");
                }
            }

            void newHitObjects(int[,] newMatrix)
            {

                // 创建临时列表存储对象
                List<HitObject> newObjects = new List<HitObject>();
                //遍历newMatrix
                for (int i = 0; i < newMatrix.GetLength(0); i++)
                {
                    for (int j = 0; j < newMatrix.GetLength(1); j++)
                    {
                        int oldindex = newMatrix[i, j];
                        if (oldindex >= 0)
                        {
                            newObjects.Add(CopyHitObjectbyPX(beatmap.HitObjects[oldindex],
                                ColumnPositionMapper.ColumnToPositionX((int)options.TargetKeys, j)
                            ));
                        }
                    }
                }

                beatmap.HitObjects.Clear();
                // 在遍历完成后添加所有新对象
                beatmap.HitObjects.AddRange(newObjects);
                HitObjectSort();
            }

            //保存操作
            void BeatmapSave()
            {
                beatmap.MetadataSection.Creator = "Krr Conv. & " + beatmap.MetadataSection.Creator;
                beatmap.DifficultySection.CircleSize = (float)options.TargetKeys;
                beatmap.MetadataSection.Version =
                    "[" + CS + "to" + options.TargetKeys + "C] " + beatmap.MetadataSection.Version;

                var currentTags = beatmap.MetadataSection.Tags ?? new string[0];
                var newTags = currentTags.Concat(new[] { "krrcream's converter" }).ToArray();
                beatmap.MetadataSection.Tags = newTags;

                string directory = Path.GetDirectoryName(filepath);
                string baseFilename = getfilename(beatmap);
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
            }

            void HitObjectSort()
            {
                beatmap.HitObjects.Sort((a, b) =>
                {
                    if (a.StartTime == b.StartTime)
                    {
                        return a.Position.X.CompareTo(b.Position.X);
                    }
                    else
                    {
                        return a.StartTime.CompareTo(b.StartTime);
                    }
                });
            }

            void DensityReducer(int[,] matrix, int maxToRemovePerRow, Random random)
            {
                if (maxToRemovePerRow <= 0) return;

                int rows = matrix.GetLength(0);
                int cols = matrix.GetLength(1);

                // 记录每列被删除的次数，用于全局平衡
                var columnDeletionCounts = new int[cols];
                
                // 遍历每一行进行密度降低
                for (int i = 0; i < rows; i++)
                {
                    // 统计当前行中有效note的数量
                    var activeNotes = new List<int>(); // 存储有效note的列索引
                    for (int j = 0; j < cols; j++)
                    {
                        if (matrix[i, j] >= 0)
                        {
                            activeNotes.Add(j);
                        }
                    }

                    // 如果当前行note数已经少于最小键数，跳过
                    if (activeNotes.Count <= (int)options.MinKeys) continue;

                    // 计算目标note数：基于比例缩减，但不低于最小键数
                    int targetNotes = Math.Max(
                        (int)options.MinKeys,
                        Math.Min(activeNotes.Count,
                            (int)(activeNotes.Count * (double)options.MaxKeys / options.TargetKeys))
                    );

                    // 计算需要删除的数量
                    int toRemove = Math.Max(0, activeNotes.Count - targetNotes);
                    if (toRemove <= 0) continue;

                    // 根据权重选择要删除的列
                    var columnsToRemove = new List<int>();

                    // 创建临时列表用于选择
                    var candidates = new List<int>(activeNotes);

                    for (int r = 0; r < toRemove && candidates.Count > 0; r++)
                    {
                        // 计算权重（被删除次数越少，权重越高）
                        var weights = new double[candidates.Count];
                        double totalWeight = 0;

                        for (int j = 0; j < candidates.Count; j++)
                        {
                            // 权重与历史删除次数成反比，确保全局平衡
                            weights[j] = 1.0 / (1.0 + columnDeletionCounts[candidates[j]]);
                            totalWeight += weights[j];
                        }

                        // 轮盘赌选择
                        double randomValue = random.NextDouble() * totalWeight;
                        double currentWeight = 0;
                        int selectedIndex = 0;

                        for (int j = 0; j < candidates.Count; j++)
                        {
                            currentWeight += weights[j];
                            if (randomValue <= currentWeight)
                            {
                                selectedIndex = j;
                                break;
                            }
                        }

                        // 执行删除
                        int columnToRemove = candidates[selectedIndex];
                        matrix[i, columnToRemove] = -1;
                        columnDeletionCounts[columnToRemove]++; // 更新该列的删除计数
                        columnsToRemove.Add(columnToRemove);
                        candidates.RemoveAt(selectedIndex);
                    }
                }
            }
        }

        public HitObject CopyHitObjectbyPX(HitObject hitObject, int position)
        {
            // 复制所有基本属性
            Vector2 newPosition = new Vector2(position, hitObject.Position.Y);
            int startTime = hitObject.StartTime;
            int endTime = hitObject.EndTime;
            HitSoundType hitSound = hitObject.HitSound;

            // 正确复制Extras，确保不为null
            Extras newExtras = hitObject.Extras != null ? 
                new Extras(
                    hitObject.Extras.SampleSet,
                    hitObject.Extras.AdditionSet,
                    hitObject.Extras.CustomIndex,
                    hitObject.Extras.Volume,
                    hitObject.Extras.SampleFileName
                ) : new Extras();

            // 保持原始对象的其他属性
            bool isNewCombo = hitObject.IsNewCombo;
            int comboOffset = hitObject.ComboOffset;

            // 根据WriteHelper.TypeByte的逻辑来判断对象类型
            // 检查是否是长音符（mania模式下）
            bool isHoldNote = (hitObject.EndTime > hitObject.StartTime);
    
            if (isHoldNote)
            {
                // 创建ManiaHoldNote对象
                return new OsuParsers.Beatmaps.Objects.Mania.ManiaHoldNote(
                    newPosition,
                    startTime,
                    endTime,
                    hitSound,
                    newExtras,
                    isNewCombo,
                    comboOffset
                );
            }
            else
            {
                // 创建普通HitObject对象
                return new OsuParsers.Beatmaps.Objects.Mania.ManiaNote(
                    newPosition,
                    startTime,
                    endTime,
                    hitSound,
                    newExtras,
                    isNewCombo,
                    comboOffset
                );
            }
        }


        
        

        string getfilename(Beatmap beatmap)
        {
            // 清理文件名中的非法字符
            string artist = beatmap.MetadataSection.Artist ?? "";
            string title = beatmap.MetadataSection.Title ?? "";
            string creator = beatmap.MetadataSection.Creator ?? "";
            string version = beatmap.MetadataSection.Version ?? "";

            // 使用正则表达式移除所有非法字符
            string invalidCharsPattern = $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]";
            artist = Regex.Replace(artist, invalidCharsPattern, "");
            title = Regex.Replace(title, invalidCharsPattern, "");
            creator = Regex.Replace(creator, invalidCharsPattern, "");
            version = Regex.Replace(version, invalidCharsPattern, "");

            return $"{artist} - {title} ({creator}) [{version}]";
        }
        int positionXtocolumn(int CS, int X)
        {
            int column = (int)Math.Floor(X * (double)CS / 512);
            return column;
        }

        
        
        public class ColumnPositionMapper
        {
            private static readonly int[][] key_value_MTX = 
            {
                new[] {256},
                new[] {128, 384},
                new[] {85, 256, 426},
                new[] {64, 192, 320, 448},
                new[] {51, 153, 256, 358, 460},
                new[] {42, 128, 213, 298, 384, 469},
                new[] {36, 109, 182, 256, 329, 402, 475},
                new[] {32, 96, 160, 224, 288, 352, 416, 480},
                new[] {28, 85, 142, 199, 256, 312, 369, 426, 483},
                new[] {25, 76, 128, 179, 230, 281, 332, 384, 435, 486},
                new[] {23, 69, 116, 162, 209, 256, 302, 349, 395, 442, 488},
                new[] {21, 64, 106, 149, 192, 234, 277, 320, 362, 405, 448, 490},
                new[] {19, 59, 98, 137, 177, 256, 216, 295, 334, 374, 413, 452, 492},
                new[] {18, 54, 91, 128, 164, 201, 237, 274, 310, 347, 384, 420, 457, 493},
                new[] {17, 51, 85, 119, 153, 187, 221, 256, 290, 324, 358, 392, 426, 460, 494},
                new[] {16, 48, 80, 112, 144, 176, 240, 208, 272, 304, 336, 368, 400, 432, 464, 496},
                new[] {15, 45, 75, 135, 165, 105, 195, 225, 316, 286, 256, 376, 346, 406, 436, 466, 496},
                new[] {16, 48, 80, 112, 128, 144, 176, 208, 240, 272, 304, 336, 368, 384, 400, 432, 464, 496},
                new[] {13, 39, 66, 93, 120, 147, 174, 201, 228, 255, 282, 309, 336, 363, 390, 417, 444, 471, 498},
                new[] {12, 37, 63, 88, 114, 140, 165, 191, 216, 242, 268, 293, 319, 344, 370, 396, 421, 447, 472, 498},
                new[] {12, 36, 60, 85, 109, 133, 158, 182, 207, 231, 255, 280, 304, 328, 353, 377, 402, 426, 450, 475, 499},
                new[] {11, 34, 57, 80, 104, 127, 150, 173, 197, 220, 243, 267, 290, 313, 336, 360, 383, 406, 429, 453, 476, 499},
                new[] {11, 33, 55, 77, 100, 122, 144, 166, 189, 211, 233, 255, 278, 300, 322, 344, 367, 389, 411, 433, 456, 478, 500},
                new[] {10, 31, 52, 74, 95, 116, 138, 159, 180, 202, 223, 244, 266, 287, 308, 330, 351, 372, 394, 415, 436, 458, 479, 500}
            };

            public static int ColumnToPositionX(int cs1, int column)
            {
                // CS-1参数对应二维数组的行索引
                var row = key_value_MTX[cs1-1];
                
                // 返回对应列的值
                return row[column];
            }
        }


        class OscillatorGenerator
        {
            private int _maxValue;
            private int _currentValue;
            private int _direction;
            private readonly Random _random; 
            public OscillatorGenerator(int maxValue, Random random = null)
            {
                if (maxValue <= 0)
                {
                    throw new ArgumentException("maxValue 必须大于零");
                }

                _maxValue = maxValue;
                _random = random ?? new Random();
                _currentValue = _random.Next(1, maxValue);
                _direction = _random.Next(0, 2) == 0 ? -1 : 1;
            }
        

            public int GetCurrent()
            {
                return _currentValue;
            }

            public void Next()
            {
                _currentValue += _direction;

                // 检查是否需要改变方向
                if (_currentValue > _maxValue)
                {
                    _currentValue = _maxValue - 1;
                    _direction = -1;
                }
                else if (_currentValue <= 0)
                {
                    _currentValue = 0;
                    _direction = 1;
                }
            }
        }
        
        
    }
}