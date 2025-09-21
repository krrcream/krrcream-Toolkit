using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace krrTools.tools.Get_files
{
    public partial class GetFilesWindow
    {
        public GetFilesWindow()
        {
            InitializeComponent();
            var viewModel = new GetFilesViewModel();
            DataContext = viewModel;
        }

        private async void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = FileDataGrid.SelectedItems.Cast<OsuFileInfo>().ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No items selected.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string message = $"Are you sure you want to delete {selectedItems.Count} .osu file(s)? This action cannot be undone.";
            string caption = "Confirm Delete";

            var result = MessageBox.Show(message, caption, MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                var vm = (GetFilesViewModel)DataContext;
                await DeleteSelectedFilesAsync(selectedItems, vm);
            }
        }

        //异步后台删除文件
        private Task DeleteSelectedFilesAsync(List<OsuFileInfo> filesToDelete, GetFilesViewModel viewModel)
        {
            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file.FilePath))
                    {
                        File.Delete(file.FilePath);
                    }

                    // 从主列表和过滤视图中移除
                    viewModel.OsuFiles.Remove(file);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to delete {file.FilePath}: {ex.Message}");
                }
            }

            // 刷新ICollectionView以更新UI
            viewModel.FilteredOsuFiles.Refresh();

            MessageBox.Show($"{filesToDelete.Count} file(s) deleted successfully.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
            return Task.CompletedTask;
        }
    }

    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;
            return true;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return !booleanValue;
            return false;
        }
    }
}
