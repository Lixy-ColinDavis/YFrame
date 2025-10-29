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

namespace YFrame
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        #region INotifyPropertyChanged接口实现
        public event PropertyChangedEventHandler? PropertyChanged;
        

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

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

        private Grid _grid_Show_Array;
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



        public ICommand Btn_Exit_Command { get; set; }                  // 退出事件
        public ICommand ToggleLeftToolWindowCommand { get; set; }       // 左侧抽屉事件
        public ICommand ToggleRightToolWindowCommand { get; set; }      // 右侧抽屉事件
        public ICommand ToggleLightThemeCommand { get; set; }           // 亮主题事件
        public ICommand ToggleDarkThemeCommand { get; set; }            // 暗主题事件
        public ICommand Title_Move_Command { get; set; }                // 窗体拖拽移动
        public ICommand Btn_Minimize_Command { get; set; }              // 窗体最小化
        public ICommand ToggleChineseCommand { get; set; }              // 中文切换
        public ICommand ToggleEnglishCommand { get; set; }              // 中文切换

        public static YF_Manager.DelegateFunctionModel.dvFunc_Vs_s dlg_Show_Cpu_Memory;

        public ObservableCollection<PluginsModel> lsPlugins { get; } = new ObservableCollection<PluginsModel>(); // 插件列表
        

        public MainWindowViewModel()
        {
            MainWindow.logger.LogInfo("主框架初始化-开始");
            InitUI();
            InitCommond();

            YF_Manager_Log.d_LogWrite = Show_Log;
            MainWindow.logger.LogInfo("主框架初始化-完成");
        }

        /// <summary>
        /// 初始化UI
        /// </summary>
        /// <remarks>
        /// </remarks>
        private void InitUI()
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
                    lsPlugins.Add(new PluginsModel() { Name = item.Value, ID = item.Key, Status = 0 });
                }

                // 读取并加载、显示目标插件
                UserControl uc = UserControlsService.Instance.GetControl(UserControlsService.Instance.DctControls.FirstOrDefault().Value);
                Grid_Show_Array.Children.Add(uc);
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("InitUI", ex.Message);
            }
        }

        /// <summary>
        /// 初始化命令绑定
        /// </summary>
        private void InitCommond()
        {
            try 
            { 
                // 初始化命令
                // 左抽屉
                ToggleLeftToolWindowCommand = new YF_RelayCommand(() => { LeftVisible = !LeftVisible; MainWindow.logger.LogInfo("左侧边栏-" + (LeftVisible == true ? "开" : "关")); });
                // 右抽屉
                ToggleRightToolWindowCommand = new YF_RelayCommand(() => { RightVisible = !RightVisible; MainWindow.logger.LogInfo("右侧边栏-" + (LeftVisible == true ? "开" : "关")); });
                // 关闭按钮
                Btn_Exit_Command = new YF_RelayCommand(() => { Environment.Exit(0); MainWindow.logger.LogInfo("退出程序"); });
                // 亮色主题
                ToggleLightThemeCommand = new YF_RelayCommand(() => { App.ChangeTheme("Common/Themes/LightTheme.xaml"); MainWindow.logger.LogInfo("主题切换-亮"); });
                // 暗色主题
                ToggleDarkThemeCommand = new YF_RelayCommand   (() => { App.ChangeTheme("Common/Themes/DarkTheme.xaml"); MainWindow.logger.LogInfo("主题切换-暗"); });
                // 最小化
                Btn_Minimize_Command = new YF_RelayCommand(() => { Application.Current.MainWindow.WindowState = WindowState.Minimized; MainWindow.logger.LogInfo("窗体最小化"); });
                // 移动事件
                Title_Move_Command = new YF_RelayCommand<object>(param =>
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
                });
                // 中文按钮
                ToggleChineseCommand = new YF_RelayCommand(() => { App.ChangeLanguage("zh"); MainWindow.logger.LogInfo("语言切换-中文"); });
                // 英文按钮
                ToggleEnglishCommand = new YF_RelayCommand(() => { App.ChangeLanguage("en"); MainWindow.logger.LogInfo("语言切换-英文"); });


                // 委托绑定
                // 显示CPU-Memory
                dlg_Show_Cpu_Memory = Show_Cpu_Memory;
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("InitCommond", ex.Message);
            }
            
        }

        /// <summary>
        /// 窗体移动事件
        /// </summary>
        private void Move_Window(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.OriginalSource is Border) // 只有当点击的是右侧空白区域时才拖拽
                {
                    var window = Window.GetWindow((DependencyObject)sender);
                    window.DragMove();
                }
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("Move_Window", ex.Message);
            }
        }

        /// <summary>
        /// 委托 刷新性能数据
        /// </summary>
        /// <param name="cpu"></param>
        /// <param name="memory"></param>
        public void Show_Cpu_Memory(string cpu, string memory)
        {
            try
            {
                Txt_Cpu = "CPU: " + cpu + "%";
                Txt_Memory = "内存: " + memory + "GB";
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("Show_Cpu_Memory", ex.Message);
            }
            
        }

        /// <summary>
        /// 委托 刷新界面log信息
        /// </summary>
        /// <param name="msg"></param>
        public void Show_Log(string msg)
        {
            try
            {
                LogText += msg + "\n";
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("Show_Log", ex.Message);
            }
            
        }
    }
}
