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
        public const double HEADER_FONT_SIZE = 18.0;
        public const double COMMON_FONT_SIZE = 16.0;

        // 颜色和画刷
        public static readonly Brush UI_TEXT_BRUSH = new SolidColorBrush(Color.FromArgb(255, 33, 33, 33));
        public static readonly Brush PANEL_BORDER_BRUSH = new SolidColorBrush(Color.FromArgb(51, 0, 0, 0)); // subtle dark border ~20%

        // 面板样式
        public static readonly CornerRadius PANEL_CORNER_RADIUS = new CornerRadius(8);
        public static readonly Thickness PANEL_PADDING = new Thickness(8);
    }
}
