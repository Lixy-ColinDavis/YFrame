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

        public static YF_Manager.DelegateFunctionModel.dvFunc_s_s dlg_Show_Cpu_Memory;

        public ObservableCollection<PluginsModel> lsPlugins { get; } = new ObservableCollection<PluginsModel>(); // 插件列表
        

        public MainWindowViewModel()
        {
            MainWindow.logger.LogInfo("主框架初始化-开始");
            InitUI();
            InitCommond();

            YF_Manager_Log.d_LogWrite = Show_Log;
            MainWindow.logger.LogInfo("主框架初始化-完成");
        }

        private void InitUI()
        {
            try
            {
                LeftVisible = true;
                RightVisible = true;

                // 初始化显示区域
                Grid_Show_Array = new Grid();
                //Grid_Show_Array.Children.Add(new PerformanceMonitor());

                UserControlsService.Instance.LoadAndShowUserControl();
                UserControl uc = UserControlsService.Instance.GetControl("AI 助手");
                Grid_Show_Array.Children.Add(uc);

                Performance_Monitor_View = new PerformanceMonitor();
                lsPlugins.Add(new PluginsModel() { Name = "AI助手", ID = "Ai Helper", Status = 0 });
                lsPlugins.Add(new PluginsModel() { Name = "HTTP文件服务器", ID = "HttpServer", Status = 0 });
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("InitUI", ex.Message);
            }
        }

        private void InitCommond()
        {
            try 
            { 
                // 初始化命令
                ToggleLeftToolWindowCommand = new YF_RelayCommand(() => { LeftVisible = !LeftVisible; MainWindow.logger.LogInfo("左侧边栏-" + (LeftVisible == true ? "开" : "关")); });
                ToggleRightToolWindowCommand = new YF_RelayCommand(() => { RightVisible = !RightVisible; MainWindow.logger.LogInfo("右侧边栏-" + (LeftVisible == true ? "开" : "关")); });
                Btn_Exit_Command = new YF_RelayCommand(() => { Environment.Exit(0); MainWindow.logger.LogInfo("退出程序"); });
                ToggleLightThemeCommand = new YF_RelayCommand(() => { App.ChangeTheme("Common/Themes/LightTheme.xaml"); MainWindow.logger.LogInfo("主题切换-亮"); });
                ToggleDarkThemeCommand = new YF_RelayCommand   (() => { App.ChangeTheme("Common/Themes/DarkTheme.xaml"); MainWindow.logger.LogInfo("主题切换-暗"); });
                Btn_Minimize_Command = new YF_RelayCommand(() => { Application.Current.MainWindow.WindowState = WindowState.Minimized; MainWindow.logger.LogInfo("窗体最小化"); });
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
                ToggleChineseCommand = new YF_RelayCommand(() => { App.ChangeLanguage("zh"); MainWindow.logger.LogInfo("语言切换-中文"); });
                ToggleEnglishCommand = new YF_RelayCommand(() => { App.ChangeLanguage("en"); MainWindow.logger.LogInfo("语言切换-英文"); });

                    dlg_Show_Cpu_Memory = Show_Cpu_Memory;
                }
                catch (Exception ex)
                {
                    MainWindow.logger.ErrorInfo("InitCommond", ex.Message);
                }
            
        }

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

        // 委托 刷新性能数据
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

        // 委托 刷新log
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
