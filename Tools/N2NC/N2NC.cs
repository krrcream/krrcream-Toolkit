using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using krrTools.Beatmaps;
using krrTools.Core;
using krrTools.Data;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Decoders;
using OsuParsers.Enums.Beatmaps;

namespace krrTools.Tools.N2NC
{
    public class N2NC : AbstractBeatmapTransformer<N2NCOptions>
    {
        protected override int[,] ProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, N2NCOptions options)
        {
            return ConvertMatrix(matrix, timeAxis, (ManiaBeatmap)beatmap, options);
        }

        protected override void ApplyChangesToHitObjects(Beatmap beatmap, int[,] processedMatrix, N2NCOptions options)
        {
            NewHitObjects((ManiaBeatmap)beatmap, processedMatrix, options);
        }

        protected override void ModifyMetadata(Beatmap beatmap, N2NCOptions options)
        {
            // N2NC 修改CS和Version
            int originalCS = (int)beatmap.DifficultySection.CircleSize;
            beatmap.DifficultySection.CircleSize = (float)options.TargetKeys;
            beatmap.MetadataSection.Version = "[" + originalCS + "to" + options.TargetKeys + "C] " + beatmap.MetadataSection.Version;
        }

        protected override string SaveBeatmap(Beatmap beatmap, string originalPath)
        {
            throw new NotImplementedException();
        }

        private int[,] ConvertMatrix(int[,] matrix, List<int> timeAxis, ManiaBeatmap beatmap, N2NCOptions options)
        {
            int CS = beatmap.KeyCount;
            int targetKeys = (int)options.TargetKeys;
            int turn = targetKeys - CS;
            var P = options.SelectedKeyTypes;
            // 创建带种子的随机数生成器
            var RG = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random(); // 使用系统时间作为种子

            if (P is { Count: > 0 } && !P.Contains(CS))
            {
                throw new ArgumentException("不在筛选的键位模式里");
            }

            if (CS == targetKeys && (int)options.TargetKeys == targetKeys)
            {
                return matrix;
            }
            double BPM = beatmap.GetBPM();
            // Debug.WriteLine("BPM：" + BPM);
            double beatLength = 60000 / BPM * 4;

            double convertTime = Math.Max(1, options.TransformSpeed * beatLength - 10);

            int[,] newMatrix = turn >= 0 ? 
                DoAddKeys(matrix, timeAxis, turn, convertTime, CS, targetKeys, beatLength, RG, options) 
                : 
                DoRemoveKeys(matrix, timeAxis, turn, convertTime, beatLength, RG, CS, options);

            return newMatrix;
        }

        private int[,] DoAddKeys(int[,] matrix, List<int> timeAxis, int turn, double convertTime,
            int CS, int targetKeys, double beatLength, Random random, N2NCOptions options)
        {
            // 生成转换矩阵
            var (oldMTX, insertMTX) = convertMTX(turn, timeAxis, convertTime, CS, random);
            int[,] newMatrix = convert(matrix, oldMTX, insertMTX, timeAxis, targetKeys, beatLength, random);
            DensityReducer(newMatrix, (int)options.TargetKeys - 18, 1, (int)options.TargetKeys, random);
            return newMatrix;
        }

        private int[,] DoRemoveKeys(int[,] matrix, List<int> timeAxis, int turn, double convertTime,
            double beatLength, Random random, int originalCS, N2NCOptions options)
         {
             var newMatrix = SmartReduceColumns(matrix, timeAxis, -turn, convertTime, beatLength);
             DensityReducer(newMatrix, (int)options.TargetKeys - 18, 1, (int)options.TargetKeys, random);
             return newMatrix;
         }

        // TODO：统一生成基础矩阵，再由模块处理更好。
        // 未来库完善，矩阵可弃用，仅供测试模式下输出运行模型
        public (int[,], int[,]) convertMTX(int turn, List<int> timeAxis,
            double convertTime, int CS, Random random)
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
        public int[,] convert(int[,] matrix, int[,] oldMTX, int[,] insertMTX, List<int> timeAxis1, int targetKeys, double beatLength, Random random)
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
                        bool flagNeedCopy = matrix[i, oldIndex] >= 0;

                        //先shift物件
                        ShiftInsert(tempRow, insertIndex);
                        if (!flagChangeCol[j] && flagNeedCopy)
                        {
                            tempRow[insertIndex] = matrix[i, oldIndex];
                        }
                        else if (flagChangeCol[j] && flagNeedCopy)
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
                            // 在matrix第i行查找targetValue所在的列索引
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
                Debug.WriteLine($"convert方法发生异常: {ex.Message}");
                Debug.WriteLine($"异常堆栈: {ex.StackTrace}");
                throw;
            }
        }

        private bool AreRowsDifferent(int[,] matrix, int row1, int row2)
        {
            int colCount = matrix.GetLength(1);
            for (int j = 0; j < colCount; j++)
            {
                if (matrix[row1, j] != matrix[row2, j])
                    return true;
            }

            return false;
        }

        private void ShiftInsert<T>(T nums, int insertIndex) where T : IList<int>
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

        private void NewHitObjects(Beatmap beatmap, int[,] newMatrix, N2NCOptions options)
        {

            // 创建临时列表存储对象
            List<HitObject> newObjects = new List<HitObject>();
            //遍历newMatrix
            for (int i = 0; i < newMatrix.GetLength(0); i++)
            {
                for (int j = 0; j < newMatrix.GetLength(1); j++)
                {
                    int oldIndex = newMatrix[i, j];
                    if (oldIndex >= 0)
                    {
                        newObjects.Add(CopyHitObjectByPX(beatmap.HitObjects[oldIndex],
                            ColumnPositionMapper.ColumnToPositionX((int)options.TargetKeys, j)
                        ));
                    }
                }
            }

            beatmap.HitObjects.Clear();
            // 在遍历完成后添加所有新对象
            beatmap.HitObjects.AddRange(newObjects);
            HitObjectSort(beatmap);
        }



        public void HitObjectSort(Beatmap beatmap)
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

        private void DensityReducer(int[,] matrix, int maxToRemovePerRow, int minKeys, int targetKeys, Random random)
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
                if (activeNotes.Count <= minKeys) continue;

                // 计算目标note数：基于比例缩减，但不低于最小键数
                int targetNotes = Math.Max(
                    minKeys,
                    Math.Min(activeNotes.Count,
                        (int)(activeNotes.Count * (double)(targetKeys - maxToRemovePerRow) / targetKeys))
                );

                // 计算需要删除的数量
                int toRemove = Math.Max(0, activeNotes.Count - targetNotes);
                if (toRemove <= 0) continue;

                // 根据权重选择要删除的列
                // var columnsToRemove = new List<int>(); // removed: previously unused

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
                    // columnsToRemove.Add(columnToRemove);
                    candidates.RemoveAt(selectedIndex);
                }
            }
        }




        public HitObject CopyHitObjectByPX(HitObject hitObject, int position)
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

        public int[,] SmartReduceColumns(int[,] orgMTX, List<int> timeAxis, int turn, double convertTime, double beatLength)
        {
            int rows = orgMTX.GetLength(0);
            int originalCols = orgMTX.GetLength(1);
            int targetCols = originalCols - turn;

            // 创建新矩阵，初始化为-1（空）
            int[,] newMatrix = new int[rows, targetCols];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < targetCols; j++)
                {
                    newMatrix[i, j] = -1;
                }
            }

            // 按时间段处理
            int regionStart = 0;

            for (int regionEnd = 1; regionEnd < rows; regionEnd++)
            {
                // 检查是否到达新区域的结束点
                bool isRegionEnd = (timeAxis[regionEnd] - timeAxis[regionStart] >= convertTime);
                bool isLastRow = (regionEnd == rows - 1);

                if (isRegionEnd || isLastRow)
                {
                    // 确保最后一个区域包含最后一行
                    if (isLastRow && !isRegionEnd)
                    {
                        regionEnd = rows - 1;
                    }

                    // 处理当前区域
                    ProcessRegion(orgMTX, newMatrix, timeAxis, regionStart, regionEnd, targetCols, beatLength);

                    // 更新下一个区域的起始点
                    regionStart = regionEnd;
                }
            }

            // 处理可能剩余的行（如果最后一段不足一个完整区域）
            if (regionStart < rows - 1)
            {
                ProcessRegion(orgMTX, newMatrix, timeAxis, regionStart, rows - 1, targetCols, beatLength);
            }

            // 处理空行
            ProcessEmptyRows(orgMTX, newMatrix, timeAxis, beatLength);

            return newMatrix;
        }

        private void ProcessRegion(int[,] orgMTX, int[,] newMatrix, List<int> timeAxis,
                                  int regionStart, int regionEnd, int targetCols, double beatLength)
        {
            int originalCols = orgMTX.GetLength(1);
            int rows = orgMTX.GetLength(0);

            // 分析区域内各列的重要性（物件数量）
            var columnWeights = new int[originalCols];
            for (int i = regionStart; i <= regionEnd; i++)
            {
                for (int j = 0; j < originalCols; j++)
                {
                    if (orgMTX[i, j] >= 0) // 统计有效物件
                    {
                        columnWeights[j]++;
                    }
                }
            }

            // 确定要移除的列（选择权重最小且风险最低的几列）
            var columnsToRemove = GetColumnsToRemove(columnWeights, targetCols, originalCols, orgMTX, regionStart, regionEnd);

            // 创建列映射关系（原列 -> 新列）
            var columnMapping = CreateColumnMapping(originalCols, columnsToRemove);

            // 处理区域内的每一行
            for (int row = regionStart; row <= regionEnd; row++)
            {
                // 复制物件到新矩阵
                for (int col = 0; col < originalCols; col++)
                {
                    int newValue = orgMTX[row, col];
                    if (newValue >= 0) // 有效物件
                    {
                        int newCol = columnMapping[col];
                        if (newCol >= 0) // 该列未被移除
                        {
                            // 检查目标位置是否可用（避免冲突）
                            if (IsPositionAvailable(newMatrix, row, newCol, timeAxis, beatLength))
                            {
                                newMatrix[row, newCol] = newValue;

                                // 如果是长条头部，复制整个长条
                                CopyLongNoteBody(orgMTX, newMatrix, row, col, newCol, rows);
                            }
                        }
                    }
                }
            }

            // 处理长条延续部分
            for (int row = regionStart; row <= regionEnd; row++)
            {
                HandleLongNoteExtensions(newMatrix, row, targetCols);
            }

            // 应用约束条件：确保每行至少有一个note
            // 应用约束条件：确保每行至少有一个note
            ApplyMinimumNotesConstraint(newMatrix, orgMTX, regionStart, regionEnd, targetCols, timeAxis, beatLength);

        }

        private void ApplyMinimumNotesConstraint(int[,] matrix, int[,] orgMTX, int startRow, int endRow, int targetCols, List<int> timeAxis, double beatLength)
        {
            // 遍历每个区域的每一行
            for (int row = startRow; row <= endRow; row++)
            {
                // 检查当前行是否有任何note
                bool hasNote = false;
                for (int col = 0; col < targetCols; col++)
                {
                    if (matrix[row, col] >= 0)
                    {
                        hasNote = true;
                        break;
                    }
                }

                // 如果当前行没有note，从orgMTX的相同行随机选取一个note插入
                if (!hasNote)
                {
                    // 查找orgMTX中当前行的有效note
                    var candidateNotes = new List<int>();
                    int originalCols = orgMTX.GetLength(1);
                    for (int col = 0; col < originalCols; col++)
                    {
                        if (orgMTX[row, col] >= 0)
                        {
                            candidateNotes.Add(col);
                        }
                    }

                    // 如果orgMTX中当前行有note
                    if (candidateNotes.Count > 0)
                    {
                        Random random = new Random();
                        // 随机选择一个候选note的列
                        int selectedOrgCol = candidateNotes[random.Next(candidateNotes.Count)];

                        // 查找matrix中可用的位置
                        var availablePositions = new List<int>();
                        for (int col = 0; col < targetCols; col++)
                        {
                            // 检查位置是否可用（前后beatLength/16+10时间内无物件）
                            if (IsPositionAvailableForEmptyRow(matrix, timeAxis, row, col, beatLength))
                            {
                                // 特别检查长条尾部时间距离要求
                                if (!IsHoldNoteTailTooClose(matrix, orgMTX, timeAxis, row, selectedOrgCol, col, beatLength))
                                {
                                    availablePositions.Add(col);
                                }
                            }
                        }

                        // 如果有可用位置
                        if (availablePositions.Count > 0)
                        {
                            // 随机选择一个可用位置
                            int targetCol = availablePositions[random.Next(availablePositions.Count)];
                            // 将note从orgMTX复制到matrix
                            matrix[row, targetCol] = orgMTX[row, selectedOrgCol];
                        }
                    }
                }
            }
        }

        private List<int> GetColumnsToRemove(int[] columnWeights, int targetCols, int originalCols,
                                            int[,] orgMTX, int regionStart, int regionEnd)
        {
            int colsToRemove = originalCols - targetCols;
            if (colsToRemove <= 0) return new List<int>();

            var columnList = new List<(int index, int weight, double risk)>();

            for (int i = 0; i < originalCols; i++)
            {
                // 计算该列的权重（note数量）
                int weight = columnWeights[i];

                // 计算该列的"空行风险"：如果移除它，有多少行会变成空？
                double risk = CalculateColumnRisk(orgMTX, i, originalCols, regionStart, regionEnd);

                columnList.Add((i, weight, risk));
            }

            // 排序：优先移除权重低 + 风险低的列
            // 首先按权重排序，权重相同时按风险排序
            columnList.Sort((a, b) =>
            {
                int weightComparison = a.weight.CompareTo(b.weight);
                if (weightComparison != 0)
                    return weightComparison;
                return a.risk.CompareTo(b.risk);
            });

            // 返回需要移除的列索引
            return columnList.Take(colsToRemove).Select(x => x.index).ToList();
        }

        private double CalculateColumnRisk(int[,] matrix, int colIndex, int totalCols, int regionStart, int regionEnd)
        {
            int totalRows = 0;
            int emptyRows = 0;

            for (int row = regionStart; row <= regionEnd; row++)
            {
                bool hasNoteInRow = false;

                // 检查该行在移除指定列后是否还有note
                for (int c = 0; c < totalCols; c++)
                {
                    if (c != colIndex && matrix[row, c] >= 0)
                    {
                        hasNoteInRow = true;
                        break;
                    }
                }

                // 如果移除该列后该行没有note了，则计为空行
                if (!hasNoteInRow && matrix[row, colIndex] >= 0)
                {
                    emptyRows++;
                }
                totalRows++;
            }

            // 如果该列本身没有note，则风险为0
            if (totalRows == 0) return 0;

            // 计算空行比例作为风险值
            return (double)emptyRows / totalRows;
        }

        private int[] CreateColumnMapping(int originalCols, List<int> columnsToRemove)
         {
             var mapping = new int[originalCols];
             int newColIndex = 0;

             for (int oldCol = 0; oldCol < originalCols; oldCol++)
             {
                 if (!columnsToRemove.Contains(oldCol))
                 {
                     mapping[oldCol] = newColIndex++;
                 }
                 else
                 {
                     // 被移除的列映射为-1
                     mapping[oldCol] = -1;
                 }
             }

             return mapping;
         }

        private bool IsPositionAvailable(int[,] matrix, int row, int col, List<int> timeAxis, double beatLength)
        {
            if (matrix[row, col] != -1)
                return false;

            // 检查前面几行
            for (int r = Math.Max(0, row - 3); r < row; r++)
            {
                if (timeAxis[row] - timeAxis[r] <= beatLength / 16 + 10)
                {
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;
                }
            }

            // 检查后面几行
            int rows = matrix.GetLength(0);
            for (int r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
            {
                if (timeAxis[r] - timeAxis[row] <= beatLength / 16 + 10)
                {
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;
                }
            }

            return true;
        }

        private void HandleLongNoteExtensions(int[,] newMatrix, int row, int targetCols)
        {
            // 处理延续到当前行的长条身体部分
            for (int col = 0; col < targetCols; col++)
            {
                if (newMatrix[row, col] == -1) // 位置为空
                {
                    // 检查是否应该填充长条身体
                    if (row > 0 && newMatrix[row - 1, col] == -7)
                    {
                        newMatrix[row, col] = -7;
                    }
                }
            }
        }

        private void CopyLongNoteBody(int[,] orgMTX, int[,] newMatrix, int startRow, int oldCol, int newCol, int totalRows)
        {
            // 复制长条的身体部分
            int row = startRow + 1;
            while (row < totalRows && orgMTX[row, oldCol] == -7)
            {
                // 确保目标列不会越界
                if (newCol < newMatrix.GetLength(1))
                {
                    newMatrix[row, newCol] = -7;
                }
                row++;
            }
        }

        private void ProcessEmptyRows(int[,] orgMTX, int[,] newMatrix, List<int> timeAxis, double beatLength)
        {
            int rows = newMatrix.GetLength(0);
            int targetCols = newMatrix.GetLength(1);
            int originalCols = orgMTX.GetLength(1);

            Random random = new Random();

            // 查找所有空行
            for (int row = 0; row < rows; row++)
            {
                // 检查当前行是否为空行（全为-1）
                bool isEmptyRow = true;
                for (int col = 0; col < targetCols; col++)
                {
                    if (newMatrix[row, col] >= 0)
                    {
                        isEmptyRow = false;
                        break;
                    }
                }

                // 如果是空行，进行处理
                if (isEmptyRow)
                {
                    // 第一步：尝试直接插入note到可用位置
                    if (TryInsertNoteDirectly(newMatrix, orgMTX, timeAxis, row, targetCols, originalCols, beatLength, random))
                    {
                        continue; // 成功插入，跳过第二步
                    }

                    // 第二步：尝试通过删除其他列的note来腾出空间
                    TryClearSpaceAndInsert(orgMTX, newMatrix, timeAxis, row, targetCols, originalCols, beatLength, random);
                }
            }
        }

        // 尝试直接插入note到当前行的可用位置
        private bool TryInsertNoteDirectly(int[,] newMatrix, int[,] orgMTX, List<int> timeAxis, int row,
            int targetCols, int originalCols, double beatLength, Random random)
        {
            // 收集当前行中所有可用的位置（前后时间窗口内无冲突）
            var availableCols = new List<int>();
            for (int col = 0; col < targetCols; col++)
            {
                if (IsPositionAvailableForEmptyRow(newMatrix, timeAxis, row, col, beatLength))
                {
                    availableCols.Add(col);
                }
            }

            // 如果没有可用位置，返回false
            if (availableCols.Count == 0)
                return false;

            // 从原始矩阵中找到该时间点的有效note
            var candidateNotes = new List<(int orgCol, int noteIndex)>();
            for (int orgCol = 0; orgCol < originalCols; orgCol++)
            {
                if (orgMTX[row, orgCol] >= 0)
                {
                    candidateNotes.Add((orgCol, orgMTX[row, orgCol]));
                }
            }

            // 如果没有候选note，返回false
            if (candidateNotes.Count == 0)
                return false;

            // 随机选择一个可用位置和一个候选note
            int targetCol = availableCols[random.Next(availableCols.Count)];
            var selectedNote = candidateNotes[random.Next(candidateNotes.Count)];

            // 检查是否为长条，并验证长条尾部是否满足时间距离要求
            if (IsHoldNoteTailTooClose(newMatrix, orgMTX, timeAxis, row, selectedNote.orgCol, targetCol, beatLength))
            {
                return false; // 不满足长条尾部时间距离要求
            }

            newMatrix[row, targetCol] = selectedNote.noteIndex;

            return true;
        }

        // 检查长条尾部是否过于接近下一个note
        private bool IsHoldNoteTailTooClose(int[,] newMatrix, int[,] orgMTX, List<int> timeAxis,
            int row, int orgCol, int targetCol, double beatLength)
        {
            double minTimeDistance = beatLength / 16 - 10; // 注意这里是-10

            // 检查原始矩阵中该位置是否为长条头部
            int rows = orgMTX.GetLength(0);
            int holdLength = 0;

            // 计算长条长度
            for (int r = row + 1; r < rows; r++)
            {
                if (orgMTX[r, orgCol] == -7)
                {
                    holdLength++;
                }
                else
                {
                    break;
                }
            }

            var isHoldNote = holdLength > 0;

            // 如果不是长条或者长度为0，直接返回false
            if (!isHoldNote || holdLength == 0)
                return false;

            // 检查长条尾部在新矩阵中的时间距离
            int tailRow = row + holdLength;
            if (tailRow < timeAxis.Count && tailRow < newMatrix.GetLength(0))
            {
                // 检查目标列在长条尾部是否有note
                for (int r = row + 1; r <= tailRow; r++)
                {
                    if (r < newMatrix.GetLength(0) && newMatrix[r, targetCol] >= 0)
                    {
                        double timeDistance = timeAxis[r] - timeAxis[row + holdLength];
                        if (timeDistance < minTimeDistance)
                        {
                            return true; // 时间距离太近
                        }
                        break;
                    }
                }
            }

            return false;
        }

        // 尝试通过删除其他列的note来腾出空间并插入note
        private void TryClearSpaceAndInsert(int[,] orgMTX, int[,] newMatrix, List<int> timeAxis,
                                           int emptyRow, int targetCols, int originalCols,
                                           double beatLength, Random random)
        {
            double timeThreshold = beatLength / 16 + 10;
            var processedCols = new HashSet<int>(); // 记录已尝试的列

            // 找到时间范围内（前后beatLength/16+10）的所有行
            var timeRangeRows = new List<int>();
            for (int row = 0; row < newMatrix.GetLength(0); row++)
            {
                if (Math.Abs(timeAxis[row] - timeAxis[emptyRow]) <= timeThreshold)
                {
                    timeRangeRows.Add(row);
                }
            }

            // 如果没有在时间范围内的行，直接返回
            if (timeRangeRows.Count == 0)
                return;

            // 随机打乱列的顺序
            var colsToTry = Enumerable.Range(0, targetCols).ToList();
            ShuffleList(colsToTry, random);

            foreach (int col in colsToTry)
            {
                if (processedCols.Contains(col)) continue;

                // 检查该列在时间范围内是否有note可以删除
                bool hasNotesToRemove = false;
                foreach (int row in timeRangeRows)
                {
                    if (newMatrix[row, col] >= 0)
                    {
                        hasNotesToRemove = true;
                        break;
                    }
                }

                if (!hasNotesToRemove)
                    continue;

                // 保存原始状态以便恢复
                var originalValues = new Dictionary<int, int>();
                foreach (int row in timeRangeRows)
                {
                    originalValues[row] = newMatrix[row, col];
                }

                // 删除时间范围内该列的所有note
                foreach (int row in timeRangeRows)
                {
                    if (newMatrix[row, col] >= 0)
                    {
                        newMatrix[row, col] = -1;
                    }
                }

                // 检查删除后是否会产生新的空行
                bool createsEmptyRows = false;
                foreach (int row in timeRangeRows)
                {
                    bool isEmptyRow = true;
                    for (int c = 0; c < targetCols; c++)
                    {
                        if (newMatrix[row, c] != -1)
                        {
                            isEmptyRow = false;
                            break;
                        }
                    }

                    if (isEmptyRow)
                    {
                        createsEmptyRows = true;
                        break;
                    }
                }

                // 如果删除操作会产生新的空行，恢复原始状态并尝试下一列
                if (createsEmptyRows)
                {
                    foreach (var kvp in originalValues)
                    {
                        newMatrix[kvp.Key, col] = kvp.Value;
                    }
                    processedCols.Add(col);
                    continue;
                }

                // 如果删除成功且不产生新的空行，现在尝试在空行中插入note
                if (TryInsertNoteDirectly(newMatrix, orgMTX, timeAxis, emptyRow, targetCols, originalCols, beatLength, random))
                {
                    // 插入成功，完成操作
                    return;
                }
                else
                {
                    // 插入失败，恢复原始状态并尝试下一列
                    foreach (var kvp in originalValues)
                    {
                        newMatrix[kvp.Key, col] = kvp.Value;
                    }
                    processedCols.Add(col);
                }
            }

            // 如果所有列都尝试过了还是找不到满足条件的位置，就不处理了
        }



        private bool IsPositionAvailableForEmptyRow(int[,] matrix, List<int> timeAxis,
                                                   int row, int col, double beatLength)
         {
             if (matrix[row, col] != -1)
                 return false;

             // 检查前面几行
            int rows = matrix.GetLength(0);
             for (int r = Math.Max(0, row - 3); r < row; r++)
             {
                 if (timeAxis[row] - timeAxis[r] <= beatLength / 16 + 10)
                 {
                     if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                         return false;
                 }
             }

             // 检查后面几行
             for (int r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
             {
                 if (timeAxis[r] - timeAxis[row] <= beatLength / 16 + 10)
                 {
                     if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                         return false;
                 }
             }

             return true;
         }

        // 辅助方法：随机打乱列表
        private void ShuffleList<T>(List<T> list, Random random)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        class OscillatorGenerator
        {
            private readonly int _maxValue;
            private int _currentValue;
            private int _direction;
            public OscillatorGenerator(int maxValue, Random? random = null)
            {
                if (maxValue <= 0)
                {
                    throw new ArgumentException("maxValue 必须大于零");
                }

                _maxValue = maxValue;
                var rnd = random ?? new Random();
                _currentValue = rnd.Next(1, maxValue);
                _direction = rnd.Next(0, 2) == 0 ? -1 : 1;
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

        // TODO: 设置检查不合理，应重构
    }
}
