using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using krrTools.Bindable;
using krrTools.Configuration;
using Microsoft.Extensions.Logging;

namespace krrTools.Core
{
    /// <summary>
    /// 预览选项提供者接口
    /// </summary>
    public interface IPreviewOptionsProvider;

    // 基类，实现了基本的选项加载和保存逻辑
    public abstract class ToolOptionsBase : ObservableObject, IToolOptions
    {
        protected bool IsValidating { get; set; }
        public bool IsLoading { get; set; }

        /// <summary T="properties.">
        /// 验证选项值的有效性
        /// 默认实现基于OptionAttribute的Min/Max对数值属性进行限制。
        /// 支持直接属性和Bindable。
        /// 子类可重写此方法以实现自定义验证逻辑。
        /// </summary>
        public virtual void Validate()
        {
            if (IsValidating) return;

            IsValidating = true;

            try
            {
                PropertyInfo[] properties = GetType().GetProperties();

                foreach (PropertyInfo prop in properties)
                {
                    var attr = prop.GetCustomAttribute<OptionAttribute>();
                    if (attr?.Min == null || attr.Max == null) continue;

                    object value = GetPropertyValue(prop);
                    ClampNumericValue(prop, value, attr);
                }
            }
            finally
            {
                IsValidating = false;
            }
        }

        private object GetPropertyValue(PropertyInfo prop)
        {
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                object? bindable = prop.GetValue(this);
                if (bindable == null) return null!;

                PropertyInfo? valueProp = prop.PropertyType.GetProperty("Value");
                return valueProp?.GetValue(bindable) ?? null!;
            }

            return prop.GetValue(this) ?? null!;
        }

        private void SetPropertyValue(PropertyInfo prop, object value)
        {
            if (prop.PropertyType.IsGenericType &&
                prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
            {
                object? bindable = prop.GetValue(this);
                if (bindable == null) return;

                PropertyInfo? valueProp = prop.PropertyType.GetProperty("Value");
                valueProp?.SetValue(bindable, value);
            }
            else
                prop.SetValue(this, value);
        }

        private void ClampNumericValue(PropertyInfo prop, object value, OptionAttribute attr)
        {
            if (value is int intValue)
            {
                int min = Convert.ToInt32(attr.Min);
                int max = Convert.ToInt32(attr.Max);
                int clamped = Math.Clamp(intValue, min, max);
                if (clamped != intValue) SetPropertyValue(prop, clamped);
            }
            // 子类可在Validate中手动处理Clamp
        }

        /// <summary>
        /// 创建一个Bindable属性并绑定变化事件
        /// 用于简化Bindable属性的创建和变化通知
        /// </summary>
        protected Bindable<T> CreateBindable<T>(T defaultValue, Action<T>? onValueChanged = null)
        {
            var bindable = new Bindable<T>(defaultValue);
            bindable.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Bindable<T>.Value))
                {
                    OnPropertyChanged(); // Notify that this property changed
                    onValueChanged?.Invoke(bindable.Value);
                }
            };
            return bindable;
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (IsLoading || ConfigManager.IsDeserializing) return; // 加载或反序列化时避免触发PropertyChanged

            // 测试调试，显示名
            // Logger.WriteLine(LogLevel.Debug, $"[ToolOptions] Property '{e.PropertyName}' changed");
            base.OnPropertyChanged(e);
            // 设置变化时，通过UI或其他方式触发BaseOptionsManager.SaveOptions
        }

        /// <summary>
        /// 从另一个选项对象复制所有Bindable属性的值
        /// </summary>
        public void CopyFrom(ToolOptionsBase? other)
        {
            if (other == null || other.GetType() != GetType()) return;

            foreach (PropertyInfo prop in GetType().GetProperties())
            {
                if (prop.PropertyType.IsGenericType && prop.PropertyType.GetGenericTypeDefinition() == typeof(Bindable<>))
                {
                    object? sourceBindable = prop.GetValue(other);
                    object? targetBindable = prop.GetValue(this);

                    if (sourceBindable != null && targetBindable != null)
                    {
                        PropertyInfo? valueProp = prop.PropertyType.GetProperty("Value");

                        if (valueProp != null)
                        {
                            object? sourceValue = valueProp.GetValue(sourceBindable);
                            valueProp.SetValue(targetBindable, sourceValue);
                        }
                    }
                }
            }
        }

        public PresetKind SelectedPreset { get; init; } = PresetKind.Default;
    }

    /// <summary>
    /// 选项属性，用于定义选项的元数据
    /// 
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class OptionAttribute : Attribute
    {
        public string? LabelKey { get; set; } // Strings中的键，如 "DPModifyKeysCheckbox"
        public string? TooltipKey { get; set; } // Strings中的键，如 "DPModifyKeysTooltip"
        public object? DefaultValue { get; set; }
        public object? Min { get; set; }
        public object? Max { get; set; }
        public UIType UIType { get; set; } = UIType.Toggle;
        public Type? DataType { get; set; } // 数据类型，如 typeof(int), typeof(double) 等
        public double? TickFrequency { get; set; } = 1;
        public double? KeyboardStep { get; set; } = 1;

        /// <summary>
        /// 自定义显示值映射的静态字段名称 (在同一类中)
        /// 推荐使用nameof(字典)
        /// </summary>
        public string? DisplayMapField { get; set; }

        /// <summary>
        /// 自定义实际值映射的静态字段名称 (在同一类中)
        /// 用于滑条值与实际值不同的情况
        /// </summary>
        public string? ActualMapField { get; set; }

        /// <summary>
        /// 是否启用勾选框 (对于滑条)
        /// </summary>
        public bool HasCheckBox { get; set; }

        /// <summary>
        /// 勾选框对应的属性名 (必须是bool类型，在同一类中)
        /// 例如："IsChecked", "IsEnabled"
        /// </summary>
        public string? CheckBoxProperty { get; set; }

        /// <summary>
        /// 此设置变化是否会触发预览刷新
        /// 默认false，只有明确标记为true的设置变化才会发布SettingsChangedEvent
        /// </summary>
        public bool IsRefresher { get; set; }
    }

    /// <summary>
    /// UI控件类型枚举
    /// </summary>
    public enum UIType
    {
        Toggle, // CheckBox
        Slider, // Slider for numeric
        Text, // TextBox for string
        ComboBox, // 下拉框

        NumberBox // 数字输入框
        // 可根据需要添加更多类型
    }

    /// <summary>
    /// 预设类型枚举
    /// </summary>

    //TODO: 单一枚举不合理，应该由各个ToolOptions自行定义预设枚举，并自动反射创建
    public enum PresetKind
    {
        [Description("Default|默认")]
        Default = 0,
        [Description("10K Preset|10K预设")]
        TenK = 1,
        [Description("9K Preset|9K预设")]
        NineK = 2,
        [Description("8K Preset|8K预设")]
        EightK = 3,
        [Description("7K Preset|7K预设")]
        SevenK = 4,
        [Description("Inverse Space=1/4|反键 间隔1/4")]
        Inverse4 = 5,
        [Description("Inverse Space=1/2|反键 间隔1/2")]
        Inverse2 = 6,
        [Description("Note→1/2LN|米变1/2面条")]
        NoteToOneHalfLN = 7,
        [Description("Easy LN|轻面")]
        ShortLN1 = 8,
        [Description("Mid LN|中面")]
        MidLN1 = 9,
        [Description("Hard LN|大面")]
        LongLN1 = 10,
        [Description("A10K|散至10K")]
        A10K7 = 11,
        [Description("A9K|散至9K")]
        A9K7 = 12,
        [Description("A8K|散至8K")]
        A8K7 = 13,
        [Description("DownTo6K|降至6K")]
        DT6 = 14,
        [Description("DownTo4K|降至4K")]
        DT4 = 15,
        [Description("Release H|放手 难")]
        ReleaseHard = 17,
        [Description("Delete LN|删面")]
        DeleteLN = 18
    }
}
