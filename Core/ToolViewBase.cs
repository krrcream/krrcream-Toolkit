using System.Windows.Controls;
using krrTools.Configuration;

namespace krrTools.Core;

/// <summary>
/// Unified base class for tool controls that need options management
/// </summary>
/// <typeparam name="TOptions">The options type for this tool</typeparam>
public abstract class ToolViewBase<TOptions> : UserControl where TOptions : class, IToolOptions, new()
{
    private readonly ConverterEnum _toolEnum;

    protected ToolViewBase(ConverterEnum toolEnum, TOptions? injectedOptions = null)
    {
        _toolEnum = toolEnum;
        Options = injectedOptions ?? new TOptions();

        // Load options on initialization if not injected
        if (injectedOptions == null) DoLoadOptions();

        // 订阅设置变化事件
        BaseOptionsManager.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(ConverterEnum changedConverter)
    {
        if (changedConverter == _toolEnum) DoLoadOptions();
    }

    /// <summary>
    /// The options for this tool
    /// </summary>
    protected TOptions Options { get; private set; }

    private void DoLoadOptions()
    {
        var saved = BaseOptionsManager.LoadOptions<TOptions>(_toolEnum);
        if (saved != null) Options = saved;
    }
}
