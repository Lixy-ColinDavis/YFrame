using LiveCharts.Defaults;
using LiveCharts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YF_Manager;
using YFrame.Model;
using System.Reflection.Metadata;
using Castle.DynamicProxy;

namespace YFrame
{
    public class MainWindowViewModel : INotifyPropertyChanged, I_YF_Detail
    {
        #region INotifyPropertyChanged接口实现
        public event PropertyChangedEventHandler? PropertyChanged;


        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region 绑定属性

        private bool _leftVisible;  // 左抽屉显示状态
        public bool LeftVisible
        {
            get => _leftVisible;
            set
            {
                if (_leftVisible != value)
                {
                    _leftVisible = value;
                    OnPropertyChanged(nameof(LeftVisible));
                }
            }
        }

        private bool _rightVisible; // 右抽屉显示状态
        public bool RightVisible
        {
            get => _rightVisible;
            set
            {
                if (_rightVisible != value)
                {
                    _rightVisible = value;
                    OnPropertyChanged(nameof(RightVisible));
                }
            }
        }

        private string _txt_Cpu;  // CPU显示状态
        public string Txt_Cpu
        {
            get => _txt_Cpu;
            set
            {
                if (_txt_Cpu != value)
                {
                    _txt_Cpu = value;
                    OnPropertyChanged(nameof(Txt_Cpu));
                }
            }
        }

        private string _txt_Memory;  // 内存显示状态
        public string Txt_Memory
        {
            get => _txt_Memory;
            set
            {
                if (_txt_Memory != value)
                {
                    _txt_Memory = value;
                    OnPropertyChanged(nameof(Txt_Memory));
                }
            }
        }

        private string _logText;  // 日志显示
        public string LogText
        {
            get => _logText;
            set
            {
                if (_logText != value)
                {
                    _logText = value;
                    OnPropertyChanged(nameof(LogText));
                }
            }
        }



        private UserControl _performance_Monitor_View;  // 性能监视器
        public UserControl Performance_Monitor_View
        {
            get => _performance_Monitor_View;
            set
            {
                if (_performance_Monitor_View != value)
                {
                    _performance_Monitor_View = value;
                    OnPropertyChanged(nameof(Performance_Monitor_View));
                }
            }
        }

        private Grid _grid_Show_Array;  // 用户控件(插件)显示列表
        public Grid Grid_Show_Array
        {
            get => _grid_Show_Array;
            set
            {
                if (_grid_Show_Array != value)
                {
                    _grid_Show_Array = value;
                    OnPropertyChanged(nameof(Grid_Show_Array));
                }
            }
        }

        #endregion

        #region 绑定命令

        public ICommand Btn_Exit_Command { get; set; }                  // 退出事件
        public ICommand ToggleLeftToolWindowCommand { get; set; }       // 左侧抽屉事件
        public ICommand ToggleRightToolWindowCommand { get; set; }      // 右侧抽屉事件
        public ICommand ToggleLightThemeCommand { get; set; }           // 亮主题事件
        public ICommand ToggleDarkThemeCommand { get; set; }            // 暗主题事件
        public ICommand Title_Move_Command { get; set; }                // 窗体拖拽移动事件
        public ICommand Btn_Minimize_Command { get; set; }              // 窗体最小化事件
        public ICommand ToggleChineseCommand { get; set; }              // 中文切换事件
        public ICommand ToggleEnglishCommand { get; set; }              // 中文切换事件
        public ICommand Btn_Plugin_Show_Command { get; set; }           // 插件显示事件

        #endregion

        #region 全局变量

        // 委托-显示CPU、内存信息
        public static YF_Manager.YF_DelegateFunctionModel.dvFunc_Vs_s dlg_Show_Cpu_Memory;

        // 使用Lazy<T>确保线程安全的延迟初始化，避免双重检查锁定的复杂性
        // 单例模式+日志拦截器
        public static readonly Lazy<MainWindowViewModel> _instance = new Lazy<MainWindowViewModel>(
            () => new ProxyGenerator().CreateClassProxy<MainWindowViewModel>(new LogInterceptor())
            );

        public static MainWindowViewModel Instance => _instance.Value;

        #endregion

        #region 成员变量
        public ObservableCollection<PluginsModel> lsPlugins { get; } = new ObservableCollection<PluginsModel>(); // 插件列表

        CtrlDataModel CurrentUcDate { get; set; } // 当前显示的插件信息

        public string YF_ID => "YF_Frame";

        public string YF_Name => "主框架";

        // 日志对象
        public YF_Manager_Log logger;

        private readonly StringBuilder _logBuilder = new StringBuilder(); // 日志显示字符串
        private const int MaxLogLines = 500;    // 日志最大行数
        private readonly object _logLock = new();   // 日志锁
        private int _logLineCount = 0;

        #endregion

        public MainWindowViewModel()
        {

        }

        public void Init()
        {
            logger = new YF_Manager_Log(YF_Name, YF_ID);
            logger.LogInfo("主框架初始化-开始");
            InitUI();
            InitCommond();

            YF_Manager_Log.d_LogWrite = Show_Log;
            logger.LogInfo("主框架初始化-完成");
        }



        /// <summary>
        /// 初始化UI
        /// </summary>
        /// <remarks>
        /// </remarks>
        [Log(Level = LogLevel.Info, Message = "初始化UI")]
        public virtual void InitUI()
        {
            try
            {
                // 设置左右抽屉可见性
                LeftVisible = true;
                RightVisible = true;

                // 初始化性能监视器
                Performance_Monitor_View = new PerformanceMonitor();

                // 初始化插件显示区域
                Grid_Show_Array = new Grid();

                // 初始化插件加载器
                UserControlsService.Instance.LoadAndShowUserControl();

                // 插件列表添加
                foreach (var item in UserControlsService.Instance.DctControls)
                {
                    lsPlugins.Add(new PluginsModel() { Name = item.Value.Name, ID = item.Key, Status = 0 });
                }
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("InitUI", ex.Message);
            }
        }

        /// <summary>
        /// 初始化命令绑定
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "初始化命令绑定")]
        public virtual void InitCommond()
        {
            try
            {
                // 初始化命令
                // 左抽屉
                ToggleLeftToolWindowCommand = new YF_RelayCommand(() => { LeftVisible = !LeftVisible; logger.LogInfo("左侧边栏-" + (LeftVisible == true ? "开" : "关")); });
                // 右抽屉
                ToggleRightToolWindowCommand = new YF_RelayCommand(() => { RightVisible = !RightVisible; logger.LogInfo("右侧边栏-" + (RightVisible == true ? "开" : "关")); });
                // 关闭按钮
                Btn_Exit_Command = new YF_RelayCommand(() => { logger.LogInfo("退出程序"); Environment.Exit(0); });
                // 亮色主题
                ToggleLightThemeCommand = new YF_RelayCommand(() => { App.ChangeTheme("Common/Themes/LightTheme.xaml"); logger.LogInfo("主题切换-亮"); });
                // 暗色主题
                ToggleDarkThemeCommand = new YF_RelayCommand(() => { App.ChangeTheme("Common/Themes/DarkTheme.xaml"); logger.LogInfo("主题切换-暗"); });
                // 最小化
                Btn_Minimize_Command = new YF_RelayCommand(() => { Application.Current.MainWindow.WindowState = WindowState.Minimized; logger.LogInfo("窗体最小化"); });
                // 移动事件
                Title_Move_Command = new YF_RelayCommand<object>(param =>
                {
                    try
                    {
                        if (param is Border border)
                        {
                            var window = Window.GetWindow(border);
                            window?.DragMove();
                        }
                        else if (param is FrameworkElement element)
                        {
                            var window = Window.GetWindow(element);
                            window?.DragMove();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorInfo("Title_Move_Command", ex.Message);
                    }
                });
                // 中文按钮
                ToggleChineseCommand = new YF_RelayCommand(() => { App.ChangeLanguage("zh"); logger.LogInfo("语言切换-中文"); });
                // 英文按钮
                ToggleEnglishCommand = new YF_RelayCommand(() => { App.ChangeLanguage("en"); logger.LogInfo("语言切换-英文"); });
                // 插件显示
                Btn_Plugin_Show_Command = new YF_RelayCommand<string>(parameter => {
                    try
                    {
                        Grid_Show_Array.Children.Clear();

                        if (!UserControlsService.Instance.DctControls.TryGetValue(parameter, out var ctrlData))
                        {
                            logger.ErrorInfo("Btn_Plugin_Show_Command", $"插件 {parameter} 未找到");
                            return;
                        }

                        CurrentUcDate = ctrlData;
                        UserControlsService.Instance.ShowUserControl(parameter);
                        UserControl? uc = CurrentUcDate.userControl;
                        if (uc != null)
                            Grid_Show_Array.Children.Add(uc);
                        else
                            logger.ErrorInfo("Btn_Plugin_Show_Command", $"插件 {parameter} 加载失败");
                    }
                    catch (Exception ex)
                    {
                        logger.ErrorInfo("Btn_Plugin_Show_Command", ex.Message);
                    }
                });


                // 委托绑定
                // 显示CPU-Memory
                dlg_Show_Cpu_Memory = Show_Cpu_Memory;
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("InitCommond", ex.Message);
            }

        }

        /// <summary>
        /// 窗体移动事件
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "窗体移动")]
        public virtual void Move_Window(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource is Border) // 只有当点击的是右侧空白区域时才拖拽
                {
                    var window = Window.GetWindow((DependencyObject)sender);
                    window?.DragMove();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("Move_Window", ex.Message);
            }
        }

        /// <summary>
        /// 委托 刷新性能数据
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="memory"></param>
        public virtual void Show_Cpu_Memory(string cpu, string memory)
        {
            try
            {
                Txt_Cpu = "CPU: " + cpu + "%";
                Txt_Memory = "内存: " + memory + "GB";
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("Show_Cpu_Memory", ex.Message);
            }
        }



        /// <summary>
        /// 委托 刷新界面log信息
        /// </summary>
        /// <param name="msg"></param>
        public virtual void Show_Log(string msg)
        {
            try
            {
                lock (_logLock)
                {
                    _logBuilder.AppendLine(msg);
                    _logLineCount++;

                    // 从头部裁剪超出行数
                    while (_logLineCount > MaxLogLines)
                    {
                        var text = _logBuilder.ToString();
                        var newlineIdx = text.IndexOf('\n');
                        if (newlineIdx < 0) break;
                        _logBuilder.Remove(0, newlineIdx + 1);
                        _logLineCount--;
                    }
                }

                var currentText = "";
                lock (_logLock) { currentText = _logBuilder.ToString(); }

                if (Application.Current?.Dispatcher.CheckAccess() == true)
                    LogText = currentText;
                else
                    Application.Current?.Dispatcher.Invoke(() => LogText = currentText);
            }
            catch (Exception ex) { logger.ErrorInfo("Show_Log", ex.Message); }
        }


        /// <summary>
        /// 发送命令
        /// </summary>
        /// <param name="command"></param>
        /// <param name="parameter"></param>
        [Log(Level = LogLevel.Info, Message = "发送命令")]
        public virtual void SendCommand(string command, object parameter = null)
        {
            try
            {
                if (CurrentUcDate == null)
                {
                    logger.CommandInfo("[命令无目标插件] : " + command);
                }
                else
                {
                    CurrentUcDate.CommandHandler.ExecuteCommand(command, parameter);
                    logger.CommandInfo(command);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorInfo("SendCommand", ex.Message);
            }
        }
    }
}
