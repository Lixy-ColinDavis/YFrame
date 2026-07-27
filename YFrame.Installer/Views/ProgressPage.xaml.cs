using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace YFrame.Installer.Views;

public partial class ProgressPage : UserControl
{
    private readonly Storyboard _rotateStoryboard;

    public ProgressPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        _rotateStoryboard = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };
        var rotateAnim = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.2));
        Storyboard.SetTarget(rotateAnim, LoadingCircle);
        Storyboard.SetTargetProperty(rotateAnim, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
        _rotateStoryboard.Children.Add(rotateAnim);
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => _rotateStoryboard.Begin();
    private void OnUnloaded(object sender, RoutedEventArgs e) => _rotateStoryboard.Stop();

    public void UpdateProgress(double value) => ProgressBar.Value = value;

    public void UpdateStatus(string status, string? detail = null)
    {
        StatusText.Text = status;
        DetailStatusText.Text = detail ?? "";
    }

    public void AppendLog(string message)
    {
        LogViewer.Visibility = Visibility.Visible;
        LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        LogViewer.ScrollToEnd();
    }

    public void StopAnimation()
    {
        _rotateStoryboard.Stop();
        LoadingCircle.Visibility = Visibility.Collapsed;
    }
}
