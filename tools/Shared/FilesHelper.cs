using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using krrTools.tools.Listener;
using System.Windows.Forms;
using krrTools.Tools.OsuParser;
using krrTools.tools.Shared;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace krrTools.Tools.Shared
{
    public static class FilesHelper
    {
        public static void ValidateAndRunWithPackaging(string filePath, Func<string, string?> processor, bool openOsz = false, Action? onCompleted = null, bool showSuccessMessage = true)
        {
            if (!EnsureIsOsuFile(filePath)) return;

            Task.Run(() =>
            {
                try
                {
                    string? produced = processor(filePath);

                    if (!string.IsNullOrEmpty(produced))
                    {
                        try
                        {
                            if (ListenerControl.IsOpen)
                            {
                                OsuAnalyzer.AddNewBeatmapToSongFolder(produced, openOsz);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Packaging failure: inform user and log
                            Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                            {
                                MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "打包/添加谱面失败: " : "Packaging/adding beatmap failed: ") + ex.Message,
                                    SharedUIComponents.IsChineseLanguage() ? "错误|Error" : "Error|错误",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            }));
                            Debug.WriteLine($"Packaging/adding beatmap failed: {ex.Message}");
                        }
                    }

                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (showSuccessMessage)
                        {
                            MessageBoxResultHelper.TryShowSuccess(SharedUIComponents.IsChineseLanguage());
                        }
                        onCompleted?.Invoke();
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "处理文件时出错: " : "Error processing file: ") + ex.Message,
                            SharedUIComponents.IsChineseLanguage() ? "处理错误|Processing Error" : "Processing Error|处理错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        onCompleted?.Invoke();
                    }));
                    Debug.WriteLine($"ValidateAndRunWithPackaging error: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Shows a dialog to select a file or folder.
        /// </summary>
        public static string? ShowOpenFileOrFolderDialog(string title)
        {
            var dialog = new OpenFileDialog
            {
                Title = title,
                CheckFileExists = false,
                CheckPathExists = true
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// Shows a save file dialog.
        /// </summary>
        public static string? ShowSaveFileDialog(string title, string filter, string defaultExt)
        {
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                DefaultExt = defaultExt
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        /// <summary>
        /// 弹出选择文件夹对话框
        /// </summary>
        public static string? ShowFolderBrowserDialog(string description)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = description;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;
            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }
        
        /// <summary>
        /// 判断文件路径是否为有效的 .osu 文件
        /// </summary>
        public static bool EnsureIsOsuFile(string? filePath)
        {
            return !string.IsNullOrEmpty(filePath) && File.Exists(filePath) && Path.GetExtension(filePath).Equals(".osu", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 统计路径集合中的 .osu 文件数量（包括 .osz 压缩包）
        /// </summary>
        public static int GetOsuFilesCount(IEnumerable<string> paths)
        {
            int count = 0;
            foreach (var path in paths)
            {
                if (File.Exists(path))
                {
                    var ext = Path.GetExtension(path);
                    if (string.Equals(ext, ".osu", StringComparison.OrdinalIgnoreCase))
                        count++;
                    else if (string.Equals(ext, ".osz", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var archive = ZipFile.OpenRead(path);
                            count += archive.Entries.Count(e => e.Name.EndsWith(".osu", StringComparison.OrdinalIgnoreCase));
                        }
                        catch
                        {
                            Debug.WriteLine($"Error opening .osz file: {path}");
                        }
                    }
                }
                else if (Directory.Exists(path))
                {
                    count += Directory.EnumerateFiles(path, "*.osu", SearchOption.AllDirectories).Count();
                }
            }
            return count;
        }

        /// <summary>
        /// 遍历路径集合中的所有 .osu 文件
        /// </summary>
        public static IEnumerable<string> EnumerateOsuFiles(IEnumerable<string> paths)
        {
            foreach (var path in paths)
            {
                if (File.Exists(path) && Path.GetExtension(path).Equals(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    yield return path;
                }
                else if (Directory.Exists(path))
                {
                    foreach (var file in Directory.EnumerateFiles(path, "*.osu", SearchOption.AllDirectories))
                        yield return file;
                }
            }
        }
        
        public static Beatmap GetManiaBeatmap(string? filePath)
        {
            if (!EnsureIsOsuFile(filePath))
                throw new FileNotFoundException($"Invalid or missing .osu file: {filePath}");
            
            var beatmap = BeatmapDecoder.Decode(filePath);
            
            if (beatmap.GeneralSection.ModeId != 3)
                throw new ArgumentException("Beatmap is not in Mania mode (ModeId != 3)");
            
            return beatmap;
        }

        /// <summary>
        /// 验证并异步处理 .osu 文件
        /// </summary>
        public static void ValidateAndRun(string filePath, Action<string> action, Action? onCompleted = null, bool showSuccessMessage = true)
        {
            if (!FilesHelper.EnsureIsOsuFile(filePath)) return;
            Task.Run(() =>
            {
                try
                {
                    action(filePath);
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (showSuccessMessage)
                        {
                            MessageBoxResultHelper.TryShowSuccess(SharedUIComponents.IsChineseLanguage());
                        }
                        onCompleted?.Invoke();
                    }));
                }
                catch (Exception ex)
                {
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show((SharedUIComponents.IsChineseLanguage() ? "处理文件时出错: " : "Error processing file: ") + ex.Message,
                            SharedUIComponents.IsChineseLanguage() ? "处理错误|Processing Error" : "Processing Error|处理错误",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        onCompleted?.Invoke();
                    }));
                    Debug.WriteLine($"ValidateAndRun processing error: {ex.Message}");
                }
            });
        }
    }
}
