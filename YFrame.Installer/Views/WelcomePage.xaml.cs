using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace YFrame.Installer.Views;

public partial class WelcomePage : UserControl
{
    public WelcomePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var storyboard = new Storyboard();

        var opacityAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(800))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(opacityAnim, TitleText);
        Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(opacityAnim);

        var scaleXAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(800))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(scaleXAnim, TitleText);
        Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
        storyboard.Children.Add(scaleXAnim);

        var scaleYAnim = new DoubleAnimation(0.8, 1.0, TimeSpan.FromMilliseconds(800))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(scaleYAnim, TitleText);
        Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
        storyboard.Children.Add(scaleYAnim);

        var subOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600))
            { BeginTime = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(subOpacity, SubtitleText);
        Storyboard.SetTargetProperty(subOpacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(subOpacity);

        var subTransY = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(600))
            { BeginTime = TimeSpan.FromMilliseconds(300), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        Storyboard.SetTarget(subTransY, SubtitleText);
        Storyboard.SetTargetProperty(subTransY, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(subTransY);

        var descOpacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600))
            { BeginTime = TimeSpan.FromMilliseconds(600) };
        Storyboard.SetTarget(descOpacity, DescText);
        Storyboard.SetTargetProperty(descOpacity, new PropertyPath(OpacityProperty));
        storyboard.Children.Add(descOpacity);

        var descTransY = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(600))
            { BeginTime = TimeSpan.FromMilliseconds(600) };
        Storyboard.SetTarget(descTransY, DescText);
        Storyboard.SetTargetProperty(descTransY, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
        storyboard.Children.Add(descTransY);

        storyboard.Begin();
    }
}
