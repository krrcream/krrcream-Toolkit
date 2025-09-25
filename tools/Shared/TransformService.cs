using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using krrTools.tools.LNTransformer;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;

namespace krrTools.Tools.Shared
{
    public static class TransformService
    {
        // Public entrypoint: process a list of osu files with given parameters.
        public static void ProcessFiles(IEnumerable<string> allFiles, LNTransformParameters parameters)
        {
            Task.Run(() =>
            {
                var osuFiles = OsuFileProcessor.ReadMultipleFiles(allFiles, (line) =>
                {
                    if (line.StartsWith("Mode"))
                    {
                        string str = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (str != "3") return true;
                    }

                    if (line.StartsWith("CircleSize"))
                    {
                        string str = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (int.TryParse(str, out int cs))
                        {
                            if (cs > 10) return true;
                            if (parameters.CheckKeys is { Count: > 0 } && !parameters.CheckKeys.Contains(cs)) return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (parameters.IgnoreIsChecked && line.StartsWith("Creator"))
                        {
                            var str = line.Substring(line.IndexOf(':') + 1).Trim();
                            if (str.Contains("LNTransformer")) return true;
                        }

                        if (parameters.IgnoreIsChecked && line.StartsWith("Version"))
                        {
                            var str = line.Substring(line.IndexOf(':') + 1).Trim();
                            if (str.Contains("[LN")) return true;
                        }
                    }

                    return false;
                });

                foreach (var osuFile in osuFiles)
                {
                    try
                    {
                        ApplyToBeatmap(osuFile, parameters);
                        var basePath = string.IsNullOrEmpty(osuFile.path) ? string.Empty : osuFile.path;
                        var newFilepath = string.IsNullOrEmpty(basePath)
                            ? osuFile.FileName + ".osu"
                            : Path.Combine(basePath, osuFile.FileName + ".osu");

                        if (ListenerControl.IsOpen)
                        {
                            OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath);
                        }
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        MessageBox.Show($"处理 {osuFile.OriginalFile?.FullName} 时出错: {ex.Message}\n\n堆栈跟踪:\n{ex.StackTrace}", "转换错误", MessageBoxButton.OK, MessageBoxImage.Error,
                            MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
#else
                        Debug.WriteLine($"TransformService.ProcessFiles - per-file error for {osuFile?.OriginalFile?.FullName}: {ex.Message}");
#endif
                    }
                }

                // Notify user on UI thread that processing finished
                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(SharedUIComponents.IsChineseLanguage() ? "文件处理成功！" : "File processed successfully!",
                        SharedUIComponents.IsChineseLanguage() ? "成功" : "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }));
            });
        }

        // Apply the LN transformation to a single OsuFileV14 instance and save
        private static void ApplyToBeatmap(OsuFileV14 osu, LNTransformParameters parameters)
        {
            bool canBeConverted = !(IsConverted(osu) && parameters.IgnoreIsChecked);
            if (!canBeConverted) return;

            var rngSeed = int.TryParse(parameters.SeedText, out var seed) ? seed : 114514;
            var Rng = new Random(rngSeed);
            var originalLNObjects = new List<ManiaHitObject>();
            var afterObjects = new List<ManiaHitObject>();

            var timeDivide = 4 * Math.Pow(2, -parameters.DivideValue);
            var transformColumnNum = (int)parameters.ColumnValue;
            var gapValue = (int)parameters.GapValue;
            var percentage = parameters.PercentageValue / 100.0;
            var level = parameters.LevelValue;
            var originalLNIsChecked = parameters.OriginalLNIsChecked;
            var fixErrorIsChecked = parameters.FixErrorIsChecked;

            try
            {
                if (fixErrorIsChecked) osu.Recalculate();

                if (Math.Abs(parameters.OverallDifficulty - 0) > 1e-6)
                {
                    osu.General.OverallDifficulty = parameters.OverallDifficulty;
                }

                if (!string.IsNullOrEmpty(parameters.CreatorText)) osu.Metadata.Creator = parameters.CreatorText + " & LNTransformer";
                else osu.Metadata.Creator += " & LNTransformer";

                osu.Metadata.Difficulty += " [LN Level " + parameters.LevelValue + "]";

                foreach (var obj in osu.HitObjects)
                {
                    if (Math.Abs(obj.StartTime - obj.EndTime) < 1e-6) afterObjects.Add(obj);
                    else
                    {
                        originalLNObjects.Add(obj);
                        if (originalLNIsChecked) afterObjects.Add(obj);
                    }
                }

                var resultObjects = new List<ManiaHitObject>();
                var originalLNSet = new HashSet<ManiaHitObject>(originalLNObjects);
                int keys = (int)osu.General.CircleSize;
                int maxGap = gapValue;
                int gap = maxGap;

                if (transformColumnNum > keys) transformColumnNum = keys;

                var randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng,
                    transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

                foreach (var timeGroup in afterObjects.OrderBy(h => h.StartTime).GroupBy(h => h.StartTime))
                {
                    foreach (var note in timeGroup)
                    {
                        if (originalLNSet.Contains(note) && originalLNIsChecked)
                            resultObjects.Add(note);
                        else if (randomColumnSet.Contains(note.Column) && Math.Abs(note.StartTime - note.EndTime) > 1e-6)
                            resultObjects.Add(note);
                        else if (Rng.NextDouble() < percentage)
                            resultObjects.Add(note.ToLongNote(TimeRound(timeDivide, RandDistribution(Rng, note.EndTime, level * 2.0))));
                        else
                            resultObjects.Add(note.ToNote());
                    }

                    gap--;
                    if (gap == 0)
                    {
                        randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng, transformColumnNum).ToHashSet();
                        gap = maxGap;
                    }
                }

                osu.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
                osu.WriteFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyToBeatmap failed for {osu.OriginalFile?.FullName}: {ex.Message}\n{ex.StackTrace}");
                throw; // rethrow so the caller can handle per-file logging
            }
        }

        private static double RandDistribution(Random Rng, double u, double d)
        {
            if (d <= 0) return u;
            var u1 = Rng.NextDouble();
            var u2 = Rng.NextDouble();
            var z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
            var x = u + d * z;
            return x;
        }

        private static double TimeRound(double timeDivide, double num)
        {
            var remainder = num % timeDivide;
            if (remainder < timeDivide / 2) return num - remainder;
            return num + timeDivide - remainder;
        }

        private static bool IsConverted(OsuFileV14 osu)
        {
            var creator = osu.Metadata.Creator;
            if (!string.IsNullOrEmpty(creator) && creator.Contains("LNTransformer")) return true;
            var difficulty = osu.Metadata.Difficulty;
            if (!string.IsNullOrEmpty(difficulty) && Regex.IsMatch(difficulty, @"\[LN.*\]")) return true;
            return false;
        }
    }
}
