using System.Collections.Generic;
using krrTools.Configuration;
using krrTools.Localization;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerViewModel : ToolViewModelBase<KRRLNTransformerOptions>, IPreviewOptionsProvider
    {
        // 节拍显示映射字典
        private static readonly Dictionary<int, string> AlignValuesDict = new Dictionary<int, string>
        {
            { 1, "1/16" },
            { 2, "1/8" },
            { 3, "1/7" },
            { 4, "1/6" },
            { 5, "1/5" },
            { 6, "1/4" },
            { 7, "1/3" },
            { 8, "1/2" },
            { 9, "1/1" }
        };

        public KRRLNTransformerViewModel(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, true, options)
        {
            Options.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AlignDisplayText));
        }

        /// <summary>
        /// 获取对齐值的显示文本
        /// </summary>
        public string GetAlignDisplayText(double value)
        {
            int key = (int)value;
            if (AlignValuesDict.TryGetValue(key, out var displayText))
            {
                return Strings.FormatLocalized(Strings.KRRAlignLabel, displayText);
            }
            return Strings.FormatLocalized(Strings.KRRAlignLabel, value.ToString());
        }

        /// <summary>
        /// 获取对齐值的显示文本（用于绑定）
        /// </summary>
        public string AlignDisplayText => GetAlignDisplayText(Options.AlignValue);

        public IToolOptions GetPreviewOptions() => Options;
    }
}