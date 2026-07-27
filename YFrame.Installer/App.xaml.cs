using System;
using System.Windows;

namespace YFrame.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动失败:\n{ex.Message}\n\n{ex.StackTrace}",
                "YFrame Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }
}

