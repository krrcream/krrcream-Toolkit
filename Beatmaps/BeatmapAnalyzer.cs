using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// 谱面分析器 - 统一封装谱面文件解析和分析逻辑
    /// 整合了文件有效性检查、解析和难度指标计算
    /// </summary>
    public class BeatmapAnalyzer
    {
        /// <summary>
        /// 分析谱面文件，返回完整分析结果
        /// </summary>
        /// <param name="filePath">谱面文件路径</param>
        /// <returns>分析结果，如果失败返回null</returns>
        public static OsuAnalysisResult? Analyze(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) ||
                !Path.GetExtension(filePath).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                // 检查是否为Mania模式
                if (!IsManiaBeatmap(filePath))
                {
                    return null;
                }

                // 使用OsuAnalyzer进行完整分析
                return OsuAnalyzer.Analyze(filePath);
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, "[BeatmapAnalyzer] Analysis failed for {0}: {1}", filePath, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 快速检查是否为有效的Mania谱面文件
        /// </summary>
        public static bool IsManiaBeatmap(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath) ||
                !Path.GetExtension(filePath).Equals(".osu", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                // 只读取文件头部，检查Mode信息 (最多读取前50行)
                using var reader = new StreamReader(filePath);
                for (int lineCount = 0; lineCount < 50 && reader.ReadLine() is { } line; lineCount++)
                {
                    if (line.AsSpan().StartsWith("Mode:".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    {
                        // 使用Span优化：Mode: 3 -> "3"
                        var modeSpan = line.AsSpan(5).TrimStart(); // 去掉"Mode:"并去除前导空白

                        // 找到第一个非数字字符的位置
                        var numberEnd = 0;
                        while (numberEnd < modeSpan.Length && char.IsDigit(modeSpan[numberEnd]))
                        {
                            numberEnd++;
                        }

                        // 直接比较Span，避免字符串分配
                        return numberEnd > 0 && modeSpan.Slice(0, numberEnd).SequenceEqual("3".AsSpan());
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
    }
}