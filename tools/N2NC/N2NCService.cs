using System;
using System.Windows;
using krrTools.tools.Listener;
using krrTools.Tools.OsuParser;
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
                    MessageBox.Show($"Packaging/adding beatmap failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error processing file: {ex.Message}", "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }
    }
}
