using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
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
