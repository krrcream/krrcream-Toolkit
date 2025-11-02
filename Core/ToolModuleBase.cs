using System;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Bindable;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;

namespace krrTools.Core
{
    /// <summary>
    /// 转换模块类型枚举
    /// </summary>
    public enum ToolModuleType
    {
        N2NC,
        DP,
        KRRLN
        // 新模块在此添加
    }

    /// <summary>
    /// 转换模块基类 - 实现IToolModule，职责分离
    /// <para></para>
    /// 提供统一的模块框架，支持选项管理、UI创建和谱面转换。
    /// <para></para>
    /// 子类需实现ApplyToBeatmapInternal以定义具体转换逻辑。
    /// <para></para>
    /// 泛型参数TOptions、TViewModel和TControl保护类型安全，确保模块与其配置和UI组件一致。
    /// </summary>
    public abstract class ToolModuleBase<TOptions> : IToolModule
        where TOptions : ToolOptionsBase, new()
    {
        private TOptions _currentOptions = new TOptions();
        private readonly ReactiveOptions<TOptions>? _reactiveOptions;

        protected ToolModuleBase(ReactiveOptions<TOptions>? reactiveOptions = null)
        {
            _reactiveOptions = reactiveOptions;
            LoadCurrentOptions();
            // 订阅设置变化事件
            ConfigManager.SettingsChanged += OnSettingsChanged;
        }

        private void OnSettingsChanged(ConverterEnum changedConverter)
        {
            if (changedConverter.ToString() == ModuleType.ToString()) LoadCurrentOptions();
        }

        /// <summary>
        /// 加载当前选项
        /// </summary>
        private void LoadCurrentOptions()
        {
            _currentOptions = ConfigManager.LoadOptions<TOptions>(
                                  (ConverterEnum)Enum.Parse(typeof(ConverterEnum), ModuleType.ToString())) ??
                              CreateDefaultOptionsInternal();
        }

        /// <summary>
        /// 模块类型
        /// </summary>
        public abstract ToolModuleType ModuleType { get; }

        // /// <summary>
        // /// 选项类型（实现 Configuration.IToolModule）
        // /// </summary>
        // public Type OptionsType => typeof(TOptions);

        /// <summary>
        /// 模块内部名称（用于配置和文件）
        /// </summary>
        public virtual string ModuleName
        {
            get => ModuleType.ToString();
        }

        /// <summary>
        /// 模块显示名称
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 创建默认选项（内部）
        /// </summary>
        protected virtual TOptions CreateDefaultOptionsInternal()
        {
            return new TOptions();
        }

        /// <summary>
        /// 获取最新的选项设置 - 优先使用注入的ReactiveOptions，然后从DI容器获取
        /// 解决响应式架构中_currentOptions不会实时更新的问题
        /// </summary>
        protected TOptions GetLatestOptions()
        {
            try
            {
                // 优先使用注入的ReactiveOptions
                if (_reactiveOptions != null) return _reactiveOptions.Options;

                // 回退到从DI容器获取
                IServiceProvider services = App.Services;
                if (services.GetService(typeof(ReactiveOptions<TOptions>)) is ReactiveOptions<TOptions> reactOptions)
                    return reactOptions.Options;

                Logger.WriteLine(LogLevel.Error, $"[{ModuleType}Module] ReactiveOptions is null, Use the default settings");
                return _currentOptions;
            }
            catch (Exception ex)
            {
                Logger.WriteLine(LogLevel.Error, $"[{ModuleType}Module] Getting the latest settings failed: {ex.Message}，Use the default settings");
                return _currentOptions;
            }
        }

        /// <summary>
        /// 应用转换到谱面（内部实现，由子类提供具体逻辑）
        /// 子类应在此方法中：
        /// 1. 获取最新选项设置
        /// 2. 判断是否需要转换
        /// 3. 如果需要转换，则执行转换逻辑并返回true，否则返回false
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        /// <returns>true如果发生了实际转换，false如果没有变化</returns>
        protected abstract bool ApplyToBeatmapInternal(Beatmap beatmap);

        /// <summary>
        /// 是否有实际的转换修改（用于避免不必要的文件保存）
        /// </summary>
        public bool HasChanges { get; protected set; }

        /// <summary>
        /// 重置修改状态
        /// </summary>
        public void ResetChangesState()
        {
            HasChanges = false;
        }

        /// <summary>
        /// 实现 IApplyToBeatmap 接口
        /// </summary>
        public void ApplyToBeatmap(Beatmap beatmap)
        {
            Beatmap b = beatmap; // as Beatmap ?? throw new ArgumentException("IBeatmap must be Beatmap"); // 类型检查已在调用处完成
            HasChanges = ApplyToBeatmapInternal(b);
        }
    }
}
