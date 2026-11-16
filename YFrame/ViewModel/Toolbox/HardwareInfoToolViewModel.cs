using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using YF_Manager;
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

        /// <summary>内存条描述列表（最多展示 4 条）</summary>
        public ObservableCollection<string> MemoryModules { get; } = new();

        /// <summary>物理磁盘描述列表（最多展示 4 块）</summary>
        public ObservableCollection<string> Disks { get; } = new();

        /// <summary>刷新命令</summary>
        public YF_RelayCommand RefreshCommand { get; }

        /// <summary>复制硬件摘要命令</summary>
        public YF_RelayCommand CopyCommand { get; }

        /// <summary>
        /// 构造时立即异步读取一次硬件信息
        /// </summary>
        public HardwareInfoToolViewModel()
        {
            RefreshCommand = new YF_RelayCommand(() => _ = LoadAsync());
            CopyCommand = new YF_RelayCommand(CopySummary);
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
