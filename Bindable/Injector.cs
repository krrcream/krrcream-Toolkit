using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace krrTools.Bindable;

/// <summary>
/// Helper class for automatic dependency injection using [Inject] attributes.
/// Provides a centralized way to inject services into objects based on property attributes.
///
/// 推荐用法：
/// 1. 在类中标记属性：[Inject] public IService Service { get; set; } = null!;
/// 2. 在构造函数中调用：this.InjectServices();
/// 3. 或者继承 ReactiveViewModelBase（自动注入）
///
/// 高级用法（完全自动）：
/// - 使用 Injector.Create&lt;T&gt;() 工厂方法
/// - 使用 [AutoInject] 标记类实现完全自动注入
/// </summary>
public static class Injector
{
    /// <summary>
    /// 工厂方法：创建对象并自动注入依赖
    /// </summary>
    public static T Create<T>() where T : new()
    {
        var instance = new T();
        InjectServices(instance);
        return instance;
    }

    /// <summary>
    /// 工厂方法：创建对象并自动注入依赖（带参数构造函数）
    /// </summary>
    public static T Create<T>(params object[] args)
    {
        var instance = (T)Activator.CreateInstance(typeof(T), args)!;
        InjectServices(instance);
        return instance;
    }

    /// <summary>
    /// 检查类是否标记了 [AutoInject] 属性
    /// </summary>
    private static bool ShouldAutoInject(Type type)
    {
        return type.GetCustomAttribute<AutoInjectAttribute>() != null;
    }

    /// <summary>
    /// Inject services into properties marked with [Inject] attribute.
    /// Only injects if the property is currently null.
    /// </summary>
    public static void InjectServices(object target)
    {
        if (target == null) return;

        var targetType = target.GetType();
        var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var injectAttribute = property.GetCustomAttribute<InjectAttribute>();
            if (injectAttribute == null) continue;

            // Skip if property already has a value
            if (property.GetValue(target) != null) continue;

            // Determine service type
            var serviceType = injectAttribute.ServiceType ?? property.PropertyType;

            try
            {
                var service = App.Services.GetRequiredService(serviceType);
                property.SetValue(target, service);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to inject service of type {serviceType.FullName} into property {property.Name} of {targetType.FullName}",
                    ex);
            }
        }
    }
}

/// <summary>
/// 扩展方法，让注入调用更优雅
/// </summary>
public static class InjectorExtensions
{
    /// <summary>
    /// 自动注入当前对象中标记了 [Inject] 的属性
    /// </summary>
    public static void InjectServices(this object target)
    {
        Injector.InjectServices(target);
    }
}