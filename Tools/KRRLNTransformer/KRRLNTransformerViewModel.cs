using System;
using System.Collections.Generic;
using System.ComponentModel;
using krrTools.Bindable;
using krrTools.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace krrTools.Tools.KRRLNTransformer
{
    public class KRRLNTransformerViewModel : ToolViewModelBase<KRRLNTransformerOptions>, IPreviewOptionsProvider
    {
        private readonly IEventBus _eventBus;

        // 节拍显示映射字典
        public static readonly Dictionary<double, string> AlignValuesDict = new()
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

        public static readonly Dictionary<double, string> LengthThresholdDict = new()
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
            { 10, "∞" }
        };

        public KRRLNTransformerViewModel(KRRLNTransformerOptions options) : base(ConverterEnum.KRRLN, true, options)
        {
            _eventBus = App.Services.GetRequiredService<IEventBus>();
            
            // Subscribe to all Bindable<T> property changes
            SubscribeToPropertyChanges();
        }

        private void SubscribeToPropertyChanges()
        {
            // Bindable<T> properties automatically handle change notifications
            // No manual subscription needed for UI updates
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // Forward property changes to trigger SettingsChangedEvent
            OnPropertyChanged(e.PropertyName);
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
