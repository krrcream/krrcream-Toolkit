// filepath: e:\BASE CODE\GitHub\krrcream-Toolkit\tools\Shared\MessageBoxResultHelper.cs
using System.Windows;
using krrTools.Localization;

namespace krrTools.Utilities
{
    internal static class MessageBoxResultHelper
    {
        public static void ShowSuccess()
        {
            // Central place to show success; can be adjusted to suppress/pop notifications later
            MessageBox.Show(
                Strings.FileProcessedSuccessfully.Localize(),
                Strings.Success.Localize(),
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
        }
    }
}

