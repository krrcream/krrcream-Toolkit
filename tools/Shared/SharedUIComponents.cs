using System;
using System.Windows;
using System.Windows.Media;
using System.Reflection; // 添加此引用以支持反射获取特性
using System.ComponentModel; // 添加此引用以支持DescriptionAttribute
using System.Globalization; // 添加此引用以支持区域性设置
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Windows.Controls;
using krrTools.tools.Shared;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace krrTools.Tools.Shared
{
    /// <summary>
    /// 统一的UI控件库，为所有工具提供一致的界面组件和样式
    /// </summary>
    public static class SharedUIComponents
    {
        // Optional override for language selection. If null, the system culture is used.
        private static bool? ForceChinese { get; set; }

        // Event raised when the effective language selection changes (so UI can update)
        public static event Action? LanguageChanged;

        // Set the ForceChinese flag and notify listeners
        private static void SetForceChinese(bool? forceChinese)
        {
            ForceChinese = forceChinese;
            // Persist immediately
            SaveAppSettings();

            // Asynchronous notification to avoid blocking UI
            Application.Current.Dispatcher.BeginInvoke(() => 
            {
                LanguageChanged?.Invoke();
            });
        }

        // Toggle the language selection (used by simple toggle UIs)
        public static void ToggleLanguage()
        {
            SetForceChinese(!(ForceChinese ?? !IsChineseLanguage()));
        }

        // Simple app-level settings persisted to LocalAppData/krrTools/appsettings.json
        private class AppSettings
        {
            public bool? ForceChineseValue { get; init; }
        }

        private static readonly string _appSettingsPath;

        static SharedUIComponents()
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OptionsManager.BaseAppFolderName);
                Directory.CreateDirectory(folder);
                _appSettingsPath = Path.Combine(folder, "appsettings.json");
                LoadAppSettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SharedUIComponents static ctor failed: {ex.Message}");
                _appSettingsPath = string.Empty;
            }
        }

        private static void LoadAppSettings()
        {
            try
            {
                if (string.IsNullOrEmpty(_appSettingsPath) || !File.Exists(_appSettingsPath)) return;
                var json = File.ReadAllText(_appSettingsPath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var s = JsonSerializer.Deserialize<AppSettings>(json, opts);
                if (s is { ForceChineseValue: not null })
                {
                    ForceChinese = s.ForceChineseValue;
                }
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Failed to load app settings (IO): {ex.Message}");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to load app settings (JSON): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Failed to load app settings (unauthorized): {ex.Message}");
            }
        }

        private static void SaveAppSettings()
        {
            try
            {
                if (string.IsNullOrEmpty(_appSettingsPath)) return;
                var s = new AppSettings { ForceChineseValue = ForceChinese };
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(s, opts);
                File.WriteAllText(_appSettingsPath, json);
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Failed to save app settings (IO): {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"Failed to save app settings (unauthorized): {ex.Message}");
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Failed to save app settings (JSON): {ex.Message}");
            }
        }

        // 统一UI样式相关常量，参考LN工具的字体风格
        // Text color used throughout the Fluent-like UI — exposed public so other parts of the app can reuse it
        public static readonly Brush UiTextBrush = new SolidColorBrush(Color.FromRgb(0x21, 0x21, 0x21));
        public const double HeaderFontSize = 20.0;
        public const double ComFontSize = 18.0;
        // Fluent / acrylic visual tokens
        // Use a lighter translucent tint so the underlying acrylic blur is visible (less opaque)
        private static SolidColorBrush _panelBackgroundBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)); // ~40% white tint
        public static Brush PanelBackgroundBrush
        {
            get => _panelBackgroundBrush;
            set
            {
                if (value is SolidColorBrush scb) _panelBackgroundBrush = new SolidColorBrush(scb.Color);
                else _panelBackgroundBrush = new SolidColorBrush(_panelBackgroundBrush.Color);
            }
        }

        public static void SetPanelBackgroundAlpha(byte alpha)
        {
            // Ensure brush is not frozen before modifying
            if (_panelBackgroundBrush.IsFrozen)
                _panelBackgroundBrush = _panelBackgroundBrush.Clone();
            var c = _panelBackgroundBrush.Color;
            _panelBackgroundBrush.Color = Color.FromArgb(alpha, c.R, c.G, c.B);
        }
         // Softer border for the frosted look
         public static readonly Brush PanelBorderBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x00, 0x00, 0x00)); // subtle dark border ~20%
         public static readonly CornerRadius PanelCornerRadius = new CornerRadius(8);
         private static readonly Thickness PanelPadding = new Thickness(8);

        /// <summary>
        /// Helper to create a rounded acrylic panel with common padding and border.
        /// </summary>
        private static Border CreateAcrylicPanel(UIElement inner)
        {
            return new Border
            {
                Background = PanelBackgroundBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = PanelCornerRadius,
                Padding = PanelPadding,
                Child = inner
            };
        }
        
        /// <summary>
        /// Creates an acrylic panel with common background, border, and corner radius, allowing optional margin and padding.
        /// </summary>
        public static Border CreateStandardPanel(UIElement inner, Thickness? margin = null, Thickness? padding = null)
        {
            var border = CreateAcrylicPanel(inner);
            if (margin.HasValue) border.Margin = margin.Value;
            if (padding.HasValue) border.Padding = padding.Value;
            return border;
        }
         
         public static bool IsChineseLanguage()
         {
             if (ForceChinese.HasValue) return ForceChinese.Value;
             return CultureInfo.CurrentUICulture.Name.Contains("zh");
         }

         private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Collections.Concurrent.ConcurrentDictionary<string, string[]>> _enumCache = new();

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
         public static void SetLocalizedToolTip(FrameworkElement element, string? tooltipText)
         {
             if (string.IsNullOrEmpty(tooltipText)) return;
             if (tooltipText.Contains('|'))
             {
                 var parts = tooltipText.Split('|', 2);
                 element.ToolTip = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
                 void UpdateTip() { var p = tooltipText.Split('|', 2); element.ToolTip = IsChineseLanguage() && p.Length > 1 ? p[1] : p[0]; }
                 LanguageChanged += UpdateTip;
                 element.Unloaded += (_, _) => LanguageChanged -= UpdateTip;
             }
             else
             {
                 element.ToolTip = tooltipText;
             }
         }

         public static TextBlock CreateStandardTextBlock()
         {
             return new TextBlock
             {
                 FontSize = ComFontSize,
                 Foreground = UiTextBrush
             };
         }
         
         public static TextBlock CreateHeaderLabel(string text)
         {
             var tb = new TextBlock { FontSize = HeaderFontSize, FontWeight = FontWeights.Bold };
             if (!string.IsNullOrEmpty(text) && text.Contains('|'))
             {
                 void Update()
                 {
                     var parts = text.Split('|', 2);
                     tb.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
                 }
                 Update();
                 LanguageChanged += Update;
                 tb.Unloaded += (_, _) => LanguageChanged -= Update;
             }
             else
             {
                 tb.Text = text;
             }
             return tb;
         }
  
         public static FrameworkElement CreateLabeledRow(string labelText, UIElement control, Thickness rowMargin)
         {
             var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
             var label = CreateHeaderLabel(labelText);
             if (!string.IsNullOrEmpty(labelText) && labelText.Contains('|'))
             {
                 void Update()
                 {
                     var parts = labelText.Split('|', 2);
                     label.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
                 }
                 Update();
                 LanguageChanged += Update;
                 label.Unloaded += (_, _) => LanguageChanged -= Update;
             }
             else
             {
                 label.Text = labelText;
             }
             label.Margin = new Thickness(0, 0, 0, 4);
             panel.Children.Add(label);
             if (control is FrameworkElement fe) panel.Children.Add(fe);
             return panel;
         }
         
         public static Slider CreateStandardSlider(double minimum, double maximum, double height, bool isSnapToTickEnabled)
         {
             return new Slider
             {
                 Minimum = minimum,
                 Maximum = maximum,
                 Height = double.IsNaN(height) ? double.NaN : height,
                 IsSnapToTickEnabled = isSnapToTickEnabled,
                 HorizontalAlignment = HorizontalAlignment.Stretch
             };
         }
         
         public static TextBox CreateStandardTextBox()
         {
             return new TextBox
             {
                 FontSize = ComFontSize,
                 VerticalContentAlignment = VerticalAlignment.Center,
                 Background = Brushes.Transparent,
                 BorderBrush = PanelBorderBrush,
                 Padding = new Thickness(6)
             };
         }
         
         public static Button CreateStandardButton(string content, string? tooltip = null)
         {
             // Use a TextBlock as content so we can measure and shrink the font when needed.
             var tb = new TextBlock { FontSize = ComFontSize, TextTrimming = TextTrimming.CharacterEllipsis };
             // Bilingual support: if content contains '|', split and update on language change
             if (!string.IsNullOrEmpty(content) && content.Contains('|'))
             {
                 void Update()
                 {
                     var parts = content.Split(['|'], 2);
                     tb.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
                 }
                 Update();
                 LanguageChanged += Update;
                 // remove handler when button unloads
             }
             else
             {
                 tb.Text = content;
             }
             var btn = new Button
             {
                 Content = tb,
                 // cap padding so distance from text to button edge is at most 5
                 Padding = new Thickness(5),
                 // small default margin to match app style
                 Margin = new Thickness(5, 0, 0, 0),
                 FontSize = ComFontSize,
                 Background = Brushes.Transparent,
                 BorderBrush = PanelBorderBrush,
                 BorderThickness = new Thickness(1),
                 Foreground = UiTextBrush
             };

             // Attach auto-shrink behavior so long text reduces font to fit available width
             AttachAutoShrinkBehavior(btn, tb);

             // Set tooltip
             SetLocalizedToolTip(btn, tooltip);

             // Handle language change unloading
             if (!string.IsNullOrEmpty(content) && content.Contains('|'))
             {
                 void UpdateText()
                 {
                     var parts = content.Split('|', 2);
                     tb.Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0];
                 }
                 LanguageChanged += UpdateText;
                 btn.Unloaded += (_, _) => LanguageChanged -= UpdateText;
             }

             return btn;
         }

         private static void AttachAutoShrinkBehavior(Button btn, TextBlock tb)
         {
             // btn and tb are non-nullable parameters
              double minFont = 8.0;
              double maxFont = tb.FontSize;
  
              void Adjust()
              {
                     if (btn.ActualWidth <= 0) return;
                     // available width inside the button minus horizontal padding
                     var leftPad = Math.Min(btn.Padding.Left, 5.0);
                     var rightPad = Math.Min(btn.Padding.Right, 5.0);
                     var pad = leftPad + rightPad + 2.0; // 2px for border/measure slack
                     double available = Math.Max(0, btn.ActualWidth - pad);

                     tb.FontSize = maxFont;
                     tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                     double desired = tb.DesiredSize.Width;
                     while (desired > available && tb.FontSize > minFont)
                     {
                         tb.FontSize = Math.Max(minFont, tb.FontSize - 0.5);
                         tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                         desired = tb.DesiredSize.Width;
                     }
              }
  
             btn.SizeChanged += (_, _) => Adjust();
             tb.TargetUpdated += (_, _) => Adjust();
             btn.Loaded += (_, _) => Adjust();

             // Also respond to language changes (text may change) so we re-measure and shrink if needed
             LanguageChanged += Adjust;
             btn.Unloaded += (_, _) => LanguageChanged -= Adjust;
          }

          public static CheckBox CreateStandardCheckBox(string content, string? tooltip = null)
          {
              var cb = new CheckBox { FontSize = ComFontSize, Margin = new Thickness(2, 0, 10, 0), Background = Brushes.Transparent };
              if (!string.IsNullOrEmpty(content) && content.Contains('|'))
              {
                 var parts = content.Split('|', 2);
                 var tb = new TextBlock { Text = IsChineseLanguage() && parts.Length > 1 ? parts[1] : parts[0], FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
                  cb.Content = tb;
                 void UpdateText() { var p = content.Split('|', 2); tb.Text = IsChineseLanguage() && p.Length > 1 ? p[1] : p[0]; }
                 LanguageChanged += UpdateText;
                 cb.Unloaded += (_, _) => LanguageChanged -= UpdateText;
              }
              else
              {
                  cb.Content = new TextBlock { Text = content, FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
              }
              SetLocalizedToolTip(cb, tooltip);
              return cb;
          }

          /// <summary>
          /// Applies shared default styles for common controls to the application resources.
          /// </summary>
          public static void ApplyDefaultControlStyles(ResourceDictionary appRes)
          {
              // Ensure default font settings
              var famKey = nameof(ResourceKeys.AppFontFamily);
              var sizeKey = nameof(ResourceKeys.AppFontSize);
              if (!appRes.Contains(famKey)) appRes[famKey] = new FontFamily("Segoe UI");
              if (!appRes.Contains(sizeKey)) appRes[sizeKey] = 14.0;

              // Button style
              if (!appRes.Contains(typeof(Button)))
              {
                  var btnStyle = new Style(typeof(Button));
                  btnStyle.Setters.Add(new Setter(Control.FontFamilyProperty, appRes[famKey]));
                  btnStyle.Setters.Add(new Setter(Control.FontSizeProperty, appRes[sizeKey]));
                  btnStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 6, 10, 6)));
                  btnStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
                  btnStyle.Setters.Add(new Setter(Control.VerticalContentAlignmentProperty, VerticalAlignment.Center));
                  btnStyle.Setters.Add(new Setter(Control.BackgroundProperty, PanelBackgroundBrush));
                  btnStyle.Setters.Add(new Setter(Control.ForegroundProperty, UiTextBrush));
                  btnStyle.Setters.Add(new Setter(Control.BorderBrushProperty, PanelBorderBrush));
                  btnStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
                  appRes[typeof(Button)] = btnStyle;
              }

              // TextBox style
              if (!appRes.Contains(typeof(TextBox)))
              {
                  var tbStyle = new Style(typeof(TextBox));
                  tbStyle.Setters.Add(new Setter(Control.FontFamilyProperty, appRes[famKey]));
                  tbStyle.Setters.Add(new Setter(Control.FontSizeProperty, appRes[sizeKey]));
                  tbStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6)));
                  appRes[typeof(TextBox)] = tbStyle;
              }

              // TextBlock style
              if (!appRes.Contains(typeof(TextBlock)))
              {
                  var tbs = new Style(typeof(TextBlock));
                  tbs.Setters.Add(new Setter(Control.FontFamilyProperty, appRes[famKey]));
                  tbs.Setters.Add(new Setter(Control.FontSizeProperty, appRes[sizeKey]));
                  appRes[typeof(TextBlock)] = tbs;
              }

              // DataGrid style
              if (!appRes.Contains(typeof(DataGrid)))
              {
                  var dgStyle = new Style(typeof(DataGrid));
                  dgStyle.Setters.Add(new Setter(Control.FontFamilyProperty, appRes[famKey]));
                  dgStyle.Setters.Add(new Setter(Control.FontSizeProperty, appRes[sizeKey]));
                  dgStyle.Setters.Add(new Setter(DataGrid.RowHeightProperty, 20.0));
                  dgStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(4)));
                  appRes[typeof(DataGrid)] = dgStyle;
              }
          }
     }
 }
