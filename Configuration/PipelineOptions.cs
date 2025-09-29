using System.Collections.Generic;

namespace krrTools.Configuration
{
    /// <summary>
    /// 管道选项，用于配置工具链
    /// </summary>
    public class PipelineOptions : IToolOptions
    {
        /// <summary>
        /// 管道步骤列表
        /// </summary>
        public List<PipelineStep> Steps { get; set; } = new();

        /// <summary>
        /// 管道名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 验证选项
        /// </summary>
        public void Validate()
        {
            // 验证每个步骤的选项
            foreach (var step in Steps)
            {
                step.Options?.Validate();
            }
        }
    }

    /// <summary>
    /// 管道步骤
    /// </summary>
    public class PipelineStep
    {
        /// <summary>
        /// 工具名称
        /// </summary>
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// 工具选项
        /// </summary>
        public IToolOptions? Options { get; set; }
    }
}