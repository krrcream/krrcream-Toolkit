using System.Collections.Generic;
using System.Globalization;
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
            Options.Alignment.PropertyChanged += (_, _) => OnPropertyChanged(nameof(AlignDisplayText));
            Options.LNAlignment.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LNAlignDisplayText));
        }

        /// <summary>
        /// 获取对齐值的显示文本
        /// </summary>
        private string GetAlignDisplayText(double value, string prefix)
        {
            int key = (int)value;
            if (AlignValuesDict.TryGetValue(key, out var displayText))
            {
                return Strings.FormatLocalized(prefix, displayText);
            }
            return Strings.FormatLocalized(prefix, value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// 获取对齐值的显示文本（用于绑定）
        /// </summary>
        public string AlignDisplayText => GetAlignDisplayText(Options.Alignment.Value, Strings.KRRAlignLabel);

        /// <summary>
        /// 获取LN对齐值的显示文本（用于绑定）
        /// </summary>
        public string LNAlignDisplayText => GetAlignDisplayText(Options.LNAlignment.Value, Strings.KRRLNAlignLabel);

        public IToolOptions GetPreviewOptions() => Options;
    }
}