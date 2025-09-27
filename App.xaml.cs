using System.Windows;

namespace krrTools;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var main = new MainWindow();
        main.Show();
    }
}