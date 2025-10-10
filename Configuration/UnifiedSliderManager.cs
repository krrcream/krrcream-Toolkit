using System;
using System.Collections.Generic;
using System.Windows;

using Expression = System.Linq.Expressions.Expression;

namespace krrTools.Configuration
{
    /// <summary>
    /// 统一滑条管理器 - 全局滑条配置和约束管理
    /// 核心功能：统一三个工具的滑条创建、约束和绑定策略
    /// 设计目标：消除架构不一致，提供标准化滑条管理方案
    /// </summary>
    public static class UnifiedSliderManager
    {
        /// <summary>
        /// 滑条类型枚举
        /// </summary>
        public enum SliderType
        {
            /// <summary>基础数值滑条 - 绑定到Options，无响应式约束</summary>
            Basic,
            /// <summary>响应式滑条 - 通过ViewModel包装，支持约束逻辑</summary>
            Reactive,
            /// <summary>动态范围滑条 - 支持动态最大/最小值绑定</summary>
            DynamicRange,
            /// <summary>映射滑条 - 支持显示值到实际值的映射转换</summary>
            ValueMapped
        }

        /// <summary>
        /// 滑条配置描述符
        /// </summary>
        public class SliderDescriptor
        {
            public SliderType Type { get; set; }
            public double Min { get; set; } = 0;
            public double Max { get; set; } = 100;
            public double Step { get; set; } = 1;
            public bool HasConstraints { get; set; } = false;
            public bool HasDynamicRange { get; set; } = false;
            public Dictionary<double, string>? DisplayMap { get; set; }
            public Dictionary<double, double>? ValueMap { get; set; }
            public string? Label { get; set; }
            public string? Tooltip { get; set; }
        }

        /// <summary>
        /// 工具滑条需求分析结果
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, SliderDescriptor>> ToolSliderConfigs =
            new Dictionary<string, Dictionary<string, SliderDescriptor>>
            {
                ["N2NC"] = new Dictionary<string, SliderDescriptor>
                {
                    ["TargetKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic,
                        Min = 1, Max = 18, Step = 1,
                        HasConstraints = false, // 绑定到Options，无约束
                        Label = "目标键数"
                    },
                    ["MaxKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.DynamicRange,
                        Min = 1, Max = 18, Step = 1,
                        HasConstraints = true, HasDynamicRange = true,
                        Label = "最大键数"
                    },
                    ["MinKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.DynamicRange,
                        Min = 1, Max = 18, Step = 1,
                        HasConstraints = true, HasDynamicRange = true,
                        Label = "最小键数"
                    },
                    ["TransformSpeedSlot"] = new SliderDescriptor
                    {
                        Type = SliderType.ValueMapped,
                        Min = 1, Max = 8, Step = 1,
                        HasConstraints = false, // ViewModel中定义的计算属性
                        Label = "转换速度"
                    }
                },
                ["DP"] = new Dictionary<string, SliderDescriptor>
                {
                    ["LMaxKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic, // 目前绑定到Options
                        Min = 1, Max = 5, Step = 1,
                        HasConstraints = true, // 需要与LMinKeys约束
                        Label = "左手最大键数"
                    },
                    ["LMinKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic,
                        Min = 1, Max = 5, Step = 1,
                        HasConstraints = true,
                        Label = "左手最小键数"
                    },
                    ["RMaxKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic,
                        Min = 1, Max = 5, Step = 1,
                        HasConstraints = true,
                        Label = "右手最大键数"
                    },
                    ["RMinKeys"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic,
                        Min = 1, Max = 5, Step = 1,
                        HasConstraints = true,
                        Label = "右手最小键数"
                    }
                },
                ["KRRLN"] = new Dictionary<string, SliderDescriptor>
                {
                    ["LengthThreshold"] = new SliderDescriptor
                    {
                        Type = SliderType.ValueMapped,
                        Min = 0, Max = 10, Step = 1,
                        HasConstraints = false,
                        Label = "长度阈值"
                    },
                    ["ShortPercentage"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic,
                        Min = 0, Max = 100, Step = 1,
                        HasConstraints = false,
                        Label = "短面条百分比"
                    },
                    ["ShortLevel"] = new SliderDescriptor
                    {
                        Type = SliderType.Basic,
                        Min = 0, Max = 100, Step = 1,
                        HasConstraints = false,
                        Label = "短面条等级"
                    }
                    // ... 其他KRRLN滑条
                }
            };

        /// <summary>
        /// 创建标准化滑条 - 根据工具和属性名自动选择策略
        /// </summary>
        public static UIElement CreateStandardSlider<T>(string toolName, string propertyName, T options, object viewModel) where T : class
        {
            if (!ToolSliderConfigs.TryGetValue(toolName, out var toolConfig) ||
                !toolConfig.TryGetValue(propertyName, out var descriptor))
            {
                // 回退到传统方式
                return CreateFallbackSlider(options, propertyName);
            }

            return descriptor.Type switch
            {
                SliderType.Basic => CreateBasicSlider(options, propertyName, descriptor),
                SliderType.Reactive => CreateReactiveSlider(viewModel, propertyName, descriptor),
                SliderType.DynamicRange => CreateDynamicRangeSlider(options, viewModel, propertyName, descriptor),
                SliderType.ValueMapped => CreateValueMappedSlider(viewModel, propertyName, descriptor),
                _ => CreateFallbackSlider(options, propertyName)
            };
        }

        /// <summary>
        /// 创建基础滑条（绑定到Options）
        /// </summary>
        private static UIElement CreateBasicSlider<T>(T options, string propertyName, SliderDescriptor descriptor) where T : class
        {
            // 使用反射创建属性选择器
            var parameter = Expression.Parameter(typeof(T), "o");
            var property = Expression.Property(parameter, propertyName);
            var lambda = Expression.Lambda<Func<T, double>>(Expression.Convert(property, typeof(double)), parameter);

            return SettingsBinder.CreateTemplatedSlider(options, lambda);
        }

        /// <summary>
        /// 创建响应式滑条（绑定到ViewModel包装属性）
        /// </summary>
        private static UIElement CreateReactiveSlider(object viewModel, string propertyName, SliderDescriptor descriptor)
        {
            // 通过反射动态创建ViewModel属性绑定
            var viewModelType = viewModel.GetType();
            var parameter = Expression.Parameter(viewModelType, "vm");
            var property = Expression.Property(parameter, propertyName);
            var lambda = Expression.Lambda(Expression.Convert(property, typeof(double)), parameter);

            // 使用SettingsBinder的泛型重载
            var method = typeof(SettingsBinder)
                .GetMethod(nameof(SettingsBinder.CreateTemplatedSlider))
                ?.MakeGenericMethod(viewModelType);

            return (UIElement)method!.Invoke(null, new[] { viewModel, lambda, null, descriptor.DisplayMap })!;
        }

        /// <summary>
        /// 创建动态范围滑条（支持动态最大/最小值）
        /// </summary>
        private static UIElement CreateDynamicRangeSlider<T>(T options, object viewModel, string propertyName, SliderDescriptor descriptor) where T : class
        {
            var parameter = Expression.Parameter(typeof(T), "o");
            var property = Expression.Property(parameter, propertyName);
            var lambda = Expression.Lambda<Func<T, double>>(Expression.Convert(property, typeof(double)), parameter);

            // 根据属性名推导动态范围属性
            var dynamicMaxProperty = propertyName switch
            {
                "MaxKeys" => "MaxKeysMaximum",
                "MinKeys" => "MinKeysMaximum", 
                _ => null
            };

            if (dynamicMaxProperty != null)
            {
                return SettingsBinder.CreateTemplatedSliderWithDynamicMax(
                    options, lambda, viewModel, dynamicMaxProperty);
            }

            return CreateBasicSlider(options, propertyName, descriptor);
        }

        /// <summary>
        /// 创建值映射滑条（显示值映射）
        /// </summary>
        private static UIElement CreateValueMappedSlider(object viewModel, string propertyName, SliderDescriptor descriptor)
        {
            // 类似响应式滑条，但带有值映射
            return CreateReactiveSlider(viewModel, propertyName, descriptor);
        }

        /// <summary>
        /// 回退方案（使用传统SettingsBinder）
        /// </summary>
        private static UIElement CreateFallbackSlider<T>(T options, string propertyName) where T : class
        {
            var parameter = Expression.Parameter(typeof(T), "o");
            var property = Expression.Property(parameter, propertyName);
            var lambda = Expression.Lambda<Func<T, double>>(Expression.Convert(property, typeof(double)), parameter);

            return SettingsBinder.CreateTemplatedSlider(options, lambda);
        }

        /// <summary>
        /// 获取工具的滑条配置
        /// </summary>
        public static Dictionary<string, SliderDescriptor>? GetToolSliderConfig(string toolName)
        {
            return ToolSliderConfigs.TryGetValue(toolName, out var config) ? config : null;
        }

        /// <summary>
        /// 注册自定义工具滑条配置
        /// </summary>
        public static void RegisterToolSliderConfig(string toolName, Dictionary<string, SliderDescriptor> config)
        {
            ToolSliderConfigs[toolName] = config;
        }
    }
}