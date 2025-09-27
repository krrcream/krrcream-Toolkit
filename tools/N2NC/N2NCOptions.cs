using System.Collections.Generic;
using krrTools.tools.Shared;

namespace krrTools.tools.N2NC
{
    /// <summary>
    /// 转换选项类，用于封装所有转换参数
    /// </summary>
    public class N2NCOptions : IToolOptions
    {
        public double TargetKeys { get; set; }

        public double MaxKeys { get; set; }

        public double MinKeys { get; set; }

        public double TransformSpeed { get; set; }

        public int? Seed { get; set; }

        public List<int>? SelectedKeyTypes { get; set; }

        public KeySelectionFlags? SelectedKeyFlags { get; set; }

        public PresetKind SelectedPreset { get; set; } = PresetKind.Default;

        public void Validate()
        {
            if (TransformSpeed <= 0) TransformSpeed = 1.0;
        }
    }
}