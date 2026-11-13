using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YF_Manager;

namespace YFrame
{
    /// <summary>
    /// 性能监视器卡片：每秒采集 CPU/内存，使用自绘 Sparkline 显示 CPU 走势
    /// </summary>
    public partial class PerformanceMonitor : UserControl
    {
        /// <summary>走势图保留的采样点数量（1 秒一个点，共 60 秒窗口）</summary>
        private const int HistoryCount = 60;

        /// <summary>CPU / 内存计数器</summary>
        private PerformanceCounter? cpuCounter;
        private PerformanceCounter? ramCounter;

        /// <summary>总内存大小（MB）</summary>
        private float totalMemoryMB;

        /// <summary>日志降频计数：每 12 次采样记录一次文件日志</summary>
        private int CounterTimes;

        /// <summary>CPU 历史数据（按时间从旧到新），供 Sparkline 绘制</summary>
        private readonly List<double> _cpuHistory = new(HistoryCount);

        /// <summary>CPU 指数平滑值，仅用于走势图，降低采样噪声造成的锯齿</summary>
        private double _smoothedCpu = -1;

        /// <summary>UI 定时器（1 秒采样）</summary>
        private DispatcherTimer? _timer;

        public PerformanceMonitor()
        {
            YF_Manager_Main.logger.LogInfo("性能监视器初始化-开始");
            InitializeComponent();

            // 初始化历史数据为 0，先铺满整条走势图，后续采样再逐步抬升
            for (int i = 0; i < HistoryCount; i++)
                _cpuHistory.Add(0);
            Spark_Cpu.Values = _cpuHistory.ToArray();

            // 后台线程执行耗时的 WMI 和 PerformanceCounter 初始化
            ThreadPool.QueueUserWorkItem(_ =>
            {
                InitializeCounters();

                // 初始化完成后回到 UI 线程启动定时器
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    _timer.Tick += UpdatePerformanceData;
                    _timer.Start();

                    YF_Manager_Main.logger.LogInfo("性能监视器初始化-完成");
                });
            });

            // 控件卸载时停止定时器并释放计数器，避免后台常驻
            Unloaded += (_, _) =>
            {
                _timer?.Stop();
                _timer = null;
                cpuCounter?.Dispose();
                ramCounter?.Dispose();
                cpuCounter = null;
                ramCounter = null;
            };
        }

        /// <summary>
        /// 初始化 WMI 总内存与性能计数器，并丢弃首次无效采样
        /// </summary>
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
            YF_Manager_Main.logger.LogInfo(
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

        /// <summary>
        /// 每秒刷新 CPU / 内存数据、走势图与内存占用条
        /// </summary>
        private void UpdatePerformanceData(object? sender, EventArgs e)
        {
            try
            {
                if (cpuCounter == null || ramCounter == null || totalMemoryMB <= 0)
                    return;

                // 取 CPU 使用率与可用内存（MB）
                float cpuUsage = cpuCounter.NextValue();
                float availableMemoryMB = ramCounter.NextValue();
                float usedMemoryMB = totalMemoryMB - availableMemoryMB;
                float memoryUsagePercent = (usedMemoryMB / totalMemoryMB) * 100;

                double usedGB = usedMemoryMB / 1024.0;
                double totalGB = totalMemoryMB / 1024.0;

                // 更新卡片上的数值
                Txt_CpuValue.Text = cpuUsage.ToString("0.0");
                Txt_MemUsed.Text = usedGB.ToString("0.0");
                Txt_MemTotal.Text = $"/ {totalGB:0.0} GB";
                Bar_Mem.Value = Math.Max(0, Math.Min(100, memoryUsagePercent));

                // 已用 / 总计文案跟随当前语言资源
                Txt_MemUsedSummary.Text = FormatMemoryText("key_PerfUsedFormat", "已用 {0} GB", usedGB);
                Txt_MemTotalSummary.Text = FormatMemoryText("key_PerfTotalFormat", "总计 {0} GB", totalGB);

                // 追加历史点并保持固定窗口，整块数组赋给 Sparkline 触发重绘
                // 先做指数平滑，避免 1 秒采样噪声让曲线出现密集锯齿
                _smoothedCpu = _smoothedCpu < 0
                    ? cpuUsage
                    : _smoothedCpu * 0.72 + cpuUsage * 0.28;
                _cpuHistory.Add(_smoothedCpu);
                if (_cpuHistory.Count > HistoryCount)
                    _cpuHistory.RemoveAt(0);
                Spark_Cpu.Values = _cpuHistory.ToArray();

                // 同步状态栏（沿用原有静态委托）
                MainWindowViewModel.dlg_Show_Cpu_Memory(
                    cpuUsage.ToString("0.0"),
                    $"{usedGB:0.0}/{totalGB:0.0}");

                // 文件日志降频，避免每秒写盘
                if (CounterTimes++ % 12 == 0)
                    YF_Manager_Main.logger.LogInfo(
                        $"CPU:{cpuUsage:0.0}%  内存:{usedGB:0.0}GB/{totalGB:0.0}GB");
            }
            catch (Exception ex)
            {
                // Windows 性能计数器损坏时可尝试 cmd lodctr /R 修复
                YF_Manager_Main.logger.ErrorInfo("UpdatePerformanceData", ex.Message);
            }
        }

        /// <summary>
        /// 读取语言资源中的内存文案格式并填入数值
        /// </summary>
        /// <param name="key">语言资源键</param>
        /// <param name="fallback">资源缺失时的中文后备格式</param>
        /// <param name="value">GB 数值</param>
        private static string FormatMemoryText(string key, string fallback, double value)
        {
            string format = Application.Current?.TryFindResource(key) as string ?? fallback;
            return string.Format(format, value.ToString("0.0"));
        }
    }
}
