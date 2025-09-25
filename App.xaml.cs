using System.Windows;
using Wpf.Ui;

namespace krrTools;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var main = new MainWindow();
        main.Show();
    }
}