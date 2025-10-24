using krrTools.Core;
using krrTools.Localization;
using OsuParsers.Beatmaps;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerModule : ToolModuleBase<KRRLNTransformerOptions, KRRLNTransformerViewModel, KRRLNTransformerView>
    {
        public override ToolModuleType ModuleType
        {
            get => ToolModuleType.KRRLN;
        }

        public override string DisplayName
        {
            get => Strings.TabKRRsLN;
        }

        /// <summary>
        /// 应用转换到谱面（内部实现）- 获取最新的运行时设置
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        /// <returns>true如果发生了实际转换，false如果没有变化</returns>
        protected override bool ApplyToBeatmapInternal(Beatmap beatmap)
        {
            // 获取最新的选项设置 - 响应式系统实时更新
            KRRLNTransformerOptions options = GetLatestOptions();

            // 判断是否需要转换
            bool willTransform = WillTransformOccur(beatmap, options);

            if (!willTransform) return false; // 不需要转换，直接返回

            // 执行转换
            var transformer = new KRRLN();
            transformer.TransformBeatmap(beatmap, options);
            return true;
        }

        /// <summary>
        /// 判断根据当前选项是否会发生实际转换
        /// </summary>
        private bool WillTransformOccur(Beatmap beatmap, KRRLNTransformerOptions options)
        {
            // 检查是否需要生成LN
            bool needLN = options.LongPercentage.Value > 0 || options.ShortPercentage.Value > 0;

            // 检查OD是否需要改变
            bool needOD = options.ODValue.Value.HasValue && Math.Abs(options.ODValue.Value.Value - beatmap.DifficultySection.OverallDifficulty) > 0.01;

            return needLN || needOD;
        }
    }
}
