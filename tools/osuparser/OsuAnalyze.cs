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

namespace krrTools.Tools.OsuParser
{
    public class OsuAnalysisResult
    {
        public string? Diff { get; init; }
        public string? Title { get; init; }
        public string? Artist { get; init; }
        public string? Creator { get; init; }
        public string? BPM { get; init; }
        public List<double> BPM_List { get; set; } = new List<double>();
        public double BPM_Main { get; set; }
        public double XXY_SR { get; init; }
        public double KRR_LV { get; init; }
        public double Keys { get; init; }
        public double OD { get; init; }
        public double HP { get; init; }
        public double LNPercent { get; init; }
        public double BeatmapID { get; init; }
        public double BeatmapSetID { get; init; }
    }

    public class OsuAnalyzer
    {
        private readonly SRCalculator calculator = new SRCalculator();

        public OsuAnalysisResult Analyze(string? filePath)
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
            double krrLV = -1;
            if (Keys1 <= 10)
            {
                var (a, b, c) = Keys1 == 10 
                    ? (-0.0773, 3.8651, -3.4979)
                    : (-0.0644, 3.6139, -3.0677);

                double LV = a * xxySR * xxySR + b * xxySR + c;
                krrLV = LV > 0 ? LV : -1;
            }

            var (bpmList, bpmMain) = GetBPMInfo(beatmap);

            var result = new OsuAnalysisResult
            {
                Diff = beatmap.MetadataSection.Version,
                XXY_SR = xxySR,
                KRR_LV = krrLV,
                Title = beatmap.MetadataSection.Title,
                Artist = beatmap.MetadataSection.Artist,
                Creator = beatmap.MetadataSection.Creator,
                Keys = Keys1,
                BPM_List = bpmList,
                BPM_Main = bpmMain,
                BPM = FormatBPMDisplay(bpmList, bpmMain),
                OD = OD1,
                HP = beatmap.DifficultySection.HPDrainRate,
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

        // Simplified GetBPMInfo: returns BPM list and primary BPM only (no segment objects)
        private (List<double> BPMList, double PrimaryBpm) GetBPMInfo(Beatmap beatmap)
        {
            List<double> bpms = new List<double>();
            List<double> offsets = new List<double>();

            foreach (var tp in beatmap.TimingPoints)
            {
                if (tp.BeatLength > 0)
                {
                    bpms.Add(Math.Round(60000.0 / tp.BeatLength, 3));
                    offsets.Add(tp.Offset);
                }
            }

            // dedupe by offset
            var distinct = offsets.Zip(bpms, (t, b) => new { Time = t, BPM = b })
                .DistinctBy(x => x.Time)
                .ToList();
            offsets = distinct.Select(x => x.Time).ToList();
            bpms = distinct.Select(x => x.BPM).ToList();

            if (bpms.Count == 0)
                return ([120], 120);
            if (bpms.Count == 1)
                return ([bpms[0]], bpms[0]);

            // compute durations per timing point
            var durations = new List<double>();
            for (int i = 0; i < offsets.Count; i++)
            {
                double start = offsets[i];
                double end = (i < offsets.Count - 1) ? offsets[i + 1] : (
                    beatmap.HitObjects.Count > 0 ? beatmap.HitObjects.Last().StartTime : start + 1);
                durations.Add(Math.Max(0, end - start));
            }

            // merge near-equal bpms and filter too-short segments
            const double mergeTolerance = 0.5; // BPM
            const double minDurationMs = 250; // ms
            var merged = new List<(double bpm, double duration, double offset)>();

            for (int i = 0; i < bpms.Count; i++)
            {
                double b = bpms[i];
                double d = durations[i];
                double o = offsets[i];
                if (d < minDurationMs)
                    continue;

                var idx = merged.FindIndex(m => Math.Abs(m.bpm - b) <= mergeTolerance);
                if (idx < 0)
                {
                    merged.Add((b, d, o));
                }
                else
                {
                    var exist = merged[idx];
                    double totalDur = exist.duration + d;
                    if (totalDur > 0)
                    {
                        double newBpm = (exist.bpm * exist.duration + b * d) / totalDur;
                        merged[idx] = (newBpm, exist.duration + d, Math.Min(exist.offset, o));
                    }
                    else
                    {
                        merged[idx] = ((exist.bpm + b) / 2.0, exist.duration + d, Math.Min(exist.offset, o));
                    }
                }
            }

            // fallback: if nothing survived filtering, pick the longest original segment
            if (merged.Count == 0)
            {
                int maxIdx = durations.Select((val, idx) => (val, idx)).OrderByDescending(x => x.val).First().idx;
                double fallbackBpm = bpms[Math.Min(maxIdx, bpms.Count - 1)];
                return ([Math.Round(fallbackBpm, 3)], Math.Round(fallbackBpm, 3));
            }

            var finalBpms = merged.Select(m => Math.Round(m.bpm, 3)).Distinct().ToList();
            var primary = Math.Round(merged.OrderByDescending(m => m.duration).First().bpm, 3);
            return (finalBpms, primary);
        }

        private string FormatBPMDisplay(List<double>? bpmList, double primary)
        {
            if (bpmList == null || bpmList.Count == 0)
                return "120";
            if (bpmList.Count == 1)
                return bpmList[0].ToString(CultureInfo.InvariantCulture);

            double min = bpmList.Min();
            double max = bpmList.Max();
            return string.Format(CultureInfo.InvariantCulture, "{0}({1} - {2})", primary, min, max);
        }

        // Compatibility helpers
        public string GetBPM(Beatmap beatmap)
        {
            var (list, primary) = GetBPMInfo(beatmap);
            return FormatBPMDisplay(list, primary);
        }

        private double GetPrimaryBpm(Beatmap beatmap)
        {
            var (_, primary) = GetBPMInfo(beatmap);
            return primary;
        }

        // keep older name for compatibility
        public double GetBPMMain(Beatmap beatmap) => GetPrimaryBpm(beatmap);

        public static void AddNewBeatmapToSongFolder(string newBeatmapFile)
        {
            // 获取.osu文件所在的目录作为歌曲文件夹
            string? songFolder = Path.GetDirectoryName(newBeatmapFile);
            if (string.IsNullOrEmpty(songFolder))
            {
                MessageBox.Show($"Invalid beatmap file path: {newBeatmapFile}", "Error");
                return;
            }

            Console.WriteLine(songFolder);
            
            // 创建.osz文件
            string outputOsz = Path.GetFileName(songFolder) + ".osz";
            string? parentDir = Path.GetDirectoryName(songFolder);
            if (string.IsNullOrEmpty(parentDir))
            {
                MessageBox.Show($"Unable to determine parent directory for: {songFolder}", "Error");
                return;
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
                    return;
                }

                ZipFile.CreateFromDirectory(songFolder, fullOutputPath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Failed to create {fullOutputPath} {Environment.NewLine}{Environment.NewLine}{e.Message}", "Error");
                return;
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
                return;
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
            
            // 4. 打开. osz
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
        
    }
}