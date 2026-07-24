using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using YFrame.Installer.Controls;

namespace YFrame.Installer.Views;

public partial class FinishPage : UserControl
{
    public FinishPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var scaleAnim = new DoubleAnimation(0, 1.2, TimeSpan.FromMilliseconds(400))
            { EasingFunction = new ElasticEase { Oscillations = 3, Springiness = 5 } };
        scaleAnim.Completed += (s, a) =>
        {
            var bounceBack = new DoubleAnimation(1.2, 1.0, TimeSpan.FromMilliseconds(200))
                { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            CheckBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounceBack);
            CheckBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounceBack);
        };
        CheckBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
        CheckBorder.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

        var colors = new[]
        {
            Color.FromRgb(255, 107, 107), Color.FromRgb(255, 217, 61),
            Color.FromRgb(107, 203, 119), Color.FromRgb(77, 150, 255),
            Color.FromRgb(155, 89, 182), Color.FromRgb(63, 185, 80)
        };
        var random = new Random();
        ConfettiParticles.ParticleColor = new SolidColorBrush(colors[random.Next(colors.Length)]);
    }
}
