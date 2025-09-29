using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
using Microsoft.Extensions.Logging;

namespace krrTools.tools.LNTransformer
{
    public static class TransformService
    {
        private static readonly ILogger _logger = LoggerFactoryHolder.CreateLogger<string>();

        // Public entrypoint: process a list of osu files with given parameters.
        public static void ProcessFiles(IEnumerable<string> allFiles, YLsLNTransformerOptions parameters)
        {
            foreach (var file in allFiles)
            {
                _logger.LogInformation("转换器读取转换: {FilePath}", file);
            }
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
                        // 移除CheckKeys检查，因为这个属性已移除
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
                    _logger.LogError(ex, "处理文件时出错: {FileName}", osuFile.OriginalFile?.FullName);
#endif
                }
            }

            // Notify user on UI thread that processing finished
            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
            {
                // Removed MessageBox.Show to avoid duplicate notifications; FileDispatcher handles it now
            }));
        }

        // Apply the LN transformation to a single OsuFileV14 instance and save
        public static void ApplyToBeatmap(OsuFileV14 osu, YLsLNTransformerOptions parameters)
        {
            bool canBeConverted = !(IsConverted(osu) && parameters.IgnoreIsChecked);

            if (!canBeConverted)
            {
                return;
            }

            // Set metadata before transformation
            osu.Metadata.Creator += " & LNTransformer";

            osu.Metadata.Difficulty += " [LN Level " + parameters.LevelValue + "]";

            // Create preview parameters for core logic
            var previewParams = new YLsLNTransformerCore.LNPreviewParameters
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
            var transformed = YLsLNTransformerCore.TransformFull(osu, previewParams);

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

        // Process a single file and return the output path
        public static string? ProcessSingleFile(string filePath, YLsLNTransformerOptions parameters)
        {
            try
            {
                var osuFiles = OsuFileProcessor.ReadMultipleFiles([filePath], (line) =>
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
                            // 移除CheckKeys检查，因为这个属性已移除
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
                            var oszPath = OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath);
                            return oszPath;
                        }
                        else
                        {
                            return newFilepath;
                        }
                    }
                    catch (Exception ex)
                    {
                _logger.LogError(ex, "处理单文件时出错: {FileName}", osuFile.OriginalFile?.FullName);
                        return null;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理单文件出错");
                return null;
            }
        }

        public static OsuFileV14? ProcessSingleFileToData(string filePath, YLsLNTransformerOptions parameters)
        {
            try
            {
                var osuFiles = OsuFileProcessor.ReadMultipleFiles([filePath], (line) =>
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
                            // 移除CheckKeys检查，因为这个属性已移除
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
                        return osuFile;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "处理单文件到数据时出错: {FileName}", osuFile.OriginalFile?.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理单文件到数据出错");
            }

            return null;
        }
    }
}
