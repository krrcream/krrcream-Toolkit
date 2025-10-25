using System.Collections.Generic;

namespace krrTools.Configuration
{
    /// <summary>
    /// 统一的滑条配置系统 - 解决滑条使用不统一的问题
    /// 每个工具自定义自己的映射字典，通过字段引用使用
    /// </summary>
    public class SliderConfiguration
    {
        /// <summary>
        /// 显示值映射 - 滑条值 -> 显示文本
        /// 例如：1 -> "1/16", 2 -> "1/8"
        /// </summary>
        public Dictionary<double, string>? DisplayValueMap { get; set; }

        /// <summary>
        /// 实际值映射 - 滑条值 -> 实际使用值
        /// 例如：滑条设置1，实际返回0.025
        /// </summary>
        public Dictionary<double, double>? ActualValueMap { get; set; }

        /// <summary>
        /// 是否启用勾选框
        /// </summary>
        public bool HasCheckBox { get; set; }

        /// <summary>
        /// 勾选框默认状态
        /// </summary>
        public bool CheckBoxDefaultState { get; set; }

        /// <summary>
        /// 勾选框标签键（本地化）
        /// </summary>
        public string? CheckBoxLabelKey { get; set; }

        /// <summary>
        /// 滑条最小值
        /// </summary>
        public double Min { get; set; } = 0.0;

        /// <summary>
        /// 滑条最大值
        /// </summary>
        public double Max { get; set; } = 100.0;

        /// <summary>
        /// 滑条步长
        /// </summary>
        public double Step { get; set; } = 1.0;

        /// <summary>
        /// 键盘步长
        /// </summary>
        public double KeyboardStep { get; set; } = 1.0;

        /// <summary>
        /// 是否启用刻度对齐
        /// </summary>
        public bool SnapToTick { get; set; } = true;
    }
}
