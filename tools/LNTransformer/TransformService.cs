using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
using krrTools.Tools.Shared;

namespace krrTools.tools.LNTransformer
{
    public static class TransformService
    {
        // Public entrypoint: process a list of osu files with given parameters.
        public static void ProcessFiles(IEnumerable<string> allFiles, LNTransformerOptions parameters)
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
        private static void ApplyToBeatmap(OsuFileV14 osu, LNTransformerOptions parameters)
        {
            bool canBeConverted = !(IsConverted(osu) && parameters.IgnoreIsChecked);

            if (!canBeConverted)
            {
                return;
            }

            // Set metadata before transformation
            if (!string.IsNullOrEmpty(parameters.CreatorText))
            {
                osu.Metadata.Creator = parameters.CreatorText + " & LNTransformer";
            }
            else
            {
                osu.Metadata.Creator += " & LNTransformer";
            }

            osu.Metadata.Difficulty += " [LN Level " + parameters.LevelValue + "]";

            // Create preview parameters for core logic
            var previewParams = new Preview.PreviewTransformation.LNPreviewParameters
            {
                LevelValue = parameters.LevelValue,
                PercentageValue = parameters.PercentageValue,
                DivideValue = parameters.DivideValue,
                ColumnValue = parameters.ColumnValue,
                GapValue = parameters.GapValue,
                OriginalLN = parameters.OriginalLNIsChecked,
                FixError = parameters.FixErrorIsChecked,
                OverallDifficulty = parameters.OverallDifficulty
            };

            // Use core logic to transform
            var transformed = LNTransformerCore.TransformFull(osu, previewParams);

            // Apply changes to original osu
            osu.HitObjects = transformed.HitObjects;
            osu.General.OverallDifficulty = transformed.General.OverallDifficulty;

            // Handle special case for level -3: update difficulty name
            if ((int)parameters.LevelValue == -3)
            {
                int columnNum = (int)parameters.ColumnValue;
                if (columnNum > (int)osu.General.CircleSize)
                {
                    columnNum = (int)osu.General.CircleSize;
                }
                osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0, $"[LN-Lv{parameters.LevelValue:N0}-C{columnNum:N0}]");
            }
            else
            {
                int columnNum = (int)parameters.ColumnValue;
                if (columnNum > (int)osu.General.CircleSize)
                {
                    columnNum = (int)osu.General.CircleSize;
                }
                osu.Metadata.Difficulty = osu.Metadata.Difficulty.Insert(0, columnNum != 0 ? $"[LN-Lv{parameters.LevelValue:N0}-C{columnNum:N0}]" : $"[LN-Lv{parameters.LevelValue:N0}]");
            }

            osu.WriteFile();
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
