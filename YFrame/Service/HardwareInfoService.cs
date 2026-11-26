using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using YFrame.Model;
using YFrame.Service.Telemetry;

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
            using Activity? activity = TelemetryService.StartActivity("hardware.scan");
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
            snapshot.Monitors = QueryMonitors();

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

        /// <summary>查询物理磁盘列表，并补充接口类型、介质、固件与健康状态</summary>
        private static List<DiskSnapshot> QueryDisks()
        {
            var disks = Query(
                "SELECT * FROM Win32_DiskDrive",
                obj => new DiskSnapshot
                {
                    Index = Int(obj, "Index"),
                    Model = Text(obj, "Model"),
                    SizeBytes = UInt64(obj, "Size"),
                    InterfaceType = Text(obj, "InterfaceType"),
                    MediaType = Text(obj, "MediaType"),
                    FirmwareRevision = Text(obj, "FirmwareRevision"),
                    SerialNumber = Text(obj, "SerialNumber"),
                    TransferRate = (uint)Math.Max(0, Int(obj, "TransferRate")),
                    PnpDeviceId = Text(obj, "PNPDeviceID")
                });

            // MSFT_PhysicalDisk 提供 BusType/MediaType/HealthStatus，比 Win32_DiskDrive 的 InterfaceType 更准确
            var storage = QueryStorage(
                "SELECT DeviceId, MediaType, BusType, HealthStatus FROM MSFT_PhysicalDisk",
                obj => new
                {
                    DeviceId = Text(obj, "DeviceId"),
                    MediaType = Int(obj, "MediaType"),
                    BusType = Int(obj, "BusType"),
                    HealthStatus = Int(obj, "HealthStatus")
                });

            foreach (var disk in disks)
            {
                var detail = storage.FirstOrDefault(s => s.DeviceId == disk.Index.ToString());
                if (detail != null)
                {
                    disk.BusTypeText = BusTypeName(detail.BusType);
                    disk.MediaTypeText = StorageMediaTypeName(detail.MediaType);
                    disk.HealthStatus = HealthStatusName(detail.HealthStatus);
                }
                else
                {
                    disk.BusTypeText = InferBusType(disk.PnpDeviceId, disk.InterfaceType);
                    disk.MediaTypeText = disk.MediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase) ? "SSD" : string.Empty;
                }

                // NVMe 通过父 PCIe 设备读取当前链路速率/宽度
                if (string.Equals(disk.BusTypeText, "NVMe", StringComparison.OrdinalIgnoreCase)
                    || disk.PnpDeviceId.Contains("NVME", StringComparison.OrdinalIgnoreCase))
                {
                    disk.PcieLinkText = QueryPcieLink(disk.PnpDeviceId);
                }
            }

            AttachDriveRoots(disks);
            return disks;
        }

        /// <summary>
        /// 通过 WMI 关联把物理磁盘映射到第一个固定逻辑盘符。
        /// </summary>
        private static void AttachDriveRoots(List<DiskSnapshot> disks)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
                searcher.Options.Timeout = QueryTimeout;
                using var collection = searcher.Get();

                foreach (ManagementObject diskObject in collection)
                {
                    using (diskObject)
                    {
                        int index = Int(diskObject, "Index");
                        var disk = disks.FirstOrDefault(item => item.Index == index);
                        if (disk == null) continue;

                        foreach (ManagementObject partition in diskObject.GetRelated("Win32_DiskPartition"))
                        {
                            using (partition)
                            {
                                foreach (ManagementObject logical in partition.GetRelated("Win32_LogicalDisk"))
                                {
                                    using (logical)
                                    {
                                        // DriveType=3 表示本地固定磁盘，排除光驱/U盘/网络盘
                                        if (Int(logical, "DriveType") != 3) continue;

                                        string deviceId = Text(logical, "DeviceID").Trim();
                                        if (string.IsNullOrEmpty(deviceId)) continue;

                                        disk.DriveRoot = deviceId.TrimEnd('\\') + "\\";
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(disk.DriveRoot)) break;
                        }
                    }
                }
            }
            catch
            {
                // 盘符映射失败不影响磁盘型号/容量等基础信息
            }
        }

        /// <summary>
        /// 从 root\Microsoft\Windows\Storage 命名空间查询存储设备。
        /// </summary>
        private static List<T> QueryStorage<T>(string wql, Func<ManagementObject, T> map)
        {
            var result = new List<T>();
            try
            {
                var scope = new ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery(wql));
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
                // 存储命名空间不可用时退回 Win32_DiskDrive 的接口字段
            }
            return result;
        }

        /// <summary>将 STORAGE_BUS_TYPE 数值转换为可读名称</summary>
        private static string BusTypeName(int busType) => busType switch
        {
            1 => "SCSI",
            3 => "ATA",
            7 => "USB",
            8 => "RAID",
            10 => "SAS",
            11 => "SATA",
            14 => "Virtual",
            16 => "Spaces",
            17 => "NVMe",
            18 => "SCM",
            19 => "UFS",
            _ => "Unknown"
        };

        /// <summary>将 MSFT_PhysicalDisk.MediaType 数值转换为 SSD/HDD</summary>
        private static string StorageMediaTypeName(int mediaType) => mediaType switch
        {
            3 => "HDD",
            4 => "SSD",
            5 => "SCM",
            _ => string.Empty
        };

        /// <summary>将 MSFT_PhysicalDisk.HealthStatus 数值转换为可读状态</summary>
        private static string HealthStatusName(int health) => health switch
        {
            0 => "正常",
            1 => "警告",
            2 => "异常",
            _ => string.Empty
        };

        /// <summary>无法读取 MSFT_PhysicalDisk 时，从 PNP 设备 ID 推断接口类型</summary>
        private static string InferBusType(string pnpDeviceId, string interfaceType)
        {
            if (pnpDeviceId.Contains("NVME", StringComparison.OrdinalIgnoreCase)) return "NVMe";
            if (pnpDeviceId.Contains("USB", StringComparison.OrdinalIgnoreCase)) return "USB";
            return string.IsNullOrWhiteSpace(interfaceType) ? string.Empty : interfaceType;
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
        /// 查询全部显示器：型号、厂商、序列号、尺寸，以及每台显示器的当前分辨率与刷新率。
        /// </summary>
        private static List<MonitorSnapshot> QueryMonitors()
        {
            var modes = GetDisplayModes();
            var names = new List<(string Name, string Manufacturer, string Serial)>();
            var sizes = new List<double>();

            try
            {
                var scope = new ManagementScope(@"\\.\root\wmi");
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
                            names.Add((string.IsNullOrWhiteSpace(friendly) ? "显示器" : friendly, manufacturer, serial));
                        }
                    }
                }

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
                            sizes.Add(widthCm > 0 && heightCm > 0
                                ? Math.Round(Math.Sqrt(widthCm * widthCm + heightCm * heightCm) / 2.54, 1)
                                : 0);
                        }
                    }
                }
            }
            catch
            {
                // 显示器 WMI 在部分显卡/远程桌面下不可用，仍用 EnumDisplayMonitors 的分辨率
            }

            int count = Math.Max(modes.Count, names.Count);
            if (count == 0)
                count = 1;

            var result = new List<MonitorSnapshot>();
            for (int i = 0; i < count; i++)
            {
                var mode = i < modes.Count ? modes[i] : null;
                var info = i < names.Count ? names[i] : (Name: $"显示器 {i + 1}", Manufacturer: string.Empty, Serial: string.Empty);

                result.Add(new MonitorSnapshot
                {
                    Name = info.Name,
                    Manufacturer = info.Manufacturer,
                    SerialNumber = info.Serial,
                    SizeInch = i < sizes.Count ? sizes[i] : 0,
                    Width = mode?.Width ?? 0,
                    Height = mode?.Height ?? 0,
                    RefreshRate = mode?.RefreshRate ?? 0
                });
            }

            return result;
        }

        /// <summary>
        /// 通过 EnumDisplayMonitors 枚举当前所有显示器，并读取各自的当前分辨率与刷新率。
        /// </summary>
        private static List<DisplayMode> GetDisplayModes()
        {
            var result = new List<DisplayMode>();
            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref NativeRect rect, IntPtr data) =>
                {
                    var info = new MonitorInfoEx { cbSize = Marshal.SizeOf<MonitorInfoEx>() };
                    if (!GetMonitorInfo(hMonitor, ref info)) return true;

                    var mode = new DevMode { dmSize = (short)Marshal.SizeOf<DevMode>() };
                    bool hasMode = EnumDisplaySettings(info.szDevice, -1, ref mode);
                    result.Add(new DisplayMode
                    {
                        Width = hasMode ? mode.dmPelsWidth : rect.Right - rect.Left,
                        Height = hasMode ? mode.dmPelsHeight : rect.Bottom - rect.Top,
                        RefreshRate = hasMode ? mode.dmDisplayFrequency : 0,
                        IsPrimary = (info.dwFlags & 1) != 0,
                        Left = rect.Left
                    });
                    return true;
                }, IntPtr.Zero);

                // 主屏优先，其余按水平位置排列，便于和 WMI 显示器顺序大致对应
                result.Sort((a, b) =>
                {
                    if (a.IsPrimary != b.IsPrimary) return a.IsPrimary ? -1 : 1;
                    return a.Left.CompareTo(b.Left);
                });
            }
            catch
            {
                // 枚举失败时返回空列表，由 WMI 信息兜底
            }
            return result;
        }

        /// <summary>EnumDisplayMonitors 所需的矩形结构</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        /// <summary>显示器信息结构</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfoEx
        {
            public int cbSize;
            public NativeRect rcMonitor;
            public NativeRect rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        /// <summary>显示模式结构（仅使用分辨率与刷新率字段）</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        /// <summary>内部显示模式数据</summary>
        private sealed class DisplayMode
        {
            public int Width { get; set; }
            public int Height { get; set; }
            public int RefreshRate { get; set; }
            public bool IsPrimary { get; set; }
            public int Left { get; set; }
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx info);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref NativeRect rect, IntPtr data);
        /// <summary>
        /// 执行 WMI 查询并将结果映射为强类型对象。
        /// 单次查询独立释放 ManagementObject，防止 WMI 句柄累积。
        /// </summary>
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
                // 单类查询失败时保持空列表，不影响其他硬件卡片
            }
            return result;
        }

        /// <summary>将 WmiMonitorID 的 ushort 数组字段解码为字符串</summary>
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
        /// <summary>
        /// 从显卡驱动注册表读取 64 位显存大小，修正 WMI AdapterRAM 的 32 位溢出。
        /// </summary>
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
        /// <summary>
        /// 读取 NVMe 设备父 PCIe 设备的当前链路速率与宽度，输出如 “PCIe 4.0 x4”。
        /// </summary>
        private static string QueryPcieLink(string pnpDeviceId)
        {
            try
            {
                if (CM_Locate_DevNodeW(out uint diskNode, pnpDeviceId, 0) != 0)
                    return "PCIe";

                if (CM_Get_Parent(out uint parentNode, diskNode, 0) != 0)
                    return "PCIe";

                uint speed = GetPciProperty(parentNode, 9);   // DEVPKEY_PciDevice_CurrentLinkSpeed
                uint width = GetPciProperty(parentNode, 10);  // DEVPKEY_PciDevice_CurrentLinkWidth

                string generation = speed switch
                {
                    1 => "1.0",
                    2 => "2.0",
                    3 => "3.0",
                    4 => "4.0",
                    5 => "5.0",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(generation)) return "PCIe";
                return width > 0 ? $"PCIe {generation} x{width}" : $"PCIe {generation}";
            }
            catch
            {
                return "PCIe";
            }
        }

        /// <summary>读取指定 PCI 设备属性的 UInt32 值</summary>
        private static uint GetPciProperty(uint devNode, uint propertyId)
        {
            var key = new DevPropKey
            {
                fmtid = Guid.Parse("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62"),
                pid = propertyId
            };

            uint size = 0;
            CM_Get_DevNode_PropertyW(devNode, ref key, out _, IntPtr.Zero, ref size, 0);
            if (size < sizeof(uint)) return 0;

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                int rc = CM_Get_DevNode_PropertyW(devNode, ref key, out uint type, buffer, ref size, 0);
                if (rc != 0 || type != 7) return 0; // DEVPROP_TYPE_UINT32
                return (uint)Marshal.ReadInt32(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>设备属性键</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct DevPropKey
        {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_DevNode_PropertyW(uint dnDevInst, ref DevPropKey propertyKey,
            out uint propertyType, IntPtr propertyBuffer, ref uint propertyBufferSize, uint ulFlags);
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
