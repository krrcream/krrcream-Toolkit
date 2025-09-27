using krrTools.tools.Preview;
using krrTools.tools.Shared;

namespace krrTools.tools.LNTransformer
{
    public class LNTransformerViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public LNTransformerViewModel()
        {
            // Try to load saved options; if none, keep defaults
            try
            {
                var saved = OptionsManager.LoadOptions<LNTransformerOptions>(OptionsManager.LNToolName, OptionsManager.ConfigFileName);
                if (saved != null)
                {
                    Options = saved;
                }
            }
            catch
            {
                // best-effort load; ignore errors
            }
        }

        private bool _isProcessing;
        private LNTransformerOptions _options = new LNTransformerOptions();

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        /// <summary>
        /// LN Transformer 工具选项
        /// </summary>
        public LNTransformerOptions Options
        {
            get => _options;
            set => SetProperty(ref _options, value);
        }

        /// <summary>
        /// 获取 LN 预览参数
        /// </summary>
        public LNTransformerCore.LNPreviewParameters GetLNPreviewParameters()
        {
            return new LNTransformerCore.LNPreviewParameters
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