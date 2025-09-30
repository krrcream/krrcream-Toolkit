using System;

namespace krrTools.Core
{
    /// <summary>
    /// 工具模块接口，用于自动注册
    /// </summary>
    public partial interface IToolModule
    {
        /// <summary>
        /// 枚举值
        /// </summary>
        object EnumValue { get; }

        /// <summary>
        /// 选项类型
        /// </summary>
        Type OptionsType { get; }
    }
}