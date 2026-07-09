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
                // 移除旧的主题资源（查找文件名含 "Theme" 的字典）
                var oldDict = Application.Current.Resources.MergedDictionaries
                    .FirstOrDefault(d => d.Source?.ToString().Contains("Theme") == true);
                if (oldDict != null)
                {
                    Application.Current.Resources.MergedDictionaries.Remove(oldDict);
                }

                // 加载新主题
                var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(newTheme);
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("ChangeTheme", ex.Message);
                MessageBox.Show($"主题切换失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
