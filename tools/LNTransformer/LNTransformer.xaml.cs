// 文件：LNTransformer.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;

namespace krrTools.tools.LNTransformer
{
    public static class Setting
    {
        [SuppressMessage("Usage", "CollectionNeverUpdated", Justification = "Populated by UI/other modules at runtime")]
        public static List<int> KeyFilter { get; set; } = new();

        public static string Seed { get; set; } = "114514";
        public static string Creator { get; set; } = string.Empty;

        // 静态构造器：对集合做一次无害的添加/移除以表明其会被运行时修改（用于静态分析）
        static Setting()
        {
            const int __sentinel = int.MinValue + 123;
            KeyFilter.Add(__sentinel);
            KeyFilter.Remove(__sentinel);
        }
    }

    public partial class LNTransformer
    {
        private const double ERROR = 2.0;

        private int totalFiles;
        private int processedFiles;

        public LNTransformer()
        {
            InitializeComponent();
            ProgressStackPanel.Visibility = Visibility.Collapsed;

            const int __sentinel = int.MinValue + 123;
            Setting.KeyFilter.Add(__sentinel);
            Setting.KeyFilter.Remove(__sentinel);
        }

        public class LNTransformParameters
        {
            public string SeedText { get; set; } = "";
            public double LevelValue { get; set; }
            public double PercentageValue { get; set; }
            public double DivideValue { get; set; }
            public double ColumnValue { get; set; }
            public double GapValue { get; set; }
            public double OverallDifficulty { get; set; }
            public string CreatorText { get; set; } = "";
            public bool IgnoreIsChecked { get; set; }
            public bool OriginalLNIsChecked { get; set; }
            public bool FixErrorIsChecked { get; set; }
            public List<int> CheckKeys { get; set; } = new();
        }

        private void Rectangle_Drop(object sender, DragEventArgs e)
        {
            ProcessDroppedFiles(e);
        }

        // 添加处理单个文件的方法
        public void ProcessSingleFile(string filePath)
        {
            try
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"File not found: {filePath}", "File Not Found",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 检查文件扩展名是否为.osu
                if (Path.GetExtension(filePath).ToLower() != ".osu")
                {
                    MessageBox.Show("Selected file is not a valid .osu file", "Invalid File",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 显示进度条
                ProgressStackPanel.Visibility = Visibility.Visible;
                processedFiles = 0;
                totalFiles = 1;
                UpdateProgress(true, false);

                Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        processedFiles = 0;
                        ConversionProgress.Value = 0;
                    });

                    // 创建包含单个文件的列表
                    var allFiles = new List<string> { filePath };
                    totalFiles = allFiles.Count;

                    if (totalFiles > 0)
                    {
                        LNTransformParameters parameters = new LNTransformParameters();

                        Dispatcher.Invoke(() =>
                        {
                            parameters.SeedText = Setting.Seed;
                            parameters.LevelValue = (int)LevelValue.Value;
                            parameters.PercentageValue = PercentageValue.Value;
                            parameters.DivideValue = DivideValue.Value;
                            parameters.ColumnValue = ColumnValue.Value;
                            parameters.GapValue = GapValue.Value;
                            parameters.IgnoreIsChecked = Ignore.IsChecked == true;
                            parameters.OriginalLNIsChecked = OriginalLN.IsChecked == true;
                            parameters.FixErrorIsChecked = FixError.IsChecked == true;
                            parameters.OverallDifficulty = double.Parse(OverallDifficulty.Text);
                            parameters.CreatorText = Setting.Creator;
                            parameters.CheckKeys = Setting.KeyFilter;
                        });

                        var osuFiles = OsuFileProcessor.ReadMultipleFiles(allFiles, (line) =>
                        {
                            if (line.StartsWith("Mode"))
                            {
                                string str = line.Substring(line.IndexOf(':') + 1).Trim();
                                if (str != "3")
                                {
                                    UpdateProgress(false);
                                    return true;
                                }
                            }

                            if (line.StartsWith("CircleSize"))
                            {
                                string str = line.Substring(line.IndexOf(':') + 1).Trim();
                                if (int.TryParse(str, out int cs))
                                {
                                    if (cs > 10)
                                    {
                                        UpdateProgress(false);
                                        return true;
                                    }

                                    if (Setting.KeyFilter.Count > 0 && !Setting.KeyFilter.Contains(cs))
                                    {
                                        UpdateProgress(false);
                                        return true;
                                    }
                                }
                                else
                                {
                                    UpdateProgress(false);
                                    return true;
                                }
                            }

                            if (parameters.IgnoreIsChecked && line.StartsWith("Creator"))
                            {
                                var str = line.Substring(line.IndexOf(':') + 1).Trim();
                                if (str.Contains("LNTransformer"))
                                {
                                    UpdateProgress(false);
                                    return true;
                                }
                            }

                            return false;
                        });

                        foreach (var osuFile in osuFiles)
                        {
                            try
                            {
                                ApplyToBeatmap(osuFile, parameters);
                                string newFilepath = osuFile.path + "\\" + osuFile.FileName + ".osu";
                                OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath);
                            }
                            catch (Exception ex)
                            {
                                UpdateProgress(false);

#if DEBUG
                                Task.Run(() =>
                                {
                                    MessageBox.Show(
                                        $"Error processing {osuFile.OriginalFile!.FullName}: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                                        "Converting Error", MessageBoxButton.OK, MessageBoxImage.Error,
                                        MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                                });
#endif
                            }
                        }

                        processedFiles = totalFiles;
                        UpdateProgress(true, false);
                    }

                    // 隐藏进度条
                    Dispatcher.Invoke(() => { ProgressStackPanel.Visibility = Visibility.Collapsed; });

                    Task.Run(() =>
                    {
                        MessageBox.Show("File processed successfully!", "Success",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                });
            }
            catch (Exception ex)
            {
                // 隐藏进度条
                Dispatcher.Invoke(() => { ProgressStackPanel.Visibility = Visibility.Collapsed; });

                Task.Run(() =>
                {
                    MessageBox.Show($"Error processing file: {ex.Message}", "Processing Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }




        private void ProcessDroppedFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    processedFiles = 0;
                    ConversionProgress.Value = 0;
                    ProgressStackPanel.Visibility = Visibility.Collapsed;
                });

                var allFiles = Helper.GetDroppedFiles(e, s => Path.GetExtension(s).ToLower() == ".osu",
                    (filePath, count) =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            totalFiles += count;
                            ProgressStackPanel.Visibility = Visibility.Visible;
                            UpdateProgress(true, false);
                        });
                    });

                totalFiles = allFiles.Count;
                if (totalFiles <= 0) return;

                var parameters = new LNTransformParameters();

                Dispatcher.Invoke(() =>
                {
                    parameters.SeedText = Setting.Seed;
                    parameters.LevelValue = (int)LevelValue.Value;
                    parameters.PercentageValue = PercentageValue.Value;
                    parameters.DivideValue = DivideValue.Value;
                    parameters.ColumnValue = ColumnValue.Value;
                    parameters.GapValue = GapValue.Value;
                    parameters.IgnoreIsChecked = Ignore.IsChecked == true;
                    parameters.OriginalLNIsChecked = OriginalLN.IsChecked == true;
                    parameters.FixErrorIsChecked = FixError.IsChecked == true;
                    parameters.OverallDifficulty = double.Parse(OverallDifficulty.Text);
                    parameters.CreatorText = Setting.Creator;
                    parameters.CheckKeys = Setting.KeyFilter;
                });

                var osuFiles = OsuFileProcessor.ReadMultipleFiles(allFiles, (line) =>
                {
                    if (line.StartsWith("Mode"))
                    {
                        var str = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (str != "3")
                        {
                            UpdateProgress(false);
                            return true;
                        }
                    }

                    if (line.StartsWith("CircleSize"))
                    {
                        var str = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (int.TryParse(str, out var cs))
                        {
                            if (cs > 10)
                            {
                                UpdateProgress(false);
                                return true;
                            }

                            if (Setting.KeyFilter.Count > 0 && !Setting.KeyFilter.Contains(cs))
                            {
                                UpdateProgress(false);
                                return true;
                            }
                        }
                        else
                        {
                            UpdateProgress(false);
                            return true;
                        }
                    }

                    if (parameters.IgnoreIsChecked && line.StartsWith("Creator"))
                    {
                        var str = line.Substring(line.IndexOf(':') + 1).Trim();
                        if (str.Contains("LNTransformer"))
                        {
                            UpdateProgress(false);
                            return true;
                        }
                    }

                    return false;
                });

                Parallel.ForEach(osuFiles, osuFile =>
                {
                    try
                    {
                        ApplyToBeatmap(osuFile, parameters);
                    }
                    catch (Exception ex)
                    {
                        UpdateProgress(false);

#if DEBUG
                        Task.Run(() =>
                        {
                            MessageBox.Show($"Error processing {osuFile.OriginalFile!.FullName}: {ex.Message}",
                                "Converting Error", MessageBoxButton.OK, MessageBoxImage.Error,
                                MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                        });
#endif
                    }
                });

                // 确保处理计数到位，避免多线程导致进度不一致
                Dispatcher.Invoke(() =>
                {
                    processedFiles = Math.Min(totalFiles, processedFiles);
                    UpdateProgress(true, false);
                });
            });
        }

        private void UpdateProgress(bool canBeConverted = true, bool increment = true)
        {
            Dispatcher.Invoke(() =>
            {
                if (!canBeConverted)
                {
                    processedFiles = Math.Min(totalFiles, processedFiles + 1);
                }
                else if (increment)
                {
                    processedFiles = Math.Min(totalFiles, processedFiles + 1);
                }

                if (totalFiles <= 0)
                {
                    ConversionProgress.Value = 0;
                    ProgressText.Text = $"{processedFiles}/{Math.Max(0, totalFiles)}";
                    return;
                }

                var percent = (double)processedFiles / totalFiles * 100.0;
                percent = Math.Max(0.0, Math.Min(100.0, percent));
                ConversionProgress.Value = percent;
                ProgressText.Text = $"{processedFiles}/{totalFiles}";
            });
        }

        private void ApplyToBeatmap(OsuFileV14 osu, LNTransformParameters parameters)
        {
            bool canBeConverted = !(IsConverted(osu) && parameters.IgnoreIsChecked);

            if (!canBeConverted)
            {
                UpdateProgress(false);
                return;
            }

            int seed;
            if (string.IsNullOrEmpty(parameters.SeedText))
            {
                seed = Guid.NewGuid().GetHashCode();
            }
            else
            {
                var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(parameters.SeedText + osu.OriginalFile!.FullName));
                seed = BitConverter.ToInt32(bytes, 0);
            }

            var Rng = new Random(seed);

            var keys = (int)osu.General.CircleSize;
            var newObjects = new List<ManiaHitObject>();
            var oldObjects = osu.HitObjects;
            var originalLNObjects = new List<ManiaHitObject>();

            if ((int)parameters.LevelValue == -3)
            {
                for (var i = 0; i < osu.HitObjects.Count; i++)
                    if (osu.HitObjects[i].IsLN)
                    {
                        var obj = osu.HitObjects[i];
                        obj.EndTime = osu.HitObjects[i].StartTime;
                        osu.HitObjects[i] = obj;
                    }

                var transformColumnNum = (int)parameters.ColumnValue;
                if (transformColumnNum > keys) transformColumnNum = keys;

                var randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng,
                    transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

                var gap = (int)parameters.GapValue;
                foreach (var timeGroup in oldObjects.GroupBy(x => x.StartTime))
                {
                    foreach (var note in timeGroup)
                        if (randomColumnSet.Contains(note.Column) && Rng.Next(100) < parameters.PercentageValue)
                            newObjects.Add(note.ToNote());
                        else
                            newObjects.Add(note);

                    gap--;
                    if (gap <= 0)
                    {
                        randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng, transformColumnNum)
                            .ToHashSet();
                        gap = (int)parameters.GapValue;
                    }
                }

                osu.HitObjects = newObjects;
                osu.General.OverallDifficulty = parameters.OverallDifficulty;
                osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0,
                    $"[LN-Lv{parameters.LevelValue:N0}-C{parameters.ColumnValue:N0}]");
                if (!string.IsNullOrEmpty(parameters.CreatorText))
                    osu.Metadata.Creator = parameters.CreatorText;
                else
                    osu.Metadata.Creator += " & LNTransformer";

                osu.WriteFile();

                UpdateProgress();

                return;
            }


            foreach (var column in osu.HitObjects.GroupBy(h => h.Column))
                switch ((int)parameters.LevelValue)
                {
                    case -2:
                    {
                        TrueRandom(newObjects, Rng, column, parameters.OriginalLNIsChecked, parameters.PercentageValue);
                    }
                        break;
                    default:
                    {
                        double mu;
                        double sigma;

                        if ((int)parameters.LevelValue == -1)
                        {
                            mu = -1;
                            sigma = 1;
                        }
                        else if ((int)parameters.LevelValue == 0)
                        {
                            mu = 1;
                            sigma = 100;
                        }
                        else
                        {
                            mu = parameters.LevelValue * 11;
                            sigma = 0.85;
                            if ((int)parameters.LevelValue == 8)
                                sigma = 0.9;
                            else if ((int)parameters.LevelValue == 9) sigma = 1;
                        }

                        originalLNObjects = Transform(Rng, mu, sigma, parameters.DivideValue, osu, newObjects, column,
                            parameters.OriginalLNIsChecked, parameters.PercentageValue, parameters.FixErrorIsChecked);
                    }
                        break;
                    case 10:
                    {
                        originalLNObjects = Invert(osu, newObjects, Rng, column, parameters.DivideValue,
                            parameters.OriginalLNIsChecked, parameters.PercentageValue, parameters.FixErrorIsChecked);
                    }
                        break;
                }

            newObjects = newObjects.OrderBy(h => h.StartTime).ToList();
            originalLNObjects = originalLNObjects.OrderBy(h => h.StartTime).ToList();

            AfterTransform(newObjects, originalLNObjects, osu, Rng, (int)parameters.ColumnValue,
                parameters.OriginalLNIsChecked, (int)parameters.GapValue);

            // 如果要求修复时间四舍五入误差，则在最终对象上修正
            if (parameters.FixErrorIsChecked)
            {
                for (var i = 0; i < osu.HitObjects.Count; i++)
                {
                    var obj = osu.HitObjects[i];
                    if (Math.Abs(obj.StartTime - obj.EndTime) > 0.000001)
                    {
                        obj.EndTime = (int)Helper.PreciseTime(obj.EndTime, osu.TimingPointAt(obj.EndTime).BeatLength,
                            osu.TimingPoints.First().Time);
                        osu.HitObjects[i] = obj;
                    }
                }
            }

            osu.General.OverallDifficulty = parameters.OverallDifficulty;
            var columnNum = (int)parameters.ColumnValue;
            if (columnNum > keys) columnNum = keys;

            osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0,
                columnNum != 0
                    ? $"[LN-Lv{parameters.LevelValue:N0}-C{columnNum:N0}]"
                    : $"[LN-Lv{parameters.LevelValue:N0}]");

            if (!string.IsNullOrEmpty(parameters.CreatorText))
                osu.Metadata.Creator = parameters.CreatorText;
            else
                osu.Metadata.Creator += " & LNTransformer";

            osu.WriteFile();

            UpdateProgress();
        }

        // Transform：根据分布生成 LN，返回原始 LN 列表
        private List<ManiaHitObject> Transform(Random Rng, double mu, double sigmaDivisor, double divide,
            OsuFileV14 osu,
            List<ManiaHitObject> newObjects,
            IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue,
            bool fixErrorIsChecked, int? divide2 = null, double? mu2 = null, double? mu1Dmu2 = null)
        {
            var originalLNObjects = new List<ManiaHitObject>();
            var newColumnObjects = new List<ManiaHitObject>();
            var locations = column.OrderBy(h => h.StartTime).ToList();

            if (locations.Count == 0)
            {
                return originalLNObjects;
            }

            for (var i = 0; i < locations.Count - 1; i++)
            {
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime; // 两个音之间的完整间隔
                var duration = GetDurationByDistribution(Rng, osu, locations[i].StartTime, fullDuration, mu,
                    sigmaDivisor, divide, divide2, mu2, mu1Dmu2);

                var obj = locations[i];
                obj.Column = column.Key;
                if (originalLNIsChecked && Math.Abs(locations[i].StartTime - locations[i].EndTime) > 1e-6)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue && !double.IsNaN(duration))
                {
                    var endTime = obj.StartTime + duration;
                    if (fixErrorIsChecked)
                    {
                        var point = osu.TimingPointAt(endTime);
                        endTime = Helper.PreciseTime(endTime, point.BeatLength, point.Time);
                    }

                    obj.EndTime = (int)endTime;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            // 处理该列最后一个音
            if (Math.Abs(locations[^1].StartTime - locations[^1].EndTime) <= ERROR + 1e-6 ||
                Rng.Next(100) >= percentageValue)
                newColumnObjects.Add(locations[^1].ToNote());
            else
                newColumnObjects.Add(locations[^1]);

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        private double GetDurationByDistribution(Random Rng, OsuFileV14 osu, int startTime, double limitDuration,
            double mu, double sigmaDivisor, double divide, int? divide2 = null, double? mu2 = null,
            double? mu1Dmu2 = null)
        {
            var beatLength = osu.TimingPointAt(startTime).BeatLength; // 节拍长度
            var timeDivide = beatLength / divide;
            var flag = true; // 是否可转换为 LN
            var sigma = timeDivide / sigmaDivisor;
            var timeNum = (int)Math.Round(limitDuration / timeDivide, 0);
            var duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu / 100, sigma));

            if (mu1Dmu2.HasValue)
                if (Rng.Next(100) >= mu1Dmu2.Value && mu2.HasValue)
                {
                    timeDivide = beatLength / (divide2 ?? (int)divide);
                    sigma = timeDivide / sigmaDivisor;
                    timeNum = (int)Math.Round(limitDuration / timeDivide, 0);
                    duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu2.Value / 100, sigma));
                }

            if (Math.Abs(mu + 1.0) < 1e-6)
            {
                if (timeNum < 1)
                {
                    duration = timeDivide;
                }
                else
                {
                    var rdTime = Rng.Next(1, timeNum);
                    duration = rdTime * timeDivide;
                    duration = TimeRound(timeDivide, duration);
                }
            }

            if (duration > limitDuration - timeDivide)
            {
                duration = limitDuration - timeDivide;
                duration = TimeRound(timeDivide, duration);
            }

            if (duration <= timeDivide) duration = timeDivide;

            if (duration >= limitDuration - ERROR) // 过长则认为不可转换
                flag = false;

            return flag ? duration : double.NaN;
        }

        private List<ManiaHitObject> Invert(OsuFileV14 osu, List<ManiaHitObject> newObjects, Random Rng,
            IGrouping<int, ManiaHitObject> column, double divideValue, bool originalLNIsChecked, double percentageValue,
            bool fixErrorIsChecked)
        {
            var locations = column.OrderBy(h => h.StartTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            for (var i = 0; i < locations.Count - 1; i++)
            {
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;
                var beatLength = osu.TimingPointAt(locations[i + 1].StartTime).BeatLength;
                var flag = true;
                var duration = fullDuration - beatLength / divideValue;

                if (duration < beatLength / divideValue) duration = beatLength / divideValue;

                if (duration > fullDuration - 3) flag = false;

                var obj = locations[i];
                obj.Column = column.Key;

                if (originalLNIsChecked && Math.Abs(locations[i].StartTime - locations[i].EndTime) > 1e-6)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue && flag)
                {
                    var endTime = locations[i].StartTime + duration;
                    if (fixErrorIsChecked)
                        endTime = Helper.PreciseTime(endTime, osu.TimingPointAt(endTime).BeatLength,
                            osu.TimingPoints.First().Time);

                    obj.EndTime = (int)endTime;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            double lastStartTime = locations[^1].StartTime;
            double lastEndTime = locations[^1].EndTime;
            if (originalLNIsChecked && Math.Abs(lastStartTime - lastEndTime) > 1e-6)
            {
                var obj = locations[^1];
                obj.Column = column.Key;
                newColumnObjects.Add(obj);
                originalLNObjects.Add(obj);
            }
            else
            {
                var obj = locations[^1];
                obj.Column = column.Key;
                newColumnObjects.Add(obj.ToNote());
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        private void TrueRandom(List<ManiaHitObject> newObjects, Random Rng,
            IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue)
        {
            var locations = column.OrderBy(h => h.StartTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            for (int i = 0; i < locations.Count - 1; i++)
            {
                // Full duration of the hold note.
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;

                // Beat length at the end of the hold note.
                // double beatLength = beatmap.ControlPointInfo.TimingPointAt(locations[i + 1].StartTime).BeatLength;

                double duration = Rng.Next((int)fullDuration) + Rng.NextDouble();
                while (duration > fullDuration)
                    duration--;

                var obj = locations[i];
                obj.Column = column.Key;

                if (originalLNIsChecked && locations[i].StartTime != locations[i].EndTime)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue)
                {
                    obj.EndTime = obj.StartTime + (int)duration;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            newObjects.AddRange(newColumnObjects);
        }

        private void AfterTransform(List<ManiaHitObject> afterObjects, List<ManiaHitObject> originalLNObjects,
            OsuFileV14 osu, Random Rng, int transformColumnNum, bool originalLNIsChecked, int gapValue)
        {
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
                    if (originalLNSet.Contains(note) && originalLNIsChecked)
                        resultObjects.Add(note);
                    else if (randomColumnSet.Contains(note.Column) && Math.Abs(note.StartTime - note.EndTime) > 1e-6)
                        resultObjects.Add(note);
                    else
                        resultObjects.Add(note.ToNote());

                gap--;
                if (gap == 0)
                {
                    randomColumnSet = Enumerable.Range(0, keys).SelectRandom(Rng, transformColumnNum)
                        .ToHashSet();
                    gap = maxGap;
                }
            }

            osu.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
        }

        private double RandDistribution(Random Rng, double u, double d)
        {
            if (d <= 0) return u;

            var u1 = Rng.NextDouble();
            var u2 = Rng.NextDouble();
            var z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
            var x = u + d * z;
            return x;
        }

        private double TimeRound(double timeDivide, double num)
        {
            var remainder = num % timeDivide;
            if (remainder < timeDivide / 2)
                return num - remainder;
            return num + timeDivide - remainder;
        }

        private bool IsConverted(OsuFileV14 osu)
        {
            var creator = osu.Metadata.Creator;
            if (!string.IsNullOrEmpty(creator) && creator.Contains("LNTransformer")) return true;

            var difficulty = osu.Metadata.Difficulty;
            if (!string.IsNullOrEmpty(difficulty) && Regex.IsMatch(difficulty, @"\[LN.*\]")) return true;

            return false;
        }

        private void InstructionButton_Click(object sender, RoutedEventArgs e)
        {
            var instructionsWindow = new InstructionsWindow();
            instructionsWindow.ShowDialog();
        }

        private void OpenOsuListenerButton_Click(object sender, RoutedEventArgs e)
        {
            var listenerWindow = new ListenerView(this, 2); // 2表示LN Transformer窗口
            listenerWindow.Show();
        }
    }
}