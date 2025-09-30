using System;
using System.Globalization;
using System.ComponentModel;
using System.Reflection;
using krrTools.Configuration;

namespace krrTools.Localization
{
    /// <summary>
    /// 管理应用程序的本地化设置和语言切换
    /// </summary>
    public static class LocalizationManager
    {
        private static bool? ForceChinese { get; set; }

        public static event Action? LanguageChanged;

        public static void SetForceChinese(bool? forceChinese)
        {
            ForceChinese = forceChinese;
            BaseOptionsManager.SetForceChinese(forceChinese);
            LanguageChanged?.Invoke();
        }

        public static void ToggleLanguage()
        {
            SetForceChinese(!IsChineseLanguage());
        }

        /// <summary>
        /// 检查当前是否为中文语言
        /// </summary>
        public static bool IsChineseLanguage()
        {
            if (ForceChinese.HasValue) return ForceChinese.Value;
            return CultureInfo.CurrentUICulture.Name.Contains("zh");
        }

        /// <summary>
        /// 根据枚举的Description特性获取本地化显示名称
        /// </summary>
        public static string GetLocalizedEnumDisplayName<T>(T enumValue) where T : Enum
        {
            var type = typeof(T);
            if (!_enumCache.TryGetValue(type, out var dict))
            {
                dict = new System.Collections.Concurrent.ConcurrentDictionary<string, string[]>();
                foreach (var field in type.GetFields())
                {
                    var attr = field.GetCustomAttribute<DescriptionAttribute>();
                    if (attr != null && !string.IsNullOrEmpty(attr.Description) && attr.Description.Contains('|'))
                    {
                        dict[field.Name] = attr.Description.Split('|', 2);
                    }
                }
                _enumCache[type] = dict;
            }

            if (dict.TryGetValue(enumValue.ToString(), out var parts) && IsChineseLanguage() && parts.Length > 1)
            {
                return parts[1];
            }
            return parts != null ? parts[0] : enumValue.ToString();
        }

        /// <summary>
        /// 设置本地化的ToolTip
        /// </summary>
        public static void SetLocalizedToolTip(System.Windows.FrameworkElement element, string? tooltipText)
        {
            if (string.IsNullOrEmpty(tooltipText)) return;
            if (tooltipText.Contains('|'))
            {
                var parts = tooltipText.Split('|', 2);
                element.ToolTip = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
                void UpdateTip() { element.Dispatcher.Invoke(() => { var p = tooltipText.Split('|', 2); element.ToolTip = IsChineseLanguage() && p.Length > 1 ? p[1] : p[0]; }); }
                LanguageChanged += UpdateTip;
                element.Unloaded += (_, _) => LanguageChanged -= UpdateTip;
            }
            else
            {
                element.ToolTip = tooltipText;
            }
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Collections.Concurrent.ConcurrentDictionary<string, string[]>> _enumCache = new();
    }
}