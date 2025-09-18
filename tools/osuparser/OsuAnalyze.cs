using System;
using System.Collections.Generic;
using System.IO;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using System.Linq;
using krrTools.Tools.OsuParser; 

namespace krrTools.Tools.OsuParser
{
    public class OsuAnalysisResult
    {
        public string Diff { get; set; }
        public double XXYSR { get; set; }
        public double KRRLV { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Creator { get; set; }
        public double Keys { get; set; }
        public double OD { get; set; }
        public double HP { get; set; }
        public double LNPercent { get; set; }
        public double BeatmapID { get; set; }
        public double BeatmapSetID { get; set; }
        public string BPM { get; set; }
    }
    
    
    public class OsuAnalyzer
    {
        public SRCalculator calculator = new SRCalculator();
        
        public OsuAnalysisResult Analyze(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件未找到", filePath);

            Beatmap beatmap = BeatmapDecoder.Decode(filePath); 
            if (beatmap.GeneralSection.ModeId!=3)
                throw new ArgumentException("不是mania模式");
            
            var Keys1 = (int)beatmap.DifficultySection.CircleSize;
            var OD1 = beatmap.DifficultySection.OverallDifficulty;
            var notes = calculator.getNotes(beatmap);
            double xxySR = calculator.Calculate(notes, Keys1, OD1);      
            double KRRLV = -1;
            string BPM = "0";
            if (Keys1 <= 10)
            {
                var (a, b, c) = Keys1 == 10 
                    ? (-0.0773, 3.8651, -3.4979)
                    : (-0.0644, 3.6139, -3.0677);
                
                double LV = a * xxySR * xxySR + b * xxySR + c;
                KRRLV = LV > 0 ? LV : -1;
            }
            var result = new OsuAnalysisResult
            {
                Diff = beatmap.MetadataSection.Version,
                XXYSR = xxySR,
                KRRLV = KRRLV,
                Title = beatmap.MetadataSection.Title,
                Artist = beatmap.MetadataSection.Artist,
                Creator = beatmap.MetadataSection.Creator,
                Keys = Keys1,
                BPM = GetBPM(beatmap),
                OD = OD1,
                HP = beatmap.DifficultySection.HPDrainRate,
                LNPercent = LnPercent(beatmap),
                BeatmapID = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID
            };
    
            return result;
        }
     
        public double LnPercent(Beatmap beatmap)
        {
            double Z = beatmap.HitObjects.Count;
            double LN = beatmap.HitObjects.Where(hitObject => hitObject.EndTime > hitObject.StartTime).Count();  
            return LN / Z * 100;
        }
        public string GetBPM(Beatmap beatmap)
        {
            double BPM = 0;
            List<double> BPMList = new List<double>();
            List<double> Times = new List<double>();
            
            foreach (var BL in beatmap.TimingPoints)
            {
                if (BL.BeatLength > 0)
                {
                    BPMList.Add(Math.Round(60000 / BL.BeatLength, 3));
                    Times.Add(BL.Offset);
                }
            }
            //根据Times去重，如果Times有相同的数字，则删除掉，同时删除BPMList中对应的元素
            var distinctPairs = Times.Zip(BPMList, (t, b) => new { Time = t, BPM = b })
                .DistinctBy(x => x.Time)
                .ToList();
            Times = distinctPairs.Select(x => x.Time).ToList();
            BPMList = distinctPairs.Select(x => x.BPM).ToList();
            
            
            //如果BPMList长度为0，则返回"0"
            if (BPMList.Count == 0)
                return "120";
            //如果BPMList长度为1，则返回BPMList[0]
            if (BPMList.Count == 1)
                return BPMList[0].ToString();
            //如果BPMList长度大于1
            //对Times进行处理，第i个的值是第i+1个的值减去第i个的值，最后一个是beatmap.HitObjects.Last().StartTime-Times.Last()
            for (int i = 0; i < Times.Count - 1; i++)
            {
                Times[i] = Times[i + 1] - Times[i];
            }
            Times.Add(beatmap.HitObjects.Last().StartTime - Times.Last());
            //选出Times最大值的索引，获取对应索引的BPMlist中的值作为BPM
            BPM = BPMList[Times.IndexOf(Times.Max())];
            return BPM + "(" + BPMList.Min().ToString() + " - " + BPMList.Max().ToString() + ")";
        }
        
    }
}