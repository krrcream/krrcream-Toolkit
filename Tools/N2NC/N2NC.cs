using System;
using System.Collections.Generic;
using System.Linq;
using krrTools.Beatmaps;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC 转换算法实现
    /// </summary>
    public class N2NC
    {
        /// <summary>
        /// 执行谱面转换
        /// </summary>
        public void TransformBeatmap(Beatmap beatmap, N2NCOptions options)
        {
            var random = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();
            var (matrix, timeAxis) = beatmap.BuildMatrix();
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options, random);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options);
        }

        /// <summary>
        /// 处理音符矩阵
        /// </summary>
        private NoteMatrix ProcessMatrix(NoteMatrix matrix, List<int> timeAxis, Beatmap beatmap, N2NCOptions options, Random random)
        {
            return ConvertMatrix(matrix, timeAxis, beatmap, options, random);
        }

        /// <summary>
        /// 将处理后的矩阵应用到谱面对象
        /// </summary>
        private void ApplyChangesToHitObjects(Beatmap beatmap, NoteMatrix processedMatrix, N2NCOptions options)
        {
            NewHitObjects(beatmap, processedMatrix, options);

            // 修改元数据
            var originalCS = (int)beatmap.DifficultySection.CircleSize;
            var tag = $"[{originalCS}to{options.TargetKeys.Value}C]";
            if (!beatmap.MetadataSection.Version.Contains(tag))
            {
                beatmap.DifficultySection.CircleSize = (float)options.TargetKeys.Value;
                beatmap.MetadataSection.Version = tag + " " + beatmap.MetadataSection.Version;
            }
        }

        private NoteMatrix ConvertMatrix(NoteMatrix matrix, List<int> timeAxis, Beatmap beatmap, N2NCOptions options, Random random)
        {
            var CS = (int)beatmap.DifficultySection.CircleSize;
            var targetKeys = (int)options.TargetKeys.Value;
            var turn = targetKeys - CS;
            var P = options.SelectedKeyTypes;
            // 使用传入的随机数生成器
            var RG = random;

            // Console.WriteLine($"[N2NC] ConvertMatrix: CS={CS}, targetKeys={targetKeys}, turn={turn}, matrix={matrix.Rows}x{matrix.Cols}, timeAxis={timeAxis.Count}");

            if (P is { Count: > 0 } && !P.Contains(CS))
            {
                Console.WriteLine($"[WARN] 谱面键数 {CS} 不在筛选的键位模式里 {string.Join(",", P)}，跳过转换");
                return matrix;
            }

            if (CS == targetKeys && (int)options.TargetKeys.Value == targetKeys)
            {
                return matrix;
            }

            var BPM = beatmap.GetBPM();
            // _logger.LogDebug($"BPM：{BPM}");
            var beatLength = 60000 / BPM * 4;
            // Logger.WriteLine("BPM：" + BPM);
            var convertTime = Math.Max(1, options.TransformSpeed.Value * beatLength - 10);

            var newMatrix = turn >= 0
                ? DoAddKeys(matrix, timeAxis, turn, convertTime, CS, targetKeys, beatLength, RG, options)
                : DoRemoveKeys(matrix, timeAxis, turn, convertTime, beatLength, RG, CS, options);

            return newMatrix;
        }

        private NoteMatrix DoAddKeys(NoteMatrix matrix, List<int> timeAxis, int turn, double convertTime,
            int CS, int targetKeys, double beatLength, Random random, N2NCOptions options)
        {
            // 生成转换矩阵
            var (oldMTX, insertMTX) = convertMTX(turn, timeAxis, convertTime, CS, random);
            var newMatrix = convert(matrix, oldMTX, insertMTX, timeAxis, targetKeys, beatLength, random);
            DensityReducer(newMatrix, (int)options.TargetKeys.Value - (int)options.MaxKeys.Value, (int)options.MinKeys.Value, (int)options.TargetKeys.Value, random);
            return newMatrix;
        }

        private NoteMatrix DoRemoveKeys(NoteMatrix matrix, List<int> timeAxis, int turn, double convertTime,
            double beatLength, Random random, int originalCS, N2NCOptions options)
        {
#pragma warning disable CS0219 // Variable is assigned but its value is never used
            var _ = originalCS;
#pragma warning restore CS0219
            var newMatrix = SmartReduceColumns(matrix, timeAxis, -turn, convertTime, beatLength, random);
            DensityReducer(newMatrix, (int)options.TargetKeys.Value - (int)options.MaxKeys.Value, (int)options.MinKeys.Value, (int)options.TargetKeys.Value, random);
            return newMatrix;
        }

        // TODO：统一生成基础矩阵，再由模块处理更好。
        // 未来库完善，矩阵可弃用，仅供测试模式下输出运行模型
        public (NoteMatrix, NoteMatrix) convertMTX(int turn, List<int> timeAxis,
            double convertTime, int CS, Random random)
        {
            var rows = timeAxis.Count;
            var cols = turn; // 需要添加的列数

            if (rows == 0 || cols == 0)
                return (new NoteMatrix(rows, cols), new NoteMatrix(rows, cols));

            // 初始化两个矩阵
            var oldMTX = new NoteMatrix(rows, cols);
            var insertMTX = new NoteMatrix(rows, cols);

            // 生成 oldMTX 矩阵
            for (var col = 0; col < cols; col++)
            {
                // 为每一列创建一个震荡数字生成器，范围是 0 到 CS-1
                var oldIndex = new OscillatorGenerator(CS - 1, random);
                // 重置时间计数器
                double timeCounter = 0;
                var lastTime = timeAxis[0];

                for (var row = 0; row < rows; row++)
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

                var randomMoves = random.Next(0, CS - 1);
                for (var i = 0; i < randomMoves; i++) oldIndex.Next();
            }

            // 生成 insertMTX 矩阵
            for (var col = 0; col < cols; col++)
            {
                // 为每一列创建一个震荡数字生成器，范围是 0 到 (CS + col)
                // 随着列的增加，可插入位置也在增加
                var insertIndex = new OscillatorGenerator(CS + col, random);
                double timeCounter = 0;
                var lastTime = timeAxis[0];

                for (var row = 0; row < rows; row++)
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

                var randomMoves = random.Next(0, CS - 1 + col); // 随机移动0-2次
                for (var i = 0; i < randomMoves; i++) insertIndex.Next();
            }

            return (oldMTX, insertMTX);
        }

        // 转换操作
        public NoteMatrix convert(NoteMatrix matrix, NoteMatrix oldMTX, NoteMatrix insertMTX, List<int> timeAxis1, int targetKeys,
            double beatLength, Random random)
        {
            try
            {
                var rows = matrix.Rows;
                var originalCols = matrix.Cols;
                var newCols = targetKeys;
                var turn1 = oldMTX.Cols; // oldMTX的列数

                // 创建一个新的矩阵，列数为目标键数，行数与原矩阵相同，初始化为-1
                var newMatrix = new NoteMatrix(rows, newCols);

                // 处理每一行
                for (var i = 0; i < rows; i++)
                {
                    // 创建临时数组
                    var tempRow = new int[newCols];

                    // 是否改变convert位置

                    var flagChangeCol = new bool[turn1];
                    var changeRowIndex = -1;
                    if (i >= 1 && (AreRowsDifferent(oldMTX, i, i - 1) || AreRowsDifferent(insertMTX, i, i - 1)))
                    {
                        changeRowIndex = i;
                        for (var j = 0; j < turn1; j++) flagChangeCol[j] = true;
                    }

                    // 初始化为-1
                    for (var k = 0; k < newCols; k++) tempRow[k] = -1;

                    var orgCurrentRow = matrix.GetRowSpan(i);
                    // 先复制原始矩阵的这一行内容到临时数组的左侧
                    for (var j = 0; j < originalCols && j < newCols; j++) tempRow[j] = orgCurrentRow[j];

                    //检查每根面条后续占用的行数
                    var LNCount = new Dictionary<int, int>();
                    for (var j = 0; j < originalCols; j++)
                        if (matrix[i, j] >= 0)
                        {
                            var count = 0;
                            var k = 1;
                            while (i + k < rows && matrix[i + k, j] == -7)
                            {
                                count++;
                                k++;
                            }

                            LNCount[j] = count;
                        }
                    //插入复制的物件

                    for (var j = 0; j < turn1; j++)
                    {
                        var oldIndex = oldMTX[i, j];
                        var insertIndex = insertMTX[i, j];

                        //检查原本位置是否有物件需要复制
                        var flagNeedCopy = matrix[i, oldIndex] >= 0;

                        //先shift物件
                        ShiftInsert(tempRow, insertIndex);
                        if (!flagChangeCol[j] && flagNeedCopy)
                        {
                            tempRow[insertIndex] = matrix[i, oldIndex];
                        }
                        else if (flagChangeCol[j] && flagNeedCopy)
                        {
                            if (timeAxis1[i] - timeAxis1[changeRowIndex] < beatLength / 16 * 3 + 10) continue;

                            tempRow[insertIndex] = matrix[i, oldIndex];
                            flagChangeCol[j] = false;
                        }
                    }

                    //填充newMatrix 第i行
                    for (var j = 0; j < newCols; j++)
                        if (tempRow[j] >= 0 && newMatrix[i, j] == -1)
                            newMatrix[i, j] = tempRow[j];

                    /*
                    填充面条身体
                    */
                    foreach (var kvp in LNCount)
                    {
                        var originalColumn = kvp.Key; // 原始列号
                        var lnLength = kvp.Value; // 长音符长度

                        var newValue = matrix[i, originalColumn]; // 要查找的值
                        var newColumns = new List<int>(); // 存储所有匹配的列索引

                        // 查找newValue在newMatrix[i]行中的所有位置
                        for (var col = 0; col < newCols; col++)
                            if (newMatrix[i, col] == newValue)
                                newColumns.Add(col);

                        // 为每个匹配的位置填充长音符身体部分
                        foreach (var newColumn in newColumns)
                            // 填充长音符的身体部分（-7表示长音符身体）
                            for (var k = 1; k <= lnLength && i + k < rows; k++)
                                newMatrix[i + k, newColumn] = -7;
                    }
                }

                //位置映射，拷贝newMatrix到colsMatrix;    
                var colsMatrix = new NoteMatrix(newMatrix.Rows, newMatrix.Cols);
                newMatrix.CopyTo(colsMatrix);

                for (var j = 0; j < newCols; j++)
                for (var i = 1; i < rows; i++)
                    if (colsMatrix[i, j] == -7)
                        colsMatrix[i, j] = colsMatrix[i - 1, j];

                for (var i = 0; i < rows; i++)
                for (var j = 0; j < newCols; j++)
                {
                    var targetValue = colsMatrix[i, j];
                    if (targetValue >= 0)
                        // 在matrix第i行查找targetValue所在的列索引
                        for (var c = 0; c < originalCols; c++)
                            if (matrix[i, c] == targetValue)
                            {
                                colsMatrix[i, j] = c;
                                break;
                            }
                }

                //删除矩阵
                var needDeleteMTX = new bool[colsMatrix.Rows, colsMatrix.Cols];

                for (var j = 0; j < newCols; j++)
                for (var i = 0; i < rows - 1; i++)
                    // 如果当前值和下一个值不同，且都不为-1
                    if (colsMatrix[i, j] != -1 && colsMatrix[i + 1, j] != -1 && colsMatrix[i, j] != colsMatrix[i + 1, j])
                    {
                        var changeRow = i; // 变化点
                        var nextValue = colsMatrix[i + 1, j]; // 变化后的值

                        // 从变化点之后开始检查
                        for (var k = 1; i + 1 + k < rows; k++)
                        {
                            var checkRow = i + 1 + k;

                            // 如果值变了，停止检查
                            if (colsMatrix[checkRow, j] != nextValue)
                                break;

                            // 如果时间差小于阈值，标记为需要删除
                            if (timeAxis1[checkRow] - timeAxis1[changeRow] < beatLength / 16 * 2 + 10)
                                needDeleteMTX[checkRow, j] = true;
                            else
                                break; // 时间差太大，停止
                        }
                    }

                for (var i = 0; i < rows; i++)
                for (var j = 0; j < newCols; j++)
                    if (needDeleteMTX[i, j])
                        newMatrix[i, j] = -1;

                return newMatrix;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[N2NC] convert方法发生异常: {0}", ex.Message);
                Logger.WriteLine(LogLevel.Error, "[N2NC] 异常堆栈: {0}", ex.StackTrace ?? "null");
                throw;
            }
        }

        private bool AreRowsDifferent(NoteMatrix matrix, int row1, int row2)
        {
            var colCount = matrix.Cols;
            for (var j = 0; j < colCount; j++)
                if (matrix[row1, j] != matrix[row2, j])
                    return true;

            return false;
        }

        private void ShiftInsert<T>(T nums, int insertIndex) where T : IList<int>
        {
            // 检查insertIndex是否为有效下标
            if (insertIndex >= 0 && insertIndex <= nums.Count - 1)
            {
                // 从右向左移动元素，避免覆盖
                for (var i = nums.Count - 1; i > insertIndex; i--) nums[i] = nums[i - 1];

                // 将insertIndex位置设为-1
                nums[insertIndex] = -1;
            }
            else
            {
                throw new IndexOutOfRangeException("insertIndex 超出有效范围");
            }
        }

        private void NewHitObjects(Beatmap beatmap, NoteMatrix newMatrix, N2NCOptions options)
        {
            // 创建临时列表存储对象
            var newObjects = new List<HitObject>();
            //遍历newMatrix
            for (var i = 0; i < newMatrix.Rows; i++)
            for (var j = 0; j < newMatrix.Cols; j++)
            {
                var oldIndex = newMatrix[i, j];
                if (oldIndex >= 0)
                    newObjects.Add(BeatmapExtensions.CopyHitObjectByPositionX(beatmap.HitObjects[oldIndex],
                        ColumnPositionMapper.ColumnToPositionX((int)options.TargetKeys.Value, j)
                    ));
            }

            beatmap.HitObjects.Clear();
            // 在遍历完成后添加所有新对象
            beatmap.HitObjects.AddRange(newObjects);
            beatmap.SortHitObjects();
        }

        private void DensityReducer(NoteMatrix matrix, int maxToRemovePerRow, int minKeys, int targetKeys, Random random)
        {
            if (maxToRemovePerRow <= 0) return;

            var rows = matrix.Rows;
            var cols = matrix.Cols;

            // 记录每列被删除的次数，用于全局平衡
            var columnDeletionCounts = new int[cols];

            // 遍历每一行进行密度降低
            for (var i = 0; i < rows; i++)
            {
                // 统计当前行中有效note的数量
                var activeNotes = new List<int>(); // 存储有效note的列索引
                for (var j = 0; j < cols; j++)
                    if (matrix[i, j] >= 0)
                        activeNotes.Add(j);

                // 如果当前行note数已经少于最小键数，跳过
                if (activeNotes.Count <= minKeys) continue;

                // 计算目标note数：基于比例缩减，但不低于最小键数
                var targetNotes = Math.Max(
                    minKeys,
                    Math.Min(activeNotes.Count,
                        (int)(activeNotes.Count * (double)(targetKeys - maxToRemovePerRow) / targetKeys))
                );

                // 计算需要删除的数量
                var toRemove = Math.Max(0, activeNotes.Count - targetNotes);
                if (toRemove <= 0) continue;

                // 根据权重选择要删除的列
                // var columnsToRemove = new List<int>(); // removed: previously unused

                // 创建临时列表用于选择
                var candidates = new List<int>(activeNotes);

                for (var r = 0; r < toRemove && candidates.Count > 0; r++)
                {
                    // 计算权重（被删除次数越少，权重越高）
                    var weights = new double[candidates.Count];
                    double totalWeight = 0;

                    for (var j = 0; j < candidates.Count; j++)
                    {
                        // 权重与历史删除次数成反比，确保全局平衡
                        weights[j] = 1.0 / (1.0 + columnDeletionCounts[candidates[j]]);
                        totalWeight += weights[j];
                    }

                    // 轮盘赌选择
                    var randomValue = random.NextDouble() * totalWeight;
                    double currentWeight = 0;
                    var selectedIndex = 0;

                    for (var j = 0; j < candidates.Count; j++)
                    {
                        currentWeight += weights[j];
                        if (randomValue <= currentWeight)
                        {
                            selectedIndex = j;
                            break;
                        }
                    }

                    // 执行删除
                    var columnToRemove = candidates[selectedIndex];
                    matrix[i, columnToRemove] = -1;
                    columnDeletionCounts[columnToRemove]++; // 更新该列的删除计数
                    // columnsToRemove.Add(columnToRemove);
                    candidates.RemoveAt(selectedIndex);
                }
            }
        }



        public NoteMatrix SmartReduceColumns(NoteMatrix orgMTX, List<int> timeAxis, int turn, double convertTime, double beatLength, Random random)
        {
            var rows = orgMTX.Rows;
            var originalCols = orgMTX.Cols;
            var targetCols = originalCols - turn;

            // 创建新矩阵，初始化为-1（空）
            var newMatrix = new NoteMatrix(rows, targetCols);
            for (var i = 0; i < rows; i++)
            for (var j = 0; j < targetCols; j++)
                newMatrix[i, j] = -1;

            // 按时间段处理
            var regionStart = 0;

            for (var regionEnd = 1; regionEnd < rows; regionEnd++)
            {
                // 检查是否到达新区域的结束点
                var isRegionEnd = timeAxis[regionEnd] - timeAxis[regionStart] >= convertTime;
                var isLastRow = regionEnd == rows - 1;

                if (isRegionEnd || isLastRow)
                {
                    // 确保最后一个区域包含最后一行
                    if (isLastRow && !isRegionEnd) regionEnd = rows - 1;

                    // 处理当前区域
                    ProcessRegion(orgMTX, newMatrix, timeAxis, regionStart, regionEnd, targetCols, beatLength, random);

                    // 更新下一个区域的起始点
                    regionStart = regionEnd;
                }
            }

            // 处理可能剩余的行（如果最后一段不足一个完整区域）
            if (regionStart < rows - 1)
                ProcessRegion(orgMTX, newMatrix, timeAxis, regionStart, rows - 1, targetCols, beatLength, random);

            // 处理空行
            ProcessEmptyRows(orgMTX, newMatrix, timeAxis, beatLength, random);

            return newMatrix;
        }

        private void ProcessRegion(NoteMatrix orgMTX, NoteMatrix newMatrix, List<int> timeAxis,
            int regionStart, int regionEnd, int targetCols, double beatLength, Random random)
        {
            var originalCols = orgMTX.Cols;
            var rows = orgMTX.Rows;

            // 分析区域内各列的重要性（物件数量）
            var columnWeights = new int[originalCols];
            for (var i = regionStart; i <= regionEnd; i++)
            for (var j = 0; j < originalCols; j++)
                if (orgMTX[i, j] >= 0) // 统计有效物件
                    columnWeights[j]++;

            // 确定要移除的列（选择权重最小且风险最低的几列）
            var columnsToRemove =
                GetColumnsToRemove(columnWeights, targetCols, originalCols, orgMTX, regionStart, regionEnd);

            // 创建列映射关系（原列 -> 新列）
            var columnMapping = CreateColumnMapping(originalCols, columnsToRemove);

            // 处理区域内的每一行
            for (var row = regionStart; row <= regionEnd; row++)
                // 复制物件到新矩阵
            for (var col = 0; col < originalCols; col++)
            {
                var newValue = orgMTX[row, col];
                if (newValue >= 0) // 有效物件
                {
                    var newCol = columnMapping[col];
                    if (newCol >= 0) // 该列未被移除
                        // 检查目标位置是否可用（避免冲突）
                        if (IsPositionAvailable(newMatrix, row, newCol, timeAxis, beatLength))
                        {
                            newMatrix[row, newCol] = newValue;

                            // 如果是长条头部，复制整个长条
                            CopyLongNoteBody(orgMTX, newMatrix, row, col, newCol, rows);
                        }
                }
            }

            // 处理长条延续部分
            for (var row = regionStart; row <= regionEnd; row++) HandleLongNoteExtensions(newMatrix, row, targetCols);

            // 应用约束条件：确保每行至少有一个note
            // 应用约束条件：确保每行至少有一个note
            ApplyMinimumNotesConstraint(newMatrix, orgMTX, regionStart, regionEnd, targetCols, timeAxis, beatLength, random);
        }

        private void ApplyMinimumNotesConstraint(NoteMatrix matrix, NoteMatrix orgMTX, int startRow, int endRow, int targetCols,
            List<int> timeAxis, double beatLength, Random random)
        {
            // 遍历每个区域的每一行
            for (var row = startRow; row <= endRow; row++)
            {
                // 检查当前行是否有任何note
                var hasNote = false;
                for (var col = 0; col < targetCols; col++)
                    if (matrix[row, col] >= 0)
                    {
                        hasNote = true;
                        break;
                    }

                // 如果当前行没有note，从orgMTX的相同行随机选取一个note插入
                if (!hasNote)
                {
                    // 查找orgMTX中当前行的有效note
                    var candidateNotes = new List<int>();
                    var originalCols = orgMTX.Cols;
                    for (var col = 0; col < originalCols; col++)
                        if (orgMTX[row, col] >= 0)
                            candidateNotes.Add(col);

                    // 如果orgMTX中当前行有note
                    if (candidateNotes.Count > 0)
                    {
                        // 随机选择一个候选note的列
                        var selectedOrgCol = candidateNotes[random.Next(candidateNotes.Count)];

                        // 查找matrix中可用的位置
                        var availablePositions = new List<int>();
                        for (var col = 0; col < targetCols; col++)
                            // 检查位置是否可用（前后beatLength/16+10时间内无物件）
                            if (IsPositionAvailableForEmptyRow(matrix, timeAxis, row, col, beatLength))
                                // 特别检查长条尾部时间距离要求
                                if (!IsHoldNoteTailTooClose(matrix, orgMTX, timeAxis, row, selectedOrgCol, col, beatLength))
                                    availablePositions.Add(col);

                        // 如果有可用位置
                        if (availablePositions.Count > 0)
                        {
                            // 随机选择一个可用位置
                            var targetCol = availablePositions[random.Next(availablePositions.Count)];
                            // 将note从orgMTX复制到matrix
                            matrix[row, targetCol] = orgMTX[row, selectedOrgCol];
                        }
                    }
                }
            }
        }

        private List<int> GetColumnsToRemove(int[] columnWeights, int targetCols, int originalCols,
            NoteMatrix orgMTX, int regionStart, int regionEnd)
        {
            var colsToRemove = originalCols - targetCols;
            if (colsToRemove <= 0) return new List<int>();

            var columnList = new List<(int index, int weight, double risk)>();

            for (var i = 0; i < originalCols; i++)
            {
                // 计算该列的权重（note数量）
                var weight = columnWeights[i];

                // 计算该列的"空行风险"：如果移除它，有多少行会变成空？
                var risk = CalculateColumnRisk(orgMTX, i, originalCols, regionStart, regionEnd);

                columnList.Add((i, weight, risk));
            }

            // 排序：优先移除权重低 + 风险低的列
            // 首先按权重排序，权重相同时按风险排序
            columnList.Sort((a, b) =>
            {
                var weightComparison = a.weight.CompareTo(b.weight);
                if (weightComparison != 0)
                    return weightComparison;
                return a.risk.CompareTo(b.risk);
            });

            // 返回需要移除的列索引
            return columnList.Take(colsToRemove).Select(x => x.index).ToList();
        }

        private double CalculateColumnRisk(NoteMatrix matrix, int colIndex, int totalCols, int regionStart, int regionEnd)
        {
            var totalRows = 0;
            var emptyRows = 0;

            for (var row = regionStart; row <= regionEnd; row++)
            {
                var hasNoteInRow = false;

                // 检查该行在移除指定列后是否还有note
                for (var c = 0; c < totalCols; c++)
                    if (c != colIndex && matrix[row, c] >= 0)
                    {
                        hasNoteInRow = true;
                        break;
                    }

                // 如果移除该列后该行没有note了，则计为空行
                if (!hasNoteInRow && matrix[row, colIndex] >= 0) emptyRows++;
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
            var newColIndex = 0;

            for (var oldCol = 0; oldCol < originalCols; oldCol++)
                if (!columnsToRemove.Contains(oldCol))
                    mapping[oldCol] = newColIndex++;
                else
                    // 被移除的列映射为-1
                    mapping[oldCol] = -1;

            return mapping;
        }

        private bool IsPositionAvailable(NoteMatrix matrix, int row, int col, List<int> timeAxis, double beatLength)
        {
            if (matrix[row, col] != -1)
                return false;

            // 检查前面几行
            for (var r = Math.Max(0, row - 3); r < row; r++)
                if (timeAxis[row] - timeAxis[r] <= beatLength / 16 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            // 检查后面几行
            var rows = matrix.Rows;
            for (var r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
                if (timeAxis[r] - timeAxis[row] <= beatLength / 16 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            return true;
        }

        private void HandleLongNoteExtensions(NoteMatrix newMatrix, int row, int targetCols)
        {
            // 处理延续到当前行的长条身体部分
            for (var col = 0; col < targetCols; col++)
                if (newMatrix[row, col] == -1) // 位置为空
                    // 检查是否应该填充长条身体
                    if (row > 0 && newMatrix[row - 1, col] == -7)
                        newMatrix[row, col] = -7;
        }

        private void CopyLongNoteBody(NoteMatrix orgMTX, NoteMatrix newMatrix, int startRow, int oldCol, int newCol, int totalRows)
        {
            // 复制长条的身体部分
            var row = startRow + 1;
            while (row < totalRows && orgMTX[row, oldCol] == -7)
            {
                // 确保目标列不会越界
                if (newCol < newMatrix.Cols) newMatrix[row, newCol] = -7;
                row++;
            }
        }

        private void ProcessEmptyRows(NoteMatrix orgMTX, NoteMatrix newMatrix, List<int> timeAxis, double beatLength, Random random)
        {
            var rows = newMatrix.Rows;
            var targetCols = newMatrix.Cols;
            var originalCols = orgMTX.Cols;

            // 查找所有空行
            for (var row = 0; row < rows; row++)
            {
                // 检查当前行是否为空行（全为-1）
                var isEmptyRow = true;
                for (var col = 0; col < targetCols; col++)
                    if (newMatrix[row, col] >= 0)
                    {
                        isEmptyRow = false;
                        break;
                    }

                // 如果是空行，进行处理
                if (isEmptyRow)
                {
                    // 第一步：尝试直接插入note到可用位置
                    if (TryInsertNoteDirectly(newMatrix, orgMTX, timeAxis, row, targetCols, originalCols, beatLength,
                            random)) continue; // 成功插入，跳过第二步

                    // 第二步：尝试通过删除其他列的note来腾出空间
                    TryClearSpaceAndInsert(orgMTX, newMatrix, timeAxis, row, targetCols, originalCols, beatLength, random);
                }
            }
        }

        // 尝试直接插入note到当前行的可用位置
        private bool TryInsertNoteDirectly(NoteMatrix newMatrix, NoteMatrix orgMTX, List<int> timeAxis, int row,
            int targetCols, int originalCols, double beatLength, Random random)
        {
            // 收集当前行中所有可用的位置（前后时间窗口内无冲突）
            var availableCols = new List<int>();
            for (var col = 0; col < targetCols; col++)
                if (IsPositionAvailableForEmptyRow(newMatrix, timeAxis, row, col, beatLength))
                    availableCols.Add(col);

            // 如果没有可用位置，返回false
            if (availableCols.Count == 0)
                return false;

            // 从原始矩阵中找到该时间点的有效note
            var candidateNotes = new List<(int orgCol, int noteIndex)>();
            for (var orgCol = 0; orgCol < originalCols; orgCol++)
                if (orgMTX[row, orgCol] >= 0)
                    candidateNotes.Add((orgCol, orgMTX[row, orgCol]));

            // 如果没有候选note，返回false
            if (candidateNotes.Count == 0)
                return false;

            // 随机选择一个可用位置和一个候选note
            var targetCol = availableCols[random.Next(availableCols.Count)];
            var selectedNote = candidateNotes[random.Next(candidateNotes.Count)];

            // 检查是否为长条，并验证长条尾部是否满足时间距离要求
            if (IsHoldNoteTailTooClose(newMatrix, orgMTX, timeAxis, row, selectedNote.orgCol, targetCol, beatLength))
                return false; // 不满足长条尾部时间距离要求

            newMatrix[row, targetCol] = selectedNote.noteIndex;

            return true;
        }

        // 检查长条尾部是否过于接近下一个note
        private bool IsHoldNoteTailTooClose(NoteMatrix newMatrix, NoteMatrix orgMTX, List<int> timeAxis,
            int row, int orgCol, int targetCol, double beatLength)
        {
            var minTimeDistance = beatLength / 16 - 10; // 注意这里是-10

            // 检查原始矩阵中该位置是否为长条头部
            var rows = orgMTX.Rows;
            var holdLength = 0;

            // 计算长条长度
            for (var r = row + 1; r < rows; r++)
                if (orgMTX[r, orgCol] == -7)
                    holdLength++;
                else
                    break;

            var isHoldNote = holdLength > 0;

            // 如果不是长条或者长度为0，直接返回false
            if (!isHoldNote || holdLength == 0)
                return false;

            // 检查长条尾部在新矩阵中的时间距离
            var tailRow = row + holdLength;
            if (tailRow < timeAxis.Count && tailRow < newMatrix.Rows)
                // 检查目标列在长条尾部是否有note
                for (var r = row + 1; r <= tailRow; r++)
                    if (r < newMatrix.Rows && newMatrix[r, targetCol] >= 0)
                    {
                        double timeDistance = timeAxis[r] - timeAxis[row + holdLength];
                        if (timeDistance < minTimeDistance) return true; // 时间距离太近
                        break;
                    }

            return false;
        }

        // 尝试通过删除其他列的note来腾出空间并插入note
        private void TryClearSpaceAndInsert(NoteMatrix orgMTX, NoteMatrix newMatrix, List<int> timeAxis,
            int emptyRow, int targetCols, int originalCols,
            double beatLength, Random random)
        {
            var timeThreshold = beatLength / 16 + 10;
            var processedCols = new HashSet<int>(); // 记录已尝试的列

            // 找到时间范围内（前后beatLength/16+10）的所有行
            var timeRangeRows = new List<int>();
            for (var row = 0; row < newMatrix.Rows; row++)
                if (Math.Abs(timeAxis[row] - timeAxis[emptyRow]) <= timeThreshold)
                    timeRangeRows.Add(row);

            // 如果没有在时间范围内的行，直接返回
            if (timeRangeRows.Count == 0)
                return;

            // 随机打乱列的顺序
            var colsToTry = Enumerable.Range(0, targetCols).ToList();
            ShuffleList(colsToTry, random);

            foreach (var col in colsToTry)
            {
                if (processedCols.Contains(col)) continue;

                // 检查该列在时间范围内是否有note可以删除
                var hasNotesToRemove = false;
                foreach (var row in timeRangeRows)
                    if (newMatrix[row, col] >= 0)
                    {
                        hasNotesToRemove = true;
                        break;
                    }

                if (!hasNotesToRemove)
                    continue;

                // 保存原始状态以便恢复
                var originalValues = new Dictionary<int, int>();
                foreach (var row in timeRangeRows) originalValues[row] = newMatrix[row, col];

                // 删除时间范围内该列的所有note
                foreach (var row in timeRangeRows)
                    if (newMatrix[row, col] >= 0)
                        newMatrix[row, col] = -1;

                // 检查删除后是否会产生新的空行
                var createsEmptyRows = false;
                foreach (var row in timeRangeRows)
                {
                    var isEmptyRow = true;
                    for (var c = 0; c < targetCols; c++)
                        if (newMatrix[row, c] != -1)
                        {
                            isEmptyRow = false;
                            break;
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
                    foreach (var kvp in originalValues) newMatrix[kvp.Key, col] = kvp.Value;
                    processedCols.Add(col);
                    continue;
                }

                // 如果删除成功且不产生新的空行，现在尝试在空行中插入note
                if (TryInsertNoteDirectly(newMatrix, orgMTX, timeAxis, emptyRow, targetCols, originalCols, beatLength,
                        random))
                {
                    // 插入成功，完成操作
                    return;
                }
                else
                {
                    // 插入失败，恢复原始状态并尝试下一列
                    foreach (var kvp in originalValues) newMatrix[kvp.Key, col] = kvp.Value;
                    processedCols.Add(col);
                }
            }

            // 如果所有列都尝试过了还是找不到满足条件的位置，就不处理了
        }


        private bool IsPositionAvailableForEmptyRow(NoteMatrix matrix, List<int> timeAxis,
            int row, int col, double beatLength)
        {
            if (matrix[row, col] != -1)
                return false;

            // 检查前面几行
            var rows = matrix.Rows;
            for (var r = Math.Max(0, row - 3); r < row; r++)
                if (timeAxis[row] - timeAxis[r] <= beatLength / 16 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            // 检查后面几行
            for (var r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
                if (timeAxis[r] - timeAxis[row] <= beatLength / 16 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            return true;
        }

        // 辅助方法：随机打乱列表
        private void ShuffleList<T>(List<T> list, Random random)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private class OscillatorGenerator
        {
            private readonly int _maxValue;
            private int _currentValue;
            private int _direction;

            public OscillatorGenerator(int maxValue, Random? random = null)
            {
                if (maxValue <= 0) throw new ArgumentException("maxValue 必须大于零");

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