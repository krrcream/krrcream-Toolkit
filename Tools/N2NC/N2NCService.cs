using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using krrTools.Data;
using krrTools.UI;

namespace krrTools.Tools.N2NC
{
    // TODO: 文件处理放在外部，这个文件未来要移除
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
        public static Beatmap ProcessBeatmap(Beatmap beatmap, N2NCOptions options)
        {
            var converter = new N2NC { options = options };
            var resultBeatmap = converter.NToNCToData(beatmap);
            return resultBeatmap;
        }
    }
}
