using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// 通过 OnStartup 构建 DI 容器并启动主窗口
    /// </summary>
    public partial class App : Application
    {
        public static YF_Manager_Log logger = new YF_Manager_Log("App", "Interaction App");

        /// <summary>
        /// 应用启动入口：构建 DI 容器、创建主窗口
        /// 替代 StartupUri，改为代码手动启动以支持依赖注入
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // 调用父类（Application）的 OnStartup 方法, 确保 WPF 框架的标准行为得到执行
            base.OnStartup(e);

            // 初始化静态 logger（供 LogInterceptor 等底层组件使用）
            YF_Manager_Main.logger = new YF_Manager_Log("主控类", "YF_Manager");

            // 构建 DI 容器
            var services = new ServiceCollection();
            ConfigureServices(services);
            var provider = services.BuildServiceProvider();

            // 设置全局 DI 持有者，供插件和底层库解析服务
            YF_Di.Provider = provider;

            // 从 DI 容器获取主窗口（ViewModel 通过属性注入已完成初始化）
            var mainWindow = provider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        /// <summary>
        /// 注册所有服务到 DI 容器
        /// </summary>
        private static void ConfigureServices(ServiceCollection services)
        {
            // ===== YF_Manager 层：AOP 单例服务（为保持插件兼容性） =====
            // 注册服务
            services.AddSingleton(_ => YF_Messenger.Instance);
            services.AddSingleton(_ => YF_FileHelper.Instance);
            services.AddSingleton<YF_Manager_Log>(sp =>
                new YF_Manager_Log("主框架", "YF_Frame", sp.GetRequiredService<YF_FileHelper>()));

            // ===== YFrame 层：非 AOP 服务（直接构造函数注入） =====
            services.AddSingleton(sp => new LogService(
                sp.GetRequiredService<YF_Manager_Log>(),
                sp.GetRequiredService<YF_Messenger>()
            ));

            // UserControlsService：AOP 代理 + 依赖注入
            services.AddSingleton(sp =>
            {
                var proxy = new ProxyGenerator().CreateClassProxy<UserControlsService>(
                    new LogInterceptor()
                );
                // 暂时不设回调，等 PluginService 创建后再设置
                proxy.InitializeDependencies(
                    sp.GetRequiredService<YF_Manager_Log>(),
                    null! // 回调稍后由 MainWindowViewModel 注入
                );
                return proxy;
            });

            // PluginService：依赖 YF_Manager_Log + YF_Messenger + UserControlsService
            services.AddSingleton(sp => new PluginService(
                sp.GetRequiredService<YF_Manager_Log>(),
                sp.GetRequiredService<YF_Messenger>(),
                sp.GetRequiredService<UserControlsService>()
            ));

            // HotkeyService：AOP 代理
            services.AddSingleton(_ =>
                new ProxyGenerator().CreateClassProxy<HotkeyService>(new LogInterceptor())
            );

            // TrayIconService：AOP 代理
            services.AddSingleton(_ =>
                new ProxyGenerator().CreateClassProxy<TrayIconService>(new LogInterceptor())
            );

            // ===== MainWindowViewModel：AOP 代理 + 全部依赖注入 =====
            services.AddSingleton(sp =>
            {
                // 解析所有子服务
                var logService = sp.GetRequiredService<LogService>();
                var pluginService = sp.GetRequiredService<PluginService>();
                var userControlsService = sp.GetRequiredService<UserControlsService>();
                var hotkeyService = sp.GetRequiredService<HotkeyService>();
                var trayIconService = sp.GetRequiredService<TrayIconService>();
                var messenger = sp.GetRequiredService<YF_Messenger>();
                var fileHelper = sp.GetRequiredService<YF_FileHelper>();

                // 设置 UserControlsService 的回调（将其转发到 PluginService）
                userControlsService.OnPluginCallback = (pluginId, evt) =>
                    pluginService.HandlePluginCallback(pluginId, evt);

                // 创建 MainWindowViewModel 的 AOP 代理
                var proxy = new ProxyGenerator().CreateClassProxy<MainWindowViewModel>(
                    new LogInterceptor()
                );

                // 属性注入所有依赖
                proxy.InitializeDependencies(
                    sp.GetRequiredService<YF_Manager_Log>(),
                    logService,
                    pluginService,
                    userControlsService,
                    hotkeyService,
                    trayIconService,
                    messenger,
                    fileHelper
                );

                return proxy;
            });

            // ===== MainWindow：通过构造函数注入 ViewModel =====
            services.AddSingleton(sp =>
            {
                var vm = sp.GetRequiredService<MainWindowViewModel>();
                var hotkeyService = sp.GetRequiredService<HotkeyService>();
                var trayIconService = sp.GetRequiredService<TrayIconService>();
                return new MainWindow(vm, hotkeyService, trayIconService);
            });
        }

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
