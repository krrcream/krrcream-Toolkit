using System.Collections.Generic;
using krrTools.Configuration;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerViewModel : ToolViewModelBase<KRRLNTransformerOptions>, IPreviewOptionsProvider
    {
        // 节拍显示映射字典
        public static readonly Dictionary<double, string> AlignValuesDict = new Dictionary<double, string>
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

        public static readonly Dictionary<double, string> LengthThresholdDict = new Dictionary<double, string>
        {
            { 0, "Off" },
            { 1, "1/16" },
            { 2, "1/8" },
            { 3, "1/7" },
            { 4, "1/6" },
            { 5, "1/5" },
            { 6, "1/4" },
            { 7, "1/3" },
            { 8, "1/2" },
            { 9, "1/1" },
            { 10, "2/1" },
            { 11, "3/1" },
            { 12, "4/1" }
        };
        
        public KRRLNTransformerViewModel(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, true, options)
        {
        }

        public IToolOptions GetPreviewOptions() => Options;
    }
}