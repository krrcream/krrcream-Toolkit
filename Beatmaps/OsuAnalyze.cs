using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.Beatmaps
{
    public class OsuAnalysisResult
    {
        // File information
        public string? FilePath { get; init; }
        public string? FileName { get; init; }
        
        // Basic metadata
        public string? Diff { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Creator { get; init; }
        public string? BPMDisplay { get; init; }
        
        // Difficulty settings
        public double Keys { get; init; }
        public double OD { get; init; }
        public double HP { get; init; }

        // Analysis results
        public double XXY_SR { get; init; }
        public double KRR_LV { get; init; }
        public double LNPercent { get; init; }

        // Performance metrics
        public int NotesCount { get; init; }
        public double MaxKPS { get; init; }
        public double AvgKPS { get; init; }

        // Beatmap identifiers
        public double BeatmapID { get; init; }
        public double BeatmapSetID { get; init; }
        
        // Raw beatmap object (optional, for advanced usage)
        public Beatmap? Beatmap { get; init; }
    }

    public static class OsuAnalyzer
    {
        public static OsuAnalysisResult Analyze(string filePath)
        {
            var beatmap = BeatmapDecoder.Decode(filePath);

            var (Keys1, OD1, xxySR, krrLV) = PerformAnalysis(beatmap);
            int notesCount = beatmap.HitObjects.Count;
            double maxKPS = beatmap.MaxBPM;
            double avgKPS = beatmap.MainBPM;
            
            // gather standard metadata with OsuParsers
            var bpmDisplay = GetBPMDisplay(beatmap);

            var result = new OsuAnalysisResult
            {
                // File information
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                
                // Basic metadata
                Diff = beatmap.MetadataSection.Version,
                Title = beatmap.MetadataSection.Title,
                Artist = beatmap.MetadataSection.Artist,
                Creator = beatmap.MetadataSection.Creator,
                BPMDisplay = bpmDisplay,
                
                // Difficulty settings
                Keys = Keys1,
                OD = OD1,
                HP = beatmap.DifficultySection.HPDrainRate,

                // Analysis results
                XXY_SR = xxySR,
                KRR_LV = krrLV,
                LNPercent = beatmap.GetLNPercent(),

                // Performance metrics
                NotesCount = notesCount,
                MaxKPS = maxKPS,
                AvgKPS = avgKPS,

                // Beatmap identifiers
                BeatmapID = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID,
                
                // Raw beatmap object
                Beatmap = beatmap
            };

            return result;
        }

        public static OsuAnalysisResult Analyze(string filePath, Beatmap beatmap)
        {
            var (Keys1, OD1, xxySR, krrLV) = PerformAnalysis(beatmap);
            int notesCount = beatmap.HitObjects.Count;
            double maxKPS = beatmap.MaxBPM;
            double avgKPS = beatmap.MainBPM;
            var bpmDisplay = GetBPMDisplay(beatmap);
            var result = new OsuAnalysisResult
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),

                // Basic metadata
                Diff = beatmap.MetadataSection.Version,
                Title = beatmap.MetadataSection.Title,
                Artist = beatmap.MetadataSection.Artist,
                Creator = beatmap.MetadataSection.Creator,
                BPMDisplay = bpmDisplay,

                // Difficulty settings
                Keys = Keys1,
                OD = OD1,
                HP = beatmap.DifficultySection.HPDrainRate,

                // Analysis results
                XXY_SR = xxySR,
                KRR_LV = krrLV,
                LNPercent = beatmap.GetLNPercent(),

                // Performance metrics
                NotesCount = notesCount,
                MaxKPS = maxKPS,
                AvgKPS = avgKPS,

                // Beatmap identifiers
                BeatmapID = beatmap.MetadataSection.BeatmapID,
                BeatmapSetID = beatmap.MetadataSection.BeatmapSetID,

                // Raw beatmap object
                Beatmap = beatmap
            };

            return result;
        }
        
        private static string GetBPMDisplay(Beatmap beatmap)
        {
            var bpm = beatmap.MainBPM;
            var bpmMax = beatmap.MaxBPM;
            var bpmMin = beatmap.MinBPM;
            
            string BPMFormat = string.Format(CultureInfo.InvariantCulture, "{0}({1} - {2})", bpm, bpmMin, bpmMax);
                
            return BPMFormat;
        }

        private static (int keys, double od, double xxySr, double krrLv) PerformAnalysis(Beatmap beatmap)
        {
            // 创建新的SRCalculator实例，避免多线程竞争
            var calculator = new SRCalculator();

            var keys = (int)beatmap.DifficultySection.CircleSize;
            var od = beatmap.DifficultySection.OverallDifficulty;
            var notes = calculator.getNotes(beatmap);
            double xxySr = calculator.Calculate(notes, keys, od, out _);
            double krrLv = CalculateKrrLevel(keys, xxySr);

            return (keys, od, xxySr, krrLv);
        }

        private static double CalculateKrrLevel(int keys, double xxySr)
        {
            double krrLv = -1;
            if (keys <= 10)
            {
                var (a, b, c) = keys == 10
                    ? (-0.0773, 3.8651, -3.4979)
                    : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySr * xxySr + b * xxySr + c;
                krrLv = LV > 0 ? LV : -1;
            }

            return krrLv;
        }

        public static string? AddNewBeatmapToSongFolder(string newBeatmapFile, bool openOsz = false)
        {
            // 获取.osu文件所在的目录作为歌曲文件夹
            string? songFolder = Path.GetDirectoryName(newBeatmapFile);
            if (string.IsNullOrEmpty(songFolder))
            {
                Logger.WriteLine(LogLevel.Error, Strings.InvalidBeatmapFilePath.Localize() + ": " + newBeatmapFile);
                return null;
            }

            Logger.WriteLine(LogLevel.Debug,$"OsuAnalyzer{songFolder}");

            // 创建.osz文件
            string outputOsz = Path.GetFileName(songFolder) + ".osz";
            string? parentDir = Path.GetDirectoryName(songFolder);
            if (string.IsNullOrEmpty(parentDir))
            {
                // TODO: 路径为空时，改成自销毁通知
                Logger.WriteLine(LogLevel.Error, Strings.UnableToDetermineParentDirectory.Localize() + ": " + songFolder);
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
                    Logger.WriteLine(LogLevel.Error, Strings.SourceSongFolderDoesNotExist.Localize() + ": " + songFolder);
                    return null;
                }

                ZipFile.CreateFromDirectory(songFolder, fullOutputPath);
            }
            catch (Exception e)
            {
                Logger.WriteLine(LogLevel.Error, $"Failed to create {fullOutputPath} {Environment.NewLine}{Environment.NewLine}{e.Message}");
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
                Logger.WriteLine(LogLevel.Error, Strings.FailedToAddBeatmapToArchive.Localize() + ": " + Environment.NewLine + Environment.NewLine + e.Message);
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
                Logger.WriteLine(LogLevel.Warning, Strings.FailedToDeleteTemporaryBeatmapFile.Localize() + ": " + newBeatmapFile + " " + Environment.NewLine + Environment.NewLine + e.Message);
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
                    Logger.WriteLine(LogLevel.Error, "There was an error opening the generated .osz file. This is probably because .osz files have not been configured to open with osu!.exe on this system." + Environment.NewLine + Environment.NewLine +
                                    "To fix this, download any map from the website, right click the .osz file, click properties, beside Opens with... click Change..., and select osu!. " +
                                    "You'll know the problem is fixed when you can double click .osz files to open them with osu!");
                }
            }

            // return the created osz path as success indicator
            return fullOutputPath;
        }

        public static List<double> GetBeatLengthAxis(Dictionary<double, double> beatLengthDict, double mainBPM,
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

        /// <summary>
        /// 计算 YLS LV (基于 XXY SR)
        /// </summary>
        public static double CalculateYlsLevel(double xxyStarRating)
        {
            const double LOWER_BOUND = 2.76257856739498;
            const double UPPER_BOUND = 10.5541834716376;

            if (xxyStarRating is >= LOWER_BOUND and <= UPPER_BOUND)
            {
                return FittingFormula(xxyStarRating);
            }

            if (xxyStarRating is < LOWER_BOUND and > 0)
            {
                return 3.6198 * xxyStarRating;
            }

            if (xxyStarRating is > UPPER_BOUND and < 12.3456789)
            {
                return (2.791 * xxyStarRating) + 0.5436;
            }

            return double.NaN;
        }

        private static double FittingFormula(double x)
        {
            // TODO: 实现正确的拟合公式
            // For now, returning a placeholder value
            return x * 1.5; // Replace with actual formula
        }
    }
}