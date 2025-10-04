using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Beatmaps
{
    public class OsuAnalysisResult
    {
        public string? Diff { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Creator { get; init; }
        public string? BPMDisplay { get; init; }
        // public double BPM_Main { get; init; }
        public double Keys { get; init; }
        public double OD { get; init; }
        public double HP { get; init; }

        // Custom properties unique to OsuAnalyzer
        public double XXY_SR { get; init; }
        public double KRR_LV { get; init; }
        public double LNPercent { get; init; }

        public int NotesCount { get; init; }
        public double MaxKPS { get; init; }
        public double AvgKPS { get; init; }

        public double BeatmapID { get; init; }
        public double BeatmapSetID { get; init; }
    }

    public class OsuAnalyzer
    {
        private readonly SRCalculator calculator = new();

        public OsuAnalysisResult Analyze(string? filePath)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);

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
            // var bpm = GetMainBpm(filePath);

            // Calculate notes count, max KPS, and average KPS
            var (notesCount, maxKPS, avgKPS) = CalculateKPSMetrics(beatmap);

            var result = new OsuAnalysisResult
            {
                Diff = beatmap.MetadataSection.Version,
                Title = beatmap.MetadataSection.Title,
                Artist = beatmap.MetadataSection.Artist,
                Creator = beatmap.MetadataSection.Creator,
                BPMDisplay = bpmDisplay,
                // BPM_Main = bpm,
                Keys = Keys1,
                OD = OD1,
                HP = beatmap.DifficultySection.HPDrainRate,

                // Custom properties unique to OsuAnalyzer
                XXY_SR = xxySR,
                KRR_LV = krrLV,
                LNPercent = beatmap.GetLNPercent(),

                NotesCount = notesCount,
                MaxKPS = maxKPS,
                AvgKPS = avgKPS,

                BeatmapID = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID
            };

            return result;
        }

        private (int notesCount, double maxKPS, double avgKPS) CalculateKPSMetrics(Beatmap beatmap)
        {
            int notesCount = beatmap.HitObjects.Count;

            // Get main BPM from first timing point
            var firstTimingPoint = beatmap.TimingPoints.FirstOrDefault(tp => tp.BeatLength > 0);
            if (firstTimingPoint == null)
            {
                return (notesCount, 0, 0);
            }

            double mainBPM = 60000.0 / firstTimingPoint.BeatLength;
            double beatLength = 60000.0 / mainBPM; // Duration of one beat in ms
            double measureLength = beatLength * 4; // Duration of 4/4 measure in ms

            // Sort hit objects by time
            var sortedHitObjects = beatmap.HitObjects.OrderBy(ho => ho.StartTime).ToList();

            if (sortedHitObjects.Count == 0)
            {
                return (notesCount, 0, 0);
            }

            // Start from first timing point
            double startTime = firstTimingPoint.Offset;
            double endTime = sortedHitObjects.Max(ho => Math.Max(ho.StartTime, ho.EndTime));

            var kpsValues = new List<double>();

            // Divide into 4/4 measure regions
            for (double currentTime = startTime; currentTime < endTime; currentTime += measureLength)
            {
                double regionEnd = currentTime + measureLength;
                int notesInRegion = sortedHitObjects.Count(ho =>
                    ho.StartTime >= currentTime && ho.StartTime < regionEnd);

                if (notesInRegion > 0)
                {
                    double regionDurationSeconds = measureLength / 1000.0;
                    double kps = notesInRegion / regionDurationSeconds;
                    kpsValues.Add(kps);
                }
            }

            double maxKPS = kpsValues.Count > 0 ? kpsValues.Max() : 0;
            double avgKPS = kpsValues.Count > 0 ? kpsValues.Average() : 0;

            return (notesCount, maxKPS, avgKPS);
        }

        private string GetBPMDisplay(string? filePath)
        {
            if (filePath == null || !File.Exists(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var beatmap = BeatmapDecoder.Decode(filePath);
            var bpm = beatmap.MainBPM;
            var bpmMax = beatmap.MaxBPM;
            var bpmMin = beatmap.MinBPM;
            
            string BPMFormat = string.Format(CultureInfo.InvariantCulture, "{0}({1} - {2})", bpm, bpmMin, bpmMax);
                
            return BPMFormat;
        }

        public static string? AddNewBeatmapToSongFolder(string newBeatmapFile, bool openOsz = false)
        {
            // 获取.osu文件所在的目录作为歌曲文件夹
            string? songFolder = Path.GetDirectoryName(newBeatmapFile);
            if (string.IsNullOrEmpty(songFolder))
            {
                MessageBox.Show(Strings.InvalidBeatmapFilePath.Localize() + ": " + newBeatmapFile, Strings.Error.Localize());
                return null;
            }

            Logger.WriteLine(LogLevel.Debug,$"OsuAnalyzer{songFolder}");

            // 创建.osz文件
            string outputOsz = Path.GetFileName(songFolder) + ".osz";
            string? parentDir = Path.GetDirectoryName(songFolder);
            if (string.IsNullOrEmpty(parentDir))
            {
                MessageBox.Show(Strings.UnableToDetermineParentDirectory.Localize() + ": " + songFolder, Strings.Error.Localize());
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
                    MessageBox.Show(Strings.SourceSongFolderDoesNotExist.Localize() + ": " + songFolder, Strings.Error.Localize());
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
                MessageBox.Show(Strings.FailedToAddBeatmapToArchive.Localize() + ": " + Environment.NewLine + Environment.NewLine + e.Message, Strings.Error.Localize());
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
                MessageBox.Show(Strings.FailedToDeleteTemporaryBeatmapFile.Localize() + ": " + newBeatmapFile + " " + Environment.NewLine + Environment.NewLine + e.Message, Strings.Warning.Localize());
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

        public List<double> GetBeatLengthAxis(Dictionary<double, double> beatLengthDict, double mainBPM,
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