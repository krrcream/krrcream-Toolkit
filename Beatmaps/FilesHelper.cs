using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace krrTools.Beatmaps
{
    public static class FilesHelper
    {
        private class Win32Window(IntPtr handle) : System.Windows.Forms.IWin32Window
        {
            public IntPtr Handle { get; } = handle;
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
        public static string ShowFolderBrowserDialog(string description)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = description;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;
            if (Application.Current?.MainWindow != null)
            {
                var hwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                dialog.ShowDialog(new Win32Window(hwnd));
            }
            else
            {
                dialog.ShowDialog();
            }

            return dialog.SelectedPath;
        }

        /// <summary>
        /// 弹出选择文件夹对话框，使用指定的窗口作为父窗口
        /// </summary>
        public static string ShowFolderBrowserDialog(string description, Window? owner)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = description;
            dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            dialog.ShowNewFolderButton = true;

            if (owner != null)
            {
                var hwnd = new WindowInteropHelper(owner).Handle;
                dialog.ShowDialog(new Win32Window(hwnd));
            }
            else if (Application.Current?.MainWindow != null)
            {
                var hwnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
                dialog.ShowDialog(new Win32Window(hwnd));
            }
            else
            {
                dialog.ShowDialog();
            }

            return dialog.SelectedPath;
        }
    }
}