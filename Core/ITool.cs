using krrTools.Configuration;
using OsuParsers.Beatmaps;

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
        /// 处理单个文件（options 为 null 时使用内部加载的默认设置）
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项，为 null 时使用默认</param>
        /// <returns>输出文件路径，失败返回null</returns>
        string? ProcessFileSave(string filePath, IToolOptions? options = null);

        /// <summary>
        /// 处理Beatmap对象并返回转换后的Beatmap（options 为 null 时使用内部加载的默认设置）
        /// </summary>
        /// <param name="input">输入Beatmap</param>
        /// <param name="options">工具选项，为 null 时使用默认</param>
        /// <returns>转换后的Beatmap，失败返回null</returns>
        Beatmap? ProcessBeatmap(Beatmap input, IToolOptions? options = null);
    }
}