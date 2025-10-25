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
            foreach (string path in paths)
            {
                if (File.Exists(path) && Path.GetExtension(path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                    yield return path;
                else if (Directory.Exists(path))
                {
                    foreach (string file in Directory.EnumerateFiles(path, "*.osu", SearchOption.AllDirectories))
                        yield return file;
                }
            }
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
                        ReadOnlySpan<char> modeSpan = line.AsSpan(5).TrimStart(); // 去掉"Mode:"并去除前导空白

                        // 找到第一个非数字字符的位置
                        int numberEnd = 0;
                        while (numberEnd < modeSpan.Length && char.IsDigit(modeSpan[numberEnd])) numberEnd++;

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

        /// <summary>
        /// 限制文件路径长度不超过255个字符
        /// </summary>
        /// <param name="fullPath">完整文件路径</param>
        /// <returns>处理后的文件路径</returns>
        public static string Limit255PathLength(this string fullPath)
        {
            if (fullPath.Length <= 255)
                return fullPath;

            int excessLength = fullPath.Length - 255;
            string directory = Path.GetDirectoryName(fullPath);
            string fileName = Path.GetFileName(fullPath);

            // 减掉文件名开头的第0到excessLength+3个字符，然后在文件名开头加".."
            if (excessLength + 3 < fileName.Length)
            {
                fileName = ".." + fileName.Substring(excessLength + 3);
                return Path.Combine(directory, fileName);
            }
            else
            {
                // 如果超出了太多以至于无法简单添加".."，则使用原来的处理方式
                string pathWithoutExtension = fullPath.Substring(0, fullPath.Length - 4);
                return pathWithoutExtension.Substring(0, 249) + "...osu";
            }
        }
    }
}
