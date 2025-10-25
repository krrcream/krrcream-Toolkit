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
            bool willTransform = WillTransformOccur(options);

            if (!willTransform) return false; // 不需要转换，直接返回

            // 执行转换
            var transformer = new KRRLN();
            transformer.TransformBeatmap(beatmap, options);
            return true;
        }

        /// <summary>
        /// 判断根据当前选项是否会发生实际转换
        /// </summary>
        private bool WillTransformOccur(KRRLNTransformerOptions options)
        {
            // 如果长按和短按百分比都是0，不会生成任何LN
            if (options.LongPercentage.Value <= 0 && options.ShortPercentage.Value <= 0)
            {
                // 还需要检查OD是否会改变
                return options.ODValue.Value.HasValue;
            }

            return true;
        }
    }
}
