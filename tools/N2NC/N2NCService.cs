using System;
using System.Windows;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;
using Microsoft.Extensions.Logging;

namespace krrTools.tools.N2NC
{
    public static class N2NCService
    {
        private static readonly ILogger _logger = LoggerFactoryHolder.CreateLogger<string>();

        public static string? ProcessSingleFile(string filePath, N2NCOptions options, bool openOsz = false)
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
                string newFilepath = converter.NToNC(filePath);
                _logger.LogInformation("转换器创建文件: {NewFilePath}", newFilepath);

                try
                {
                    if (ListenerControl.IsOpen)
                    {
                        var oszPath = OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath, openOsz);
                        _logger.LogInformation("添加到歌曲文件夹: {OszPath}", oszPath);
                        return oszPath;
                    }
                    else
                    {
                        _logger.LogInformation("返回文件路径: {NewFilePath}", newFilepath);
                        return newFilepath;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "打包/添加谱面失败");
                    MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "打包/添加谱面失败: " : "Packaging/adding beatmap failed: ") + ex.Message, 
                        SharedUIComponents.IsChineseLanguage() ? "错误|Error" : "Error|错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
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
    }
}
