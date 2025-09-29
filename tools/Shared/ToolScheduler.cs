using System.Collections.Generic;
using System.Threading.Tasks;
using OsuParsers.Beatmaps;

namespace krrTools.tools.Shared
{
    /// <summary>
    /// 工具调度器，负责管理和执行工具
    /// </summary>
    public class ToolScheduler
    {
        private readonly Dictionary<string, ITool> _tools = new();

        /// <summary>
        /// 注册工具
        /// </summary>
        /// <param name="tool">工具实例</param>
        public void RegisterTool(ITool tool)
        {
            _tools[tool.Name] = tool;
        }

        /// <summary>
        /// 获取已注册的工具列表
        /// </summary>
        public IEnumerable<ITool> GetRegisteredTools()
        {
            return _tools.Values;
        }

        /// <summary>
        /// 异步执行单个工具（使用工具内部加载的设置）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>输出文件路径，失败返回null</returns>
        public async Task<string?> ExecuteSingleAsync(string toolName, string filePath)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return null;

            return await tool.ProcessFileAsync(filePath);
        }

        /// <summary>
        /// 异步执行单个工具（使用指定的选项）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项</param>
        /// <returns>输出文件路径，失败返回null</returns>
        public async Task<string?> ExecuteSingleAsync(string toolName, string filePath, IToolOptions options)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return null;

            return await Task.Run(() => tool.ProcessFileWithOptions(filePath, options));
        }

        /// <summary>
        /// 同步执行单个工具（使用工具内部加载的设置）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>输出文件路径，失败返回null</returns>
        public string? ExecuteSingle(string toolName, string filePath)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return null;

            return tool.ProcessFile(filePath);
        }

        /// <summary>
        /// 同步执行单个工具（使用指定的选项）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="filePath">输入文件路径</param>
        /// <param name="options">工具选项</param>
        /// <returns>输出文件路径，失败返回null</returns>
        public string? ExecuteSingle(string toolName, string filePath, IToolOptions options)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return null;

            return tool.ProcessFileWithOptions(filePath, options);
        }

        /// <summary>
        /// 异步执行工具管道
        /// </summary>
        /// <param name="pipeline">管道步骤列表</param>
        /// <param name="filePath">输入文件路径</param>
        /// <returns>最终输出文件路径，失败返回null</returns>
        public async Task<string?> ExecutePipelineAsync(IEnumerable<(string ToolName, IToolOptions Options)> pipeline, string filePath)
        {
            string currentPath = filePath;
            foreach (var (toolName, options) in pipeline)
            {
                var result = await ExecuteSingleAsync(toolName, currentPath, options);
                if (result == null)
                    return null;
                currentPath = result;
            }
            return currentPath;
        }

        /// <summary>
        /// 处理Beatmap对象（使用工具内部加载的设置）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="beatmap">输入Beatmap</param>
        /// <returns>处理后的Beatmap，失败返回null</returns>
        public Beatmap? ProcessBeatmap(string toolName, Beatmap beatmap)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return null;

            return tool.ProcessBeatmapToData(beatmap);
        }

        /// <summary>
        /// 处理Beatmap对象（使用指定的选项）
        /// </summary>
        /// <param name="toolName">工具名称</param>
        /// <param name="beatmap">输入Beatmap</param>
        /// <param name="options">工具选项</param>
        /// <returns>处理后的Beatmap，失败返回null</returns>
        public Beatmap? ProcessBeatmap(string toolName, Beatmap beatmap, IToolOptions options)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
                return null;

            return tool.ProcessBeatmapToDataWithOptions(beatmap, options);
        }

        /// <summary>
        /// 执行Beatmap管道
        /// </summary>
        /// <param name="pipeline">管道步骤列表</param>
        /// <param name="beatmap">输入Beatmap</param>
        /// <returns>最终处理后的Beatmap，失败返回null</returns>
        public Beatmap? ExecuteBeatmapPipeline(IEnumerable<(string ToolName, IToolOptions Options)> pipeline, Beatmap beatmap)
        {
            Beatmap currentBeatmap = beatmap;
            foreach (var (toolName, options) in pipeline)
            {
                var result = ProcessBeatmap(toolName, currentBeatmap, options);
                if (result == null)
                    return null;
                currentBeatmap = result;
            }
            return currentBeatmap;
        }
    }
}