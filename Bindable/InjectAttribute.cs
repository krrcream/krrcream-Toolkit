using System;

namespace krrTools.Bindable
{
    /// <summary>
    /// Attribute to mark properties that should be automatically injected with services.
    /// Properties marked with this attribute will be automatically resolved from the service provider
    /// when the object is created, if they are still null.
    /// 
    /// 使用示例：
    /// [Inject] private IEventBus EventBus { get; set; } = null!;
    /// 
    /// 优势：
    /// - 避免构造函数参数过多
    /// - 支持条件注入（如果已有值则不注入）
    /// - 自动处理服务解析失败的情况
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class InjectAttribute : Attribute
    {
        /// <summary>
        /// Optional service type to inject. If not specified, uses the property type.
        /// </summary>
        public Type? ServiceType { get; set; }
    }
}