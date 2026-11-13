using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace YFrame.Controls
{
    /// <summary>
    /// 逐字竖排文本控件：用于概览页折叠卡上的竖排标题。
    /// 不依赖 RotateTransform，避免被窄布局槽裁切或换行。
    /// </summary>
    public class VerticalText : FrameworkElement
    {
        /// <summary>要竖排显示的文本</summary>
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(VerticalText),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>文字颜色</summary>
        public static readonly DependencyProperty ForegroundProperty =
            DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(VerticalText),
                new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>字号</summary>
        public static readonly DependencyProperty FontSizeProperty =
            DependencyProperty.Register(nameof(FontSize), typeof(double), typeof(VerticalText),
                new FrameworkPropertyMetadata(11.5,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>字体</summary>
        public static readonly DependencyProperty FontFamilyProperty =
            DependencyProperty.Register(nameof(FontFamily), typeof(FontFamily), typeof(VerticalText),
                new FrameworkPropertyMetadata(new FontFamily("DengXian"),
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>字重</summary>
        public static readonly DependencyProperty FontWeightProperty =
            DependencyProperty.Register(nameof(FontWeight), typeof(FontWeight), typeof(VerticalText),
                new FrameworkPropertyMetadata(FontWeights.Normal,
                    FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Brush Foreground
        {
            get => (Brush)GetValue(ForegroundProperty);
            set => SetValue(ForegroundProperty, value);
        }

        public double FontSize
        {
            get => (double)GetValue(FontSizeProperty);
            set => SetValue(FontSizeProperty, value);
        }

        public FontFamily FontFamily
        {
            get => (FontFamily)GetValue(FontFamilyProperty);
            set => SetValue(FontFamilyProperty, value);
        }

        public FontWeight FontWeight
        {
            get => (FontWeight)GetValue(FontWeightProperty);
            set => SetValue(FontWeightProperty, value);
        }

        /// <summary>
        /// 逐字测量：宽度取最宽字符，高度为字数 × 行距
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            string[] chars = SplitTextElements(Text);
            double lineHeight = FontSize * 1.35;
            double width = 0;

            foreach (string ch in chars)
            {
                var formatted = CreateFormattedText(ch);
                width = Math.Max(width, formatted.Width);
            }

            return new Size(width, chars.Length * lineHeight);
        }

        /// <summary>
        /// 逐字绘制：每个字符水平居中，按行距向下排列
        /// </summary>
        protected override void OnRender(DrawingContext dc)
        {
            string[] chars = SplitTextElements(Text);
            if (chars.Length == 0) return;

            double lineHeight = FontSize * 1.35;
            double y = 0;
            foreach (string ch in chars)
            {
                var formatted = CreateFormattedText(ch);
                double x = (ActualWidth - formatted.Width) / 2;
                // 让每个字形在自己的行框内垂直居中，避免高低不齐
                dc.DrawText(formatted, new Point(x, y + (lineHeight - formatted.Height) / 2));
                y += lineHeight;
            }
        }

        /// <summary>
        /// 按文本元素（而非 UTF-16 码元）拆分，兼容代理对字符
        /// </summary>
        private static string[] SplitTextElements(string? text)
        {
            if (string.IsNullOrEmpty(text)) return Array.Empty<string>();

            var enumerator = StringInfo.GetTextElementEnumerator(text);
            var list = new System.Collections.Generic.List<string>();
            while (enumerator.MoveNext())
                list.Add(enumerator.GetTextElement());
            return list.ToArray();
        }

        /// <summary>
        /// 创建单个字符的 FormattedText，统一使用当前 DPI 渲染
        /// </summary>
        private FormattedText CreateFormattedText(string text)
        {
            return new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyles.Normal, FontWeight, FontStretches.Normal),
                FontSize,
                Foreground,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }
    }
}
