using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using krrTools.Beatmaps;
using krrTools.Configuration;
using krrTools.Bindable;
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
        where TViewModel : ToolViewModelBase<TOptions>, IToolViewModel
        where TControl : ToolViewBase<TOptions>, IToolControl
    {
        protected TOptions _currentOptions = new();
        protected ReactiveOptions<TOptions>? _reactiveOptions;

        protected ToolModuleBase(ReactiveOptions<TOptions>? reactiveOptions = null)
        {
            _reactiveOptions = reactiveOptions;
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
        /// 获取最新的选项设置 - 优先使用注入的ReactiveOptions，然后从DI容器获取
        /// 解决响应式架构中_currentOptions不会实时更新的问题
        /// </summary>
        protected TOptions GetLatestOptions()
        {
            try
            {
                // 优先使用注入的ReactiveOptions
                if (_reactiveOptions != null)
                {
                    return _reactiveOptions.Options;
                }

                // 回退到从DI容器获取
                var services = App.Services;
                if (services.GetService(typeof(ReactiveOptions<TOptions>)) is ReactiveOptions<TOptions> reactOptions)
                {
                    return reactOptions.Options;
                }

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
        /// 创建工具实例
        /// </summary>
        public virtual ITool CreateTool()
        {
            // var logger = App.Services.GetService(typeof(ILogger<GenericTool>)) as ILogger<GenericTool>;
            return new GenericTool(this, this); // 传入 module 和 applier
        }

        /// <summary>
        /// 实现 IApplyToBeatmap 接口
        /// </summary>
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            var b = beatmap as Beatmap ?? throw new ArgumentException("IBeatmap must be Beatmap");
            ApplyToBeatmapInternal(b);
        }

        /// <summary>
        /// 创建默认选项
        /// </summary>
        IToolOptions IToolModule.CreateDefaultOptions()
        {
            return new TOptions();
        }

        /// <summary>
        /// 创建UI控件
        /// </summary>
        IToolControl IToolModule.CreateControl()
        {
            return (IToolControl)Activator.CreateInstance(typeof(TControl), ModuleType)!;
        }

        /// <summary>
        /// 创建ViewModel
        /// </summary>
        IToolViewModel IToolModule.CreateViewModel()
        {
            return (IToolViewModel)Activator.CreateInstance(typeof(TViewModel), ModuleType)!;
        }
    }

    /// <summary>
    /// 通用工具实现 - 实现ITool和IApplyToBeatmap，职责分离
    /// </summary>
    public class GenericTool(IToolModule module, IApplyToBeatmap applier) : ITool, IApplyToBeatmap
    {
        public string Name => module.ModuleName;

        public IToolOptions DefaultOptions => module.CreateDefaultOptions();

        /// <summary>
        /// 处理单个文件 - 内部使用IApplyToBeatmap进行Beatmap转换
        /// </summary>
        public string? ProcessFileSave(string filePath, IToolOptions? options = null)
        {
            try
            {
                // 解码谱面
                var beatmap = BeatmapDecoder.Decode(filePath).GetManiaBeatmap();
                if (beatmap == null) return null;

                // 克隆谱面
                var clonedBeatmap = CloneBeatmap(beatmap);
                var maniaBeatmap = clonedBeatmap as IBeatmap ?? ManiaBeatmap.FromBeatmap(clonedBeatmap);
                ApplyToBeatmap(maniaBeatmap);

                // 保存
                var outputPath = (maniaBeatmap as Beatmap ?? clonedBeatmap).GetOutputOsuFileName();
                var outputDir = Path.GetDirectoryName(filePath);
                var fullOutputPath = Path.Combine(outputDir!, outputPath);
                (maniaBeatmap as Beatmap ?? clonedBeatmap).Save(fullOutputPath);
                return fullOutputPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] 处理文件失败: {filePath}\n{ex}");
                return null;
            }
        }

        public async Task<string?> ProcessFileSaveAsync(string filePath, IToolOptions? options = null)
        {
            return await Task.Run(() => ProcessFileSave(filePath, options));
        }

        /// <summary>
        /// 实现IApplyToBeatmap - 委托给内部applier
        /// </summary>
        public void ApplyToBeatmap(IBeatmap beatmap)
        {
            applier.ApplyToBeatmap(beatmap);
        }

        /// <summary>
        /// 克隆Beatmap以避免修改原始对象
        /// </summary>
        private Beatmap CloneBeatmap(Beatmap input)
        {
            // 手动克隆以避免修改原始beatmap
            var cloned = new Beatmap();

            // 复制所有属性
            cloned.GeneralSection = input.GeneralSection;
            // 克隆MetadataSection以避免修改Version
            cloned.MetadataSection = Activator.CreateInstance(input.MetadataSection.GetType()) as dynamic;
            if (cloned.MetadataSection != null)
            {
                cloned.MetadataSection.Title = input.MetadataSection.Title;
                cloned.MetadataSection.TitleUnicode = input.MetadataSection.TitleUnicode;
                cloned.MetadataSection.Artist = input.MetadataSection.Artist;
                cloned.MetadataSection.ArtistUnicode = input.MetadataSection.ArtistUnicode;
                cloned.MetadataSection.Creator = input.MetadataSection.Creator;
                cloned.MetadataSection.Version = input.MetadataSection.Version;
                cloned.MetadataSection.Source = input.MetadataSection.Source;
                cloned.MetadataSection.Tags = input.MetadataSection.Tags;
            }
            // 克隆DifficultySection以避免修改CircleSize
            cloned.DifficultySection = Activator.CreateInstance(input.DifficultySection.GetType()) as dynamic;
            if (cloned.DifficultySection != null)
            {
                cloned.DifficultySection.HPDrainRate = input.DifficultySection.HPDrainRate;
                cloned.DifficultySection.CircleSize = input.DifficultySection.CircleSize;
                cloned.DifficultySection.OverallDifficulty = input.DifficultySection.OverallDifficulty;
                cloned.DifficultySection.ApproachRate = input.DifficultySection.ApproachRate;
                cloned.DifficultySection.SliderMultiplier = input.DifficultySection.SliderMultiplier;
                cloned.DifficultySection.SliderTickRate = input.DifficultySection.SliderTickRate;
            }

            cloned.TimingPoints = new List<OsuParsers.Beatmaps.Objects.TimingPoint>(input.TimingPoints);
            cloned.HitObjects = new List<OsuParsers.Beatmaps.Objects.HitObject>(input.HitObjects);
            cloned.OriginalFilePath = input.OriginalFilePath;

            return cloned;
        }
    }
}