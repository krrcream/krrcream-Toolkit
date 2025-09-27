using System.Threading.Tasks;

namespace krrTools.tools.Shared
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
        /// 同步处理单个文件
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项</param>
        /// <returns>输出文件路径，失败返回null</returns>
        string? ProcessFile(string filePath, IToolOptions options);

        /// <summary>
        /// 异步处理单个文件
        /// </summary>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项</param>
        /// <returns>输出文件路径，失败返回null</returns>
        Task<string?> ProcessFileAsync(string filePath, IToolOptions options);
    }
}