using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using YF_Manager;
using YFrame.Model;
using YFrame.Service;
using YFrame.Service.CloudBase;

namespace YFrame.ViewModel.Toolbox
{
    /// <summary>
    /// 硬件检查工具 ViewModel：
    /// 打开时异步读取一次 CPU / GPU / 内存 / 硬盘 / 主板 / BIOS / 系统静态信息，
    /// 不做性能计数器订阅，因此不会产生实时占用数据。
    /// </summary>
    public class HardwareInfoToolViewModel : ViewModelBase
    {
        /// <summary>从当前语言资源读取字符串</summary>
        private static string R(string key)
            => Application.Current?.TryFindResource(key) as string ?? key;

        /// <summary>从当前语言资源读取并格式化字符串</summary>
        private static string RF(string key, params object[] args)
            => string.Format(R(key), args);

        private bool _isLoading;
        private string _statusText = string.Empty;

        /// <summary>是否正在读取硬件信息</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>头部状态文本（读取中/更新时间/错误提示）</summary>
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _cpuName = string.Empty;
        /// <summary>处理器名称</summary>
        public string CpuName
        {
            get => _cpuName;
            set => SetProperty(ref _cpuName, value);
        }

        private string _cpuDetail = string.Empty;
        /// <summary>处理器核心/线程/主频/插槽汇总</summary>
        public string CpuDetail
        {
            get => _cpuDetail;
            set => SetProperty(ref _cpuDetail, value);
        }

        private string _gpuName = string.Empty;
        /// <summary>显卡名称（多卡时合并显示）</summary>
        public string GpuName
        {
            get => _gpuName;
            set => SetProperty(ref _gpuName, value);
        }

        private string _gpuDetail = string.Empty;
        /// <summary>显存与驱动版本</summary>
        public string GpuDetail
        {
            get => _gpuDetail;
            set => SetProperty(ref _gpuDetail, value);
        }

        private string _memoryTotal = string.Empty;
        /// <summary>内存总容量</summary>
        public string MemoryTotal
        {
            get => _memoryTotal;
            set => SetProperty(ref _memoryTotal, value);
        }

        private string _boardText = string.Empty;
        /// <summary>主板厂商与型号</summary>
        public string BoardText
        {
            get => _boardText;
            set => SetProperty(ref _boardText, value);
        }

        private string _boardSerial = string.Empty;
        /// <summary>主板序列号</summary>
        public string BoardSerial
        {
            get => _boardSerial;
            set => SetProperty(ref _boardSerial, value);
        }

        private string _systemText = string.Empty;
        /// <summary>整机厂商与型号</summary>
        public string SystemText
        {
            get => _systemText;
            set => SetProperty(ref _systemText, value);
        }

        private string _computerName = string.Empty;
        /// <summary>计算机名</summary>
        public string ComputerName
        {
            get => _computerName;
            set => SetProperty(ref _computerName, value);
        }

        private string _biosText = string.Empty;
        /// <summary>BIOS 版本与发布日期</summary>
        public string BiosText
        {
            get => _biosText;
            set => SetProperty(ref _biosText, value);
        }

        private string _osText = string.Empty;
        /// <summary>操作系统名称、版本与架构</summary>
        public string OsText
        {
            get => _osText;
            set => SetProperty(ref _osText, value);
        }

        private string _audioOutputText = "—";
        /// <summary>输出设备（默认扬声器）显示文本</summary>
        public string AudioOutputText
        {
            get => _audioOutputText;
            set => SetProperty(ref _audioOutputText, value);
        }

        private string _audioInputText = "—";
        /// <summary>输入设备（默认麦克风）显示文本</summary>
        public string AudioInputText
        {
            get => _audioInputText;
            set => SetProperty(ref _audioInputText, value);
        }

        private string _audioTestText = string.Empty;
        /// <summary>音频测试状态文本</summary>
        public string AudioTestText
        {
            get => _audioTestText;
            set => SetProperty(ref _audioTestText, value);
        }

        private bool _isTestingAudio;
        /// <summary>是否正在执行音频测试</summary>
        public bool IsTestingAudio
        {
            get => _isTestingAudio;
            set => SetProperty(ref _isTestingAudio, value);
        }

        private double _audioInputLevel;
        /// <summary>音频输入实时电平（0~1），用于标题右侧进度条</summary>
        public double AudioInputLevel
        {
            get => _audioInputLevel;
            set => SetProperty(ref _audioInputLevel, value);
        }

        private string _audioCountdownText = string.Empty;
        /// <summary>输入测试倒计时文本（3.0s ~ 0.1s）</summary>
        public string AudioCountdownText
        {
            get => _audioCountdownText;
            set => SetProperty(ref _audioCountdownText, value);
        }

        private bool _showAudioCountdown;
        /// <summary>是否显示输入测试倒计时</summary>
        public bool ShowAudioCountdown
        {
            get => _showAudioCountdown;
            set => SetProperty(ref _showAudioCountdown, value);
        }
        private string _monitorName = "—";
        /// <summary>显示器名称</summary>
        public string MonitorName
        {
            get => _monitorName;
            set => SetProperty(ref _monitorName, value);
        }

        private string _monitorDetail = string.Empty;
        /// <summary>显示器尺寸、分辨率与刷新率</summary>
        public string MonitorDetail
        {
            get => _monitorDetail;
            set => SetProperty(ref _monitorDetail, value);
        }

        private string _monitorMeta = string.Empty;
        /// <summary>显示器厂商、序列号与多屏数量</summary>
        public string MonitorMeta
        {
            get => _monitorMeta;
            set => SetProperty(ref _monitorMeta, value);
        }
        /// <summary>内存条描述列表（最多展示 4 条）</summary>
        public ObservableCollection<string> MemoryModules { get; } = new();

        /// <summary>物理磁盘描述与独立测速结果（最多展示 4 块）</summary>
        public ObservableCollection<DiskDisplayItem> Disks { get; } = new();

        /// <summary>显示器列表（多屏时逐台展示）</summary>
        public ObservableCollection<MonitorDisplayItem> Monitors { get; } = new();

        /// <summary>刷新命令</summary>
        public YF_RelayCommand RefreshCommand { get; }

        /// <summary>复制硬件摘要命令</summary>
        public YF_RelayCommand CopyCommand { get; }

        /// <summary>测试默认输出设备命令</summary>
        public YF_RelayCommand TestOutputCommand { get; }

        /// <summary>测试默认输入设备命令</summary>
        public YF_RelayCommand TestInputCommand { get; }

        /// <summary>磁盘顺序读写测速命令，参数为对应磁盘行</summary>
        public YF_RelayCommand<DiskDisplayItem> TestDiskSpeedCommand { get; }

        /// <summary>
        /// 构造时立即异步读取一次硬件信息
        /// </summary>
        public HardwareInfoToolViewModel()
        {
            RefreshCommand = new YF_RelayCommand(() => _ = LoadAsync());
            CopyCommand = new YF_RelayCommand(CopySummary);
            TestOutputCommand = new YF_RelayCommand(() => _ = TestOutputAsync());
            TestInputCommand = new YF_RelayCommand(() => _ = TestInputAsync());
            TestDiskSpeedCommand = new YF_RelayCommand<DiskDisplayItem>(item => _ = TestDiskSpeedAsync(item));
            _ = LoadAsync();
        }

        /// <summary>
        /// 异步读取硬件信息并更新界面；重复调用在读取期间会被忽略
        /// </summary>
        private async Task LoadAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            StatusText = R("key_Hardware_Loading");

            try
            {
                HardwareInfoSnapshot snapshot = await Task.Run(HardwareInfoService.Load);
                Apply(snapshot);
                StatusText = RF("key_Hardware_Updated", DateTime.Now.ToString("HH:mm:ss"));

                // 后台写入 CloudBase；上传失败不能影响本地硬件页面
                _ = CloudBaseHardwareService.UploadAsync(snapshot);
            }
            catch
            {
                StatusText = R("key_Hardware_Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 将读取结果映射为界面文本；空值统一显示为“—”
        /// </summary>
        /// <param name="snapshot">硬件信息快照</param>
        private void Apply(HardwareInfoSnapshot snapshot)
        {
            CpuSnapshot cpu = snapshot.Cpu;
            CpuName = Fallback(cpu.Name);
            CpuDetail = string.Join(" · ", new[]
            {
                cpu.Cores > 0 || cpu.Threads > 0
                    ? $"{cpu.Cores} 核 / {cpu.Threads} 线程"
                    : string.Empty,
                cpu.ClockMHz > 0 ? $"{cpu.ClockMHz / 1000.0:F2} GHz" : string.Empty,
                string.IsNullOrWhiteSpace(cpu.Socket) ? string.Empty : cpu.Socket
            }.Where(s => !string.IsNullOrEmpty(s)));

            GpuName = snapshot.Gpus.Count == 0
                ? "—"
                : string.Join(" / ", snapshot.Gpus.Select(g => g.Name));
            if (snapshot.Gpus.Count > 0)
            {
                var gpu = snapshot.Gpus[0];
                GpuDetail = string.Join(" · ", new[]
                {
                    gpu.AdapterRam > 0 ? $"{R("key_Hardware_Vram")} {FormatBytes(gpu.AdapterRam)}" : string.Empty,
                    string.IsNullOrWhiteSpace(gpu.DriverVersion) ? string.Empty
                        : $"{R("key_Hardware_Driver")} {gpu.DriverVersion}"
                }.Where(s => !string.IsNullOrEmpty(s)));
            }
            else
            {
                GpuDetail = string.Empty;
            }

            MemoryTotal = snapshot.Memory.TotalBytes > 0
                ? FormatBytes(snapshot.Memory.TotalBytes)
                : "—";

            MemoryModules.Clear();
            foreach (var module in snapshot.Memory.Modules.Take(4))
            {
                string text = FormatBytes(module.CapacityBytes);
                if (module.Speed > 0) text += $" · {module.Speed} MT/s";
                if (!string.IsNullOrWhiteSpace(module.DeviceLocator)) text += $" · {module.DeviceLocator}";
                MemoryModules.Add(text);
            }
            if (snapshot.Memory.Modules.Count > 4)
                MemoryModules.Add($"… +{snapshot.Memory.Modules.Count - 4}");

            Disks.Clear();
            foreach (var disk in snapshot.Disks.Take(4))
            {
                string text = Fallback(disk.Model);
                if (disk.SizeBytes > 0) text += $" · {FormatBytes(disk.SizeBytes)}";

                // 优先显示 MSFT_PhysicalDisk 的 BusType（NVMe/SATA/USB），比 Win32_DiskDrive.InterfaceType 准确
                string busType = !string.IsNullOrWhiteSpace(disk.BusTypeText)
                    ? disk.BusTypeText
                    : disk.InterfaceType;
                if (!string.IsNullOrWhiteSpace(busType)) text += $" · {busType}";

                if (!string.IsNullOrWhiteSpace(disk.PcieLinkText))
                    text += $" · {disk.PcieLinkText}";

                if (!string.IsNullOrWhiteSpace(disk.FirmwareRevision))
                    text += $" · FW {disk.FirmwareRevision}";

                // WMI 的 TransferRate 单位为 KB/s，部分 NVMe 驱动不提供该字段
                if (disk.TransferRate > 0)
                    text += $" · {disk.TransferRate / 1024.0:F0} MB/s";

                Disks.Add(new DiskDisplayItem
                {
                    DisplayText = text,
                    DriveRoot = disk.DriveRoot
                });
            }
            if (snapshot.Disks.Count > 4)
            {
                Disks.Add(new DiskDisplayItem
                {
                    DisplayText = $"… +{snapshot.Disks.Count - 4}",
                    DriveRoot = string.Empty
                });
            }

            BoardText = JoinNonEmpty(snapshot.Board.Manufacturer, snapshot.Board.Product);
            BoardSerial = Fallback(snapshot.Board.SerialNumber);
            SystemText = JoinNonEmpty(snapshot.System.Manufacturer, snapshot.System.Model);
            ComputerName = Fallback(snapshot.System.ComputerName);

            BiosText = JoinNonEmpty(snapshot.Bios.Version, snapshot.Bios.ReleaseDate);
            OsText = string.Join(" · ", new[]
            {
                snapshot.Os.Caption,
                snapshot.Os.Version,
                snapshot.Os.Architecture
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

            AudioOutputText = FormatAudioList(snapshot.Audio.Outputs);
            AudioInputText = FormatAudioList(snapshot.Audio.Inputs);

            Monitors.Clear();
            foreach (var monitor in snapshot.Monitors)
            {
                string detail = string.Join(" · ", new[]
                {
                    monitor.SizeInch > 0 ? $"{monitor.SizeInch:F1} 英寸" : string.Empty,
                    monitor.Width > 0 && monitor.Height > 0 ? $"{monitor.Width} × {monitor.Height}" : string.Empty,
                    monitor.RefreshRate > 0 ? $"{monitor.RefreshRate} Hz" : string.Empty
                }.Where(s => !string.IsNullOrEmpty(s)));

                string meta = string.Join(" · ", new[]
                {
                    monitor.Manufacturer,
                    monitor.SerialNumber
                }.Where(s => !string.IsNullOrWhiteSpace(s)));

                Monitors.Add(new MonitorDisplayItem
                {
                    Name = Fallback(monitor.Name),
                    Detail = detail,
                    Meta = meta
                });
            }

            // 音频测试结果只在重新测试时更新，刷新硬件信息时保留最近一次结果
        }

        /// <summary>
        /// 格式化音频设备列表：最多显示两个名称，其余用 +N 提示。
        /// </summary>
        /// <param name="devices">设备名称列表</param>
        /// <returns>界面显示文本</returns>
        private static string FormatAudioList(IReadOnlyList<string> devices)
        {
            if (devices.Count == 0) return R("key_Hardware_NoAudio");
            string text = string.Join(" / ", devices.Take(2));
            if (devices.Count > 2) text += $" +{devices.Count - 2}";
            return text;
        }

        /// <summary>
        /// 测试默认输出设备：播放 1 秒 440Hz 测试音。
        /// </summary>
        private async Task TestOutputAsync()
        {
            if (IsTestingAudio) return;
            IsTestingAudio = true;
            AudioTestText = R("key_Hardware_TestingOutput");

            try
            {
                var result = await Task.Run(() =>
                {
                    bool ok = AudioTestService.PlayTestTone(out string error);
                    return (ok, error);
                });
                AudioTestText = result.ok
                    ? R("key_Hardware_OutputOk")
                    : RF("key_Hardware_AudioFailed", result.error);
            }
            finally
            {
                StopAudioCountdown();
                IsTestingAudio = false;
            }
        }

        /// <summary>输入测试倒计时计时器</summary>
        private DispatcherTimer? _audioCountdownTimer;

        /// <summary>倒计时剩余秒数</summary>
        private double _audioCountdownRemaining;

        /// <summary>
        /// 启动输入测试倒计时：从 3.0s 每 100ms 递减到 0.1s，结束后隐藏。
        /// </summary>
        private void StartAudioCountdown()
        {
            _audioCountdownRemaining = 3.0;
            AudioCountdownText = "3.0s";
            ShowAudioCountdown = true;

            _audioCountdownTimer?.Stop();
            _audioCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _audioCountdownTimer.Tick += (_, _) =>
            {
                _audioCountdownRemaining = Math.Max(0, _audioCountdownRemaining - 0.1);
                if (_audioCountdownRemaining <= 0.0001)
                {
                    _audioCountdownTimer.Stop();
                    ShowAudioCountdown = false;
                    return;
                }

                AudioCountdownText = $"{_audioCountdownRemaining:0.0}s";
            };
            _audioCountdownTimer.Start();
        }

        /// <summary>停止倒计时并隐藏文本</summary>
        private void StopAudioCountdown()
        {
            _audioCountdownTimer?.Stop();
            ShowAudioCountdown = false;
        }
        /// <summary>
        /// 测试默认输入设备：录音 3 秒并实时显示电平与倒计时。
        /// </summary>
        private async Task TestInputAsync()
        {
            if (IsTestingAudio) return;
            IsTestingAudio = true;
            AudioTestText = R("key_Hardware_TestingInput");
            AudioInputLevel = 0;
            StartAudioCountdown();
            var progress = new Progress<double>(value => AudioInputLevel = value);

            try
            {
                var result = await Task.Run(() =>
                {
                    bool ok = AudioTestService.RecordPeak(3.0, value => ((IProgress<double>)progress).Report(value),
                        out double peak, out string error);
                    return (ok, peak, error);
                });
                AudioInputLevel = result.ok ? result.peak : 0;
                AudioTestText = result.ok
                    ? RF("key_Hardware_InputLevel", result.peak * 100)
                    : RF("key_Hardware_AudioFailed", result.error);
            }
            finally
            {
                StopAudioCountdown();
                IsTestingAudio = false;
            }
        }

        /// <summary>
        /// 测试指定磁盘所在卷的顺序读写速度；每块盘独立执行。
        /// </summary>
        /// <param name="item">要测速的磁盘行</param>
        private async Task TestDiskSpeedAsync(DiskDisplayItem? item)
        {
            if (item == null || item.IsTesting) return;

            if (string.IsNullOrWhiteSpace(item.DriveRoot))
            {
                item.SpeedText = "无可用盘符";
                return;
            }

            item.IsTesting = true;
            item.SpeedText = R("key_Hardware_TestingDisk");

            try
            {
                var result = await Task.Run(() =>
                {
                    bool ok = DiskSpeedTestService.Run(item.DriveRoot, out double read, out double write, out string error);
                    return (ok, read, write, error);
                });

                item.SpeedText = result.ok
                    ? RF("key_Hardware_DiskSpeedFormat", result.read, result.write)
                    : RF("key_Hardware_AudioFailed", result.error);
            }
            finally
            {
                item.IsTesting = false;
            }
        }
        /// <summary>
        /// 复制当前硬件摘要到剪贴板
        /// </summary>
        private void CopySummary()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(R("key_Hardware_Title"));
                sb.AppendLine($"{R("key_Hardware_Cpu")}: {CpuName} ({CpuDetail})");
                sb.AppendLine($"{R("key_Hardware_Gpu")}: {GpuName} ({GpuDetail})");
                sb.AppendLine($"{R("key_Hardware_Memory")}: {MemoryTotal}");
                foreach (string module in MemoryModules) sb.AppendLine($"  - {module}");
                sb.AppendLine($"{R("key_Hardware_Disk")}:");
                foreach (var disk in Disks) sb.AppendLine($"  - {disk.DisplayText}");
                sb.AppendLine($"{R("key_Hardware_Board")}: {BoardText} / {BoardSerial}");
                sb.AppendLine($"{R("key_Hardware_System")}: {SystemText} / {ComputerName}");
                sb.AppendLine($"BIOS: {BiosText}");
                sb.AppendLine($"{R("key_Hardware_Os")}: {OsText}");
                sb.AppendLine($"{R("key_Hardware_Display")}:");
                foreach (var monitor in Monitors)
                    sb.AppendLine($"  - {monitor.Name} ({monitor.Detail}) {monitor.Meta}");
                sb.AppendLine($"{R("key_Hardware_Audio")}:");
                sb.AppendLine($"  {R("key_Hardware_Output")}: {AudioOutputText}");
                sb.AppendLine($"  {R("key_Hardware_Input")}: {AudioInputText}");

                Clipboard.SetText(sb.ToString());
                StatusText = R("key_Hardware_Copied");
            }
            catch
            {
                StatusText = R("key_Hardware_CopyFailed");
            }
        }

        /// <summary>空字符串统一显示为短横线</summary>
        private static string Fallback(string value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        /// <summary>将非空字符串用斜杠连接</summary>
        private static string JoinNonEmpty(params string[] values)
            => string.Join(" / ", values.Where(v => !string.IsNullOrWhiteSpace(v)));

        /// <summary>将字节数格式化为 B / KB / MB / GB / TB</summary>
        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1024UL * 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024 * 1024):F2} TB";
            if (bytes >= 1024UL * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
            if (bytes >= 1024UL * 1024)
                return $"{bytes / (1024.0 * 1024):F1} MB";
            if (bytes >= 1024UL)
                return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }
}

namespace YFrame.ViewModel.Toolbox
{
    /// <summary>显示器列表展示项</summary>
    public sealed class MonitorDisplayItem
    {
        /// <summary>显示器名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>尺寸、分辨率、刷新率</summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>厂商与序列号</summary>
        public string Meta { get; set; } = string.Empty;
    }

    /// <summary>磁盘列表展示项，包含该磁盘独立测速状态。</summary>
    public sealed class DiskDisplayItem : INotifyPropertyChanged
    {
        private string _speedText = string.Empty;
        private bool _isTesting;

        /// <summary>磁盘型号、容量、接口等信息。</summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>该磁盘对应盘符根目录，例如 C:\。</summary>
        public string DriveRoot { get; set; } = string.Empty;

        /// <summary>该磁盘的测速结果。</summary>
        public string SpeedText
        {
            get => _speedText;
            set
            {
                if (_speedText == value) return;
                _speedText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpeedText)));
            }
        }

        /// <summary>该磁盘是否正在测速。</summary>
        public bool IsTesting
        {
            get => _isTesting;
            set
            {
                if (_isTesting == value) return;
                _isTesting = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsTesting)));
            }
        }

        /// <summary>是否有可用盘符可以测速。</summary>
        public bool CanTest => !string.IsNullOrWhiteSpace(DriveRoot);

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}