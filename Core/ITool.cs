using System.Threading.Tasks;
using krrTools.Configuration;

namespace krrTools.Core
{
    /// <summary>
    /// 统一工具接口，所有转换工具都应实现此接口
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// 工具名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 默认选项
        /// </summary>
        IToolOptions DefaultOptions { get; }

        /// <summary>
        /// 同步处理单个文件（使用内部加载的设置）
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>输出文件路径，失败返回null</returns>
        string? ProcessFile(string filePath);

        /// <summary>
        /// 异步处理单个文件（使用内部加载的设置）
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>输出文件路径，失败返回null</returns>
        Task<string?> ProcessFileAsync(string filePath);

        /// <summary>
        /// 处理单个文件并返回转换后的数据（用于预览，使用内部加载的设置）
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>转换后的数据，失败返回null</returns>
        object? ProcessFileToData(string filePath);

        /// <summary>
        /// 处理Beatmap对象并返回转换后的Beatmap（用于预览，使用内部加载的设置）
        /// </summary>
        /// <param name="inputBeatmap">输入Beatmap</param>
        /// <returns>转换后的Beatmap，失败返回null</returns>
        OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToData(OsuParsers.Beatmaps.Beatmap inputBeatmap);

        /// <summary>
        /// 使用指定选项处理单个文件（用于UI预览等需要临时选项的场景）
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项</param>
        /// <returns>输出文件路径，失败返回null</returns>
        string? ProcessFileWithOptions(string filePath, IToolOptions options);

        /// <summary>
        /// 使用指定选项处理Beatmap对象（用于UI预览等需要临时选项的场景）
        /// </summary>
        /// <param name="inputBeatmap">输入Beatmap</param>
        /// <param name="options">工具选项</param>
        /// <returns>转换后的Beatmap，失败返回null</returns>
        OsuParsers.Beatmaps.Beatmap? ProcessBeatmapToDataWithOptions(OsuParsers.Beatmaps.Beatmap inputBeatmap, IToolOptions options);
    }
}