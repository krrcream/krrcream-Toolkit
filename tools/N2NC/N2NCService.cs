using System;
using System.Windows;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;

namespace krrTools.tools.N2NC
{
    public static class N2NCService
    {
        public static string? ProcessSingleFile(string filePath, N2NCOptions options, bool openOsz = false)
        {
            try
            {
                if (!FileProcessingHelper.EnsureIsOsuFile(filePath)) return null;

                var converter = new N2NC { options = options };
                string newFilepath = converter.NToNC(filePath);

                try
                {
                    if (ListenerControl.IsOpen)
                    {
                        var oszPath = OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath, openOsz);
                        return oszPath;
                    }
                    else
                    {
                        return newFilepath;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "打包/添加谱面失败: " : "Packaging/adding beatmap failed: ") + ex.Message, 
                        SharedUIComponents.IsChineseLanguage() ? "错误|Error" : "Error|错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "处理文件时出错: " : "Error processing file: ") + ex.Message, 
                    SharedUIComponents.IsChineseLanguage() ? "处理错误|Processing Error" : "Processing Error|处理错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
