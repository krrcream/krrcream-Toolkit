using System;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Bindable;
using Microsoft.Extensions.Logging;
using OsuParsers.Beatmaps;
using OsuParsers.Decoders;

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
    /// 转换模块基类 - 实现IToolModule和IApplyToBeatmap，职责分离
    /// 提供统一的模块框架，支持选项管理、UI创建和谱面转换。
    /// 子类需实现ApplyToBeatmapInternal以定义具体转换逻辑。
    /// </summary>
    public abstract class ToolModuleBase<TOptions, TViewModel, TControl> : IToolModule, IApplyToBeatmap
        where TOptions : ToolOptionsBase, new()
        where TViewModel : ToolViewModelBase<TOptions>
        where TControl : ToolViewBase<TOptions>
    {
        protected TOptions _currentOptions = new();
        [Obsolete("Use _reactiveOptions instead. This will be removed after testing.")]
        protected ObservableOptions<TOptions>? _observableOptions;
        protected ReactiveOptions<TOptions>? _reactiveOptions;

        protected ToolModuleBase(ReactiveOptions<TOptions>? reactiveOptions = null)
        {
            _reactiveOptions = reactiveOptions;
            _observableOptions = reactiveOptions as ObservableOptions<TOptions>; // Backward compatibility
            LoadCurrentOptions();
            // 订阅设置变化事件
            BaseOptionsManager.SettingsChanged += OnSettingsChanged;
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
            _currentOptions = BaseOptionsManager.LoadOptions<TOptions>((ConverterEnum)Enum.Parse(typeof(ConverterEnum), ModuleType.ToString())) ?? CreateDefaultOptionsInternal();
        }

        /// <summary>
        /// 模块类型
        /// </summary>
        public abstract ToolModuleType ModuleType { get; }

        /// <summary>
        /// 枚举值（实现 Configuration.IToolModule）
        /// </summary>
        public object EnumValue => ModuleType;

        /// <summary>
        /// 选项类型（实现 Configuration.IToolModule）
        /// </summary>
        public Type OptionsType => typeof(TOptions);

        /// <summary>
        /// 模块内部名称（用于配置和文件）
        /// </summary>
        public virtual string ModuleName => ModuleType.ToString();

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
        /// 获取最新的选项设置 - 优先使用注入的ReactiveOptions，其次ObservableOptions，然后从DI容器获取
        /// 解决响应式架构中_currentOptions不会实时更新的问题
        /// </summary>
        protected TOptions GetLatestOptions()
        {
            try
            {
                // 优先使用注入的ReactiveOptions
                if (_reactiveOptions != null)
                {
                    Console.WriteLine($"[{ModuleType}Module] 使用注入的ReactiveOptions中的最新设置进行转换");
                    return _reactiveOptions.Options;
                }

                // 回退到ObservableOptions (backward compatibility)
#pragma warning disable CS0618 // Type or member is obsolete
                if (_observableOptions != null)
                {
                    Console.WriteLine($"[{ModuleType}Module] 使用注入的ObservableOptions中的最新设置进行转换");
                    return _observableOptions.Options;
                }
#pragma warning restore CS0618

                // 回退到从DI容器获取
                var services = App.Services;
                if (services.GetService(typeof(ReactiveOptions<TOptions>)) is ReactiveOptions<TOptions> reactOptions)
                {
                    Console.WriteLine($"[{ModuleType}Module] 使用ReactiveOptions中的最新设置进行转换");
                    return reactOptions.Options;
                }

#pragma warning disable CS0618 // Type or member is obsolete
                if (services.GetService(typeof(ObservableOptions<TOptions>)) is ObservableOptions<TOptions> obsOptions)
                {
                    Console.WriteLine($"[{ModuleType}Module] 使用ObservableOptions中的最新设置进行转换");
                    return obsOptions.Options;
                }
#pragma warning restore CS0618
            
                Console.WriteLine($"[{ModuleType}Module] 无法获取ReactiveOptions，使用默认设置");
                return _currentOptions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{ModuleType}Module] 获取最新设置失败: {ex.Message}，使用默认设置");
                return _currentOptions;
            }
        }

        /// <summary>
        /// 应用转换到谱面（内部实现，由子类提供具体逻辑）
        /// </summary>
        /// <param name="beatmap">谱面对象</param>
        protected abstract void ApplyToBeatmapInternal(Beatmap beatmap);

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        public virtual TViewModel CreateViewModel()
        {
            // Try to get injected options from DI container
            var services = App.Services;
            if (services.GetService(typeof(ObservableOptions<TOptions>)) is ObservableOptions<TOptions> obsOptions)
                // Use the DI constructor with options
                return (TViewModel)Activator.CreateInstance(typeof(TViewModel), obsOptions.Options, true)!;
            else
                // Fallback to default constructor with tool enum
                return (TViewModel)Activator.CreateInstance(typeof(TViewModel), ModuleType, true)!;
        }

        /// <summary>
        /// 创建UI控件
        /// </summary>
        public virtual TControl CreateControl()
        {
            // Try to get injected options from DI container
            var services = App.Services;
            if (services.GetService(typeof(ObservableOptions<TOptions>)) is ObservableOptions<TOptions> obsOptions)
                // Use the DI constructor with options
                return (TControl)Activator.CreateInstance(typeof(TControl), obsOptions.Options)!;
            else
                // Fallback to default constructor with tool enum
                return (TControl)Activator.CreateInstance(typeof(TControl), ModuleType)!;
        }

        /// <summary>
        /// 创建工具实例
        /// </summary>
        public virtual ITool CreateTool()
        {
            var logger = App.Services.GetService(typeof(ILogger<GenericTool>)) as ILogger<GenericTool>;
            return new GenericTool(this, this, logger); // 传入 module 和 applier
        }

        /// <summary>
        /// 实现 IApplyToBeatmap 接口
        /// </summary>
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var b = beatmap as Beatmap ?? throw new ArgumentException("IBeatmap must be Beatmap");
            ApplyToBeatmapInternal(b);
        }

        // ITool实现

        // /// <summary>
        // /// 处理单个文件
        // /// </summary>
        // public string? ProcessFileSave(string filePath, IToolOptions? opts = null)
        // {
        //     var options = opts ?? DefaultOptions;
        //     try
        //     {
        //         var beatmap = BeatmapDecoder.Decode(filePath).GetManiaBeatmap();
        //         var processedBeatmap = ProcessBeatmap(beatmap, options);
        //
        //         var outputPath = BeatmapFileHelper.GenerateOutputPath(filePath, ModuleName);
        //
        //         // 写入文件
        //         if (BeatmapFileHelper.SaveBeatmapToFile(processedBeatmap, outputPath)) return outputPath;
        //     }
        //     catch (Exception ex)
        //     {
        //         Console.WriteLine($"[ERROR] 处理文件时出错，使用模块 {Name}: {ex.Message}");
        //     }
        //
        //     return null;
        // }

        // /// <summary>
        // /// 处理Beatmap对象
        // /// </summary>
        // private Beatmap ProcessBeatmap(Beatmap input, IToolOptions? options = null)
        // {
        //     var opts = options as TOptions ?? _currentOptions;
        //     var maniaBeatmap = ManiaBeatmap.FromBeatmap(input);
        //     ApplyToBeatmap(maniaBeatmap);
        //     return maniaBeatmap;
        // }

        /// <summary>
        /// 创建默认选项
        /// </summary>
        IToolOptions IToolModuleInfo.CreateDefaultOptions()
        {
            return new TOptions();
        }

        /// <summary>
        /// 创建UI控件
        /// </summary>
        object IToolFactory.CreateControl()
        {
            return Activator.CreateInstance(typeof(TControl), ModuleType)!;
        }

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        object IToolFactory.CreateViewModel()
        {
            return Activator.CreateInstance(typeof(TViewModel), ModuleType)!;
        }
    }

    /// <summary>
    /// 通用工具实现 - 实现ITool和IApplyToBeatmap，职责分离
    /// </summary>
    public class GenericTool(IToolModule module, IApplyToBeatmap applier, ILogger<GenericTool>? logger = null) : ITool, IApplyToBeatmap
    {
        public string Name => module.ModuleName;

        public IToolOptions DefaultOptions => module.CreateDefaultOptions();

        /// <summary>
        /// 处理单个文件 - 内部使用IApplyToBeatmap进行Beatmap转换
        /// </summary>
        public string? ProcessFileSave(string filePath, IToolOptions? opts = null)
        {
            try
            {
                var beatmap = BeatmapDecoder.Decode(filePath).GetManiaBeatmap();
                var maniaBeatmap = beatmap as IBeatmap ?? ManiaBeatmap.FromBeatmap(beatmap);
            
                // 使用IApplyToBeatmap进行转换
                ApplyToBeatmap(maniaBeatmap);

                var outputPath = BeatmapFileHelper.GenerateOutputPath(filePath, module.ModuleName);

                // 写入文件
                if (BeatmapFileHelper.SaveBeatmapToFile(maniaBeatmap as Beatmap ?? beatmap, outputPath)) return outputPath;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "处理文件时出错，使用模块 {ModuleName}: {Message}", Name, ex.Message);
                Console.WriteLine($"[ERROR] 处理文件时出错，使用模块 {Name}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 实现IApplyToBeatmap - 委托给内部applier
        /// </summary>
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            applier.ApplyToBeatmap(beatmap);
        }
    }
}