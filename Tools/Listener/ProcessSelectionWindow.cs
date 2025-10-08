using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using krrTools.UI;
using Microsoft.Extensions.Logging;

namespace krrTools.Tools.Listener
{
    internal class ProcessSelectionWindow : Window
    {
        public Process? SelectedProcess { get; private set; }
        private ListBox? ProcessListBox;

        public ProcessSelectionWindow(Process[] processes)
        {
            BuildUI(processes);
            SharedUIComponents.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        Title = "选择osu!进程"; // TODO: localize if needed
                        // update any labels/buttons by rebuilding
                        var dc = DataContext;
                        Content = null;
                        BuildUI(null); // rebuild without processes? but need to keep
                        DataContext = dc;
                    }
                    catch (Exception ex) { Logger.WriteLine(LogLevel.Error, "[ProcessSelectionWindow] ProcessSelectionWindow inner rebuild failed: {0}", ex.Message); }
                }));
            }
            catch (Exception ex) { Logger.WriteLine(LogLevel.Error, "[ProcessSelectionWindow] ProcessSelectionWindow OnLanguageChanged invoke failed: {0}", ex.Message); }
        }

        private void BuildUI(Process[]? processes)
        {
            Title = "选择osu!进程";
            Width = 500;
            Height = 300;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var card = new Wpf.Ui.Controls.Card { Margin = new Thickness(20) };
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var tb = new Wpf.Ui.Controls.TextBlock { Text = "检测到多个osu!进程，请选择一个：", Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(tb, 0);
            grid.Children.Add(tb);

            ProcessListBox = new ListBox { Margin = new Thickness(0, 0, 0, 10) };
            if (processes != null)
            {
                foreach (var p in processes)
                {
                    try
                    {
                        string exePath = p.MainModule?.FileName ?? "Unknown";
                        ProcessListBox.Items.Add(new ListBoxItem { Content = $"PID: {p.Id}, Path: {exePath}", Tag = p });
                    }
                    catch
                    {
                        ProcessListBox.Items.Add(new ListBoxItem { Content = $"PID: {p.Id}, Path: Unknown", Tag = p });
                    }
                }
            }
            Grid.SetRow(ProcessListBox, 1);
            grid.Children.Add(ProcessListBox);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(sp, 2);

            var okBtn = new Wpf.Ui.Controls.Button { Content = "确定", Appearance = Wpf.Ui.Controls.ControlAppearance.Primary, Width = 80, Margin = new Thickness(0, 0, 10, 0) };
            okBtn.Click += OkButton_Click;
            var cancelBtn = new Wpf.Ui.Controls.Button { Content = "取消", Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary, Width = 80 };
            cancelBtn.Click += CancelButton_Click;

            sp.Children.Add(okBtn);
            sp.Children.Add(cancelBtn);
            grid.Children.Add(sp);

            card.Content = grid;
            Content = card;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProcessListBox?.SelectedItem is ListBoxItem { Tag: Process p })
            {
                SelectedProcess = p;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("请选择一个进程。");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        protected override void OnClosed(EventArgs e)
        {
            SharedUIComponents.LanguageChanged -= OnLanguageChanged;
            base.OnClosed(e);
        }
    }
}