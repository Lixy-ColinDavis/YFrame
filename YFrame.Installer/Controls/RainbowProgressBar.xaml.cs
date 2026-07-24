using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace YFrame.Installer.Controls;

public partial class RainbowProgressBar : UserControl
{
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(RainbowProgressBar),
            new PropertyMetadata(0.0, OnValueChanged));

    public RainbowProgressBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var storyboard = (Storyboard)FindResource("ScanAnimation");
        storyboard.Begin();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not RainbowProgressBar bar || bar.ActualWidth <= 0) return;
        var value = Math.Max(0, Math.Min(1, (double)e.NewValue));
        bar.ProgressBorder.Width = bar.ActualWidth * value;
        bar.PercentText.Text = $"{value * 100:F0}%";
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        var targetWidth = ActualWidth * Value;
        var anim = new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        ProgressBorder.BeginAnimation(WidthProperty, anim);
    }
}
