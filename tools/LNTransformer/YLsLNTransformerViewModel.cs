using krrTools.tools.Shared;

namespace krrTools.tools.LNTransformer
{
    public class YLsLNTransformerViewModel : ToolViewModelBase<YLsLNTransformerOptions>
    {
        public YLsLNTransformerViewModel() : base(OptionsManager.YLsLNToolName, autoSave: false)
        {
            // LN tool doesn't auto-save, only loads
        }

        /// <summary>
        /// 获取 LN 预览参数
        /// </summary>
        public YLsLNTransformerCore.LNPreviewParameters GetLNPreviewParameters()
        {
            return new YLsLNTransformerCore.LNPreviewParameters
            {
                LevelValue = Options.LevelValue,
                PercentageValue = Options.PercentageValue,
                DivideValue = Options.DivideValue,
                ColumnValue = Options.ColumnValue,
                GapValue = Options.GapValue,
                OriginalLN = Options.OriginalLNIsChecked,
                FixError = Options.FixErrorIsChecked,
                OverallDifficulty = Options.OverallDifficulty
            };
        }
    }
}