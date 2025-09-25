// filepath: e:\BASE CODE\GitHub\krrcream-Toolkit\tools\Shared\MessageBoxResultHelper.cs
using System.Windows;

namespace krrTools.Tools.Shared
{
    internal static class MessageBoxResultHelper
    {
        public static void TryShowSuccess(bool isChinese)
        {
            // Central place to show success; can be adjusted to suppress/pop notifications later
            MessageBox.Show(isChinese ? "文件处理成功！" : "File processed successfully!",
                isChinese ? "成功" : "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

