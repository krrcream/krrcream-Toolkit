using System;
using System.Windows;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
using krrTools.tools.Shared;
using krrTools.Tools.Shared;
using System.Diagnostics;

namespace krrTools.tools.N2NC
{
    public static class N2NCService
    {
        public static string? ProcessSingleFile(string filePath, N2NCOptions options, bool openOsz = false)
        {
            try
            {
                Debug.WriteLine($"N2NCService.ProcessSingleFile called with file: {filePath}");
                Debug.WriteLine($"Options: TargetKeys={options.TargetKeys}, TransformSpeed={options.TransformSpeed}, Seed={options.Seed}");

                if (!FilesHelper.EnsureIsOsuFile(filePath)) 
                {
                    Debug.WriteLine("File is not a valid .osu file");
                    return null;
                }

                var converter = new N2NC { options = options };
                string newFilepath = converter.NToNC(filePath);
                Debug.WriteLine($"NToNC returned: {newFilepath}");

                try
                {
                    if (ListenerControl.IsOpen)
                    {
                        var oszPath = OsuAnalyzer.AddNewBeatmapToSongFolder(newFilepath, openOsz);
                        Debug.WriteLine($"Added to song folder: {oszPath}");
                        return oszPath;
                    }
                    else
                    {
                        Debug.WriteLine($"Returning file path: {newFilepath}");
                        return newFilepath;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in packaging/adding: {ex.Message}");
                    MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "打包/添加谱面失败: " : "Packaging/adding beatmap failed: ") + ex.Message, 
                        SharedUIComponents.IsChineseLanguage() ? "错误|Error" : "Error|错误", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ProcessSingleFile: {ex.Message}");
                MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "处理文件时出错: " : "Error processing file: ") + ex.Message, 
                    SharedUIComponents.IsChineseLanguage() ? "处理错误|Processing Error" : "Processing Error|处理错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
