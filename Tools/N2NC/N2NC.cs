using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using krrTools.Beatmaps;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Enums;
using OsuParsers.Beatmaps.Objects;
using OsuParsers.Extensions;
using krrTools.Localization;
using OsuParsers.Beatmaps.Objects.Mania;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC 转换算法实现
    /// </summary>
    public class N2NC
    {
        /// <summary>
        /// 修改metadeta,放在每个转谱器开头
        /// </summary>
        private readonly double[] TransformSpeedValues = {0.125, 0.25, 0.5, 0.75, 1, 2, 3, 4, 999};
        
        private void MetadetaChange(Beatmap beatmap, N2NCOptions options)
        {

            var originalCS = beatmap.DifficultySection.CircleSize;
            //修改CS
            beatmap.DifficultySection.CircleSize = (float)options.TargetKeys.Value;

            string NtoNCVersionName = $"[{originalCS}to{options.TargetKeys.Value}C]";

            // 修改作者 保持叠加转谱后的标签按顺序唯一
            beatmap.MetadataSection.Creator =
                CreatorManager.AddTagToCreator(beatmap.MetadataSection.Creator, Strings.NToNCTag);

            // 替换Version （允许叠加转谱）
            beatmap.MetadataSection.Version = NtoNCVersionName + " " + beatmap.MetadataSection.Version;

            // 替换标签，保证唯一
            var existingTags = new HashSet<string>(beatmap.MetadataSection.Tags ?? Enumerable.Empty<string>());
            var requiredTags = new[] { Strings.ConverterTag, Strings.NToNCTag, "Krr" };

            var newTags = requiredTags
                .Where(tag => !existingTags.Contains(tag))
                .Concat(beatmap.MetadataSection.Tags ?? Enumerable.Empty<string>())
                .ToArray();

            beatmap.MetadataSection.Tags = newTags;
            // 修改ID 但是维持beatmapsetID
            beatmap.MetadataSection.BeatmapID = 0;
        }
        /// <summary>
        /// 将处理后的矩阵应用到谱面对象
        /// </summary>
        private void ApplyChangesToHitObjects(Beatmap beatmap, Matrix processedMatrix, N2NCOptions options)
        {

           
            // 创建临时列表存储对象
            var notes = beatmap.HitObjects.AsManiaNotes();
            var newObjects = new List<HitObject>().AsManiaNotes();
            int targetKeys = processedMatrix.Cols;
            //遍历newMatrix添加对象
            var MTXspan = processedMatrix.AsSpan();
            var PX = new newPositionX(targetKeys);
            
            for (int i = 0; i < MTXspan.Length; i++)
            { 
                int oldIndex = MTXspan[i];
                int col = i % targetKeys;
           
                
                if (oldIndex >= 0)
                {
                    var newNote = notes[oldIndex].CloneNote();
                    newNote.Position = PX.Vector2(col);
                    newObjects.Add(newNote);
                }
            }
            
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(newObjects);
            beatmap.SortHitObjects();
        }

        /// <summary>
        /// 执行谱面转换
        /// </summary>
        public void TransformBeatmap(Beatmap beatmap, N2NCOptions options)
        {
            Console.WriteLine($"[N2NC options.speed] {options.TransformSpeed.Value} 对应值：{TransformSpeedValues[(int)options.TransformSpeed.Value]}");
            //在最开头判断，减少不必要的进程
            var keyFlags = options.SelectedKeyFlags;
            if (keyFlags.HasValue && keyFlags.Value != KeySelectionFlags.None)
            {
                var AlignmentPreProcessCS = Math.Clamp((int)beatmap.DifficultySection.CircleSize - 3, 0, 8);
                bool isSelected = ((int)keyFlags.Value & (1 << AlignmentPreProcessCS)) != 0;
                if (!isSelected)
                {
                    return;
                }
            }
            var random = options.Seed.HasValue ? new Random(options.Seed.Value) : new Random();
            var (matrix, timeAxisTemp) = beatmap.getMTXandTimeAxis();
            var timeAxis = CollectionsMarshal.AsSpan(timeAxisTemp);
            
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options, random);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options);
            MetadetaChange(beatmap, options);
        }

        /// <summary>
        /// 处理音符矩阵
        /// </summary>
        private Matrix ProcessMatrix(Matrix matrix, Span<int> timeAxis, Beatmap beatmap, N2NCOptions options,
            Random random)
        {
            var CS = (int)beatmap.DifficultySection.CircleSize;
            var targetKeys = (int)options.TargetKeys.Value;
            var maxKeys = (int)options.MaxKeys.Value;
            var minKeys = (int)options.MinKeys.Value;
            var convertTime = Math.Max(1, TransformSpeedValues[(int)options.TransformSpeed.Value] * 60000 / beatmap.MainBPM * 4 - 10);
            // 使用传入的随机数生成器
            var RG = random;
            
            // 初始化所需轴
            var notes = beatmap.HitObjects.AsManiaNotes();
            // 时间轴
            Span<double> beatLengthAxis = GenerateBeatLengthAxis(timeAxis, notes);
            // 索引轴
            Span<int> endTimeIndexAxis = GenerateEndTimeIndex(notes); 
            var orgColIndex= GenerateOrgColIndex(matrix);
            
            // DoAddKeys
            return DoKeys(matrix, endTimeIndexAxis, timeAxis,  beatLengthAxis, orgColIndex, CS, targetKeys, maxKeys, minKeys, convertTime,RG);

        } 
        
        /// <summary>
        /// 封装成其他文件中也能使用的方法
        /// </summary>
        public Matrix DoKeys(Matrix matrix,Span<int> endTimeIndexAxis ,Span<int> timeAxis, Span<double> beatlengthAxis, Span<int> orgColIndex, int CS, int targetKeys, int maxKeys, int minKeys, double convertTime, Random random)
        {
            var turn = targetKeys - CS;
    
            if (turn >= 0)
            {
                // AddKeys 逻辑
                bool maxKeysequal = maxKeys == CS; //最大键数等于原CS 执行 autoMap优化方法
                var (oldMTX, insertMTX) = convertMTX(turn, timeAxis, convertTime, CS, random, maxKeysequal);
                Matrix newMatrix = convert(matrix,endTimeIndexAxis, oldMTX, insertMTX, orgColIndex, timeAxis, targetKeys, beatlengthAxis , maxKeysequal);
                DensityReducer(newMatrix, maxKeys, minKeys, targetKeys, random);
                return newMatrix;
            }
            else
            {
                // RemoveKeys 逻辑
                var newMatrix = SmartReduceColumns(matrix, timeAxis, -turn, convertTime, beatlengthAxis, random);
                DensityReducer(newMatrix, maxKeys, minKeys, targetKeys, random);
                return newMatrix;
            }
        }
        
        public Span<double> GenerateBeatLengthAxis(Span<int> timeAxis, List<ManiaNote> maniaObjects)
        {
            List<double> result1 = Enumerable.Repeat(-1.0, timeAxis.Length).ToList();
            // 按时间排序处理note
            var sortedNotes = maniaObjects
                .Select((note, index) => new { Note = note, Index = index })
                .OrderBy(x => x.Note.StartTime)
                .ToList();
    
            int currentTimeAxisIndex = 0;
            foreach (var item in sortedNotes)
            {
                // 找到该note在timeAxis中的位置
                while (currentTimeAxisIndex < timeAxis.Length && 
                       timeAxis[currentTimeAxisIndex] < item.Note.StartTime)
                {
                    currentTimeAxisIndex++;
                }
        
                if (currentTimeAxisIndex < timeAxis.Length && 
                    timeAxis[currentTimeAxisIndex] == item.Note.StartTime)
                {
                    result1[currentTimeAxisIndex] = item.Note.BeatLengthOfThisNote;
                }
            }
            return CollectionsMarshal.AsSpan(result1);
        }
        // 获取原始列号索引，公用方法
        public Span<int> GenerateEndTimeIndex(List<ManiaNote> maniaObjects)
        {
            List<int> result = new List<int>();
            foreach (var item in maniaObjects)
            {
                result.Add(item.EndTime);
            }
            return CollectionsMarshal.AsSpan(result);
        }
        
        public Span<int> GenerateOrgColIndex(Matrix matrix)
        {
            var OrgColIndex = new List<int>();
            var cols = matrix.Cols;
            var matrixSpan = matrix.AsSpan();
            for (int i = 0; i < matrixSpan.Length; i++)
            {
                if (matrixSpan[i] >= 0)
                {
                    OrgColIndex.Add(i % cols );
                }
            }
            return CollectionsMarshal.AsSpan(OrgColIndex);
        }
        public (Matrix, Matrix) convertMTX(int turn, Span<int> timeAxis,
            double convertTime, int CS , Random random, bool ifMaxKeysequal = false)
        {
            var rows = timeAxis.Length;
            
            if (rows == 0)
                throw new ArgumentException("行或者列为0，无法创建convert矩阵.");

            // 初始化两个矩阵
            var oldMTX = new Matrix(rows, turn);
            var insertMTX = new Matrix(rows, turn);
            if (!ifMaxKeysequal) // 如果最大键数与目标键数不相等，则生成矩阵，否则维持-1矩阵
            {
                // 生成 oldMTX 矩阵
                for (var col = 0; col < turn; col++)
                {
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
            }
            for (var col = 0; col < turn; col++)
            {

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
        public Matrix convert(Matrix matrix,Span<int> EndTimeIndexAxis ,Matrix oldMTX, Matrix insertMTX, Span<int> orgColIndex, Span<int> timeAxis,
            int targetKeys, Span<double> beatLengthAxis, bool maxKeysEqualTargetKeys)
        {
            try
            {
                var rows = matrix.Rows;
                var originalCols = matrix.Cols;
                var turn = oldMTX.Cols; // oldMTX的列数
                // 1.初步convert。MappingStep1
                var newMatrix = PerformInitialConvert(matrix, oldMTX, insertMTX, targetKeys, turn, rows, originalCols, maxKeysEqualTargetKeys);
                // 2.生成位置映射
                var Mark = GenerateDeleteMark(newMatrix,timeAxis ,EndTimeIndexAxis, beatLengthAxis, orgColIndex , targetKeys);
                // 3.根据位置映射删除note
                ApplyPositionBasedDeletion(newMatrix, Mark);
                return newMatrix;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[N2NC] convert方法发生异常: {0}", ex.Message);
                Logger.WriteLine(LogLevel.Error, "[N2NC] 异常堆栈: {0}", ex.StackTrace ?? "null");
                throw;
            }
        }

        /// <summary>
        /// 执行初步转换 - MappingStep1
        /// </summary>
        private Matrix PerformInitialConvert(Matrix matrix, Matrix oldMTX, Matrix insertMTX, int targetKeys, int turn
            , int rows, int originalCols, bool ifMaxKeysequal)
        {
            // 创建一个新的矩阵，列数为目标键数，行数与原矩阵相同，初始化为-1
            var newMatrix = new Matrix(rows, targetKeys);

            // 处理每一行
            for (var i = 0; i < rows; i++)
            {
                // 创建临时数组
                var tempRow = new int[targetKeys];

                // 初始化为-1
                for (var k = 0; k < targetKeys; k++) tempRow[k] = -1;

                var orgCurrentRow = matrix.GetRowSpan(i);
                // 先复制原始矩阵的这一行内容到临时数组的左侧
                for (var j = 0; j < originalCols && j < targetKeys; j++) tempRow[j] = orgCurrentRow[j];
                
                //插入复制的物件
                if (!ifMaxKeysequal)//如果最大键数与目标键数不相等，则生成矩阵，否则维持插入位置为-1
                {
                    for (var j = 0; j < turn; j++)
                    {
                        var oldIndex = oldMTX[i, j];
                        var insertIndex = insertMTX[i, j];
                        ShiftInsert(tempRow, insertIndex);
                        if (matrix[i, oldIndex] >= 0)
                            tempRow[insertIndex] = matrix[i, oldIndex];
                    }
                }else if (ifMaxKeysequal)
                {
                    for (var j = 0; j < turn; j++)
                    {
                        var insertIndex = insertMTX[i, j];
                        ShiftInsert(tempRow, insertIndex);
                    }
                }
                for (var j = 0; j < targetKeys; j++)
                {
                    newMatrix[i, j] = tempRow[j];
                }
            }
            
            return newMatrix;
        }

        /// <summary>
        /// 生成位置映射
        /// </summary>
        private BoolMatrix GenerateDeleteMark(Matrix newMatrix,Span<int> timeAxis , Span<int> EndTimeIndexAxis 
            , Span<double> beatLengthAxis,  Span<int> orgColIndexAxis, int targetKeys)
        {
            var mark = new BoolMatrix(newMatrix.Rows, newMatrix.Cols);
            var markSpan = mark.AsSpan();
            var newMatrixSpan = newMatrix.AsSpan();
            var endTimeTempRow= new Span<int>(new int[targetKeys]);
            var convertTimePointRow = new Span<int>(new int[targetKeys]);
            var orgColIndexRow = new Span<int>(new int[targetKeys]);
            convertTimePointRow.Fill(timeAxis[0]);
            orgColIndexRow.Fill(-1);
            //临时index
            int oldIndex = -1;
            int preOldIndex = -1;
            int preRowI = -1;
            int row = -1;
            int col = -1;
            for (int i = targetKeys; i < newMatrixSpan.Length; i++)
            {
                oldIndex = newMatrixSpan[i];
                preRowI = i - targetKeys;
                preOldIndex = newMatrixSpan[preRowI];
                row = i / targetKeys;
                col = i % targetKeys;
                //1/4节拍时间，计算的时候+10作为子弹处理时间，-10作为面尾处理时间
                double space = beatLengthAxis[row - 1] / 4 ;
                //处理面尾（注意是索引轴）
                if (preOldIndex >= 0)
                {
                    endTimeTempRow[col] = Math.Max(EndTimeIndexAxis[preOldIndex], endTimeTempRow[col]);
                }
                if(timeAxis[row] < endTimeTempRow[col] +  space - 10)
                {
                    markSpan[i] = true;                        
                }
                //处理子弹（注意是时间轴）
                if (oldIndex >= 0 && orgColIndexAxis[oldIndex] != orgColIndexRow[col])
                {
                    orgColIndexRow[col] = orgColIndexAxis[oldIndex];
                    convertTimePointRow[col] = timeAxis[row - 1];
                }
                if (timeAxis[row] < convertTimePointRow[col] + space + 10)
                {
                    markSpan[i] = true;
                }
            }
            return mark;
        }

        /// <summary>
        /// 根据位置映射删除note
        /// </summary>
        private void ApplyPositionBasedDeletion(Matrix newMatrix, BoolMatrix Mark)
        {
            var newMatrixSpan = newMatrix.AsSpan();
            var MarkSpan = Mark.AsSpan();
            for (int i = 0; i < newMatrixSpan.Length; i++)
            {
                if (MarkSpan[i])
                {
                    newMatrixSpan[i] = -1;
                }
            }
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

    

        private void DensityReducer(Matrix matrix, int maxKeys, int minKeys, int targetKeys, Random random)
        {
            int maxToRemovePerRow = targetKeys - maxKeys;
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



        public Matrix SmartReduceColumns(Matrix orgMTX, Span<int> timeAxis, int turn, double convertTime,
            Span<double> beatLengthAxis, Random random)
        {
            var rows = orgMTX.Rows;
            var originalCols = orgMTX.Cols;
            var targetCols = originalCols - turn;

            // 创建新矩阵，初始化为-1（空）
            var newMatrix = new Matrix(rows, targetCols);
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
                    ProcessRegion(orgMTX, newMatrix, timeAxis, regionStart, regionEnd, targetCols, beatLengthAxis, random);

                    // 更新下一个区域的起始点
                    regionStart = regionEnd;
                }
            }

            // 处理可能剩余的行（如果最后一段不足一个完整区域）
            if (regionStart < rows - 1)
                ProcessRegion(orgMTX, newMatrix, timeAxis, regionStart, rows - 1, targetCols, beatLengthAxis, random);

            // 处理空行
            ProcessEmptyRows(orgMTX, newMatrix, timeAxis, beatLengthAxis, random);

            return newMatrix;
        }

        private void ProcessRegion(Matrix orgMTX, Matrix newMatrix, Span<int> timeAxis,
            int regionStart, int regionEnd, int targetCols, Span<double> beatLengthAxis, Random random)
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
            {
                for (var col = 0; col < originalCols; col++)
                {
                    var newValue = orgMTX[row, col];
                    if (newValue >= 0) // 有效物件
                    {
                        var newCol = columnMapping[col];
                        if (newCol >= 0) // 该列未被移除
                            // 检查目标位置是否可用（避免冲突）
                    
                            if (IsPositionAvailable(newMatrix, row, newCol, timeAxis, beatLengthAxis[row]))
                            {
                                newMatrix[row, newCol] = newValue;

                                // 如果是长条头部，复制整个长条
                                CopyLongNoteBody(orgMTX, newMatrix, row, col, newCol, rows);
                            }
                    }
                }
            }    
            // 处理长条延续部分
            for (var row = regionStart; row <= regionEnd; row++) HandleLongNoteExtensions(newMatrix, row, targetCols);

            // 应用约束条件：确保每行至少有一个note
            // 应用约束条件：确保每行至少有一个note
            ApplyMinimumNotesConstraint(newMatrix, orgMTX, regionStart, regionEnd, targetCols, timeAxis, beatLengthAxis,
                random);
        }

        private void ApplyMinimumNotesConstraint(Matrix matrix, Matrix orgMTX, int startRow, int endRow,
            int targetCols,
            Span<int> timeAxis, Span<double> beatLengthAxis, Random random)
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
                            if (IsPositionAvailableForEmptyRow(matrix, timeAxis, row, col, beatLengthAxis[row]))
                                // 特别检查长条尾部时间距离要求
                                if (!IsHoldNoteTailTooClose(matrix, orgMTX, timeAxis, row, selectedOrgCol, col,
                                        beatLengthAxis[row]))
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
            Matrix orgMTX, int regionStart, int regionEnd)
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

        private double CalculateColumnRisk(Matrix matrix, int colIndex, int totalCols, int regionStart,
            int regionEnd)
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

        private bool IsPositionAvailable(Matrix matrix, int row, int col, Span<int> timeAxis, double beatLength)
        {
            if (matrix[row, col] != -1)
                return false;

            // 检查前面几行
            for (var r = Math.Max(0, row - 3); r < row; r++)
                if (timeAxis[row] - timeAxis[r] <= beatLength / 14 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            // 检查后面几行
            var rows = matrix.Rows;
            for (var r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
                if (timeAxis[r] - timeAxis[row] <= beatLength / 14 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            return true;
        }

        private void HandleLongNoteExtensions(Matrix newMatrix, int row, int targetCols)
        {
            // 处理延续到当前行的长条身体部分
            for (var col = 0; col < targetCols; col++)
                if (newMatrix[row, col] == -1) // 位置为空
                    // 检查是否应该填充长条身体
                    if (row > 0 && newMatrix[row - 1, col] == -7)
                        newMatrix[row, col] = -7;
        }

        private void CopyLongNoteBody(Matrix orgMTX, Matrix newMatrix, int startRow, int oldCol, int newCol,
            int totalRows)
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

        private void ProcessEmptyRows(Matrix orgMTX, Matrix newMatrix, Span<int> timeAxis, Span<double>beatLengthAxis,
            Random random)
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
                    if (TryInsertNoteDirectly(newMatrix, orgMTX, timeAxis, row, targetCols, originalCols, beatLengthAxis[row],
                            random)) continue; // 成功插入，跳过第二步

                    // 第二步：尝试通过删除其他列的note来腾出空间
                    TryClearSpaceAndInsert(orgMTX, newMatrix, timeAxis, row, targetCols, originalCols, beatLengthAxis[row],
                        random);
                }
            }
        }

        // 尝试直接插入note到当前行的可用位置
        private bool TryInsertNoteDirectly(Matrix newMatrix, Matrix orgMTX, Span<int> timeAxis, int row,
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
        private bool IsHoldNoteTailTooClose(Matrix newMatrix, Matrix orgMTX, Span<int> timeAxis,
            int row, int orgCol, int targetCol, double beatLength)
        {
            var minTimeDistance = beatLength / 14 - 10; // 注意这里是-10

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
            if (tailRow < timeAxis.Length && tailRow < newMatrix.Rows)
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
        private void TryClearSpaceAndInsert(Matrix orgMTX, Matrix newMatrix, Span<int> timeAxis,
            int emptyRow, int targetCols, int originalCols,
            double beatLength, Random random)
        {
            var timeThreshold = beatLength / 14 + 10;
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


        private bool IsPositionAvailableForEmptyRow(Matrix matrix, Span<int> timeAxis,
            int row, int col, double beatLength)
        {
            if (matrix[row, col] != -1)
                return false;

            // 检查前面几行
            var rows = matrix.Rows;
            for (var r = Math.Max(0, row - 3); r < row; r++)
                if (timeAxis[row] - timeAxis[r] <= beatLength / 14 + 10)
                    if (matrix[r, col] >= 0 || matrix[r, col] == -7)
                        return false;

            // 检查后面几行
            for (var r = row + 1; r <= Math.Min(rows - 1, row + 3); r++)
                if (timeAxis[r] - timeAxis[row] <= beatLength / 14 + 10)
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
            private bool _isSpecialCase;

            public OscillatorGenerator(int maxValue, Random? random = null)
            {
                if (maxValue < 0) throw new ArgumentException("maxValue 必须不小于零");

                _maxValue = maxValue;
                // 处理特殊情况
                if (maxValue == 0)
                {
                    _currentValue = 0;
                    _isSpecialCase = true;
                }
                else if (maxValue == 1)
                {
                    var rnd = random ?? new Random();
                    _currentValue = rnd.Next(0, 2); // 0 或 1
                    _direction = rnd.Next(0, 2) == 0 ? -1 : 1;
                    _isSpecialCase = true;
                }
                else
                {
                    // 正常情况
                    var rnd = random ?? new Random();
                    _currentValue = rnd.Next(1, maxValue);
                    _direction = rnd.Next(0, 2) == 0 ? -1 : 1;
                    _isSpecialCase = false;
                }
            }


            public int GetCurrent()
            {
                return _currentValue;
            }

            public void Next()
            {
                if (_isSpecialCase)
                {
                    if (_maxValue == 0)
                    {
                        // maxValue=0 时始终返回 0
                        _currentValue = 0;
                    }
                    else if (_maxValue == 1)
                    {
                        // maxValue=1 时在 0 和 1 之间切换
                        _currentValue = 1 - _currentValue;
                    }
                }
                else
                {
                    // 正常逻辑
                    _currentValue += _direction;

                    // 检查是否需要改变方向
                    if (_currentValue > _maxValue)
                    {
                        _currentValue = _maxValue - 1;
                        _direction = -1;
                    }
                    else if (_currentValue < 0)
                    {
                        _currentValue = 1;
                        _direction = 1;
                    }
                }
            }
        }
    }
}