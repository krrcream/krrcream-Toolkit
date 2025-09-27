using System.Collections.Generic;
using krrTools.tools.Shared;

namespace krrTools.tools.N2NC
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class N2NCOptions : IToolOptions
    {
        /// <summary>
        /// 目标键数
        /// </summary>
        public double TargetKeys { get; set; }

        /// <summary>
        /// 最大键数权重
        /// </summary>
        public double MaxKeys { get; set; }

        /// <summary>
        /// 最小键数权重
        /// </summary>
        public double MinKeys { get; set; }

        /// <summary>
        /// 变换速度（实际数值，保留以兼容现有调用）
        /// </summary>
        public double TransformSpeed { get; set; }

        /// <summary>
        /// 种子值
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// 键数筛选器 - 选中的键数类型
        /// </summary>
        public List<int>? SelectedKeyTypes { get; set; }

        /// <summary>
        /// 复合的键数选择 flags
        /// </summary>
        public KeySelectionFlags? SelectedKeyFlags { get; set; }

        /// <summary>
        /// 选中的预设
        /// </summary>
        public PresetKind SelectedPreset { get; set; } = PresetKind.Default;

        public void Validate()
        {
            if (TransformSpeed <= 0) TransformSpeed = 1.0;
        }
    }
}