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
            { 1, "1/8" },
            { 2, "1/6" },
            { 3, "1/4" },
            { 4, "1/3" },
            { 5, "1/2" },
            { 6, "1/1" },
            { 7, "3/2" },
            { 8, "2/1" },
            { 9, "4/1" },
            { 10, "∞"} 
        };
        
        public KRRLNTransformerViewModel(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, true, options)
        {
        }

        public IToolOptions GetPreviewOptions() => new KRRLNTransformerOptions
        {
            Short = new KRRLNTransformerOptions.ShortSettings
            {
                PercentageValue = Options.Short.PercentageValue,
                LevelValue = Options.Short.LevelValue,
                LimitValue = Options.Short.LimitValue,
                RandomValue = Options.Short.RandomValue
            },
            Long = new KRRLNTransformerOptions.LongSettings
            {
                PercentageValue = Options.Long.PercentageValue,
                LevelValue = Options.Long.LevelValue,
                LimitValue = Options.Long.LimitValue,
                RandomValue = Options.Long.RandomValue
            },
            LengthThreshold = new KRRLNTransformerOptions.LengthThresholdSettings
            {
                Value = Options.LengthThreshold.Value
            },
            Alignment = new KRRLNTransformerOptions.AlignmentSettings
            {
                Value = Options.Alignment.Value
            },
            LNAlignment = new KRRLNTransformerOptions.LNAlignmentSettings
            {
                Value = Options.LNAlignment.Value
            },
            General = new KRRLNTransformerOptions.GeneralSettings
            {
                ProcessOriginalIsChecked = Options.General.ProcessOriginalIsChecked,
                ODValue = Options.General.ODValue
            },
            Seed = Options.Seed
        };
    }
}