using System.IO;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// 统一的Beatmap调度器，负责从文件加载Beatmap
    /// </summary>
    public static class BeatmapScheduler
    {
        /// <summary>
        /// 从文件路径获取Beatmap对象
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>Beatmap对象，失败返回null</returns>
        public static Beatmap? GetBeatmapFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                return BeatmapDecoder.Decode(filePath);
            }
            catch
            {
                return null;
            }
        }
    }
}