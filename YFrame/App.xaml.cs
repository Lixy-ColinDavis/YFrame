using System.Configuration;
using System.Data;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static YF_Manager_Log logger = new YF_Manager_Log("App", "Interaction App");

        /// <summary>
        /// 切换语言
        /// </summary>
        /// <param name="lang">zh / en</param>
        public static void ChangeLanguage(string lang)
        {
            try
            {
                var dict = new ResourceDictionary();
                dict.Source = lang switch
                {
                    "en" => new Uri("Common/Language/en-US.xaml", UriKind.Relative),
                    _ => new Uri("Common/Language/zh-CN.xaml", UriKind.Relative)
                };

                // 移除旧的语言资源
                var oldDict = Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.ToString().Contains("en-US") == true);
                if (oldDict == null)
                    oldDict = Current.Resources.MergedDictionaries
                        .FirstOrDefault(d => d.Source?.ToString().Contains("zh-CN") == true);
                if (oldDict != null)
                    Current.Resources.MergedDictionaries.Remove(oldDict);

                // 添加新的语言资源
                Current.Resources.MergedDictionaries.Add(dict);
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("ChangeLanguage", ex.Message);
            }
        }

        /// <summary>
        /// 切换主题
        /// </summary>
        /// <param name="themePath">主题文件相对路径，如 "Common/Themes/DarkGrayTheme.xaml"</param>
        public static void ChangeTheme(string themePath)
        {
            try
            {
                // 移除旧的主题资源（仅匹配 /Themes/*Theme.xaml，避免误删 ControlStyles.xaml）
                var merged = Application.Current.Resources.MergedDictionaries;
                int oldIndex = -1;
                for (int i = 0; i < merged.Count; i++)
                {
                    var src = merged[i].Source?.ToString();
                    if (src != null && src.Contains("/Themes/") && src.EndsWith("Theme.xaml"))
                    {
                        oldIndex = i;
                        break;
                    }
                }
                if (oldIndex >= 0)
                    merged.RemoveAt(oldIndex);

                // 在原位置插入新主题，保持 MergedDictionaries 顺序不变
                var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
                merged.Insert(oldIndex >= 0 ? oldIndex : 0, newTheme);
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("ChangeTheme", ex.Message);
                MessageBox.Show($"主题切换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
