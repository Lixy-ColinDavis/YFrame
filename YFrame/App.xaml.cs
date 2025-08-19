using System.Configuration;
using System.Data;
using System.Windows;

namespace YFrame
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static void ChangeLanguage(string lang)
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
            Current.Resources.MergedDictionaries.Remove(oldDict);


            // 添加新的语言资源
            Current.Resources.MergedDictionaries.Add(dict);
        }
    }

}
