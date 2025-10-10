using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using krrTools.Localization;
using Border = Wpf.Ui.Controls.Border;
using Button = Wpf.Ui.Controls.Button;
using StackPanel = Wpf.Ui.Controls.StackPanel;
using TextBlock = Wpf.Ui.Controls.TextBlock;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace krrTools.UI
{
    /// <summary>
    /// 统一的UI控件库，为所有工具提供一致的界面组件和样式
    /// </summary>
    public static class SharedUIComponents
    {
        public static event Action? LanguageChanged
        {
            add => LocalizationService.LanguageChanged += value;
            remove => LocalizationService.LanguageChanged -= value;
        }

        // 统一UI样式相关常量
        private static readonly Brush UiTextBrush = UIConstants.UiTextBrush;
        public const double HeaderFontSize = UIConstants.HeaderFontSize;
        private const double ComFontSize = UIConstants.CommonFontSize;

        private static SolidColorBrush _panelBackgroundBrush = new(Color.FromArgb(102, 255, 255, 255));

        public static Brush PanelBackgroundBrush
        {
            get => _panelBackgroundBrush;
            set
            {
                if (value is SolidColorBrush scb) _panelBackgroundBrush = new SolidColorBrush(scb.Color);
                else _panelBackgroundBrush = new SolidColorBrush(_panelBackgroundBrush.Color);
            }
        }

        public static readonly Brush PanelBorderBrush = UIConstants.PanelBorderBrush;
        public static readonly CornerRadius PanelCornerRadius = UIConstants.PanelCornerRadius;
        private static readonly Thickness PanelPadding = UIConstants.PanelPadding;

        public static Border CreateStandardPanel(UIElement inner, Thickness? margin = null, Thickness? padding = null)
        {
            var border = new Border
            {
                Background = PanelBackgroundBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = PanelCornerRadius,
                Padding = PanelPadding,
                Child = inner
            };

            if (margin.HasValue) border.Margin = margin.Value;
            if (padding.HasValue) border.Padding = padding.Value;
            return border;
        }

        public static TextBlock CreateHeaderLabel(string text)
        {
            var tb = new TextBlock
            {
                FontSize = HeaderFontSize,
                FontWeight = FontWeights.Bold,
                Foreground = UiTextBrush
            };

            if (!string.IsNullOrEmpty(text) && text.Contains('|'))
                tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                    new Binding("Value") { Source = text.GetLocalizedString() });
            else
                tb.Text = text;

            return tb;
        }

        public static FrameworkElement CreateLabeledRow(string labelText, UIElement control, Thickness rowMargin)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical, Margin = rowMargin };
            var label = CreateHeaderLabel(labelText);
            label.Margin = new Thickness(0, 0, 0, 4);
            panel.Children.Add(label);
            if (control is FrameworkElement fe) panel.Children.Add(fe);
            return panel;
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
            var tb = new TextBlock { FontSize = ComFontSize, TextTrimming = TextTrimming.CharacterEllipsis };

            if (!string.IsNullOrEmpty(content) && content.Contains('|'))
                tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                    new Binding("Value") { Source = content.GetLocalizedString() });
            else
                tb.Text = content;
            var btn = new Button
            {
                Content = tb,
                // cap padding so distance from text to button edge is at most 5
                Padding = new Thickness(5),
                // small default margin to match app style
                Margin = new Thickness(5, 0, 0, 0),
                FontSize = ComFontSize,
                Background = PanelBackgroundBrush,
                BorderBrush = PanelBorderBrush,
                BorderThickness = new Thickness(1),
                Foreground = UiTextBrush
            };
        
            LocalizationService.SetLocalizedToolTip(btn, tooltip);

            return btn;
        }

        public static CheckBox CreateStandardCheckBox(string content, string? tooltip = null)
        {
            var cb = new CheckBox
                { FontSize = ComFontSize, Margin = new Thickness(2, 0, 10, 0), Background = Brushes.Transparent };
            var tb = new TextBlock { FontSize = ComFontSize, TextWrapping = TextWrapping.Wrap };
            if (!string.IsNullOrEmpty(content) && content.Contains('|'))
                tb.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
                    new Binding("Value") { Source = content.GetLocalizedString() });
            else
                tb.Text = content;
            cb.Content = tb;
        
            LocalizationService.SetLocalizedToolTip(cb, tooltip);
        
            return cb;
        }
    }
}