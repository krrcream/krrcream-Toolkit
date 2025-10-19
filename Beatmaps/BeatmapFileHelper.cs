using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using krrTools.Localization;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Beatmaps
{
    /// <summary>
    /// Beatmap 文件相关工具类
    /// </summary>
    public static class BeatmapFileHelper
    {
        /// <summary>
        /// 遍历路径集合中的所有 .osu 文件
        /// </summary>
        public static IEnumerable<string> EnumerateOsuFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
                if (File.Exists(path) && Path.GetExtension(path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                    yield return path;
                else if (Directory.Exists(path))
                    foreach (var file in Directory.EnumerateFiles(path, "*.osu", SearchOption.AllDirectories))
                        yield return file;
        }

        /// <summary>
        /// 判断文件路径是否为有效的 .osu 文件
        /// </summary>
        public static bool IsValidOsuFile(string? filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath) &&
                   Path.GetExtension(filePath).Equals(".osu", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 快速检查是否为有效的Mania谱面文件
        /// </summary>
        public static bool IsManiaBeatmap(string filePath)
        {
            try
            {
                // 只读取文件头部，检查Mode信息 (最多读取前20行)
                using var reader = new StreamReader(filePath);
                for (int lineCount = 0; lineCount < 20 && reader.ReadLine() is { } line; lineCount++)
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