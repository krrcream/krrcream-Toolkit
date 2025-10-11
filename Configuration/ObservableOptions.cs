using System.ComponentModel;
using System.Runtime.CompilerServices;
using System;
using krrTools.Bindable;

namespace krrTools.Configuration
{
    [Obsolete("Use ReactiveOptions<T> instead. This class will be removed after testing.")]
    public class ObservableOptions<TOptions> : INotifyPropertyChanged, IDisposable where TOptions : ToolOptionsBase, new()
    {
        private TOptions _options;
        private readonly IEventBus _eventBus;

        public TOptions Options
        {
            get => _options;
            private set
            {
                if (!Equals(_options, value))
                {
                    var oldOptions = _options;
                    _options = value;

                    // 发布设置变更事件到响应式总线
                    _eventBus.Publish(new SettingsChangedEvent
                    {
                        PropertyName = typeof(TOptions).Name,
                        OldValue = oldOptions,
                        NewValue = value,
                        SettingsType = typeof(TOptions)
                    });

                    OnPropertyChanged();

                    Console.WriteLine($"[ObservableOptions] {typeof(TOptions).Name} 选项已更新");
                }
            }
        }

        public ObservableOptions(ConverterEnum converter, IEventBus eventBus)
        {
            _eventBus = eventBus;
            _options = BaseOptionsManager.LoadOptions<TOptions>(converter) ?? new TOptions();
            BaseOptionsManager.SettingsChanged += OnSettingsChanged;

            // 转发内部Options对象的PropertyChanged事件
            _options.PropertyChanged += OnOptionsPropertyChanged;

            Console.WriteLine($"[ObservableOptions] {typeof(TOptions).Name} 响应式选项初始化完成");
        }

        /// <summary>
        /// 转发内部Options对象的PropertyChanged事件
        /// </summary>
        private void OnOptionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 转发PropertyChanged事件，这样监听ObservableOptions的对象就能收到内部Options的变化
            OnPropertyChanged(e.PropertyName);
        }

        private void OnSettingsChanged(ConverterEnum changedConverter)
        {
            // 简单匹配：N2NC -> N2NCOptions, DP -> DPToolOptions, KRRLN -> KRRLNTransformerOptions
            var expectedConverter = typeof(TOptions).Name switch
            {
                "N2NCOptions" => ConverterEnum.N2NC,
                "DPToolOptions" => ConverterEnum.DP,
                "KRRLNTransformerOptions" => ConverterEnum.KRRLN,
                _ => (ConverterEnum?)null
            };

            if (expectedConverter == changedConverter)
                Options = BaseOptionsManager.LoadOptions<TOptions>(changedConverter) ?? new TOptions();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            Console.WriteLine($"[ObservableOptions] {typeof(TOptions).Name} 资源释放");
            BaseOptionsManager.SettingsChanged -= OnSettingsChanged;
            _options.PropertyChanged -= OnOptionsPropertyChanged;
        }

        // 隐式转换为TOptions
        public static implicit operator TOptions(ObservableOptions<TOptions> observable)
        {
            return observable.Options;
        }
    }
}