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
            MainWindowViewModel.Instance.logger.LogInfo("性能监视器初始化-开始");
            InitializeComponent();

            // UI 线程：初始化图表（6个初始数据点，5秒采样间隔，共30秒窗口）
            SeriesCollection = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "CPU",
                    Values = new ChartValues<ObservableValue>
                    {
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0)
                    },
                    PointGeometry = DefaultGeometries.Circle,
                    PointGeometrySize = 10
                },
                new LineSeries
                {
                    Title = "内存",
                    Values = new ChartValues<ObservableValue>
                    {
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0),
                        new ObservableValue(0), new ObservableValue(0)
                    },
                    PointGeometry = DefaultGeometries.Square,
                    PointGeometrySize = 10
                }
            };
            Labels = new[] { "25秒前", "20秒前", "15秒前", "10秒前", "5秒前", "现在" };
            YFormatter = value => value.ToString("N0");
            DataContext = this;

            // 后台线程：仅执行耗时的 WMI 和 PerformanceCounter 初始化
            ThreadPool.QueueUserWorkItem(_ =>
            {
                InitializeCounters();

                // 初始化完成后回到 UI 线程启动 Timer
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(5)
                    };
                    timer.Tick += UpdatePerformanceData;
                    timer.Start();

                    MainWindowViewModel.Instance.logger.LogInfo("性能监视器初始化-完成");
                });
            });
        }

        private void InitializeCounters()
        {
            using (var searcher = new ManagementObjectSearcher(
                new ObjectQuery("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem")))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        totalMemoryMB = Convert.ToInt64(obj["TotalPhysicalMemory"]) / (1024 * 1024);
                    }
                }
            }
            MainWindowViewModel.Instance.logger.LogInfo(
                $"总系统内存：{totalMemoryMB}MB, {(totalMemoryMB / 1024).ToString("0.0")}GB");

            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            // 预热：丢弃首次无效采样
            cpuCounter.NextValue();
            ramCounter.NextValue();
            Thread.Sleep(100);
            cpuCounter.NextValue();
            ramCounter.NextValue();
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
                OnPropertyChanged(nameof(Labels)); // 通知UI更新

                MainWindowViewModel.dlg_Show_Cpu_Memory(cpuUsage.ToString("0.0"), $"{(usedMemoryMB / 1024).ToString("0.0")}/{(totalMemoryMB / 1024).ToString("0.0")}");

                if (CounterTimes++ % 12 == 0)
                    MainWindowViewModel.Instance.logger.LogInfo($"" +
                        $"CPU:{cpuUsage.ToString("0.0")}%  " +
                        $"内存:{(usedMemoryMB / 1024).ToString("0.0")}GB/{(totalMemoryMB / 1024).ToString("0.0")}GB"
                        );
            }
            catch (Exception ex)
            {
                // 由于 Windows 性能计数器损坏 => cmd lodctr / R
                MainWindowViewModel.Instance.logger.ErrorInfo("UpdatePerformanceData", ex.Message);
            }
        }

        // 更新图表数据
        private void UpdateChartData(float cpuUsage, float memoryUsage)
        {
            if (SeriesCollection == null || SeriesCollection.Count < 2)
                return;

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
                MainWindowViewModel.Instance.logger.ErrorInfo("UpdateChartData", ex.Message);
            }
        }

    }
}
