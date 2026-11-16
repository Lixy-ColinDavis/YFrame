using System.Collections.Generic;

namespace YFrame.Model
{
    /// <summary>
    /// 硬件静态信息快照（不包含实时占用数据）
    /// </summary>
    public sealed class HardwareInfoSnapshot
    {
        /// <summary>处理器信息</summary>
        public CpuSnapshot Cpu { get; set; } = new();

        /// <summary>显卡列表（可能包含集成显卡与独立显卡）</summary>
        public List<GpuSnapshot> Gpus { get; set; } = new();

        /// <summary>内存汇总与内存条列表</summary>
        public MemorySnapshot Memory { get; set; } = new();

        /// <summary>物理磁盘列表</summary>
        public List<DiskSnapshot> Disks { get; set; } = new();

        /// <summary>主板信息</summary>
        public BoardSnapshot Board { get; set; } = new();

        /// <summary>整机与 BIOS、操作系统信息</summary>
        public SystemSnapshot System { get; set; } = new();

        /// <summary>BIOS 信息</summary>
        public BiosSnapshot Bios { get; set; } = new();

        /// <summary>操作系统信息</summary>
        public OsSnapshot Os { get; set; } = new();
    }

    /// <summary>处理器静态信息</summary>
    public sealed class CpuSnapshot
    {
        /// <summary>处理器名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>物理核心数</summary>
        public int Cores { get; set; }

        /// <summary>逻辑线程数</summary>
        public int Threads { get; set; }

        /// <summary>标称主频（MHz）</summary>
        public int ClockMHz { get; set; }

        /// <summary>插槽标识</summary>
        public string Socket { get; set; } = string.Empty;
    }

    /// <summary>显卡静态信息</summary>
    public sealed class GpuSnapshot
    {
        /// <summary>显卡名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>显存字节数（WMI AdapterRAM，部分大显存会受 32 位字段限制）</summary>
        public ulong AdapterRam { get; set; }

        /// <summary>驱动版本</summary>
        public string DriverVersion { get; set; } = string.Empty;
    }

    /// <summary>内存汇总信息</summary>
    public sealed class MemorySnapshot
    {
        /// <summary>总容量字节数</summary>
        public ulong TotalBytes { get; set; }

        /// <summary>内存条列表</summary>
        public List<MemoryModuleSnapshot> Modules { get; set; } = new();
    }

    /// <summary>单根内存条信息</summary>
    public sealed class MemoryModuleSnapshot
    {
        /// <summary>容量字节数</summary>
        public ulong CapacityBytes { get; set; }

        /// <summary>频率（MT/s）</summary>
        public int Speed { get; set; }

        /// <summary>厂商</summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>插槽位置</summary>
        public string DeviceLocator { get; set; } = string.Empty;
    }

    /// <summary>物理磁盘静态信息</summary>
    public sealed class DiskSnapshot
    {
        /// <summary>磁盘型号</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>容量字节数</summary>
        public ulong SizeBytes { get; set; }

        /// <summary>接口类型（NVMe/SATA/USB 等）</summary>
        public string InterfaceType { get; set; } = string.Empty;

        /// <summary>介质类型</summary>
        public string MediaType { get; set; } = string.Empty;
    }

    /// <summary>主板静态信息</summary>
    public sealed class BoardSnapshot
    {
        /// <summary>主板厂商</summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>主板型号</summary>
        public string Product { get; set; } = string.Empty;

        /// <summary>主板序列号</summary>
        public string SerialNumber { get; set; } = string.Empty;
    }

    /// <summary>整机静态信息</summary>
    public sealed class SystemSnapshot
    {
        /// <summary>整机厂商</summary>
        public string Manufacturer { get; set; } = string.Empty;

        /// <summary>整机型号</summary>
        public string Model { get; set; } = string.Empty;

        /// <summary>计算机名</summary>
        public string ComputerName { get; set; } = string.Empty;

        /// <summary>系统总内存字节数</summary>
        public ulong TotalMemoryBytes { get; set; }
    }

    /// <summary>BIOS 静态信息</summary>
    public sealed class BiosSnapshot
    {
        /// <summary>BIOS 版本</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>BIOS 发布日期</summary>
        public string ReleaseDate { get; set; } = string.Empty;

        /// <summary>BIOS 厂商</summary>
        public string Manufacturer { get; set; } = string.Empty;
    }

    /// <summary>操作系统静态信息</summary>
    public sealed class OsSnapshot
    {
        /// <summary>系统名称</summary>
        public string Caption { get; set; } = string.Empty;

        /// <summary>系统版本号</summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>系统架构</summary>
        public string Architecture { get; set; } = string.Empty;
    }
}
