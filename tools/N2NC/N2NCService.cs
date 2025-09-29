using System;
using System.Windows;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.tools.N2NC
{
    public static class N2NCService
    {
        private static readonly ILogger _logger = LoggerFactoryHolder.CreateLogger<string>();

        public static Beatmap? ProcessSingleFile(string filePath, N2NCOptions options, bool openOsz = false)
        {
            try
            {
                _logger.LogInformation("转换器读取转换: {FilePath}", filePath);

                if (!FilesHelper.EnsureIsOsuFile(filePath)) 
                {
                    _logger.LogWarning("文件不是有效的.osu文件: {FilePath}", filePath);
                    return null;
                }

                var converter = new N2NC { options = options };
                var beatmap = converter.NToNC(filePath);
                _logger.LogInformation("转换器处理完成");

                return beatmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理文件时出错");
                MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "处理文件时出错: " : "Error processing file: ") + ex.Message, 
                    SharedUIComponents.IsChineseLanguage() ? "处理错误|Processing Error" : "Processing Error|处理错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// 处理Beatmap对象并返回转换后的Beatmap（用于连续处理）
        /// </summary>
        public static OsuParsers.Beatmaps.Beatmap? ProcessBeatmap(OsuParsers.Beatmaps.Beatmap beatmap, N2NCOptions options)
        {
            try
            {
                _logger.LogInformation("转换器处理Beatmap对象");

                var converter = new N2NC { options = options };
                var resultBeatmap = converter.NToNCToData(beatmap);
                _logger.LogInformation("Beatmap转换完成");

                return resultBeatmap;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理Beatmap时出错");
                return null;
            }
        }
    }
}
