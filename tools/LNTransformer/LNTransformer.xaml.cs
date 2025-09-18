using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OsuParsers.Beatmaps.Objects;

namespace krrTools
{
    /// <summary>
    /// LNTransformer.xaml 的交互逻辑
    /// </summary>
    public static class Setting
    {
        public static List<int> KeyFilter { get; set; } = new List<int>();
        public static string Seed { get; set; } = "114514";
        public static string Creator { get; set; } = string.Empty;
    }
    
    public partial class LNTransformer : Window
    {

        public const double ERROR = 2.0;

        private int totalFiles = 0;
        private int processedFiles = 0;

        public LNTransformer()
        {
            InitializeComponent();
            ProgressStackPanel.Visibility = Visibility.Collapsed;
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
            public List<int> CheckKeys { get; set; } = new List<int>();
        }
        
        private void Rectangle_Drop(object sender, DragEventArgs e)
        {
            ProcessDroppedFiles(e);
        }

        private void ProcessDroppedFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    processedFiles = 0;
                    ConversionProgress.Value = 0;
                });

                var allFiles = Helper.GetDroppedFiles(e, s => Path.GetExtension(s).ToLower() == ".osu", (filePath, count) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        totalFiles += count;
                        ProgressStackPanel.Visibility = Visibility.Visible;
                        UpdateProgress(true, false);
                    });
                });

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
                                MessageBox.Show($"Error processing {osuFile.OriginalFile!.FullName}: {ex.Message}", "Converting Error", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                            });
#endif
                        }
                    });

                    processedFiles = totalFiles;
                    UpdateProgress(true, false);
                }
            });
        }

        private void UpdateProgress(bool canBeConverted = true, bool increment = true)
        {
            Dispatcher.Invoke(() =>
            {
                if (!canBeConverted)
                {
                    totalFiles--;
                    processedFiles--;
                }

                if (totalFiles <= 0)
                {
                    return;
                }

                if (increment)
                {
                    processedFiles++;
                }

                ConversionProgress.Value = (double)processedFiles / totalFiles * 100;
                ProgressText.Text = $"{processedFiles}/{totalFiles}";
            });
        }

        public void ApplyToBeatmap(OsuFileV14 osu, LNTransformParameters parameters)
        {
            bool canBeConverted = true;

            if (IsConverted(osu) && parameters.IgnoreIsChecked)
            {
                canBeConverted = false;
            }

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
                {
                    byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(parameters.SeedText + osu.OriginalFile!.FullName));
                    seed = BitConverter.ToInt32(bytes, 0);
                }
            }
            var Rng = new Random(seed);

            int keys = (int)osu.General.CircleSize;
            var newObjects = new List<ManiaHitObject>();
            var oldObjects = osu.HitObjects;
            var originalLNObjects = new List<ManiaHitObject>();

            if ((int)parameters.LevelValue == -3)
            {
                for (int i = 0; i < osu.HitObjects.Count; i++)
                {
                    if (osu.HitObjects[i].IsLN)
                    {
                        var obj = osu.HitObjects[i];
                        obj.EndTime = osu.HitObjects[i].StartTime;
                        osu.HitObjects[i] = obj;
                    }
                }

                int transformColumnNum = (int)parameters.ColumnValue;

                if (transformColumnNum > keys)
                {
                    transformColumnNum = keys;
                }
                var randomColumnSet = Helper.SelectRandom(Enumerable.Range(0, keys), Rng, transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

                int gap = (int)parameters.GapValue;
                foreach (var timeGroup in oldObjects.GroupBy(x => x.StartTime))
                {
                    foreach (var note in timeGroup)
                    {
                        if (randomColumnSet.Contains(note.Column) && Rng.Next(100) < parameters.PercentageValue)
                        {
                            newObjects.Add(note.ToNote());
                        }
                        else
                        {
                            newObjects.Add(note);
                        }
                    }

                    gap--;
                    if (gap <= 0)
                    {
                        randomColumnSet = Helper.SelectRandom(Enumerable.Range(0, keys), Rng, transformColumnNum).ToHashSet();
                        gap = (int)parameters.GapValue;
                    }
                }
                osu.HitObjects = newObjects;
                osu.General.OverallDifficulty = parameters.OverallDifficulty;
                osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0, $"[LN-Lv{parameters.LevelValue:N0}-C{parameters.ColumnValue:N0}]");
                if (!string.IsNullOrEmpty(parameters.CreatorText))
                {
                    osu.Metadata.Creator = parameters.CreatorText;
                }
                else
                {
                    osu.Metadata.Creator += " & LNTransformer";
                }
                osu.WriteFile();

                UpdateProgress();

                return;
            }


            foreach (var column in osu.HitObjects.GroupBy(h => h.Column))
            {
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
                                {
                                    sigma = 0.9;
                                }
                                else if ((int)parameters.LevelValue == 9)
                                {
                                    sigma = 1;
                                }
                            }
                            originalLNObjects = Transform(Rng, mu, sigma, parameters.DivideValue, osu, newObjects, column, parameters.OriginalLNIsChecked, parameters.PercentageValue, parameters.FixErrorIsChecked);
                        }
                        break;
                    case 10:
                        {
                            originalLNObjects = Invert(osu, newObjects, Rng, column, parameters.DivideValue, parameters.OriginalLNIsChecked, parameters.PercentageValue, parameters.FixErrorIsChecked);
                        }
                        break;
                }
            }

            newObjects = newObjects.OrderBy(h => h.StartTime).ToList();
            originalLNObjects = originalLNObjects.OrderBy(h => h.StartTime).ToList();

            AfterTransform(newObjects, originalLNObjects, osu, Rng, (int)parameters.ColumnValue, parameters.OriginalLNIsChecked, (int)parameters.GapValue);



            osu.General.OverallDifficulty = parameters.OverallDifficulty;
            int columnNum = (int)parameters.ColumnValue;
            if (columnNum > keys)
            {
                columnNum = keys;
            }
            if (columnNum != 0)
            {
                osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0, $"[LN-Lv{parameters.LevelValue:N0}-C{columnNum:N0}]");
            }
            else
            {
                osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0, $"[LN-Lv{parameters.LevelValue:N0}]");
            }

            if (!string.IsNullOrEmpty(parameters.CreatorText))
            {
                osu.Metadata.Creator = parameters.CreatorText;
            }
            else
            {
                osu.Metadata.Creator += " & LNTransformer";
            }

            osu.WriteFile();

            UpdateProgress();
        }

        private void FixErrorAfterTransform(OsuFileV14 osu, List<ManiaHitObject> newObjects, LNTransformParameters parameters)
        {
            if (parameters.FixErrorIsChecked == true)
            {
                for (int i = 0; i < newObjects.Count; i++)
                {
                    if (newObjects[i].StartTime != newObjects[i].EndTime)
                    {
                        var obj = newObjects[i];
                        obj.EndTime = (int)Helper.PreciseTime(obj.EndTime, osu.TimingPointAt(obj.EndTime).BeatLength, osu.TimingPoints.First().Time);
                    }
                }
            }
        }

        /// <summary>
        /// Return original LN objects.
        /// </summary>
        public List<ManiaHitObject> Transform(Random Rng, double mu, double sigmaDivisor, double divide, OsuFileV14 osu, List<ManiaHitObject> newObjects,
            IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue, bool fixErrorIsChecked, int divide2 = -1, double mu2 = -2, double mu1Dmu2 = -1)
        {
            var originalLNObjects = new List<ManiaHitObject>();
            var newColumnObjects = new List<ManiaHitObject>();
            var locations = column.OrderBy(h => h.StartTime).ToList();
            for (int i = 0; i < locations.Count - 1; i++)
            {
                double offset = osu.TimingPoints.First().Time;
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime; // Full duration of the hold note.
                double duration = GetDurationByDistribution(Rng, osu, locations[i].StartTime, fullDuration, mu, sigmaDivisor, divide, divide2, mu2, mu1Dmu2);

                var obj = locations[i];
                obj.Column = column.Key;
                if (originalLNIsChecked == true && locations[i].StartTime != locations[i].EndTime)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue && !double.IsNaN(duration))
                {
                    var endTime = obj.StartTime + duration;
                    if (fixErrorIsChecked == true)
                    {
                        TimingPoint point = osu.TimingPointAt(endTime);
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

            // Dispose last note on the column

            if (Math.Abs(locations[locations.Count - 1].StartTime - locations[locations.Count - 1].EndTime) <= ERROR || Rng.Next(100) >= percentageValue)
            {
                newColumnObjects.Add(locations[locations.Count - 1].ToNote());
            }
            else
            {
                newColumnObjects.Add(locations[locations.Count - 1]);
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        public double GetDurationByDistribution(Random Rng, OsuFileV14 osu, int startTime, double limitDuration, double mu, double sigmaDivisor, double divide, double divide2 = -1, double mu2 = -2, double mu1Dmu2 = -1)
        {
            // Beat length at the end of the hold note.
            double beatLength = osu.TimingPointAt(startTime).BeatLength;
            // double beatBPM = beatmap.ControlPointInfo.TimingPointAt(startTime).BPM;
            double timeDivide = beatLength / divide; //beatBPM / 60 * 100 / Divide.Value;
            bool flag = true; // Can be transformed to LN
            double sigma = timeDivide / sigmaDivisor; // LN duration σ
            int timenum = (int)Math.Round(limitDuration / timeDivide, 0);
            int rdtime;
            double duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu / 100, sigma));

            if (mu1Dmu2 != -1)
            {
                if (Rng.Next(100) >= mu1Dmu2)
                {
                    timeDivide = beatLength / divide2;
                    sigma = timeDivide / sigmaDivisor;
                    timenum = (int)Math.Round(limitDuration / timeDivide, 0);
                    duration = TimeRound(timeDivide, RandDistribution(Rng, limitDuration * mu2 / 100, sigma));
                }
            }

            if (mu == -1)
            {
                if (timenum < 1)
                {
                    duration = timeDivide;
                }
                else
                {
                    rdtime = Rng.Next(1, timenum);
                    duration = rdtime * timeDivide;
                    duration = TimeRound(timeDivide, duration);
                }
            }

            if (duration > limitDuration - timeDivide)
            {
                duration = limitDuration - timeDivide;
                duration = TimeRound(timeDivide, duration);
            }

            if (duration <= timeDivide)
            {
                duration = timeDivide;
            }

            if (duration >= limitDuration - ERROR) // Additional processing.
            {
                flag = false;
            }

            return flag ? duration : double.NaN;
        }

        public List<ManiaHitObject> Invert(OsuFileV14 osu, List<ManiaHitObject> newObjects, Random Rng, IGrouping<int, ManiaHitObject> column, double divideValue, bool originalLNIsChecked, double percentageValue, bool fixErrorIsChecked)
        {
            var locations = column.OrderBy(h => h.StartTime).ToList();

            var newColumnObjects = new List<ManiaHitObject>();
            var originalLNObjects = new List<ManiaHitObject>();

            for (int i = 0; i < locations.Count - 1; i++)
            {
                // Full duration of the hold note.
                double fullDuration = locations[i + 1].StartTime - locations[i].StartTime;
                // Beat length at the end of the hold note.
                double beatLength = osu.TimingPointAt(locations[i + 1].StartTime).BeatLength;
                bool flag = true;
                double duration = fullDuration - (beatLength / divideValue);

                if (duration < beatLength / divideValue)
                {
                    duration = beatLength / divideValue;
                }

                if (duration > fullDuration - 3)
                {
                    flag = false;
                }

                var obj = locations[i];
                obj.Column = column.Key;

                if (originalLNIsChecked == true && locations[i].StartTime != locations[i].EndTime)
                {
                    newColumnObjects.Add(obj);
                    originalLNObjects.Add(obj);
                }
                else if (Rng.Next(100) < percentageValue && flag)
                {
                    var endTime = locations[i].StartTime + duration;
                    if (fixErrorIsChecked == true)
                    {
                        endTime = Helper.PreciseTime(endTime, osu.TimingPointAt(endTime).BeatLength, osu.TimingPoints.First().Time);
                    }
                    obj.EndTime = (int)endTime;
                    newColumnObjects.Add(obj);
                }
                else
                {
                    newColumnObjects.Add(obj.ToNote());
                }
            }

            double lastStartTime = locations[locations.Count - 1].StartTime;
            double lastEndTime = locations[locations.Count - 1].EndTime;
            if (originalLNIsChecked == true && lastStartTime != lastEndTime)
            {
                var obj = locations[locations.Count - 1];
                obj.Column = column.Key;
                newColumnObjects.Add(obj);
                originalLNObjects.Add(obj);
            }
            else
            {
                var obj = locations[locations.Count - 1];
                obj.Column = column.Key;
                newColumnObjects.Add(obj.ToNote());
            }

            newObjects.AddRange(newColumnObjects);

            return originalLNObjects;
        }

        public List<ManiaHitObject> TrueRandom(List<ManiaHitObject> newObjects, Random Rng, IGrouping<int, ManiaHitObject> column, bool originalLNIsChecked, double percentageValue)
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

                if (originalLNIsChecked == true && locations[i].StartTime != locations[i].EndTime)
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

            return originalLNObjects;
        }

        public void AfterTransform(List<ManiaHitObject> afterObjects, List<ManiaHitObject> originalLNObjects, OsuFileV14 osu, Random Rng, int transformColumnNum, bool originalLNIsChecked, int gapValue)
        {
            var resultObjects = new List<ManiaHitObject>();
            var originalLNSet = new HashSet<ManiaHitObject>(originalLNObjects);
            int keys = (int)osu.General.CircleSize;
            int maxGap = gapValue;
            int gap = maxGap;

            if (transformColumnNum > keys)
            {
                transformColumnNum = keys;
            }

            var randomColumnSet = Helper.SelectRandom(Enumerable.Range(0, keys), Rng, transformColumnNum == 0 ? keys : transformColumnNum).ToHashSet();

            foreach (var timeGroup in afterObjects.OrderBy(h => h.StartTime).GroupBy(h => h.StartTime))
            {
                foreach (var note in timeGroup)
                {
                    if (originalLNSet.Contains(note) && originalLNIsChecked == true)
                    {
                        resultObjects.Add(note);
                    }
                    else if (randomColumnSet.Contains(note.Column) && note.StartTime != note.EndTime)
                    {
                        resultObjects.Add(note);
                    }
                    else
                    {
                        resultObjects.Add(note.ToNote());
                    }
                }

                gap--;
                if (gap == 0)
                {
                    randomColumnSet = Helper.SelectRandom(Enumerable.Range(0, keys), Rng, transformColumnNum).ToHashSet();
                    gap = maxGap;
                }
            }

            osu.HitObjects = resultObjects.OrderBy(h => h.StartTime).ToList();
        }

        public double RandDistribution(Random Rng, double u, double d)
        {
            double u1, u2, z, x;
            if (d <= 0)
            {
                return u;
            }
            u1 = Rng.NextDouble();
            u2 = Rng.NextDouble();
            z = Math.Sqrt(-2 * Math.Log(u1)) * Math.Sin(2 * Math.PI * u2);
            x = u + (d * z);
            return x;
        }

        public double TimeRound(double timedivide, double num)
        {
            double remainder = num % timedivide;
            if (remainder < timedivide / 2)
                return num - remainder;
            return num + timedivide - remainder;
        }

        public bool IsConverted(OsuFileV14 osu)
        {
            if (osu.Metadata.Creator.Contains("LNTransformer"))
            {
                return true;
            }
            string difficulty = osu.Metadata.Difficulty;
            if (Regex.IsMatch(difficulty, @"\[LN.*\]"))
            {
                return true;
            }
            return false;
        }

        private void InstructionButton_Click(object sender, RoutedEventArgs e)
        {
            InstructionsWindow instructionsWindow = new InstructionsWindow();
            instructionsWindow.ShowDialog();
        }
        
    }
}