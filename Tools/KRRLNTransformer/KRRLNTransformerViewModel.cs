using krrTools.Bindable;
using krrTools.Configuration;
using krrTools.Core;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerViewModel : ToolViewModelBase<KRRLNTransformerOptions>, IPreviewOptionsProvider
    {
        private readonly IEventBus _eventBus;

        public KRRLNTransformerViewModel(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, true, options)
        {
            _eventBus = App.Services.GetRequiredService<IEventBus>();
            
            // 监听 LengthThreshold 属性变化
            options.LengthThreshold.PropertyChanged += OnLengthThresholdChanged;
        }

        private void OnLengthThresholdChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Bindable<double?>.Value))
            {
                // 通知 ShortLevelMax 属性更新
                OnPropertyChanged(nameof(ShortLevelMaximum));
                
                // 如果 ShortLevel 的值超过了新的最大值，则调整为最大值
                if (Options.ShortLevel.Value > ShortLevelMaximum)
                {
                    Options.ShortLevel.Value = ShortLevelMaximum;
                }
            }
        }

        // ShortLevel 的最大值绑定到 LengthThreshold 的值
        public double ShortLevelMaximum => (Options.LengthThreshold.Value * 4) > 256 ? 256 : Options.LengthThreshold.Value * 4;

        protected override void TriggerPreviewRefresh()
        {
            // 取消注释可以启动高频刷新，实时看面尾尺寸变化
            // （对影响无影响，但是暂时有频闪问题，尚未解决）
            // 默认是防抖刷新
            // _eventBus.Publish(new PreviewRefreshEvent { NewValue = true });
        }

        public IToolOptions GetPreviewOptions()
        {
            var previewOptions = new KRRLNTransformerOptions();
            previewOptions.ShortPercentage.Value = Options.ShortPercentage.Value;
            previewOptions.ShortLevel.Value = Options.ShortLevel.Value;
            previewOptions.ShortLimit.Value = Options.ShortLimit.Value;
            previewOptions.ShortRandom.Value = Options.ShortRandom.Value;

            previewOptions.LongPercentage.Value = Options.LongPercentage.Value;
            previewOptions.LongLevel.Value = Options.LongLevel.Value;
            previewOptions.LongLimit.Value = Options.LongLimit.Value;
            previewOptions.LongRandom.Value = Options.LongRandom.Value;

            previewOptions.LengthThreshold.Value = Options.LengthThreshold.Value;
            previewOptions.Alignment.Value = Options.Alignment.Value;
            previewOptions.LNAlignment.Value = Options.LNAlignment.Value;

            previewOptions.ProcessOriginalIsChecked.Value = Options.ProcessOriginalIsChecked.Value;
            previewOptions.ODValue.Value = Options.ODValue.Value;

            previewOptions.Seed.Value = Options.Seed.Value;

            return previewOptions;
        }
    }
}
