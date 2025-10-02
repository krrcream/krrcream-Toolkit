using System.Windows.Media;

namespace krrTools.Tools.Preview
{
    internal static class PreviewConstants
    {
        // 画布 / 布局相关
        public const double MaxContentWidth = 810.0;                 // 内容最大宽度
        public const double CanvasLeftPadding = 0;               // 画布左侧保留区域（用于标签 / 边距）

        // 单列（lane）尺寸限制
        public const double LaneMinWidth = 20.0;                     // 单列最小宽度
        public const double LaneMaxWidth = 45.0;                    // 单列最大宽度（用户需求）

        // 垂直尺寸 / 时间映射相关
        public const double MapBase = 1.0;                         // 地图顶部偏移基准
        public const double CanvasMinHeight = 150.0;                // 画布最小高度

        // 其它视觉默认值
        public const double NoteSideMargin = 1;                   // note 到列两侧的间距（每侧）
        public const double NoteFixedHeight = 8;                 // note 的固定高度（px）

        // 颜色/刷子（集中管理，方便修改）
        public static readonly SolidColorBrush TapNoteBrush = new SolidColorBrush(Color.FromArgb(255, 70, 140, 235));      // 普通 Tap 颜色
        public static readonly SolidColorBrush HoldHeadBrush = new SolidColorBrush(Color.FromArgb(255, 110, 165, 255));    // Hold 的头部颜色
        public static readonly SolidColorBrush HoldBodyBrush = new SolidColorBrush(Color.FromArgb(255, 160, 200, 255));    // Hold 的延伸体颜色

        // 新增：统一的画笔/颜色
        public static readonly SolidColorBrush OutlineBrush = new SolidColorBrush(Color.FromArgb(255, 40, 90, 180));
        public static readonly SolidColorBrush LaneEvenBrush = new SolidColorBrush(Color.FromArgb(255, 235, 240, 247));
        public static readonly SolidColorBrush LaneOddBrush = new SolidColorBrush(Color.FromArgb(255, 229, 236, 244));
        public static readonly SolidColorBrush QuarterLineBrush = new SolidColorBrush(Color.FromArgb(255, 220, 225, 230));

        // UI文本颜色
        public static readonly SolidColorBrush UiHintTextBrush = new SolidColorBrush(Color.FromArgb(255, 51, 51, 51));
        public static readonly SolidColorBrush UiSecondaryTextBrush = new SolidColorBrush(Color.FromArgb(255, 90, 99, 112));

        // 预览窗口相关（集中管理），读取开始时间后，向后读取 N 个单位长度的时长
        public const int PreviewWindowUnitCount = 16; // N，单位个数（默认 8）
        // 单位的节拍分母，4 表示 1/4 拍（四分之一拍），8 表示 1/8 拍
        public const int PreviewWindowUnitBeatDenominator = 4;
        // 切片窗口的最小长度（毫秒），用于避免零长或负长窗口
        public const int MinWindowLengthMs = 1;

        // 静态构造用于 Freeze 刷子，减少运行时分配并提升 WPF 性能
        static PreviewConstants()
        {
            TapNoteBrush.Freeze();
            HoldHeadBrush.Freeze();
            HoldBodyBrush.Freeze();

            OutlineBrush.Freeze();
            LaneEvenBrush.Freeze();
            LaneOddBrush.Freeze();
            QuarterLineBrush.Freeze();
        }
    }
}
