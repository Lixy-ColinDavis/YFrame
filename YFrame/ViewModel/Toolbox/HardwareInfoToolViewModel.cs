using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YF_Manager;
using YFrame.Model;
using YFrame.Service;

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

        /// <summary>物理磁盘描述列表（最多展示 4 块）</summary>
        public ObservableCollection<string> Disks { get; } = new();

        /// <summary>刷新命令</summary>
        public YF_RelayCommand RefreshCommand { get; }

        /// <summary>复制硬件摘要命令</summary>
        public YF_RelayCommand CopyCommand { get; }

        /// <summary>测试默认输出设备命令</summary>
        public YF_RelayCommand TestOutputCommand { get; }

        /// <summary>测试默认输入设备命令</summary>
        public YF_RelayCommand TestInputCommand { get; }

        /// <summary>
        /// 构造时立即异步读取一次硬件信息
        /// </summary>
        public HardwareInfoToolViewModel()
        {
            RefreshCommand = new YF_RelayCommand(() => _ = LoadAsync());
            CopyCommand = new YF_RelayCommand(CopySummary);
            TestOutputCommand = new YF_RelayCommand(() => _ = TestOutputAsync());
            TestInputCommand = new YF_RelayCommand(() => _ = TestInputAsync());
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
                if (!string.IsNullOrWhiteSpace(disk.InterfaceType)) text += $" · {disk.InterfaceType}";
                Disks.Add(text);
            }
            if (snapshot.Disks.Count > 4)
                Disks.Add($"… +{snapshot.Disks.Count - 4}");

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

            MonitorName = Fallback(snapshot.Monitor.Name);
            MonitorDetail = string.Join(" · ", new[]
            {
                snapshot.Monitor.SizeInch > 0 ? $"{snapshot.Monitor.SizeInch:F1} 英寸" : string.Empty,
                snapshot.Monitor.Width > 0 && snapshot.Monitor.Height > 0
                    ? $"{snapshot.Monitor.Width} × {snapshot.Monitor.Height}"
                    : string.Empty,
                snapshot.Monitor.RefreshRate > 0 ? $"{snapshot.Monitor.RefreshRate} Hz" : string.Empty
            }.Where(s => !string.IsNullOrEmpty(s)));
            MonitorMeta = string.Join(" · ", new[]
            {
                snapshot.Monitor.Manufacturer,
                string.IsNullOrWhiteSpace(snapshot.Monitor.SerialNumber) ? string.Empty : snapshot.Monitor.SerialNumber,
                snapshot.Monitor.AdditionalCount > 0 ? $"+{snapshot.Monitor.AdditionalCount}" : string.Empty
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

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
                IsTestingAudio = false;
            }
        }

        /// <summary>
        /// 测试默认输入设备：录音 1.5 秒并显示峰值电平。
        /// </summary>
        private async Task TestInputAsync()
        {
            if (IsTestingAudio) return;
            IsTestingAudio = true;
            AudioTestText = R("key_Hardware_TestingInput");

            try
            {
                var result = await Task.Run(() =>
                {
                    bool ok = AudioTestService.RecordPeak(out double peak, out string error);
                    return (ok, peak, error);
                });
                AudioTestText = result.ok
                    ? RF("key_Hardware_InputLevel", result.peak * 100)
                    : RF("key_Hardware_AudioFailed", result.error);
            }
            finally
            {
                IsTestingAudio = false;
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
                foreach (string disk in Disks) sb.AppendLine($"  - {disk}");
                sb.AppendLine($"{R("key_Hardware_Board")}: {BoardText} / {BoardSerial}");
                sb.AppendLine($"{R("key_Hardware_System")}: {SystemText} / {ComputerName}");
                sb.AppendLine($"BIOS: {BiosText}");
                sb.AppendLine($"{R("key_Hardware_Os")}: {OsText}");
                sb.AppendLine($"{R("key_Hardware_Display")}: {MonitorName} ({MonitorDetail})");
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
