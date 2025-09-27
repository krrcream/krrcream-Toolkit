using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using System.Linq;
using System.Windows;
using System.Globalization;
using OsuFileIO.Analyzer;
using OsuFileIO.OsuFile;
using OsuFileIO.OsuFileReader;
using ManiaHitObject = OsuFileIO.HitObject.Mania.ManiaHitObject;

namespace krrTools.Tools.OsuParser
{
    public class OsuAnalysisResult
    {
        public string? Diff { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Creator { get; init; }
        public string? BPM { get; init; }
        public double BPM_Main { get; init; }
        public double Keys { get; init; }
        public double OD { get; init; }
        public double HP { get; init; }

        // Custom properties unique to OsuAnalyzer
        public double XXY_SR { get; init; }
        public double KRR_LV { get; init; }
        public double LNPercent { get; init; }

        public double BeatmapID { get; init; }
        public double BeatmapSetID { get; init; }
    }

    public class OsuAnalyzer
    {
        private readonly SRCalculator calculator = new SRCalculator();

        public OsuAnalysisResult Analyze(string? filePath)
        {    
            var beatmap = BeatmapDecoder.Decode(filePath);
            if (beatmap.GeneralSection.ModeId != 3)
                throw new ArgumentException("不是mania模式");

            // compute custom stats via SRCalculator
            var Keys1 = (int)beatmap.DifficultySection.CircleSize;
            var OD1 = beatmap.DifficultySection.OverallDifficulty;
            var notes = calculator.getNotes(beatmap);
            double xxySR = calculator.Calculate(notes, Keys1, OD1);
            double krrLV = -1;
            if (Keys1 <= 10)
            {
                var (a, b, c) = Keys1 == 10
                    ? (-0.0773, 3.8651, -3.4979)
                    : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySR * xxySR + b * xxySR + c;
                krrLV = LV > 0 ? LV : -1;
            }

            // gather standard metadata with OsuParsers
            var bpmDisplay = GetBPMDisplay(filePath);
            var bpm = GetMainBpm(filePath);
            var result = new OsuAnalysisResult
            {
                Diff = beatmap.MetadataSection.Version,
                Title = beatmap.MetadataSection.Title,
                Artist = beatmap.MetadataSection.Artist,
                Creator = beatmap.MetadataSection.Creator,
                BPM = bpmDisplay,
                BPM_Main = bpm,
                Keys = Keys1,
                OD = OD1,
                HP = beatmap.DifficultySection.HPDrainRate,

                // Custom properties unique to OsuAnalyzer
                XXY_SR = xxySR,
                KRR_LV = krrLV,
                LNPercent = LnPercent(beatmap),

                BeatmapID = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID
            };

            return result;
        }

        private double LnPercent(Beatmap beatmap)
        {
            double Z = beatmap.HitObjects.Count;
            if (Z == 0) return 0;
            double LN = beatmap.HitObjects.Count(hitObject => hitObject.EndTime > hitObject.StartTime);
            return LN / Z * 100;
        }

        private string GetBPMDisplay(string? filePath)
        {
            if (filePath == null || !File.Exists(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var reader = new OsuFileReaderBuilder(filePath).Build();
            var beatmap = reader.ReadFile();
            string BPMFormat = "";
            
            if (beatmap is IReadOnlyBeatmap<ManiaHitObject> maniaHitObject)
            {
                var result = maniaHitObject.Analyze();
                var bpm = result.Bpm;
                var bpmMax = result.BpmMax;
                var bpmMin = result.BpmMin;
                BPMFormat = string.Format(CultureInfo.InvariantCulture, "{0}({1} - {2})", bpm, bpmMin, bpmMax);

            }
            return BPMFormat;
        }

        private double GetMainBpm(string? filePath)
        {
            if (filePath == null || !File.Exists(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var reader = new OsuFileReaderBuilder(filePath).Build();
            var beatmap = reader.ReadFile();
            double BPM = 0;
            
            if (beatmap is IReadOnlyBeatmap<ManiaHitObject> maniaHitObject)
            {
                var result = maniaHitObject.Analyze();
                BPM = result.Bpm;

            }
            return BPM;
        }

        public static string? AddNewBeatmapToSongFolder(string newBeatmapFile, bool openOsz = false)
        {
            // 获取.osu文件所在的目录作为歌曲文件夹
            string? songFolder = Path.GetDirectoryName(newBeatmapFile);
            if (string.IsNullOrEmpty(songFolder))
            {
                MessageBox.Show($"Invalid beatmap file path: {newBeatmapFile}", "Error");
                return null;
            }

            System.Diagnostics.Debug.WriteLine(songFolder);

            // 创建.osz文件
            string outputOsz = Path.GetFileName(songFolder) + ".osz";
            string? parentDir = Path.GetDirectoryName(songFolder);
            if (string.IsNullOrEmpty(parentDir))
            {
                MessageBox.Show($"Unable to determine parent directory for: {songFolder}", "Error");
                return null;
            }

            string fullOutputPath = Path.Combine(parentDir, outputOsz);

            if (File.Exists(fullOutputPath))
                File.Delete(fullOutputPath);

            try
            {
                // Ensure source directory exists before creating archive
                if (!Directory.Exists(songFolder))
                {
                    MessageBox.Show($"Source song folder does not exist: {songFolder}", "Error");
                    return null;
                }

                ZipFile.CreateFromDirectory(songFolder, fullOutputPath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to create {fullOutputPath} {Environment.NewLine}{Environment.NewLine}{e.Message}", "Error");
                return null;
            }

            // 2. 加入新的谱面文件到.osz
            try
            {
                using ZipArchive archive = ZipFile.Open(fullOutputPath, ZipArchiveMode.Update);
                archive.CreateEntryFromFile(newBeatmapFile, Path.GetFileName(newBeatmapFile));
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to add beatmap to archive: {Environment.NewLine}{Environment.NewLine}{e.Message}", "Error");
                return null;
            }

            // 3. 删除原本谱面
            try
            {
                if (File.Exists(newBeatmapFile))
                {
                    File.Delete(newBeatmapFile);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to delete the temporary beatmap file: {newBeatmapFile} {Environment.NewLine}{Environment.NewLine}{e.Message}", "Warning");
            }

            // 4. 打开 .osz（仅当调用方请求时）
            if (openOsz)
            {
                Process proc = new Process();
                proc.StartInfo.FileName = fullOutputPath;
                proc.StartInfo.UseShellExecute = true;
                try
                {
                    proc.Start();
                }
                catch
                {
                    MessageBox.Show("There was an error opening the generated .osz file. This is probably because .osz files have not been configured to open with osu!.exe on this system." + Environment.NewLine + Environment.NewLine +
                                    "To fix this, download any map from the website, right click the .osz file, click properties, beside Opens with... click Change..., and select osu!. " +
                                    "You'll know the problem is fixed when you can double click .osz files to open them with osu!", "Error");
                }
            }

            // return the created osz path as success indicator
            return fullOutputPath;
        }

        public Dictionary<double, double> GetBeatLengthList(Beatmap beatmap)
        {
            var tp = beatmap.TimingPoints
                .Where(p => p.BeatLength > 0)
                .OrderBy(p => p.Offset)
                .ToList();
            if (tp.Count == 0)
                return new Dictionary<double, double>();
    
            var beatLengthDict = new Dictionary<double, double>();
            foreach (var timingPoint in tp)
            {
                beatLengthDict[timingPoint.Offset] = timingPoint.BeatLength;
            }
    
            return beatLengthDict;
        }

        public List<double> GetbeatLengthAxis(Dictionary<double, double> beatLengthDict, double mainBPM,
            List<int> timeAxis)
        {
            double defaultLength = 60000 / mainBPM;
            List<double> bLAxis = new List<double>();
            for (int i = 0; i < timeAxis.Count; i++)
            {
                bLAxis.Add(defaultLength);
            }
    
            // 将字典的键转换为有序列表，便于比较
            var sortedKeys = beatLengthDict.Keys.OrderBy(k => k).ToList();
    
            for (int i = 0; i < timeAxis.Count; i++)
            {
                double currentTime = timeAxis[i];
        
                // 处理边界情况：时间点在第一个时间点之前
                if (currentTime < sortedKeys[0])
                {
                    bLAxis[i] = beatLengthDict[sortedKeys[0]];
                    continue;
                }
        
                // 处理边界情况：时间点在最后一个时间点之后
                if (currentTime >= sortedKeys[^1])
                {
                    bLAxis[i] = beatLengthDict[sortedKeys[^1]];
                    continue;
                }
        
                // 查找当前时间点对应的时间段
                for (int k = 0; k < sortedKeys.Count - 1; k++)
                {
                    if (currentTime >= sortedKeys[k] && currentTime < sortedKeys[k + 1])
                    {
                        bLAxis[i] = beatLengthDict[sortedKeys[k]];
                        break;
                    }
                }
            }
    
            return bLAxis;
        }
    }
}