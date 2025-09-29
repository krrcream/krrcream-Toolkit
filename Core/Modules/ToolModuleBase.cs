using System;
using System.Diagnostics;
using System.Threading.Tasks;
using krrTools.Configuration;
using krrTools.Core.Interfaces;
using krrTools.Data;
using OsuParsers.Beatmaps;

namespace krrTools.Core.Modules
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
        where TViewModel : ToolViewModelBase<TOptions>, new()
        where TControl : ToolControlBase<TOptions>, new()
    {
        /// <summary>
        /// 模块类型
        /// </summary>
        public abstract ToolModuleType ModuleType { get; }

        /// <summary>
        /// 模块内部名称（用于配置和文件）
        /// </summary>
        public abstract string ModuleName { get; }

        /// <summary>
        /// 模块显示名称
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 创建默认选项
        /// </summary>
        public virtual TOptions CreateDefaultOptions() => new TOptions();

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        public virtual TViewModel CreateViewModel() => new TViewModel();

        /// <summary>
        /// 创建UI控件
        /// </summary>
        public virtual TControl CreateControl() => new TControl();

        /// <summary>
        /// 创建工具实例
        /// </summary>
        public virtual ITool CreateTool() => new GenericTool(this);

        /// <summary>
        /// 核心算法：处理Beatmap
        /// </summary>
        public abstract Beatmap ProcessBeatmap(Beatmap input, TOptions options);

        /// <summary>
        /// 非泛型版本：处理Beatmap（用于GenericTool）
        /// </summary>
        public Beatmap? ProcessBeatmapWithOptions(Beatmap input, IToolOptions options)
        {
            if (options is TOptions concreteOptions)
            {
                return ProcessBeatmap(input, concreteOptions);
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
    public class GenericTool : ITool
    {
        private readonly IToolModule _module;

        public GenericTool(IToolModule module)
        {
            _module = module;
        }

        public string Name => _module.ModuleName;

        public IToolOptions DefaultOptions => _module.CreateDefaultOptions();

        public string? ProcessFile(string filePath)
        {
            return ProcessFileWithOptions(filePath, DefaultOptions);
        }

        public async Task<string?> ProcessFileAsync(string filePath)
        {
            return await Task.Run(() => ProcessFile(filePath));
        }

        public object? ProcessFileToData(string filePath)
        {
            return ProcessFileToDataWithOptions(filePath, DefaultOptions);
        }

        public Beatmap? ProcessBeatmapToData(Beatmap inputBeatmap)
        {
            return ProcessBeatmapToDataWithOptions(inputBeatmap, DefaultOptions);
        }

        public string? ProcessFileWithOptions(string filePath, IToolOptions options)
        {
            try
            {
                // 读取原始Beatmap
                var beatmap = FilesHelper.GetManiaBeatmap(filePath);
                if (beatmap == null)
                    return null;

                // 处理Beatmap
                var processedBeatmap = ProcessBeatmapToDataWithOptions(beatmap, options);
                if (processedBeatmap == null)
                    return null;

                // 生成输出路径
                var outputPath = BeatmapOutputHelper.GenerateOutputPath(filePath, _module.ModuleName);

                // 写入文件
                if (BeatmapOutputHelper.WriteBeatmapToFile(processedBeatmap, outputPath))
                {
                    return outputPath;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing file with {Name}: {ex.Message}");
            }

            return null;
        }

        public Beatmap? ProcessBeatmapToDataWithOptions(Beatmap inputBeatmap, IToolOptions options)
        {
            try
            {
                // 直接调用模块的ProcessBeatmapWithOptions方法
                return _module.ProcessBeatmapWithOptions(inputBeatmap, options);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing beatmap with {Name}: {ex.Message}");
            }

            return null;
        }

        private object? ProcessFileToDataWithOptions(string filePath, IToolOptions options)
        {
            var beatmap = FilesHelper.GetManiaBeatmap(filePath);
            return ProcessBeatmapToDataWithOptions(beatmap, options);
        }
    }
}