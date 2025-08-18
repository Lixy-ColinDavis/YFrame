using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using YF_Manager;

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

        public ICommand Btn_Exit_Command { get; set; }                  // 退出事件
        public ICommand ToggleLeftToolWindowCommand { get; set; }       // 左侧抽屉事件
        public ICommand ToggleRightToolWindowCommand { get; set; }      // 右侧抽屉事件
        public ICommand ToggleLightThemeCommand { get; set; }           // 亮主题事件
        public ICommand ToggleDarkThemeCommand { get; set; }            // 暗主题事件


        
        public static YF_Manager.DelegateFunctionModel.dvFunc_s_s dlg_Show_Cpu_Memory;

        public MainWindowViewModel()
        {
            InitUI();
            InitCommond();

            YF_Manager_Log.d_LogWrite = Show_Log;
        }

        private void InitUI()
        {
            LeftVisible = true;
            RightVisible = true;
        }

        private void InitCommond()
        {
            // 初始化命令
            ToggleLeftToolWindowCommand = new YF_Manager.YF_RelayCommand(() => { LeftVisible = !LeftVisible; });
            ToggleRightToolWindowCommand = new YF_Manager.YF_RelayCommand(() => { RightVisible = !RightVisible; });
            Btn_Exit_Command = new YF_Manager.YF_RelayCommand(() => { Environment.Exit(0); });
            ToggleLightThemeCommand = new YF_Manager.YF_RelayCommand(() => { ChangeTheme("Common/Themes/LightTheme.xaml"); });
            ToggleDarkThemeCommand = new YF_Manager.YF_RelayCommand   (() => { ChangeTheme("Common/Themes/DarkTheme.xaml"); });
            dlg_Show_Cpu_Memory = Show_Cpu_Memory;
        }

        /// <summary>
        /// 切换主题
        /// </summary>
        /// <param name="themePath"></param>
        private void ChangeTheme(string themePath)
        {
            try
            {
                // 清除现有资源
                Application.Current.Resources.MergedDictionaries.Clear();

                // 加载新主题
                var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Relative) };
                Application.Current.Resources.MergedDictionaries.Add(newTheme);
            }
            catch (Exception ex)
            {

                MainWindow.logger.ErrorInfo("ChangeTheme", ex.Message);
            }
        }

        // 委托 刷新性能数据
        public void Show_Cpu_Memory(string cpu, string memory)
        {
            Txt_Cpu = "CPU: " + cpu + "%";
            Txt_Memory = "内存: " + memory + "GB";
        }

        // 委托 刷新log
        public void Show_Log(string msg)
        {
            Console.WriteLine(msg);
        }
    }
}
