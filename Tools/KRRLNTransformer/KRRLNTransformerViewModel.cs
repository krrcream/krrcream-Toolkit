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
            
            // 订阅嵌套设置对象的属性变更事件，转发为SettingsChangedEvent
            SubscribeToNestedOptionsChanges();
        }

        /// <summary>
        /// 订阅嵌套设置对象的属性变更事件
        /// </summary>
        private void SubscribeToNestedOptionsChanges()
        {
            if (Options.Short is INotifyPropertyChanged shortOptions)
                shortOptions.PropertyChanged += OnNestedPropertyChanged;
            if (Options.Long is INotifyPropertyChanged longOptions)
                longOptions.PropertyChanged += OnNestedPropertyChanged;
            if (Options.LengthThreshold is INotifyPropertyChanged lengthOptions)
                lengthOptions.PropertyChanged += OnNestedPropertyChanged;
            if (Options.Alignment is INotifyPropertyChanged alignOptions)
                alignOptions.PropertyChanged += OnNestedPropertyChanged;
            if (Options.LNAlignment is INotifyPropertyChanged lnAlignOptions)
                lnAlignOptions.PropertyChanged += OnNestedPropertyChanged;
            if (Options.General is INotifyPropertyChanged generalOptions)
                generalOptions.PropertyChanged += OnNestedPropertyChanged;
        }

        /// <summary>
        /// 取消订阅嵌套设置对象的属性变更事件
        /// </summary>
        private void UnsubscribeFromNestedOptionsChanges()
        {
            if (Options.Short is INotifyPropertyChanged shortOptions)
                shortOptions.PropertyChanged -= OnNestedPropertyChanged;
            if (Options.Long is INotifyPropertyChanged longOptions)
                longOptions.PropertyChanged -= OnNestedPropertyChanged;
            if (Options.LengthThreshold is INotifyPropertyChanged lengthOptions)
                lengthOptions.PropertyChanged -= OnNestedPropertyChanged;
            if (Options.Alignment is INotifyPropertyChanged alignOptions)
                alignOptions.PropertyChanged -= OnNestedPropertyChanged;
            if (Options.LNAlignment is INotifyPropertyChanged lnAlignOptions)
                lnAlignOptions.PropertyChanged -= OnNestedPropertyChanged;
            if (Options.General is INotifyPropertyChanged generalOptions)
                generalOptions.PropertyChanged -= OnNestedPropertyChanged;
        }

        /// <summary>
        /// 处理嵌套设置对象的属性变更，转发为SettingsChangedEvent
        /// </summary>
        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null)
            {
                _eventBus.Publish(new SettingsChangedEvent
                {
                    PropertyName = e.PropertyName,
                    NewValue = sender,
                    SettingsType = typeof(KRRLNTransformerOptions)
                });
            }
        }

        /// <summary>
        /// 释放资源，取消所有事件订阅
        /// </summary>
        public new void Dispose()
        {
            UnsubscribeFromNestedOptionsChanges();
            base.Dispose();
        }

        public IToolOptions GetPreviewOptions()
        {
            return new KRRLNTransformerOptions
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

}