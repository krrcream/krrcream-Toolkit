using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;
using IWin32Window = System.Windows.Forms.IWin32Window;

namespace krrTools.Beatmaps
{
    public static class FilesHelper
    {
        private class Win32Window(IntPtr handle) : IWin32Window
        {
            public IntPtr Handle { get; } = handle;
        }

        /// <summary>
        /// 弹出选择文件夹对话框，使用指定的窗口作为父窗口
        /// </summary>
        public static string ShowFolderBrowserDialog(string description, Window? owner = null)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.Description = description;
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