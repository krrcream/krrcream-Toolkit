using System;
using System.Threading.Tasks;
using krrTools.Configuration;
using krrTools.Data;
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
        KRRLN,
        // 新模块在此添加
    }

    /// <summary>
    /// 转换模块基类
    /// </summary>
    public abstract class ToolModuleBase<TOptions, TViewModel, TControl> : IToolModule
        where TOptions : ToolOptionsBase, new()
        where TViewModel : ToolViewModelBase<TOptions>
        where TControl : ToolViewBase<TOptions>
    {
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
        /// 创建默认选项
        /// </summary>
        protected virtual TOptions CreateDefaultOptions() => new TOptions();

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        protected virtual TViewModel CreateViewModel()
        {
            // Try to get injected options from DI container
            var services = App.Services;
            if (services.GetService(typeof(TOptions)) is TOptions options)
            {
                // Use the DI constructor
                return (TViewModel)Activator.CreateInstance(typeof(TViewModel), options, true)!;
            }
            else
            {
                // Fallback to default constructor with tool enum
                return (TViewModel)Activator.CreateInstance(typeof(TViewModel), ModuleType, true)!;
            }
        }

        /// <summary>
        /// 创建UI控件
        /// </summary>
        protected virtual TControl CreateControl()
        {
            // Try to get injected options from DI container
            var services = App.Services;
            if (services.GetService(typeof(TOptions)) is TOptions options)
            {
                // Use the DI constructor
                return (TControl)Activator.CreateInstance(typeof(TControl), options)!;
            }
            else
            {
                // Fallback to default constructor with tool enum
                return (TControl)Activator.CreateInstance(typeof(TControl), ModuleType)!;
            }
        }

        /// <summary>
        /// 创建工具实例
        /// </summary>
        public virtual ITool CreateTool() => new GenericTool(this);

        /// <summary>
        /// 核心算法：处理Beatmap
        /// </summary>
        protected abstract Beatmap ProcessBeatmap(Beatmap input, TOptions options);
        protected abstract Beatmap ProcessSingleFile(string filePath, TOptions options);

        /// <summary>
        /// 非泛型版本：处理Beatmap（用于GenericTool）
        /// </summary>

        public Beatmap? ProcessBeatmapWithOptions(Object input, IToolOptions options)
        {
            if (options is TOptions concreteOptions)
            {
                if (input is Beatmap beatmap)
                    return ProcessBeatmap(beatmap, concreteOptions);

                if (input is string filePath)
                    return ProcessSingleFile(filePath, concreteOptions);
            }
            return null;
        }

        /// <summary>
        /// 创建默认选项
        /// </summary>
        IToolOptions IToolModule.CreateDefaultOptions() => CreateDefaultOptions();

        /// <summary>
        /// 创建UI控件
        /// </summary>
        object IToolModule.CreateControl() => CreateControl();

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        object IToolModule.CreateViewModel() => CreateViewModel();
    }

    /// <summary>
    /// 通用工具实现
    /// </summary>
    public class GenericTool(IToolModule module) : ITool
    {
        public string Name => module.ModuleName;

        public IToolOptions DefaultOptions => module.CreateDefaultOptions();

        public string? ProcessFile(string filePath, IToolOptions? options = null)
        {
            var opts = options ?? DefaultOptions;
            return ProcessFileWithOptions(filePath, opts);
        }

        public Beatmap? ProcessBeatmap(Beatmap inputBeatmap, IToolOptions? options = null)
        {
            var opts = options ?? DefaultOptions;
            
            try
            {
                return module.ProcessBeatmapWithOptions(inputBeatmap, opts);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理谱面时出错，使用模块 {Name}: {ex.Message}");
            }
            
            return null;
        }

        private string? ProcessFileWithOptions(string filePath, IToolOptions options)
        {
            try
            {
                // 处理Beatmap
                var processedBeatmap = ProcessFileToDataWithOptions(filePath, options) as Beatmap;
                
                if (processedBeatmap == null)
                    return null;

                // 生成输出路径
                var outputPath = BeatmapOutputHelper.GenerateOutputPath(filePath, module.ModuleName);

                // 写入文件
                if (BeatmapOutputHelper.SaveBeatmapToFile(processedBeatmap, outputPath))
                {
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理文件时出错，使用模块 {Name}: {ex.Message}");
            }

            return null;
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            var beatmap = FilesHelper.GetManiaBeatmap(filePath);
            return ProcessBeatmap(beatmap, options);
        }
        
        public async Task<string?> ProcessFileAsync(string filePath) => await Task.Run(() => ProcessFile(filePath));
        public object? TestFileToData(string filePath) => ProcessFileToDataWithOptions(filePath, DefaultOptions);
    }
}