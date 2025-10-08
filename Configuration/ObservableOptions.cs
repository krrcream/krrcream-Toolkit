using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace krrTools.Configuration;

/// <summary>
/// 事件驱动的选项包装器，自动响应设置变化
/// </summary>
public class ObservableOptions<TOptions> : INotifyPropertyChanged where TOptions : ToolOptionsBase, new()
{
    private TOptions _options;

    public TOptions Options
    {
        get => _options;
        private set
        {
            if (!Equals(_options, value))
            {
                _options = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableOptions(ConverterEnum converter)
    {
        _options = BaseOptionsManager.LoadOptions<TOptions>(converter) ?? new TOptions();
        BaseOptionsManager.SettingsChanged += OnSettingsChanged;
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
        {
            Options = BaseOptionsManager.LoadOptions<TOptions>(changedConverter) ?? new TOptions();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // 隐式转换为TOptions
    public static implicit operator TOptions(ObservableOptions<TOptions> observable) => observable.Options;
}