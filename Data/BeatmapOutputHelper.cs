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
        /// 将Beatmap写入到指定文件路径
        /// </summary>
        /// <param name="beatmap">要写入的Beatmap对象</param>
        /// <param name="outputPath">输出文件路径</param>
        /// <returns>是否写入成功</returns>
        public static bool WriteBeatmapToFile(Beatmap beatmap, string outputPath)
        {
            try
            {
                // 确保输出目录存在
                string? directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用ToString()方法写入文件（与KRRLNTool保持一致）
                File.WriteAllText(outputPath, beatmap.ToString());
                return true;
            }
            catch (Exception ex)
            {
                // 可以添加日志记录
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