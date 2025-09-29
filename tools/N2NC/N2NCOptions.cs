using System.Collections.Generic;
using krrTools.tools.Shared;

namespace krrTools.tools.N2NC
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class N2NCOptions : IToolOptions
    {
        public double TargetKeys { get; set; } = 10;

        public double MaxKeys { get; set; } = 10;

        public double MinKeys { get; set; } = 2;

        public double TransformSpeed { get; set; } = 1.0;

        public int? Seed { get; set; } = 114514;

        public List<int>? SelectedKeyTypes { get; set; }

        public KeySelectionFlags? SelectedKeyFlags { get; set; } = KeySelectionFlags.None;

        public PresetKind SelectedPreset { get; set; } = PresetKind.Default;

        public void Validate()
        {
            if (TransformSpeed <= 0) TransformSpeed = 1.0;
        }
    }
}