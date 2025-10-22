using System;
using System.Collections.Generic;
using System.Linq;
using krrTools.Beatmaps;
using OsuParsers.Extensions;
using OsuParsers.Beatmaps;
using OsuParsers.Enums;
using OsuParsers.Beatmaps.Objects;
using krrTools.Localization;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP 转换算法实现
    /// </summary>
    public class DP
    {
        // 常量定义
        private const int RANDOM_SEED = 114514;

        // private int _newKeyCount;
        /// <summary>
        /// 修改metadeta,放在每个转谱器开头
        /// </summary>
        private void MetadetaChange(Beatmap beatmap, DPToolOptions options)
        {
            
            var originalCS = beatmap.OrgKeys;
            string DPVersionName = $"[{originalCS}to{(int)beatmap.DifficultySection.CircleSize}DP]";
            
            // 修改作者 保持叠加转谱后的标签按顺序唯一
            beatmap.MetadataSection.Creator = CreatorManager.AddTagToCreator(beatmap.MetadataSection.Creator, Strings.DPTag);

            
            // 替换Version （允许叠加转谱）
            beatmap.MetadataSection.Version = DPVersionName + " " + beatmap.MetadataSection.Version;
            
            // 替换标签，保证唯一
            var existingTags = new HashSet<string>(beatmap.MetadataSection.Tags ?? Enumerable.Empty<string>());
            var requiredTags = new[] { Strings.ConverterTag, Strings.DPTag , "Krr"};

            var newTags = requiredTags
                .Where(tag => !existingTags.Contains(tag))
                .Concat(beatmap.MetadataSection.Tags ?? Enumerable.Empty<string>())
                .ToArray();
            
            beatmap.MetadataSection.Tags = newTags;
            // 修改ID 但是维持beatmapsetID
            beatmap.MetadataSection.BeatmapID = 0;
        }
    
        /// <summary>
        /// 执行谱面转换
        /// </summary>
        public void TransformBeatmap(Beatmap beatmap, DPToolOptions options)
        {
            var originalCircleSize = beatmap.DifficultySection.CircleSize;
            var (matrix, timeAxis) = beatmap.getMTXandTimeAxis();
            var processedMatrix = ProcessMatrix(matrix, timeAxis, beatmap, options);
            ApplyChangesToHitObjects(beatmap, processedMatrix, options, originalCircleSize);
            MetadetaChange(beatmap, options);
        }

        /// <summary>
        /// 处理音符矩阵
        /// </summary>
        private Matrix ProcessMatrix(Matrix matrix, List<int> timeAxis, Beatmap beatmap, DPToolOptions options)
        {
            bool LmirroFlag = options.LMirror.Value;
            bool RmirroFlag = options.RMirror.Value;
            
            // 如果 SingleSideKeyCount 为 null，不进行转换
            if (!options.SingleSideKeyCount.Value.HasValue)
            {
                Matrix LMTXorg = matrix.Clone();
                Matrix RMTXorg = matrix.Clone();
                if (LmirroFlag)
                    MirrorMtx(LMTXorg);
                if (RmirroFlag)
                    MirrorMtx(RMTXorg);
                return ConcatenateHorizontal(LMTXorg, RMTXorg);
            }
            var Conv = new N2NC.N2NC();
            var RG = new Random(RANDOM_SEED);
            var CS = (int)beatmap.DifficultySection.CircleSize;
            
            
            // 初始化原始矩阵和节拍长度轴
            var notes = beatmap.HitObjects.AsManiaNotes();
            var beatlengthAxis = Conv.GenerateBeatLengthAxis(matrix, timeAxis, notes);
            var orgColIndexMTX = Conv.GenerateOrgCloIndexMTX(matrix, notes);
            var targetKeys = (int)options.SingleSideKeyCount.Value;
            var convertTime = Math.Max(1, 60000 / beatmap.MainBPM * 4 - 10);
            var (LMaxKeys, LMinKeys, RMaxKeys, RMinKeys) = ((int)options.LMaxKeys.Value
                    , (int)options.LMinKeys.Value
                    , (int)options.RMaxKeys.Value
                    , (int)options.RMinKeys.Value
                );

            Matrix LMTX = Conv.DoKeys(matrix, timeAxis,  beatlengthAxis, orgColIndexMTX, CS, targetKeys, LMaxKeys, LMinKeys, convertTime,RG);    
            Matrix RMTX = Conv.DoKeys(matrix, timeAxis,  beatlengthAxis, orgColIndexMTX, CS, targetKeys, RMaxKeys, RMinKeys, convertTime,RG); 
            return ConcatenateHorizontal(LMTX, RMTX);
        }

        /// <summary>
        /// 将处理后的矩阵应用到谱面对象
        /// </summary>
        private void ApplyChangesToHitObjects(Beatmap beatmap, Matrix processedMatrix, DPToolOptions options, double originalCircleSize)
        {
            // 创建临时列表存储对象
            var notes = beatmap.HitObjects.AsManiaNotes();
            var newObjects = new List<HitObject>().AsManiaNotes();
            int targetKeys = processedMatrix.Cols;
            //遍历newMatrix添加对象
            var MTXspan = processedMatrix.AsSpan();
            for (int i = 0; i < MTXspan.Length; i++)
            { 
                int oldIndex = MTXspan[i]; 
                if (oldIndex >= 0)
                {
                    newObjects.Add(notes[oldIndex].CloneNote(NewColumn: i % targetKeys));
                }
            }
            beatmap.HitObjects.Clear();
            beatmap.HitObjects.AddRange(newObjects);
            beatmap.SortHitObjects();
            // 统一修改metadeta的形参，在这里修改CS
            if (options.SingleSideKeyCount.Value.HasValue)
            {
                beatmap.DifficultySection.CircleSize = (int)options.SingleSideKeyCount.Value.Value * 2;
            }
            else
            {
                beatmap.DifficultySection.CircleSize = (float)(originalCircleSize * 2);
            }
        }
        
        //矩阵镜像
        public void MirrorMtx(Matrix matrix)
        {
            int colCount = matrix.Cols;
            int midPoint = colCount / 2;
    
            // 从第一列开始，与对应的镜像列交换，直到中点
            for (int i = 0; i < midPoint; i++)
            {
                int mirrorIndex = colCount - 1 - i;
                matrix.SwapColumns(i, mirrorIndex);
            }
        }
        //矩阵拼接
        public static Matrix ConcatenateHorizontal(Matrix matrix1, Matrix matrix2)
        {
            // 检查行数是否相同
            if (matrix1.Rows != matrix2.Rows)
                throw new ArgumentException("矩阵行数必须相同才能进行横向拼接");
    
            // 创建结果矩阵，行数与原矩阵相同，列数为两矩阵列数之和
            var result = new Matrix(matrix1.Rows, matrix1.Cols + matrix2.Cols);
    
            // 获取所有矩阵的Span以提高性能
            var matrix1Span = matrix1.AsSpan();
            var matrix2Span = matrix2.AsSpan();
            var resultSpan = result.AsSpan();
    
            // 逐行复制数据
            for (int i = 0; i < matrix1.Rows; i++)
            {
                int matrix1RowOffset = i * matrix1.Cols;
                int matrix2RowOffset = i * matrix2.Cols;
                int resultRowOffset = i * result.Cols;
        
                // 复制第一个矩阵的行数据到结果矩阵的左侧
                for (int j = 0; j < matrix1.Cols; j++)
                {
                    resultSpan[resultRowOffset + j] = matrix1Span[matrix1RowOffset + j];
                }
        
                // 复制第二个矩阵的行数据到结果矩阵的右侧
                for (int j = 0; j < matrix2.Cols; j++)
                {
                    resultSpan[resultRowOffset + matrix1.Cols + j] = matrix2Span[matrix2RowOffset + j];
                }
            }
    
            return result;
        }
    }
}