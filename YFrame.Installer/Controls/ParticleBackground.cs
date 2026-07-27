using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace YFrame.Installer.Controls;

public class ParticleBackground : Canvas
{
    public int ParticleCount
    {
        get => (int)GetValue(ParticleCountProperty);
        set => SetValue(ParticleCountProperty, value);
    }
    public static readonly DependencyProperty ParticleCountProperty =
        DependencyProperty.Register(nameof(ParticleCount), typeof(int), typeof(ParticleBackground),
            new PropertyMetadata(60, OnParticleCountChanged));

    public Brush ParticleColor
    {
        get => (Brush)GetValue(ParticleColorProperty);
        set => SetValue(ParticleColorProperty, value);
    }
    public static readonly DependencyProperty ParticleColorProperty =
        DependencyProperty.Register(nameof(ParticleColor), typeof(Brush), typeof(ParticleBackground),
            new PropertyMetadata(Brushes.White));

    private readonly List<Particle> _particles = new();
    private readonly Random _random = new();
    private DateTime _lastFrame;
    private Point _mousePosition = new(-100, -100);

    private class Particle
    {
        public Ellipse Shape { get; set; } = null!;
        public double X { get; set; }
        public double Y { get; set; }
        public double Vx { get; set; }
        public double Vy { get; set; }
        public double Size { get; set; }
        public double Opacity { get; set; }
    }

    public ParticleBackground()
    {
        ClipToBounds = true;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeParticles();
        _lastFrame = DateTime.Now;
        if (Window.GetWindow(this) is Window window)
            window.MouseMove += (_, args) => _mousePosition = args.GetPosition(this);
        CompositionTarget.Rendering += OnRendering;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    private void InitializeParticles()
    {
        Children.Clear();
        _particles.Clear();
        for (int i = 0; i < ParticleCount; i++)
        {
            var size = _random.NextDouble() * 3 + 1.5;
            var ellipse = new Ellipse
            {
                Width = size, Height = size, Fill = ParticleColor,
                Opacity = _random.NextDouble() * 0.5 + 0.2
            };
            var particle = new Particle
            {
                Shape = ellipse,
                X = _random.NextDouble() * (ActualWidth > 0 ? ActualWidth : 800),
                Y = _random.NextDouble() * (ActualHeight > 0 ? ActualHeight : 600),
                Vx = (_random.NextDouble() - 0.5) * 0.8,
                Vy = (_random.NextDouble() - 0.5) * 0.8,
                Size = size, Opacity = ellipse.Opacity
            };
            SetLeft(ellipse, particle.X);
            SetTop(ellipse, particle.Y);
            Children.Add(ellipse);
            _particles.Add(particle);
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var dt = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        if (dt > 0.05) dt = 0.05;
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        foreach (var p in _particles)
        {
            var dx = _mousePosition.X - p.X;
            var dy = _mousePosition.Y - p.Y;
            var dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > 0 && dist < 200)
            {
                var force = 20 / (dist + 10);
                p.Vx += dx / dist * force * dt;
                p.Vy += dy / dist * force * dt;
            }
            p.X += p.Vx * dt * 60;
            p.Y += p.Vy * dt * 60;
            if (p.X < 0) { p.X = 0; p.Vx = Math.Abs(p.Vx); }
            if (p.X > width) { p.X = width; p.Vx = -Math.Abs(p.Vx); }
            if (p.Y < 0) { p.Y = 0; p.Vy = Math.Abs(p.Vy); }
            if (p.Y > height) { p.Y = height; p.Vy = -Math.Abs(p.Vy); }
            p.Vx *= 0.998;
            p.Vy *= 0.998;
            SetLeft(p.Shape, p.X);
            SetTop(p.Shape, p.Y);
        }
    }

    private static void OnParticleCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ParticleBackground pb && pb.IsLoaded)
            pb.InitializeParticles();
    }
}
