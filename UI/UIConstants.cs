using System.Windows;
using System.Windows.Media;

namespace krrTools.UI
{
    /// <summary>
    /// UI常量定义类，集中管理颜色、尺寸、字体等常量
    /// </summary>
    public static class UIConstants
    {
        // 字体大小
        public const double HeaderFontSize = 18.0;
        public const double CommonFontSize = 16.0;

        // 颜色和画刷
        public static readonly Brush UiTextBrush = new SolidColorBrush(Color.FromArgb(255, 33, 33, 33));
        public static readonly Brush PanelBorderBrush = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)); // subtle dark border ~20%

        // 面板样式
        public static readonly CornerRadius PanelCornerRadius = new CornerRadius(8);
        public static readonly Thickness PanelPadding = new Thickness(8);

        // 悬停和按下颜色
        public static readonly Color ButtonHoverColor = Color.FromArgb(255, 220, 220, 220);
        public static readonly Color ButtonPressedColor = Color.FromArgb(255, 180, 180, 180);
        public static readonly Color CloseButtonHoverColor = Color.FromArgb(255, 232, 17, 35);
        public static readonly Color CloseButtonPressedColor = Color.FromArgb(255, 200, 15, 30);
    }
}
