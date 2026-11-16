using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using YFrame.Model;

namespace YFrame.Service
{
    /// <summary>
    /// 硬件静态信息读取服务：通过 WMI 读取一次 CPU、GPU、内存、磁盘、主板、BIOS 与系统信息。
    /// 不订阅性能计数器，也不包含实时占用数据，避免持续采样。
    /// </summary>
    public static class HardwareInfoService
    {
        /// <summary>单条 WMI 查询超时时间，避免个别设备驱动导致界面长时间等待</summary>
        private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(8);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        /// <summary>
        /// 读取当前计算机的硬件静态信息。
        /// 每类信息独立查询，某一类失败不会影响其他卡片展示。
        /// </summary>
        /// <returns>硬件静态信息快照</returns>
        public static HardwareInfoSnapshot Load()
        {
            var snapshot = new HardwareInfoSnapshot();

            snapshot.Cpu = QueryCpu();
            snapshot.Gpus = QueryGpus();
            snapshot.Memory = QueryMemory();
            snapshot.Disks = QueryDisks();
            snapshot.Board = QueryBoard();
            snapshot.System = QuerySystem();
            snapshot.Bios = QueryBios();
            snapshot.Os = QueryOs();
            snapshot.Audio = QueryAudio();
            snapshot.Monitor = QueryMonitor();

            // 内存总容量优先使用内存条容量之和；若 WMI 未返回内存条，再用整机总内存兜底
            if (snapshot.Memory.TotalBytes == 0)
                snapshot.Memory.TotalBytes = snapshot.System.TotalMemoryBytes;

            return snapshot;
        }

        /// <summary>查询处理器信息</summary>
        private static CpuSnapshot QueryCpu()
        {
            var cpu = Query(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, SocketDesignation FROM Win32_Processor",
                obj => new CpuSnapshot
                {
                    Name = Text(obj, "Name"),
                    Cores = Int(obj, "NumberOfCores"),
                    Threads = Int(obj, "NumberOfLogicalProcessors"),
                    ClockMHz = Int(obj, "MaxClockSpeed"),
                    Socket = Text(obj, "SocketDesignation")
                }).FirstOrDefault();

            return cpu ?? new CpuSnapshot();
        }

        /// <summary>查询显卡列表，过滤掉远程桌面/基础渲染设备</summary>
        private static List<GpuSnapshot> QueryGpus()
        {
            var all = Query(
                "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController",
                obj => new GpuSnapshot
                {
                    Name = Text(obj, "Name"),
                    AdapterRam = UInt64(obj, "AdapterRAM"),
                    DriverVersion = Text(obj, "DriverVersion")
                });

            var filtered = all
                .Where(g => !g.Name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)
                            && !g.Name.Contains("Remote Display", StringComparison.OrdinalIgnoreCase)
                            && !g.Name.Contains("Oray", StringComparison.OrdinalIgnoreCase)
                            && !g.Name.Contains("IddDriver", StringComparison.OrdinalIgnoreCase)
                            && !g.Name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // WMI 的 AdapterRAM 是 32 位字段，12GB 等大显存会溢出成 4GB；
            // 优先用显卡驱动注册表里的 64 位 qwMemorySize 校正。
            foreach (var gpu in filtered)
            {
                if (TryGetRegistryVram(gpu.Name, out ulong vram) && vram > 0)
                    gpu.AdapterRam = vram;
            }

            return filtered.Count > 0 ? filtered : all;
        }

        /// <summary>查询内存条与总容量</summary>
        private static MemorySnapshot QueryMemory()
        {
            var modules = Query(
                "SELECT Capacity, Speed, Manufacturer, DeviceLocator FROM Win32_PhysicalMemory",
                obj => new MemoryModuleSnapshot
                {
                    CapacityBytes = UInt64(obj, "Capacity"),
                    Speed = Int(obj, "Speed"),
                    Manufacturer = Text(obj, "Manufacturer"),
                    DeviceLocator = Text(obj, "DeviceLocator")
                });

            return new MemorySnapshot
            {
                Modules = modules,
                TotalBytes = (ulong)modules.Sum(m => (decimal)m.CapacityBytes)
            };
        }

        /// <summary>查询物理磁盘列表</summary>
        private static List<DiskSnapshot> QueryDisks()
        {
            return Query(
                "SELECT Model, Size, InterfaceType, MediaType FROM Win32_DiskDrive",
                obj => new DiskSnapshot
                {
                    Model = Text(obj, "Model"),
                    SizeBytes = UInt64(obj, "Size"),
                    InterfaceType = Text(obj, "InterfaceType"),
                    MediaType = Text(obj, "MediaType")
                });
        }

        /// <summary>查询主板信息</summary>
        private static BoardSnapshot QueryBoard()
        {
            return Query(
                "SELECT Manufacturer, Product, SerialNumber FROM Win32_BaseBoard",
                obj => new BoardSnapshot
                {
                    Manufacturer = Text(obj, "Manufacturer"),
                    Product = Text(obj, "Product"),
                    SerialNumber = Text(obj, "SerialNumber")
                }).FirstOrDefault() ?? new BoardSnapshot();
        }

        /// <summary>查询整机信息</summary>
        private static SystemSnapshot QuerySystem()
        {
            return Query(
                "SELECT Manufacturer, Model, Name, TotalPhysicalMemory FROM Win32_ComputerSystem",
                obj => new SystemSnapshot
                {
                    Manufacturer = Text(obj, "Manufacturer"),
                    Model = Text(obj, "Model"),
                    ComputerName = Text(obj, "Name"),
                    TotalMemoryBytes = UInt64(obj, "TotalPhysicalMemory")
                }).FirstOrDefault() ?? new SystemSnapshot();
        }

        /// <summary>查询 BIOS 信息</summary>
        private static BiosSnapshot QueryBios()
        {
            return Query(
                "SELECT SMBIOSBIOSVersion, ReleaseDate, Manufacturer FROM Win32_BIOS",
                obj => new BiosSnapshot
                {
                    Version = Text(obj, "SMBIOSBIOSVersion"),
                    ReleaseDate = DateText(obj, "ReleaseDate"),
                    Manufacturer = Text(obj, "Manufacturer")
                }).FirstOrDefault() ?? new BiosSnapshot();
        }

        /// <summary>查询操作系统信息</summary>
        private static OsSnapshot QueryOs()
        {
            return Query(
                "SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem",
                obj => new OsSnapshot
                {
                    Caption = Text(obj, "Caption"),
                    Version = Text(obj, "Version"),
                    Architecture = Text(obj, "OSArchitecture")
                }).FirstOrDefault() ?? new OsSnapshot();
        }

        /// <summary>
        /// 查询音频端点设备，按名称粗略区分输入与输出。
        /// 测试功能统一使用 Windows 默认播放/录音设备。
        /// </summary>
        private static AudioSnapshot QueryAudio()
        {
            var audio = new AudioSnapshot();
            var endpoints = Query(
                "SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'AudioEndpoint'",
                obj => Text(obj, "Name"))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (string name in endpoints)
            {
                bool isInput = name.Contains("麦克风", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("Microphone", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("Input", StringComparison.OrdinalIgnoreCase)
                               || name.Contains("Capture", StringComparison.OrdinalIgnoreCase);
                if (isInput) audio.Inputs.Add(name);
                else audio.Outputs.Add(name);
            }

            // 部分系统没有 AudioEndpoint 类，回退到声卡名称，至少能展示设备
            if (endpoints.Count == 0)
            {
                foreach (string name in Query("SELECT Name FROM Win32_SoundDevice", obj => Text(obj, "Name"))
                             .Where(n => !string.IsNullOrWhiteSpace(n))
                             .Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    audio.Outputs.Add(name);
                }
            }

            return audio;
        }

        /// <summary>
        /// 查询显示器信息：型号、厂商、序列号、尺寸、主屏分辨率与刷新率。
        /// </summary>
        private static MonitorSnapshot QueryMonitor()
        {
            var monitor = new MonitorSnapshot
            {
                Width = GetSystemMetrics(0) > 0 ? GetSystemMetrics(0) : (int)System.Windows.SystemParameters.PrimaryScreenWidth,
                Height = GetSystemMetrics(1) > 0 ? GetSystemMetrics(1) : (int)System.Windows.SystemParameters.PrimaryScreenHeight
            };

            var refresh = Query("SELECT CurrentRefreshRate FROM Win32_VideoController", obj => Int(obj, "CurrentRefreshRate"));
            monitor.RefreshRate = refresh.Where(r => r > 0).DefaultIfEmpty(0).Max();

            try
            {
                var scope = new ManagementScope(@"\\.\root\wmi");
                scope.Connect();

                var names = new List<string>();
                using (var searcher = new ManagementObjectSearcher(scope,
                           new ObjectQuery("SELECT ManufacturerName, UserFriendlyName, SerialNumberID FROM WmiMonitorID")))
                {
                    searcher.Options.Timeout = QueryTimeout;
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)
                        {
                            string friendly = DecodeWmiString(obj["UserFriendlyName"]);
                            string manufacturer = DecodeWmiString(obj["ManufacturerName"]);
                            string serial = DecodeWmiString(obj["SerialNumberID"]);

                            if (string.IsNullOrWhiteSpace(friendly))
                                friendly = JoinNonEmpty(manufacturer, "Monitor");
                            names.Add(friendly);

                            if (string.IsNullOrWhiteSpace(monitor.Name))
                            {
                                monitor.Name = friendly;
                                monitor.Manufacturer = manufacturer;
                                monitor.SerialNumber = serial;
                            }
                        }
                    }
                }

                monitor.AdditionalCount = Math.Max(0, names.Count - 1);

                using (var searcher = new ManagementObjectSearcher(scope,
                           new ObjectQuery("SELECT MaxHorizontalImageSize, MaxVerticalImageSize FROM WmiMonitorBasicDisplayParams")))
                {
                    searcher.Options.Timeout = QueryTimeout;
                    using var collection = searcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        using (obj)
                        {
                            int widthCm = Int(obj, "MaxHorizontalImageSize");
                            int heightCm = Int(obj, "MaxVerticalImageSize");
                            if (widthCm > 0 && heightCm > 0)
                            {
                                double diagonalCm = Math.Sqrt(widthCm * widthCm + heightCm * heightCm);
                                monitor.SizeInch = Math.Round(diagonalCm / 2.54, 1);
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 显示器 WMI 在部分显卡/远程桌面下不可用，保留分辨率和刷新率
            }

            if (string.IsNullOrWhiteSpace(monitor.Name))
                monitor.Name = "主显示器";
            return monitor;
        }

        /// <summary>
        /// WmiMonitorID 的字符串字段是 ushort 数组，逐字符转换到 0 结束。
        /// </summary>
        private static string DecodeWmiString(object? value)
        {
            if (value is not ushort[] chars) return string.Empty;

            var builder = new System.Text.StringBuilder(chars.Length);
            foreach (ushort code in chars)
            {
                if (code == 0) break;
                builder.Append((char)code);
            }
            return builder.ToString().Trim();
        }

        /// <summary>将非空字符串用斜杠连接</summary>
        private static string JoinNonEmpty(params string[] values)
            => string.Join(" / ", values.Where(v => !string.IsNullOrWhiteSpace(v)));
        /// <summary>
        /// 执行 WMI 查询并将结果映射为强类型对象。
        /// 单次查询独立释放 ManagementObject，防止 WMI 句柄累积。
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="wql">WQL 查询语句</param>
        /// <param name="map">单条结果的映射函数</param>
        /// <returns>查询结果列表；查询失败时返回空列表</returns>
        private static List<T> Query<T>(string wql, Func<ManagementObject, T> map)
        {
            var result = new List<T>();
            try
            {
                using var searcher = new ManagementObjectSearcher(wql);
                searcher.Options.Timeout = QueryTimeout;

                using var collection = searcher.Get();
                foreach (ManagementObject obj in collection)
                {
                    using (obj)
                    {
                        result.Add(map(obj));
                    }
                }
            }
            catch
            {
                // 硬件信息属于辅助展示，单类查询失败时保持空卡片，不影响其他类别
            }
            return result;
        }

        /// <summary>
        /// 从显卡驱动注册表读取 64 位显存大小，修正 WMI AdapterRAM 的 32 位溢出。
        /// </summary>
        /// <param name="gpuName">显卡名称（用于匹配 DriverDesc）</param>
        /// <param name="bytes">输出显存字节数</param>
        /// <returns>是否读取成功</returns>
        private static bool TryGetRegistryVram(string gpuName, out ulong bytes)
        {
            bytes = 0;
            try
            {
                const string classKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using var baseKey = Registry.LocalMachine.OpenSubKey(classKey);
                if (baseKey == null) return false;

                foreach (string subName in baseKey.GetSubKeyNames())
                {
                    using var subKey = baseKey.OpenSubKey(subName);
                    if (subKey == null) continue;

                    string? desc = subKey.GetValue("DriverDesc") as string;
                    if (!string.Equals(desc?.Trim(), gpuName.Trim(), StringComparison.OrdinalIgnoreCase))
                        continue;

                    object? value = subKey.GetValue("HardwareInformation.qwMemorySize");
                    if (value == null) continue;

                    bytes = Convert.ToUInt64(value);
                    return bytes > 0;
                }
            }
            catch
            {
                // 注册表不可读时保留 WMI 值
            }
            return false;
        }
        /// <summary>读取字符串属性并去除首尾空白</summary>
        private static string Text(ManagementObject obj, string property)
            => obj[property]?.ToString()?.Trim() ?? string.Empty;

        /// <summary>读取整数属性，转换失败时返回 0</summary>
        private static int Int(ManagementObject obj, string property)
        {
            try { return Convert.ToInt32(obj[property]); }
            catch { return 0; }
        }

        /// <summary>读取 64 位无符号属性，转换失败时返回 0</summary>
        private static ulong UInt64(ManagementObject obj, string property)
        {
            try { return Convert.ToUInt64(obj[property]); }
            catch { return 0; }
        }

        /// <summary>将 WMI 的 DMTF 日期字符串转换为本地短日期</summary>
        private static string DateText(ManagementObject obj, string property)
        {
            try
            {
                string raw = Text(obj, property);
                return string.IsNullOrEmpty(raw)
                    ? string.Empty
                    : ManagementDateTimeConverter.ToDateTime(raw).ToString("yyyy-MM-dd");
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
