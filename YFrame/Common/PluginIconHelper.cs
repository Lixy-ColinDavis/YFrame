using System;
using System.Windows.Media;

namespace YFrame
{
    /// <summary>
    /// 插件图标辅助类：根据插件 ID / 名称生成稳定的缩写与渐变背景画刷。
    /// 主窗口插件列表与插件管理器共用，保证同一插件在两处图标一致。
    /// </summary>
    internal static class PluginIconHelper
    {
        /// <summary>
        /// 渐变配色表（起点色、终点色），取自 4 套设计稿中的插件图标色板
        /// </summary>
        private static readonly (string Start, string End)[] Palette =
        {
            ("#60A5FA", "#2563EB"), ("#A78BFA", "#7C3AED"), ("#F472B6", "#DB2777"),
            ("#34D399", "#059669"), ("#FBBF24", "#B45309"), ("#38BDF8", "#0284C7"),
            ("#818CF8", "#4F46E5"), ("#FB923C", "#C2410C"), ("#2DD4BF", "#0D9488"),
            ("#C084FC", "#9333EA")
        };

        /// <summary>
        /// 生成图标缩写：去掉 YF_ 前缀后取前两个字符（如 YF_Paint → PA）
        /// </summary>
        /// <param name="id">插件 ID，优先使用</param>
        /// <param name="name">插件名称，ID 为空时回退</param>
        /// <returns>1-2 个大写字符；两者都为空时返回 ?</returns>
        public static string GetText(string? id, string? name)
        {
            string source = string.IsNullOrWhiteSpace(id) ? (name ?? string.Empty) : id!;
            if (string.IsNullOrWhiteSpace(source)) return "?";
            source = source.StartsWith("YF_", StringComparison.OrdinalIgnoreCase)
                ? source.Substring(3)
                : source;
            return source.Length >= 2 ? source.Substring(0, 2).ToUpperInvariant()
                                      : source.ToUpperInvariant();
        }

        /// <summary>
        /// 生成图标渐变背景：按 ID 稳定哈希选色，保证同一插件颜色固定
        /// </summary>
        /// <param name="id">插件 ID，优先使用</param>
        /// <param name="name">插件名称，ID 为空时回退</param>
        /// <returns>已冻结的线性渐变画刷，可安全跨线程复用</returns>
        public static Brush GetBrush(string? id, string? name)
        {
            // 简易 FNV-1a 哈希：只用于稳定选色，不参与业务逻辑
            uint hash = 2166136261;
            foreach (char c in (string.IsNullOrWhiteSpace(id) ? (name ?? string.Empty) : id!))
            {
                hash ^= c;
                hash *= 16777619;
            }

            var pair = Palette[hash % (uint)Palette.Length];
            var brush = new LinearGradientBrush(
                (Color)ColorConverter.ConvertFromString(pair.Start),
                (Color)ColorConverter.ConvertFromString(pair.End),
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1));
            brush.Freeze();
            return brush;
        }
    }
}
