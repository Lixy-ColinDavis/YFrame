using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using LiveCharts;
using System.Management;
using System.Diagnostics;
using System.Windows.Threading;
using System.ComponentModel;


namespace YFrame
{
    /// <summary>
    /// PerformanceMonitor.xaml 的交互逻辑
    /// </summary>
    public partial class PerformanceMonitor : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SeriesCollection SeriesCollection { get; set; }
        public string[] Labels { get; set; }
        public Func<double, string> YFormatter { get; set; }

        // CPU 内存计数器
        private PerformanceCounter cpuCounter;
        private PerformanceCounter ramCounter;

        // 总内存大小
        float totalMemoryMB;

        // 一分钟计数器
        int CounterTimes = 0;

        public PerformanceMonitor()
        {
            MainWindow.logger.LogInfo("性能监视器初始化-开始");
            InitializeComponent();

            

            SeriesCollection = new SeriesCollection
            {
                
                new LineSeries
                {
                    Title = "CPU",
                    Values = new ChartValues<ObservableValue>
                    {
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0)
                    },
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10
                },
                new LineSeries
                {
                    Title = "内存",
                    Values = new ChartValues<ObservableValue>
                    {
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0),
                        new ObservableValue(0)
                    },
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 10
                }
            }; 

            Labels = new[] { "1分钟前", "50秒前", "40秒前", "30秒前", "20秒前", "现在" };
            YFormatter = value => value.ToString("N0");

            DataContext = this;
            DispatcherTimer timer = new DispatcherTimer();

            Thread thread = new Thread(() => {
                //// 获取可用内存（MB）
                //float availableMB = new PerformanceCounter("Memory", "Available MBytes").NextValue();

                //// 获取内存使用率（%）
                //float usedPercentage = new PerformanceCounter("Memory", "% Committed Bytes In Use").NextValue();

                //// 估算总物理内存（MB）
                //totalMemoryMB = availableMB / (1 -(usedPercentage) / 100);

                var query = new ObjectQuery("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                var searcher = new ManagementObjectSearcher(query);
                foreach (ManagementObject obj in searcher.Get())
                {
                    totalMemoryMB =  Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024); // 转为MB
                }

                MainWindow.logger.LogInfo($"总系统内存：{totalMemoryMB}MB, {(totalMemoryMB / 1024).ToString("0.0")}GB");

                // 初始化CPU计数器（全局CPU使用率）
                cpuCounter = new PerformanceCounter(
                    "Processor",         // 类别（处理器）
                    "% Processor Time",  // 计数器名称（CPU时间百分比）
                    "_Total"             // 实例名称（_Total表示所有核心）
                );

                // 初始化内存计数器（可用内存百分比）
                ramCounter = new PerformanceCounter(
                    "Memory",           // 类别（内存）
                    "Available MBytes"   // 计数器名称（可用内存MB）
                );

                // 启动定时器刷新数据（每1秒更新一次）
                
                timer.Interval = TimeSpan.FromSeconds(5);
                timer.Tick += UpdatePerformanceData;
                timer.Start();
            });
            thread.IsBackground = true;
            thread.Start();
            MainWindow.logger.LogInfo("性能监视器初始化-完成");
        }

        // 刷新性能数据
        private void UpdatePerformanceData(object sender, EventArgs e)
        {
            
            try
            {
                // 获取CPU使用率（取当前值）
                float cpuUsage = cpuCounter.NextValue();

                // 获取可用内存（MB）
                float availableMemoryMB = ramCounter.NextValue();

                float usedMemoryMB = totalMemoryMB - availableMemoryMB;
                float memoryUsagePercent = (usedMemoryMB / totalMemoryMB) * 100;

                // 更新图表数据
                UpdateChartData(cpuUsage, memoryUsagePercent);

                // 更新标签（可选：显示最新数据）
                Labels = new[] { "25秒前", "20秒前", "15秒前", "10秒前", "5秒前", "现在" };
                OnPropertyChanged(nameof(Labels)); // 通知UI更新

                MainWindowViewModel.dlg_Show_Cpu_Memory(cpuUsage.ToString("0.0"), $"{(usedMemoryMB / 1024).ToString("0.0")}/{(totalMemoryMB / 1024).ToString("0.0")}");

                if(CounterTimes++ % 12 == 0)
                    MainWindow.logger.LogInfo($"" +
                        $"CPU:{cpuUsage.ToString("0.0")}%  " +
                        $"内存:{(usedMemoryMB / 1024).ToString("0.0")}GB/{(totalMemoryMB / 1024).ToString("0.0")}GB"
                        );
            }
            catch (Exception ex)
            {
                // 由于 Windows 性能计数器损坏 => cmd lodctr / R
                MainWindow.logger.ErrorInfo("UpdatePerformanceData" , ex.Message);
            }
        }

        // 更新图表数据
        private void UpdateChartData(float cpuUsage, float memoryUsage)
        {
            try
            {
                // 获取当前CPU和内存的数据序列
                var cpuSeries = SeriesCollection[0].Values as ChartValues<ObservableValue>;
                var memorySeries = SeriesCollection[1].Values as ChartValues<ObservableValue>;

                // 移除最旧的数据点（保持6个数据点）
                if (cpuSeries.Count >= 6)
                {
                    cpuSeries.RemoveAt(0);
                    memorySeries.RemoveAt(0);
                }

                // 添加新的数据点
                cpuSeries.Add(new ObservableValue(cpuUsage));
                memorySeries.Add(new ObservableValue(memoryUsage));
            }
            catch (Exception ex)
            {
                MainWindow.logger.ErrorInfo("UpdateChartData", ex.Message);
            }
        }

    }
}
