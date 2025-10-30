using System.Windows;
using Microsoft.Win32;

namespace krrTools.Beatmaps
{
    public static class FilesHelper
    {
        /// <summary>
        /// 弹出选择文件夹对话框，使用指定的窗口作为父窗口
        /// </summary>
        public static string ShowFolderBrowserDialog(string description, Window? owner = null)
        {
            var dialog = new OpenFolderDialog
            {
                Title = description
            };

            Window? parentWindow = owner ?? Application.Current?.MainWindow;
            if (parentWindow != null && dialog.ShowDialog(parentWindow) == true) return dialog.FolderName;

            return string.Empty;
        }
    }
}
