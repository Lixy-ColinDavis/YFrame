using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace YFrame.Controls
{
    /// <summary>
    /// 轻量 Sparkline 控件：绘制平滑曲线 + 渐变面积，用于性能监视器。
    /// 不依赖第三方图表库，坐标轴/图例全部省略，只保留数据走势。
    /// </summary>
    public class Sparkline : FrameworkElement
    {
        /// <summary>
        /// 数据点集合（按时间从旧到新），变化时自动重绘
        /// </summary>
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 纵轴最大值，用于把数据归一化到绘制区域
        /// </summary>
        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(Sparkline),
                new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 曲线颜色
        /// </summary>
        public static readonly DependencyProperty StrokeProperty =
            DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(Sparkline),
                new FrameworkPropertyMetadata(Brushes.Cyan, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 曲线粗细
        /// </summary>
        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(Sparkline),
                new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 面积填充色；为空时按曲线颜色自动生成自上而下的透明渐变
        /// </summary>
        public static readonly DependencyProperty FillProperty =
            DependencyProperty.Register(nameof(Fill), typeof(Brush), typeof(Sparkline),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 是否在曲线下方绘制发光描边，增强霓虹感
        /// </summary>
        public static readonly DependencyProperty ShowGlowProperty =
            DependencyProperty.Register(nameof(ShowGlow), typeof(bool), typeof(Sparkline),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// 是否在最后一个数据点绘制圆点
        /// </summary>
        public static readonly DependencyProperty ShowEndPointProperty =
            DependencyProperty.Register(nameof(ShowEndPoint), typeof(bool), typeof(Sparkline),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public IReadOnlyList<double>? Values
        {
            get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public Brush Stroke
        {
            get => (Brush)GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public Brush? Fill
        {
            get => (Brush?)GetValue(FillProperty);
            set => SetValue(FillProperty, value);
        }

        public bool ShowGlow
        {
            get => (bool)GetValue(ShowGlowProperty);
            set => SetValue(ShowGlowProperty, value);
        }

        public bool ShowEndPoint
        {
            get => (bool)GetValue(ShowEndPointProperty);
            set => SetValue(ShowEndPointProperty, value);
        }

        /// <summary>
        /// 绘制平滑曲线、渐变面积与端点
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            var values = Values;
            double width = ActualWidth;
            double height = ActualHeight;
            if (values == null || values.Count < 2 || width <= 1 || height <= 1)
                return;

            // 上下留少量内边距，避免曲线贴边被裁切
            const double verticalPadding = 4;
            double usableHeight = Math.Max(1, height - verticalPadding * 2);
            double max = Maximum <= 0 ? 1 : Maximum;

            // 数据归一化：x 均分宽度，y 按最大值映射到绘制区域
            var points = new Point[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                double ratio = Math.Clamp(values[i] / max, 0, 1);
                double x = values.Count == 1 ? width / 2 : i * width / (values.Count - 1);
                double y = verticalPadding + usableHeight * (1 - ratio);
                points[i] = new Point(x, y);
            }

            // 用 Catmull-Rom 转三次贝塞尔，得到平滑曲线
            var lineGeometry = BuildSmoothGeometry(points, closeArea: false, areaBottom: height);
            var areaGeometry = BuildSmoothGeometry(points, closeArea: true, areaBottom: height);
            lineGeometry.Freeze();
            areaGeometry.Freeze();

            // 面积填充：优先使用外部 Fill，否则根据曲线色生成渐变
            Brush areaBrush = Fill ?? CreateAreaBrush(Stroke);
            dc.DrawGeometry(areaBrush, null, areaGeometry);

            // 发光描边（更粗、低透明度）增强霓虹效果
            if (ShowGlow && Stroke is SolidColorBrush)
            {
                dc.PushOpacity(0.22);
                var glowPen = new Pen(Stroke, StrokeThickness + 3)
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                glowPen.Freeze();
                dc.DrawGeometry(null, glowPen, lineGeometry);
                dc.Pop();
            }

            // 主曲线
            var linePen = new Pen(Stroke, StrokeThickness)
            {
                LineJoin = PenLineJoin.Round,
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            linePen.Freeze();
            dc.DrawGeometry(null, linePen, lineGeometry);

            // 端点圆点
            if (ShowEndPoint)
            {
                dc.DrawEllipse(Stroke, null, points[^1], 2.6, 2.6);
            }
        }

        /// <summary>
        /// 构建平滑曲线几何；closeArea 为 true 时闭合到底部形成面积区域
        /// </summary>
        /// <param name="points">已归一化的数据点</param>
        /// <param name="closeArea">是否闭合为面积</param>
        /// <param name="areaBottom">面积底部 Y 坐标</param>
        private static StreamGeometry BuildSmoothGeometry(Point[] points, bool closeArea, double areaBottom)
        {
            var geometry = new StreamGeometry();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                if (closeArea)
                {
                    // 从底部左侧起笔，保证填充区域完整覆盖曲线下方
                    ctx.BeginFigure(new Point(points[0].X, areaBottom), true, true);
                    ctx.LineTo(points[0], false, false);
                }
                else
                {
                    ctx.BeginFigure(points[0], false, false);
                }

                for (int i = 0; i < points.Length - 1; i++)
                {
                    Point p0 = points[Math.Max(0, i - 1)];
                    Point p1 = points[i];
                    Point p2 = points[i + 1];
                    Point p3 = points[Math.Min(points.Length - 1, i + 2)];

                    // Catmull-Rom 切线转贝塞尔控制点；限制在绘制区域内避免过冲
                    var c1 = new Point(
                        p1.X + (p2.X - p0.X) / 6,
                        Math.Clamp(p1.Y + (p2.Y - p0.Y) / 6, 0, areaBottom));
                    var c2 = new Point(
                        p2.X - (p3.X - p1.X) / 6,
                        Math.Clamp(p2.Y - (p3.Y - p1.Y) / 6, 0, areaBottom));

                    ctx.BezierTo(c1, c2, p2, true, false);
                }

                if (closeArea)
                {
                    // 收尾回到右下角并闭合
                    ctx.LineTo(new Point(points[^1].X, areaBottom), false, false);
                }
            }
            return geometry;
        }

        /// <summary>
        /// 根据曲线颜色生成自上而下的面积渐变，保持与主题强调色一致
        /// </summary>
        private static Brush CreateAreaBrush(Brush stroke)
        {
            Color color = stroke is SolidColorBrush solid ? solid.Color : Colors.Cyan;
            var brush = new LinearGradientBrush(
                Color.FromArgb(92, color.R, color.G, color.B),
                Color.FromArgb(0, color.R, color.G, color.B),
                90);
            brush.Freeze();
            return brush;
        }
    }
}
