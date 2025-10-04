using System.Collections.Generic;
using System.Threading.Tasks;
using krrTools.Configuration;
using OsuParsers.Beatmaps;

namespace krrTools.Core
{
    /// <summary>
    /// 模块管理器接口
    /// </summary>
    public interface IModuleManager
    {
        /// <summary>
        /// 获取所有模块
        /// </summary>
        IEnumerable<IToolModule> GetAllModules();

        /// <summary>
        /// 注册模块
        /// </summary>
        void RegisterModule(IToolModule module);

        /// <summary>
        /// 注销模块
        /// </summary>
        void UnregisterModule(IToolModule module);

        /// <summary>
        /// 获取已注册的工具列表
        /// </summary>
        IEnumerable<ITool> GetRegisteredTools();

        /// <summary>
        /// 根据名称获取工具
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <returns>工具实例，失败返回null</returns>
        ITool? GetToolByName(string toolName);

        /// <summary>
        /// 异步执行单个工具（使用指定的选项）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项</param>
        /// <returns>输出文件路径，失败返回null</returns>
        Task<string?> ExecuteSingleAsync(string toolName, string filePath, IToolOptions options);

        /// <summary>
        /// 同步执行单个工具（使用工具内部加载的设置）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>输出文件路径，失败返回null</returns>
        string? ExecuteSingle(string toolName, string filePath);

        /// <summary>
        /// 处理Beatmap对象（使用工具内部加载的设置）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="beatmap">输入Beatmap</param>
        /// <returns>处理后的Beatmap，失败返回null</returns>
        Beatmap? ProcessBeatmap(string toolName, Beatmap beatmap);

        /// <summary>
        /// 处理Beatmap对象（使用指定的选项）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="beatmap">输入Beatmap</param>
        /// <param name="options">工具选项</param>
        /// <returns>处理后的Beatmap，失败返回null</returns>
        Beatmap? ProcessBeatmap(string toolName, Beatmap beatmap, IToolOptions options);

        /// <summary>
        /// 执行Beatmap管道
        /// </summary>
        /// <param name="pipeline">管道步骤列表</param>
        /// <param name="beatmap">输入Beatmap</param>
        /// <returns>最终处理后的Beatmap，失败返回null</returns>
        Beatmap? ExecuteBeatmapPipeline(IEnumerable<(string ToolName, IToolOptions Options)> pipeline, Beatmap beatmap);

        /// <summary>
        /// 从路径加载ManiaBeatmap
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>ManiaBeatmap实例，失败返回null</returns>
        Beatmaps.ManiaBeatmap? LoadBeatmap(string filePath);
    }
}