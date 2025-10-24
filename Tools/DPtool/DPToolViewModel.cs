using System.ComponentModel;
using krrTools.Configuration;
using krrTools.Core;

namespace krrTools.Tools.DPtool
{
    /// <summary>
    /// DP工具响应式ViewModel - 双侧键位转换配置的响应式管理
    /// 核心功能：实时配置更新 + 预览联动 + 智能约束处理
    /// 融合ToolViewModelBase基础功能和Bindable的响应式能力
    /// 架构与N2NCViewModel完全一致
    /// </summary>
    public class DPToolViewModel : ToolViewModelBase<DPToolOptions>, IPreviewOptionsProvider
    {
        public DPToolViewModel(DPToolOptions options) : base(ConverterEnum.DP, true, options)
        {
            // 本地化事件处理器委托引用，用于Dispose时取消订阅
            PropertyChangedEventHandler optionsPropertyChangedHandler =
                // 订阅Options属性变化事件，用于处理约束逻辑
                OnOptionsPropertyChanged;
            Options.PropertyChanged += optionsPropertyChangedHandler;
        }
        
        /// <summary>
        /// Options属性变化事件处理器 - 处理约束逻辑
        /// </summary>
        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 处理约束逻辑，与原SetupPropertyConstraints方法相同
            switch (e.PropertyName)
            {
                case nameof(Options.ModifyKeys):
                    OnPropertyChanged(nameof(ModifyKeys));
                    break;
                case nameof(Options.LMinKeys):
                    // 约束逻辑：LMinKeys不能大于LMaxKeys
                    if (Options.LMinKeys.Value > Options.LMaxKeys.Value)
                        Options.LMinKeys.Value = Options.LMaxKeys.Value;
                    break;

                case nameof(Options.LMaxKeys):
                    // 约束逻辑：LMaxKeys不能小于LMinKeys
                    if (Options.LMaxKeys.Value < Options.LMinKeys.Value)
                        Options.LMaxKeys.Value = Options.LMinKeys.Value;
                    break;

                case nameof(Options.RMinKeys):
                    // 约束逻辑：RMinKeys不能大于RMaxKeys
                    if (Options.RMinKeys.Value > Options.RMaxKeys.Value)
                        Options.RMinKeys.Value = Options.RMaxKeys.Value;
                    break;

                case nameof(Options.RMaxKeys):
                    // 约束逻辑：RMaxKeys不能小于RMinKeys
                    if (Options.RMaxKeys.Value < Options.RMinKeys.Value)
                        Options.RMaxKeys.Value = Options.RMinKeys.Value;
                    break;
            }
        }

        // 公开属性 - 响应式架构，与N2NC保持一致
        public double? ModifyKeys
        {
            get => Options.ModifyKeys.Value;
            set => Options.ModifyKeys.Value = value;
        }
        
        public bool LMirror
        {
            get => Options.LMirror.Value;
            set => Options.LMirror.Value = value;
        }

        public bool LDensity
        {
            get => Options.LDensity.Value;
            set => Options.LDensity.Value = value;
        }

        public bool LRemove
        {
            get => Options.LRemove.Value;
            set => Options.LRemove.Value = value;
        }



        public bool RMirror
        {
            get => Options.RMirror.Value;
            set => Options.RMirror.Value = value;
        }

        public bool RDensity
        {
            get => Options.RDensity.Value;
            set => Options.RDensity.Value = value;
        }

        public bool RRemove
        {
            get => Options.RRemove.Value;
            set => Options.RRemove.Value = value;
        }



        public IToolOptions GetPreviewOptions()
        {
            var previewOptions = new DPToolOptions();
            previewOptions.LMirror.Value = Options.LMirror.Value;
            previewOptions.LDensity.Value = Options.LDensity.Value;
            previewOptions.LRemove.Value = Options.LRemove.Value;
            previewOptions.LMaxKeys.Value = Options.LMaxKeys.Value;
            previewOptions.LMinKeys.Value = Options.LMinKeys.Value;
            previewOptions.RMirror.Value = Options.RMirror.Value;
            previewOptions.RDensity.Value = Options.RDensity.Value;
            previewOptions.RRemove.Value = Options.RRemove.Value;
            previewOptions.RMaxKeys.Value = Options.RMaxKeys.Value;
            previewOptions.RMinKeys.Value = Options.RMinKeys.Value;
            return previewOptions;
        }

        // 计算属性 - 动态最大值约束
        public double LMinKeysMaximum => Options.LMaxKeys.Value;
        public double RMinKeysMaximum => Options.RMaxKeys.Value;

        /// <summary>
        /// 释放资源，取消所有事件订阅
        /// </summary>
        public new void Dispose()
        {
            // 取消Options属性变化的事件订阅
            Options.PropertyChanged -= OnOptionsPropertyChanged;
            base.Dispose();
        }
    }
}