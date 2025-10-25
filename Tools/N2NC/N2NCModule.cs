using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;
using System;

namespace krrTools.Tools.N2NC
{
    /// <summary>
    /// N2NC转换模块
    /// </summary>
    public class N2NCModule : ToolModuleBase<N2NCOptions, N2NCViewModel, N2NCView>
    {
        public override ToolModuleType ModuleType => ToolModuleType.N2NC;

        public override string DisplayName => Strings.TabN2NC;

        /// <summary>
        /// 应用转换到谱面（内部实现）- 获取最新的运行时设置
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        /// <returns>true如果发生了实际转换，false如果没有变化</returns>
        protected override bool ApplyToBeatmapInternal(Beatmap beatmap)
        {
            // 获取最新的选项设置 - 响应式系统实时更新
            N2NCOptions options = GetLatestOptions();

            // 判断是否需要转换
            bool willTransform = WillTransformOccur(beatmap, options);

            if (!willTransform) return false; // 不需要转换，直接返回

            // 执行转换
            var transformer = new N2NC();
            transformer.TransformBeatmap(beatmap, options);
            return true;
        }

        /// <summary>
        /// 判断根据当前选项是否会发生实际转换
        /// </summary>
        private bool WillTransformOccur(Beatmap beatmap, N2NCOptions options)
        {
            // 检查KeySelectionFlags
            KeySelectionFlags? keyFlags = options.SelectedKeyFlags;

            if (keyFlags.HasValue && keyFlags.Value != KeySelectionFlags.None)
            {
                int AlignmentPreProcessCS = Math.Clamp((int)beatmap.DifficultySection.CircleSize - 3, 0, 8);
                bool isSelected = ((int)keyFlags.Value & (1 << AlignmentPreProcessCS)) != 0;
                if (!isSelected) return false;
            }
            if ((int)options.MaxKeys.Value == (int)beatmap.DifficultySection.CircleSize 
                && (int)options.TargetKeys.Value == (int)beatmap.DifficultySection.CircleSize)
                return false;    
            return true;
        }
    }
}
