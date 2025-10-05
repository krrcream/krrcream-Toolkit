using System;
using System.Collections.Generic;
using System.Linq;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Core;
using OsuParsers.Beatmaps;
using OsuParsers.Beatmaps.Objects.Mania;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLN : AbstractBeatmapTransformer<KRRLNTransformerOptions>
    {
        protected override int[,] ProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, KRRLNTransformerOptions options)
        {
            
            return BuildAndProcessMatrix(matrix, timeAxis, beatmap, options);
            
        }

        protected override void ApplyChangesToHitObjects(Beatmap beatmap, int[,] mergeMTX, KRRLNTransformerOptions options)
        {
           
            List<ManiaNote> ManiaObjects = beatmap.HitObjects.OfType<ManiaNote>().ToList();
            int CS = (int)beatmap.DifficultySection.CircleSize;
            int? rows = ManiaObjects.Last().RowIndex;
            int[,] matrix2 = new int[rows.Value + 1, CS];
            for (int i = 0; i < rows.Value + 1; i++)
            {
                for (int j = 0; j < CS; j++)
                {
                    matrix2[i, j] = -1;
                }
            }
            for (int i = 0; i < ManiaObjects.Count; i++)
            {
                var obj = ManiaObjects[i];
                int? rowindex = obj.RowIndex;
                int? colindex = obj.ColIndex;
                matrix2[rowindex.Value, colindex.Value] = i;
            }


            for(int i = 0; i < beatmap.HitObjects.Count; i++)
                for (int j = 0; j < beatmap.HitObjects.Count; j++)
                {
                    if (mergeMTX[i, j] > 0);
                    int index = matrix2[i, j];
                    ManiaHoldNote note = ManiaObjects[index] as ManiaHoldNote;
                    if (note != null)
                    {
                        note.HoldLength = (int)mergeMTX[i, j];
                    }
                }
                
        }
        
        protected override void ModifyMetadata(Beatmap beatmap, KRRLNTransformerOptions options)
        {
            // 避免重复添加 Version 前缀
            if (!beatmap.MetadataSection.Version.Contains("[KRR LN.]"))
                beatmap.MetadataSection.Version = $"[KRR LN.]{beatmap.MetadataSection.Version}";

            // 避免重复拼接 Creator
            if (!beatmap.MetadataSection.Creator.Contains("Krr LN."))
                beatmap.MetadataSection.Creator = "Krr LN. & " + beatmap.MetadataSection.Creator;

            // 避免重复添加 Tag
            var currentTags = beatmap.MetadataSection.Tags ?? [];
            var tagToAdd = "krrcream's transformer LN";
            if (!currentTags.Contains(tagToAdd))
            {
                var newTags = currentTags.Concat([tagToAdd]).ToArray();
                beatmap.MetadataSection.Tags = newTags;
            }
        }

        private int[,] BuildAndProcessMatrix(int[,] matrix, List<int> timeAxis, Beatmap beatmap, KRRLNTransformerOptions parameters)
        {
            
            // 创建带种子的随机数生成器
            var RG = parameters.Seed.HasValue
                ? new Random(parameters.Seed.Value)
                : new Random();

            List<ManiaNote> ManiaObjects = beatmap.HitObjects.OfType<ManiaNote>().ToList();
            int CS = (int)beatmap.DifficultySection.CircleSize;
            int? rows = ManiaObjects.Last().RowIndex;
            double MainBPM = beatmap.MainBPM;
            // 初始化都是-1的矩阵
            int[,] matrix1 = new int[rows.Value + 1, CS];
            for (int i = 0; i < rows.Value + 1; i++)
            {
                for (int j = 0; j < CS; j++)
                {
                    matrix1[i, j] = -1;
                }
            }
            // 初始化原始长度矩阵(用于不处理Org时的面条对齐，也要和原面条对齐)和可用时间矩阵，以及长短面等待修改的矩阵
            int[,] lnLength = DeepCopyMatrix(matrix1);
            int[,] availableTimeMtx = DeepCopyMatrix(matrix1);
            int[,] longLnWaitModify = DeepCopyMatrix(matrix1);
            int[,] shortLnWaitModify = DeepCopyMatrix(matrix1);
            
            // 完成坐标矩阵，长度矩阵，可用时间矩阵初始化
            for(int i = 0; i < ManiaObjects.Count; i++)
            {
                var obj = ManiaObjects[i];
                int? rowindex = obj.RowIndex;
                int? colindex = obj.ColIndex;
                matrix1[rowindex.Value, colindex.Value] = i;
                lnLength[rowindex.Value, colindex.Value] = obj.HoldLength;
                availableTimeMtx[rowindex.Value, colindex.Value] = timeAxis[i];
            }
            // 完成是否是原LN矩阵
            bool[,] orgIsLN = new bool[rows.Value + 1, CS];
            foreach (var obj in ManiaObjects)
            {
                if (obj.GetType() == typeof(ManiaHoldNote))
                {
                    int? rowindex = obj.RowIndex;
                    int? colindex = obj.ColIndex;
                    orgIsLN[rowindex.Value, colindex.Value] = true;
                }
            }
            
            //是否处理原始面条初步判定
            if (!parameters.General.ProcessOriginalIsChecked)
            {
                for (int i = 0; i < matrix1.GetLength(0); i++)
                {
                    for (int j = 0; j < matrix1.GetLength(1); j++)
                    {
                        if (orgIsLN[i, j])
                        {
                            matrix1[i, j] = -1;
                        }
                    }
                }
            }
            
            //通过百分比标记处理位置
            int borderKey = (int)parameters.LengthThreshold.Value;
            
            bool[,] shortLNFlag = new bool[rows.Value + 1, CS];
            bool[,] longLNFlag = new bool[rows.Value + 1, CS];
            for (int i = 0; i < matrix1.GetLength(0); i++)
            {
                for (int j = 0; j < matrix1.GetLength(1); j++)
                {
                    if (matrix1[i, j] >= 0)
                    {
                        if (availableTimeMtx[i, j] > borderlist[borderKey]*ManiaObjects[matrix1[i, j]].BeatLengthOfThisNote)
                        {
                            longLNFlag[i, j] = true;
                        }
                        else
                        {
                            shortLNFlag[i, j] = true;
                        }
                    }
                }
            }
            longLNFlag = MarkByPercentage(longLNFlag, parameters.Long.PercentageValue, RG);
            shortLNFlag = MarkByPercentage(shortLNFlag, parameters.Short.PercentageValue, RG);
            longLNFlag = LimitTruePerRow(longLNFlag, (int)parameters.Long.LimitValue, RG);
            shortLNFlag = LimitTruePerRow(shortLNFlag, (int)parameters.Short.LimitValue, RG);
            
            //正式生成longLN矩阵
            for (int i = 0; i < matrix1.GetLength(0); i++)
                for (int j = 0; j < matrix1.GetLength(1); j++)
                    if (longLNFlag[i, j])
                    {
                        int indexObj = matrix1[i, j];
                        int NewLength = GenerateTriangularRandom(
                            borderlist[borderKey] * ManiaObjects[indexObj].BeatLengthOfThisNote ,
                            availableTimeMtx[i, j] - 600000/MainBPM/8 ,
                            availableTimeMtx[i, j] * parameters.Long.PercentageValue / 100 ,
                            parameters.Long.RandomValue , RG
                        );
                        longLnWaitModify[i, j] = NewLength;
                    }
            //正式生成shortLN矩阵
            for (int i = 0; i < matrix1.GetLength(0); i++)
            for (int j = 0; j < matrix1.GetLength(1); j++)
                if (shortLNFlag[i, j])
                {
                    int indexObj = matrix1[i, j];
                    int NewLength = GenerateTriangularRandom(
                        600000/MainBPM/8 ,
                        borderlist[borderKey] * ManiaObjects[indexObj].BeatLengthOfThisNote  ,
                        availableTimeMtx[i, j] * parameters.Short.PercentageValue / 100 ,
                        parameters.Long.RandomValue , RG
                    );
                    shortLnWaitModify[i, j] = NewLength;
                }
            var result = MergeMatrices(longLnWaitModify, shortLnWaitModify);
            
            for(int i = 0; i < result.GetLength(0); i++)
                for (int j = 0; j < result.GetLength(1); j++)
                {
                    Console.Write(result[i, j]);    
                }
                Console.WriteLine();
                
            return result;
            }

        private bool[,] MarkByPercentage(bool[,] MTX, double P, Random random)
        {
            // 边界情况处理
            if (P >= 100)
            {
                // 返回原始矩阵
                return MTX;
            }

            if (P <= 0)
            {
                // 返回全false矩阵
                return new bool[MTX.GetLength(0), MTX.GetLength(1)];
            }

            // 收集所有true的位置
            List<(int row, int col)> truePositions = new List<(int, int)>();
            for (int i = 0; i < MTX.GetLength(0); i++)
            {
                for (int j = 0; j < MTX.GetLength(1); j++)
                {
                    if (MTX[i, j])
                    {
                        truePositions.Add((i, j));
                    }
                }
            }

            // 如果没有true位置，直接返回全false矩阵
            if (truePositions.Count == 0)
            {
                return new bool[MTX.GetLength(0), MTX.GetLength(1)];
            }

            double ratio = 1.0 - P / 100.0;
            // 计算需要设置为false的数量
            int countToSetFalse = (int)Math.Round(truePositions.Count * ratio);
            
            // 按行分组，实现分层抽样
            var groupedByRow = truePositions.GroupBy(pos => pos.row)
                                           .ToDictionary(g => g.Key, g => g.ToList());
            
            HashSet<(int, int)> positionsToSetFalse = new HashSet<(int, int)>();
            
            foreach (var group in groupedByRow)
            {
                int row = group.Key;
                var positionsInRow = group.Value;
                int countInRow = (int)Math.Round(positionsInRow.Count * ratio);
                countInRow = Math.Min(countInRow, positionsInRow.Count);
                var selectedInRow = positionsInRow.OrderBy(x => random.Next())
                                                 .Take(countInRow);
                foreach (var pos in selectedInRow)
                {
                    positionsToSetFalse.Add(pos);
                }
            }
            // 如果还有剩余配额未满足，补充随机选择
            if (positionsToSetFalse.Count < countToSetFalse)
            {
                var remaining = truePositions.Where(pos => !positionsToSetFalse.Contains(pos))
                                            .OrderBy(pos => random.Next())
                                            .Take(countToSetFalse - positionsToSetFalse.Count);
                
                foreach (var pos in remaining)
                {
                    positionsToSetFalse.Add(pos);
                }
            }
            // 构建结果矩阵
            bool[,] result = new bool[MTX.GetLength(0), MTX.GetLength(1)];
            for (int i = 0; i < MTX.GetLength(0); i++)
            {
                for (int j = 0; j < MTX.GetLength(1); j++)
                {
                    if (MTX[i, j] && !positionsToSetFalse.Contains((i, j)))
                    {
                        result[i, j] = true;
                    }
                }
            }

            return result;
        }

        private bool[,] LimitTruePerRow(bool[,] MTX, int limit, Random random)
        {
            int rows = MTX.GetLength(0);
            int cols = MTX.GetLength(1);

            bool[,] result = new bool[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                List<int> truePositions = new List<int>();
                for (int j = 0; j < cols; j++)
                {
                    if (MTX[i, j])
                    {
                        truePositions.Add(j);
                    }
                }

                if (truePositions.Count > limit)
                {
                    var shuffledPositions = truePositions.OrderBy(x => random.Next()).ToList();
                    for (int k = 0; k < limit; k++)
                    {
                        result[i, shuffledPositions[k]] = true;
                    }
                }
                else
                {
                    for (int j = 0; j < cols; j++)
                    {
                        result[i, j] = MTX[i, j];
                    }
                }
            }

            return result;
        }

        private int GenerateTriangularRandom(double D, double U, double M, int P, Random r)
        {
            if (P <= 0 || D >= U || M > U)
                return M>U ? (int)U:(int)M;
            if (M < D)
                return (int)D;
            if (P >= 100)
                P = 100;
            // 计算实际百分比
            double p = P / 100.0;
    
            // 计算新的下界限和上界限
            double d = M - (M - D) * p;  // (D,M)距离M的p位置
            double u = M + (U - M) * p;  // (M,U)距离M的p位置
    
            // 确保新范围在原范围内
            d = Math.Max(d, D);
            u = Math.Min(u, U);
    
            // 如果范围无效，则返回中心值
            if (d >= u)
                return (int)M;
    
            // 使用三角分布生成随机数
            // 三角分布公式：F(x) = (x-a)²/((b-a)(c-a))  when a≤x≤c
            //              F(x) = 1-(b-x)²/((b-a)(b-c))  when c≤x≤b
            double a = d;  // 下限
            double b = u;  // 上限
            double c = M;  // 众数(期望最大值)
    
            // 确保众数在范围内
            c = Math.Max(a, Math.Min(b, c));
    
            Random random = r;
            double u_random = random.NextDouble();  // 生成[0,1)的随机数
    
            double result;
            if (u_random <= (c - a) / (b - a))
            {
                result = a + Math.Sqrt(u_random * (b - a) * (c - a));
            }
            else
            {
                result = b - Math.Sqrt((1 - u_random) * (b - a) * (b - c));
            }
    
            return (int)result;
        }
        
        private int[,] MergeMatrices(int[,] matrix1, int[,] matrix2)
        {
            int rows = matrix1.GetLength(0);
            int cols = matrix1.GetLength(1);

            int[,] result = new int[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    int val1 = matrix1[i, j];
                    int val2 = matrix2[i, j];

                    if (val1 >= -1 && val2 >= -1)
                    {
                        result[i, j] = Math.Max(val1, val2);
                    }
                    else if (val1 >= -1)
                    {
                        result[i, j] = val1;
                    }
                    else if (val2 >= -1)
                    {
                        result[i, j] = val2;
                    }
                    else
                    {
                        result[i, j] = -1; // 或者保持不变，根据需求决定
                    }
                }
            }

            return result;
        }
        
        private Dictionary<int, double> borderlist = new Dictionary<int, double>
        {
            { 0, 0 },
            { 1, 1.0/8 },
            { 2, 1.0/6 },
            { 3, 1.0/4 },
            { 4, 1.0/3 },
            { 5, 1.0/2 },
            { 6, 1.0/1 },
            { 7, 3.0/2 },
            { 8, 2.0/1 },
            { 9, 4.0/1 },
            { 10, 999 }
        };
  

        public new Beatmap ProcessBeatmapToData(Beatmap beatmap, KRRLNTransformerOptions parameters)
        {
            return base.ProcessBeatmapToData(beatmap, parameters);
        }

        //修改难度名，tag，和标签等
        private void changeMeta(Beatmap beatmap)
        {
            beatmap.MetadataSection.Version = $"[KRR LN.]{beatmap.MetadataSection.Version}";
            beatmap.MetadataSection.Creator = "Krr LN. & " + beatmap.MetadataSection.Creator;
            var currentTags = beatmap.MetadataSection.Tags ?? [];
            var tagToAdd = BaseOptionsManager.KRRLNDefaultTag;
            if (!currentTags.Contains(tagToAdd))
            {
                var newTags = currentTags.Concat([tagToAdd]).ToArray();
                beatmap.MetadataSection.Tags = newTags;
            }
        }
        private int[,] DeepCopyMatrix(int[,] source)
        {
            int rows = source.GetLength(0);
            int cols = source.GetLength(1);
            int[,] destination = new int[rows, cols];
    
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    destination[i, j] = source[i, j];
                }
            }
            return destination;
        }
    }
}
