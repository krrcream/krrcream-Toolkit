using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using krrTools.Configuration;
using Microsoft.Extensions.Logging;

namespace krrTools.Localization
{
    /// <summary>
    /// 统一的本地化服务，整合所有本地化功能。
    /// </summary>
    /// <example>
    /// 动态本地化用法:
    /// <code>
    /// // 1. 静态本地化（一次性显示，不自动更新）:
    /// string message = "确认删除文件?".Localize();
    /// MessageBox.Show(message);
    ///
    /// // 2. 动态本地化（UI控件自动更新）:
    /// // WPF绑定方式
    /// textBlock.SetBinding(TextBlock.TextProperty,
    ///     new Binding("Value") { Source = "标题|Title".GetLocalizedString() });
    ///
    /// // 或直接设置
    /// var localized = "菜单|Menu".GetLocalizedString();
    /// button.Content = localized.Value;
    /// localized.PropertyChanged += (s, e) => button.Content = localized.Value;
    ///
    /// // 3. 枚举本地化:
    /// string displayName = LocalizationService.GetLocalizedEnumDisplayName(MyEnum.Value);
    ///
    /// // 4. ToolTip本地化:
    /// LocalizationService.SetLocalizedToolTip(button, "点击这里|Click here");
    ///
    /// // 5. 语言切换:
    /// LocalizationService.ToggleLanguage(); // 在中英文之间切换
    /// </code>
    /// </example>
    public static class LocalizationService
    {
#region 枚举本地化

        /// <summary>
        /// 获取枚举的本地化显示名称。
        /// 从枚举值的 Description 特性中提取本地化文本。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="enumValue">枚举值。</param>
        /// <returns>本地化的显示名称。</returns>
        /// <example>
        /// <code>
        /// [Description("开始|Start")]
        /// public enum Action { Begin }
        /// string display = LocalizationService.GetLocalizedEnumDisplayName(Action.Begin);
        /// // 返回 "开始" 或 "Start" 根据当前语言
        /// </code>
        /// </example>
        public static string GetLocalizedEnumDisplayName<T>(T enumValue) where T : Enum
        {
            Type type = typeof(T);

            if (!enum_cache.TryGetValue(type, out ConcurrentDictionary<string, string[]>? dict))
            {
                dict = new ConcurrentDictionary<string, string[]>();

                foreach (FieldInfo field in type.GetFields())
                {
                    var attr = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attr != null && !string.IsNullOrEmpty(attr.Description) && attr.Description.Contains('|'))
                        dict[field.Name] = attr.Description.Split('|', 2);
                }

                enum_cache[type] = dict;
            }

            if (dict.TryGetValue(enumValue.ToString(), out string[]? parts) && IsChinese && parts.Length > 1)
                return parts[1];

            return parts != null ? parts[0] : enumValue.ToString();
        }

#endregion

#region 工具方法

        /// <summary>
        /// 为UI元素设置本地化ToolTip（自动更新）。
        /// 当语言切换时，ToolTip会自动更新。
        /// </summary>
        /// <param name="element">要设置ToolTip的UI元素。</param>
        /// <param name="tooltipText">本地化ToolTip文本，格式："中文|English"。</param>
        /// <example>
        /// <code>
        /// LocalizationService.SetLocalizedToolTip(button, "点击保存|Click to save");
        /// </code>
        /// </example>
        public static void SetLocalizedToolTip(FrameworkElement element, string? tooltipText)
        {
            if (string.IsNullOrEmpty(tooltipText)) return;

            DynamicLocalizedString localizedString = tooltipText.GetLocalizedString();
            element.ToolTip = localizedString.Value;

            void updateTip()
            {
                element.Dispatcher.Invoke(() => element.ToolTip = localizedString.Value);
            }

            LanguageChanged += updateTip;
            element.Unloaded += (_, _) => LanguageChanged -= updateTip;
        }

#endregion

#region 核心语言管理

        private static readonly ConcurrentDictionary<string, string[]>
            localization_cache = new ConcurrentDictionary<string, string[]>();

        private static readonly ConcurrentDictionary<string, DynamicLocalizedString>
            localized_string_cache = new ConcurrentDictionary<string, DynamicLocalizedString>();

        private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, string[]>>
            enum_cache = new ConcurrentDictionary<Type, ConcurrentDictionary<string, string[]>>();

        static LocalizationService()
        {
            // 从设置加载语言偏好
            bool forceChinese = BaseOptionsManager.GetForceChinese();
            IsChinese = forceChinese;
        }

        /// <summary>
        /// 语言改变事件。
        /// 当用户切换语言时触发，所有订阅此事件的UI元素会自动更新。
        /// </summary>
        public static event Action? LanguageChanged;

        /// <summary>
        /// 切换语言（中英文互换）。
        /// </summary>
        public static void ToggleLanguage()
        {
            IsChinese = !IsChinese;
            BaseOptionsManager.SetForceChinese(IsChinese);
            Logger.WriteLine(LogLevel.Debug, $"[LocalizationService] ToggleLanguage: ForceChinese set to {IsChinese}");
            LanguageChanged?.Invoke();
        }

        /// <summary>
        /// 检查当前是否为中文语言。
        /// </summary>
        /// <returns>true=中文模式, false=英文模式。</returns>
        public static bool IsChinese { get; private set; }

#endregion

#region 本地化字符串处理

        /// <summary>
        /// 静态本地化方法（不自动更新）。
        /// 适用于：
        /// <list type="bullet">
        /// <item>弹窗消息</item>
        /// <item>一次性显示的文本</item>
        /// <item>性能敏感的场景</item>
        /// </list>
        /// </summary>
        /// <param name="text">包含本地化文本的字符串，格式："中文|English"。</param>
        /// <returns>当前语言对应的文本。</returns>
        /// <example>
        /// <code>
        /// string message = "确认删除文件?".Localize();
        /// MessageBox.Show(message);
        /// </code>
        /// </example>
        public static string Localize(this string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            if (!localization_cache.TryGetValue(text, out string[]? parts))
            {
                parts = text.Split(['|'], 2);
                localization_cache[text] = parts;
            }

            return IsChinese && parts.Length > 1 ? parts[1] : parts[0];
        }

        /// <summary>
        /// 获取动态本地化字符串（自动更新）。
        /// 返回支持自动更新的对象，当语言切换时会自动通知UI更新。
        /// </summary>
        /// <param name="text">包含本地化文本的字符串，格式："中文|English"。</param>
        /// <returns>支持自动更新的本地化字符串对象。</returns>
        /// <example>
        /// <code>
        /// // WPF绑定用法:
        /// textBlock.SetBinding(TextBlock.TextProperty,
        ///     new Binding("Value") { Source = "标题|Title".GetLocalizedString() });
        ///
        /// // 手动监听用法:
        /// var localized = "菜单|Menu".GetLocalizedString();
        /// localized.PropertyChanged += (s, e) => UpdateUI();
        /// </code>
        /// </example>
        public static DynamicLocalizedString GetLocalizedString(this string text)
        {
            if (string.IsNullOrEmpty(text)) return new DynamicLocalizedString(text);

            return localized_string_cache.GetOrAdd(text, key => new DynamicLocalizedString(key));
        }

#endregion
    }

    /// <summary>
    /// 动态本地化字符串类，支持自动更新。
    /// 当语言切换时，此对象的Value属性会自动更新，并触发PropertyChanged事件。
    /// </summary>
    /// <example>
    /// <code>
    /// // 1. WPF数据绑定（推荐）:
    /// textBlock.SetBinding(TextBlock.TextProperty,
    ///     new Binding("Value") { Source = localizedString });
    ///
    /// // 2. 手动监听:
    /// localizedString.PropertyChanged += (s, e) => UpdateUI();
    ///
    /// // 3. 隐式转换为string:
    /// string text = localizedString; // 自动调用Value属性
    /// </code>
    /// </example>
    public class DynamicLocalizedString : INotifyPropertyChanged
    {
        private string _value;

        public DynamicLocalizedString(string key)
        {
            Key = key;
            _value = key.Localize();
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>
        /// 本地化字符串的键（原始文本）。
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// 当前语言对应的本地化值。
        /// 当语言切换时，此属性会自动更新并触发PropertyChanged事件。
        /// </summary>
        public string Value
        {
            get => _value;
            private set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnLanguageChanged()
        {
            Value = Key.Localize();
        }

        /// <summary>
        /// 隐式转换为string类型。
        /// 允许直接将DynamicLocalizedString赋值给string变量。
        /// </summary>
        /// <param name="ls">本地化字符串对象。</param>
        public static implicit operator string(DynamicLocalizedString ls)
        {
            return ls.Value;
        }

        ~DynamicLocalizedString()
        {
            LocalizationService.LanguageChanged -= OnLanguageChanged;
        }
    }
}
