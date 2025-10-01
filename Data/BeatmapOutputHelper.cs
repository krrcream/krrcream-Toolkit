using System;
using System.IO;
using OsuParsers.Beatmaps;

namespace krrTools.Data
{
    /// <summary>
    /// 通用的Beatmap输出助手类，提供标准化的文件写入方法
    /// </summary>
    public static class BeatmapOutputHelper
    {
        /// <summary>
        /// 将Beatmap写入到指定文件路径（安全版本，处理路径长度、目录创建、文件冲突）
        /// 如果提供filename，则path视为目录，filename为文件名
        /// </summary>
        /// <param name="beatmap">要写入的Beatmap对象</param>
        /// <param name="path">输出目录或完整路径</param>
        /// <param name="filename">文件名（可选，如果提供则path为目录）</param>
        /// <returns>是否写入成功</returns>
        public static bool SaveBeatmapToFile(Beatmap beatmap, string path, string? filename = null)
        {
            string outputPath = filename != null ? Path.Combine(path, filename) : path;

            try
            {
                // 处理路径过长问题：截断文件名而不是跳过
                if (outputPath.Length > 255)
                {
                    string directory = Path.GetDirectoryName(outputPath) ?? "";
                    string fileName = Path.GetFileName(outputPath);
                    string filenameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);

                    int maxFilenameLength = 255 - directory.Length - 1 - extension.Length; // 1 for backslash
                    if (maxFilenameLength > 3 && filenameWithoutExt.Length > maxFilenameLength)
                    {
                        filenameWithoutExt = filenameWithoutExt.Substring(0, maxFilenameLength - 3) + "...";
                        fileName = filenameWithoutExt + extension;
                        outputPath = Path.Combine(directory, fileName);
                        Console.WriteLine($"路径过长，已截断文件名: {outputPath}");
                    }
                    else
                    {
                        Console.WriteLine($"输出路径过长，无法处理: {outputPath}");
                        return false;
                    }
                }

                // 确保输出目录存在
                string? directoryPath = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // 处理文件冲突：如果存在旧转换文件，删除它
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    Console.WriteLine($"删除旧转换文件: {outputPath}");
                }

                // 使用beatmap.Save方法写入文件
                beatmap.Save(outputPath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入Beatmap文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 生成标准化的输出文件路径
        /// </summary>
        /// <param name="inputPath">输入文件路径</param>
        /// <param name="suffix">文件名后缀</param>
        /// <returns>输出文件路径</returns>
        public static string GenerateOutputPath(string inputPath, string suffix)
        {
            string directory = Path.GetDirectoryName(inputPath) ?? ".";
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(inputPath);
            string extension = Path.GetExtension(inputPath);

            return Path.Combine(directory, $"{fileNameWithoutExtension}_{suffix}{extension}");
        }

        /// <summary>
        /// 将Beatmap转换为字符串（用于调试或预览）
        /// </summary>
        /// <param name="beatmap">Beatmap对象</param>
        /// <returns>Beatmap的字符串表示</returns>
        public static string BeatmapToString(Beatmap beatmap)
        {
            return beatmap.ToString() ?? string.Empty;
        }
    }
}