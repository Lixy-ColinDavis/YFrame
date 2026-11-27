# YFrame 项目参考（AI 开发速查）

> **用途：** 让新的 AI 会话在不重复通读全部代码的情况下，准确理解 YFrame 的架构、插件契约、构建方式、更新链路和当前约束。
>
> **最后更新：** 2026-11-26
> **产品版本：** 2.1.0

---

## 0. 阅读顺序

1. `YFrame.sln`：确认项目组成；
2. `YFrame/App.xaml.cs`：启动、更新检查、DI；
3. `YFrame/MainWindow.xaml` + `MainWindow.xaml.cs`：Shell 与交互；
4. `YFrame/ViewModel/MainWindowViewModel.cs`：主 VM 门面；
5. `YFrame/Service/*`：运行时服务；
6. `YF_Manager/Common/*`：契约、AOP、配置、Mediator、工具；
7. `YFrame.Pack/`、`YFrame.Sign/`、`YFrame.update/`、`YFrame.Installer/`：交付链路；
8. 上层 `..\YF_*`：10 个插件。

---

## 1. 项目定位

YFrame 是 **Windows 桌面开发者工具平台**：

- `.NET 8.0-windows` + WPF；
- 抽屉式三栏 Shell；
- 反射加载外部插件；
- 插件零编译依赖主程序；
- 自带工具箱、日志、性能监控、主题、双语、热键、托盘；
- 具备 Pack 增量/全量包、Update 自动更新、Installer 安装器完整链路。

**路径：**

```text
C:\Users\Administrator\Desktop\code\C#\YFrame
```

解决方案：

```text
YFrame.sln
```

构建 SDK 当前为 .NET 9，但所有项目目标框架仍是 `net8.0-windows`。

---

## 2. 解决方案组成

| 项目 | 路径 | 类型 | 职责 |
|---|---|---|---|
| `YFrame` | `YFrame/YFrame.csproj` | WPF App | Shell、主 ViewModel、插件运行时、工具箱、更新检查 |
| `YF_Manager` | `YF_Manager/YF_Manager.csproj` | Class Library | 插件契约、AOP、配置、Mediator、日志、通用工具 |
| `YFrame.Tests` | `YFrame.Tests/YFrame.Tests.csproj` | xUnit | 123 个测试 |
| `YFrame.Installer` | `YFrame.Installer/YFrame.Installer.csproj` | WPF App | 向导式安装器，自包含单文件 |
| `YFrame.Pack` | `YFrame.Pack/YFrame.Pack.csproj` | WPF App | ZIP 差异、增量包、版本清单 |
| `YFrame.Sign` | `YFrame.Sign/YFrame.Sign.csproj` | WPF App | 清单签名、临时密钥、路径配置 |
| `YFrame.update` | `YFrame.update/YFrame.update.csproj` | WPF App | 自动下载、校验、备份、替换、重启 |

**解决方案不含 10 个插件**；插件位于 `C:\Users\Administrator\Desktop\code\C#\YF_*`，单独构建。

---

## 3. 启动链路

`YFrame/App.xaml.cs` 的 `OnStartup`：

```text
--uninstall / -uninstall
  → HandleUninstall + Shutdown

正常启动：
base.OnStartup
  → ShutdownMode = OnExplicitShutdown
  → 初始化 YF_Manager_Main.logger
  → TelemetryService.Initialize()（ApmEnabled）
  → 创建 app.startup Span
  → ApplyPendingUpdaterAsync()
  → ApplySavedTheme()
  → UpdateService.CheckAsync()
       ├─ 本地 == 远端 current：继续
       ├─ 本地 == 远端 pervious：UpdatePromptWindow（增量优先）
       ├─ 本地 < current 的其他版本：UpdatePromptWindow（全量）
       ├─ 本地 > current：不提示、不降级
       └─ 网络失败/无可用包：继续
  → 构建 DI 容器
  → YF_Di.Provider
  → 解析 MainWindow
  → Show
```

### 更新器接替

- 更新包包含新的 `YFrame.update.exe` 时，`YFrame.update` **不覆盖运行中的自身**；
- 写到 `YFrame.update.new.exe`；
- YFrame 下次启动在 `ApplyPendingUpdaterAsync` 中替换正式更新器；
- 文件被占用时最多重试 20 次 × 250ms；
- 失败不阻断启动，pending 文件保留。

---

## 4. 插件系统

### 4.1 契约

```csharp
public interface I_YF_Detail
{
    string YF_ID { get; }
    string YF_Name { get; }
}

public interface I_YF_Command
{
    void ExecuteCommand(string command, object? parameter = null);
    event EventHandler<PluginEventArgs> OnPluginCallback;
}
```

### 4.2 目录约定

```text
plugins/
  └── YF_XXX/
      ├── YF_XXX.dll
      ├── 依赖 DLL
      └── plugin.json（可选）
```

命名空间 = DLL 名；入口控件 = `{Namespace}.MainControl`；ViewModel = `{Namespace}.MainControlViewModel`。

### 4.3 加载流程

```text
UserControlsService.LoadAndShowUserControl()
  → 扫描 plugins/*/YF_*.dll
  → 过滤 YF_Manager.dll
  → Assembly.LoadFrom
  → 读取 I_YF_Detail 元数据（懒加载）
  → 用户点击插件
  → Activator.CreateInstance ViewModel
  → 创建 MainControl
  → 绑定 DataContext
```

### 4.4 回调与命令

- 框架 → 插件：`PluginService.SendCommand` / `I_YF_Command.ExecuteCommand`；
- 插件 → 框架：`OnPluginCallback` → `PluginService.HandlePluginCallback`；
- 全局热键 `Ctrl+Y`：`HotkeyService` → `YF_Messenger` → `PluginService.OnHotkeyPressed`；
- 路由规则：
  - `YF_ScreenOCRTranslate` / `YF_Paint` → `CaptureScreen` / `Hotkey`
  - `YF_Clicker` → `ToggleClick`
  - 其他 → `HotkeyTrigger`

### 4.5 插件输出路径（重要）

多数插件 csproj 把 `OutputPath` 写死为：

```text
YFrame/bin/Debug/net8.0-windows/plugins/{PluginName}
```

因此：

- Debug 插件构建后可直接被框架加载；
- Release 部署需要手动复制或修改 OutputPath；
- 不要假设插件会随 `YFrame.sln` 一起构建。

### 4.6 开发约束

- 只引用 `YF_Manager`，不要引用 `YFrame.exe`；
- 需要 AOP 的 public 方法必须 `virtual` 并标记 `[Log]`；
- 不在插件中重复实现日志、配置、文件、ZIP 等通用能力；
- UI 文案放 `YFrame/Common/Language/zh-CN.xaml` / `en-US.xaml`，使用 `key_*`；
- 插件设置放 `Config/...`，不要写死用户路径。

---

## 5. DI / AOP / Mediator

### 5.1 DI

- `App.ConfigureServices` 注册服务；
- AOP 代理类通过 `InitializeDependencies(...)` 属性注入；
- 非 AOP 服务使用构造函数注入；
- `YF_Di.Provider` 供插件/底层库解析。

### 5.2 AOP

- `Castle.Core` `ProxyGenerator.CreateClassProxy<T>(new LogInterceptor())`；
- `LogInterceptor` 识别 `[Log]`，记录：
  - 方法名、参数；
  - 耗时；
  - 返回值/异常；
- 同步/异步方法都支持；
- 业务异常记录后必须原样抛出，AOP 不改变调用方语义；
- 只有日志写入失败会降级为 Debug 输出，不影响业务异常。

### 5.3 Mediator

`YF_Messenger` 是轻量消息中介，消息定义在 `YF_Messages.cs`：

| 消息 | 用途 |
|---|---|
| `LogAppendMessage` | UI 日志追加 |
| `LogClearMessage` | 清空日志 |
| `PluginShownMessage` | 当前插件切换 |
| `HotkeyTriggeredMessage` | 热键触发 |
| `ScriptCommandMessage` | 新建/打开/保存脚本 |
| `PanelSwitchMessage` | 左右面板切换 |
| `ThemeChangedMessage` | 主题切换 |
| `LanguageChangedMessage` | 语言切换 |

新增跨组件通信优先用 Messenger，不要直接互相引用。

---

## 6. 运行时服务

| 服务 | 职责 |
|---|---|
| `LogService` | 500 行日志缓冲、追加/清空、日志面板 |
| `PluginService` | 插件显示、命令转发、热键路由、脚本命令、回调 |
| `UserControlsService` | 反射扫描/加载/实例化插件、懒加载、卸载 |
| `PluginManagerService` | 连接插件服务器、拉取列表、下载/解压 |
| `HotkeyService` | Win32 `RegisterHotKey`、WndProc、Ctrl+Y |
| `TrayIconService` | Shell_NotifyIcon、托盘菜单 |
| `ToolboxService` | 工具注册表、视图工厂、视图缓存 |
| `UpdateService` | 远端版本检查、三天跳过、启动 updater |
| `TelemetryService` | OpenTelemetry APM：启动/硬件/上传 Span、Error 分类 Span、OTLP |
| `CloudBaseHardwareService` | 硬件结果通过 RPC 按 `device_name` upsert |
| `HardwareInfoService` | WMI/注册表/PCIe/多显示器/音频信息，并写入硬件摘要 Span |
| `AudioTestService` | `winmm.dll` 播放/录音、电平 |
| `DiskSpeedTestService` | 指定卷 32MB 顺序读写测速 |
| `UpdateRunner` | 下载、校验、备份、替换、删除、重启 |
| `UpdateLogger` | `Log/Update/yyyy-MM-dd.log` |

---

## 7. UI 与主题

### 7.1 Shell

- `WindowStyle=None` + `AllowsTransparency=True` + `WindowChrome`；
- 圆角窗口、无边框、拖拽标题栏；
- 三栏：左侧插件/工具箱，中间工作区，右侧日志/性能；
- 顶部工具栏：热键、日志目录、屏幕缩放；
- 首页四卡手风琴；
- 插件区与工具箱叠加显示，不破坏插件状态。

### 7.2 主题

| 主题 | 文件 |
|---|---|
| 暗夜科技 | `NightTechTheme.xaml` |
| 云白简约（默认） | `CloudWhiteTheme.xaml` |
| 复古终端 | `RetroTerminalTheme.xaml` |
| 玻璃拟态 | `GlassTheme.xaml` |

- `ControlStyles.xaml` 统一按钮/输入框/下拉框/进度条/卡片；
- 字体 `DengXian`；
- 动态资源键：`WindowBrush`、`PanelBrush`、`PanelAltBrush`、`CardHoverBrush`、`AccentBrush`、`TextBrush`、`BorderBrush` 等；
- 新增控件必须使用主题资源，不要硬编码颜色。

### 7.3 首页

- `HomeCard0..3` + `HomeCol0..3`；
- 展开卡 `*`，收起卡 `64`；
- `HomeCardXHover` 是内容下方的背景层，不是遮罩；
- 当前选中卡不显示悬浮背景；
- 入场动画 `FillBehavior.Stop`，结束清除动画时钟/位移层；
- 不添加整卡半透明遮罩，避免文字可读性和性能问题。

### 7.4 工具箱

| 工具 | ID | 说明 |
|---|---|---|
| 屏幕取色器 | `color-picker` | 框选截图 + 点击取色 |
| 正则测试 | `regex-tester` | YF_RegexHelper，多行锚点归一化 |
| MD5 校验 | `md5-compare` | 单文件/双文件对比 |
| 硬件检查 | `hardware-info` | WMI + 音频 + 磁盘测速 + 多显示器 |

工具箱工具通过 `ToolboxService` 注册，视图缓存，关闭不销毁。

---

## 8. 硬件检查细节

`HardwareInfoService.Load()` 异步采集：

- CPU：`Win32_Processor`；
- GPU：`Win32_VideoController`，过滤 Microsoft Basic/Remote/Oray/虚拟显卡；
- 显存：WMI `AdapterRAM` 32 位会溢出，使用注册表 `HardwareInformation.qwMemorySize` 修正；
- 内存：`Win32_PhysicalMemory`；
- 磁盘：`Win32_DiskDrive` + `MSFT_PhysicalDisk`；
- 接口：`BusType`（NVMe/SATA/USB）；
- PCIe：父 PCIe 设备 `DEVPKEY_PciDevice_CurrentLinkSpeed/Width`，显示 `PCIe 4.0 x4`；
- 盘符映射：`Win32_DiskDriveToDiskPartition` + `Win32_LogicalDiskToPartition`；
- 显示器：`WmiMonitorID` + `EnumDisplayMonitors` + `GetMonitorInfo`；
- 音频：`Win32_PnPEntity` AudioEndpoint，区分输入/输出；
- 测速：每块盘独立按钮，写各自卷 32MB 临时文件，完成后删除。

布局：

- 两列四行卡片：CPU/GPU、内存/硬盘、音频/显示器、主板；
- 硬盘行固定 160px + 内部滚动，保证两块盘各自结果可见；
- 显示器每台两行：名称+厂商/序列号，第二行尺寸/分辨率/刷新率；
- 显示器顶部对齐，最大化后不会在顶部留大空白；
- 音频区倒计时在进度条左侧，固定 16px 高容器，进度条不上下移动。

### 8.1 可观测性

- `YFrame.csproj` 引用 `OpenTelemetry` 1.12.0 与 `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.12.0；
- APM Endpoint 固定在 `YFrame/Service/Telemetry/ApmBuiltInSettings.cs`；
- APM Token 从 DPAPI 读取 `%LocalAppData%\YFrame\secrets.dat`，不写入源码/config.conf；
- 安装/更新包可在根目录放置 `YFrame.secrets.bootstrap`，首次启动自动迁移 DPAPI；
- 引导文件不能放 `Config`，否则更新器会保留旧文件；按用户安装迁移后删除，按机器安装保留；
- `TelemetryService` 使用 `ActivitySource("YFrame")` + OTLP gRPC；
- 当前 Span：`app.startup`、`hardware.scan`、`cloudbase.upload`、`Error.<类型>`；
- `hardware.scan` 附加 `hardware.summary`、CPU/内存/GPU/磁盘/显示器/OS 标签；
- 多 GPU 使用 `hardware.gpu_names` 汇总，并提供 `hardware.gpu_0_name`、`hardware.gpu_1_name`；
- 只有 `[Error]` 日志进入 APM；
- `Error.<类型>` 直接取异常类型名并移除 `Exception` 后缀，例如 `NullReferenceException` → `Error.NullReference`；
- 无异常对象时按来源/消息兜底，CloudBase 上传失败为 `Error.upload`；
- `LogInterceptor` 会把 AOP 捕获的异常传入日志事件，因此 AOP 失败也会生成 Error Span；
- 当前没有 MeterProvider、自定义 Metric、CPU/内存指标。

### 8.2 CloudBase 硬件入库

- 服务：`YFrame/Service/CloudBase/CloudBaseHardwareService.cs`；
- 配置：`CloudBaseEnabled`、`CloudBaseEnvId`、`CloudBaseHardwareTable`、`CloudBaseHardwareRpc`；
- API Key 从 DPAPI 读取 `%LocalAppData%\YFrame\secrets.dat`，不写入源码/config.conf；
- 安装/更新包可在根目录放置 `YFrame.secrets.bootstrap`，首次启动自动迁移 DPAPI；
- 引导文件不能放 `Config`，否则更新器会保留旧文件；按用户安装迁移后删除，按机器安装保留；
- 地址：`https://{envId}.api.tcloudbasegateway.com/v1/rdb/rest/rpc/upsert_yframe_hardware`；
- 调用 RPC：数据库函数内部 `INSERT ... ON CONFLICT (device_name)`，存在更新、不存在插入；
- RPC 参数：`p_device_name`、`p_cpu`、`p_gpu`、`p_memory`、`p_disk`；
- 表字段：`device_name`、`cpu`、`gpu`、`memory`、`disk`、`updatetime`；`id`/`created_at` 用默认值；
- 多 GPU、多磁盘分别用 ` | ` 合并后写入 text 字段；
- Publishable Key 对应 `anon`，只授予函数执行权限：

```sql
GRANT EXECUTE
ON FUNCTION public.upsert_yframe_hardware(text,text,text,text,text)
TO anon;
```

- 同时撤销表级写权限：

```sql
REVOKE INSERT, UPDATE, DELETE
ON public.yframe_hardware
FROM anon;
```

- RLS 关闭只关闭行级策略，不能替代表级 GRANT 或 RPC 权限；
- `device_name` 唯一索引是 `ON CONFLICT` 的前提。

---

## 9. 更新系统

### 9.1 配置

`Config/config.conf`：

```ini
Env=uat
UpdateBaseUrl=https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/update
```

- `Env` 缺省 `uat`；`pdt` 使用生产环境；
- 实际地址：

```text
{BaseUrl}/{Env}/YF_Version.json
{BaseUrl}/{Env}/current.zip
{BaseUrl}/{Env}/increment.zip
```

- **唯一的默认根地址**：`YF_Manager/Common/UpdateDefaults.cs` 中的 `Cos_UpdateDefaults.BaseUrl`；
- `Config.DefaultUpdateBaseUrl` 引用它；
- Pack / update 链接同一个文件，不允许再写第二份根地址；
- 配置明文存储，注释说明“项目开源、COS 成本低、无安全与经济风险”。

### 9.2 启动检查

`UpdateService.CheckAsync`：

- 本地版本 = `AssemblyVersion.ToString(3)`；
- 远端 `current_version` / `pervious_version` 及 `current` / `pervious` 包信息；
- 本地 == current：不处理；
- 本地 == previous：提示，增量优先；
- 本地 < current 的其他版本：提示，走全量；
- 本地 > current：不提示、不降级；
- 相邻版本至少要有增量或全量包，跨版本必须有全量包；
- 网络失败静默跳过；
- 不更新：写 `UpdateSkipVersion` + `UpdateSkipUntil`（UTC，3 天）；
- 同一远端版本 3 天内不再提示；
- 新远端版本出现时重新提示。

### 9.3 更新器流程

`YFrame.update/UpdateRunner`：

```text
读取 --manifest / Config/config.conf
→ 下载 YF_Version.json + .sig
→ 使用 UpdateTrustKeys 对应环境公钥验签
→ 验签失败：拒绝更新
→ 本地 == current：结束
→ 本地 < current：
     ├─ 本地 == previous 且增量包可用：increment.zip
     └─ 其他旧版本 / 增量缺失：current.zip
→ 校验 size + SHA-256
→ 解压到 staging，检查路径穿越
→ 等待 YFrame 进程退出
→ 备份
→ 替换
→ 删除 removed（全量更新额外清理旧框架文件）
→ 删除整个 Backup
→ 启动 YFrame
```

- 全量更新覆盖 `current.zip` 中的框架文件；
- `Config/`、`Log/`、`plugins/`、`Backup/`、`File/` 等用户目录不覆盖、不清理；
- 根目录旧框架文件以及 `runtimes/`、`Common/` 中不属于新包的文件会被清理；
- `YFrame.Pack.exe`、`YFrame.Pack.config.json`、`YFrame.Installer.exe` 保留；
- 日志：`Log/Update/yyyy-MM-dd.log`；
- 失败保留备份，成功删除整个 `Backup`；
- 新包包含 `YFrame.update.exe` → 写 `YFrame.update.new.exe`，由 YFrame 启动接替；
- 不覆盖运行中的 updater；
- `removed` 中的 updater 文件会被忽略删除；
- 更新器 Release 发布为框架依赖单文件（约 0.6MB），需要 .NET 8 Desktop Runtime。

### 9.4 Pack

`YFrame.Pack`：

- 输入 `YF-x.y.z.zip` 两个；
- 按路径 + SHA-256 比较文件；
- 新增/修改写入 `increment.zip`；
- 最新完整包复制为 `current.zip`；
- 输出 `YF_Version.json`；
- `pervious` 拼写必须保持；
- `updatetime` 格式 `yyyy-MM-dd'T'HH-mm-ss`；
- 输出目录自动加时间戳，避免覆盖；
- 配置 `YFrame.Pack.config.json` 保存输出根目录和 URL，输入 ZIP 路径不保存；
- 启动自动扫描 exe 目录版本最高的两个 ZIP。

---

## 10. 安装器

`YFrame.Installer`：

- 构建前 `CollectPayload.ps1` 收集 YFrame 核心文件；
- 打包为 `Resources/payload.zip` 并嵌入；
- 向导：欢迎 → 路径/快捷方式 → 进度 → 完成；
- 单文件发布配置在 `Properties/PublishProfiles/SingleFile.pubxml`；
- 输出 `Installer/YFrame.Installer.exe`；
- 默认自包含，体积约 150MB。

命令：

```powershell
dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release -p:PublishProfile=SingleFile
```

---

## 11. 配置键

| 键 | 默认/说明 |
|---|---|
| `Theme` | `Common/Themes/CloudWhiteTheme.xaml` |
| `ScreenScale` | 125 |
| `LogPath` | `Log` |
| `ScriptPath` | `Config\Script` |
| `PluginPath` | `Plugins` |
| `TcpHelper_Port_Server` | 8021 |
| `TcpHelper_Port_Client` | 8022 |
| `PluginServerPort` | 9000 |
| `PluginManagerServerURL` | `http://127.0.0.1` |
| `Paddlepath` | `plugins\YF_ScreenOCRTranslate\inference` |
| `AIModelpath` | `plugins\YF_AIHelper\Model\...gguf` |
| `Env` | `uat` / `pdt` |
| `UpdateBaseUrl` | COS 根地址 |
| `ApmEnabled` | 是否启用 APM OpenTelemetry，默认 `true` |
| `ApmServiceName` | APM 服务名，默认 `YFrame` |
| `ApmEnvironment` | APM 环境，默认 `uat` |
| `CloudBaseEnabled` | 是否启用硬件入库，默认 `true` |
| `CloudBaseEnvId` | CloudBase 环境 ID |
| `CloudBaseHardwareTable` | 硬件表名，默认 `yframe_hardware` |
| `CloudBaseHardwareRpc` | 硬件 upsert RPC 名，默认 `upsert_yframe_hardware` |

---

## 12. 构建 / 测试 / 运行

```powershell
cd C:\Users\Administrator\Desktop\code\C#\YFrame

dotnet build YFrame.sln -c Debug
dotnet test YFrame.Tests/YFrame.Tests.csproj -c Debug

dotnet run --project YFrame/YFrame.csproj
```

运行输出：

```text
YFrame/bin/Debug/net8.0-windows/YFrame.exe
```

插件构建：

```powershell
dotnet build ..\YF_Clicker\YF_Clicker\YF_Clicker.csproj -c Debug
```

发布：

```powershell
dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release -p:PublishProfile=SingleFile
dotnet publish YFrame.update/YFrame.update.csproj -c Release -p:PublishProfile=SingleFile
dotnet build YFrame.Pack/YFrame.Pack.csproj -c Release
dotnet build YFrame.Sign/YFrame.Sign.csproj -c Release
```

---

## 13. 版本

`Version.props`：

```xml
<Version>2.1.0</Version>
<BuildDay>当前日期距 2000-01-01 的天数</BuildDay>
<AssemblyVersion>$(Version).$(BuildDay)</AssemblyVersion>
<FileVersion>$(Version).$(BuildDay)</FileVersion>
<InformationalVersion>$(Version)</InformationalVersion>
```

- 标题栏：`v2.1`（AssemblyVersion 前两位）；
- 关于窗口：`2.1.0.<day>` 形式；
- 文件版本：`2.1.0.<day>`；
- 产品版本：`2.1.0`；
- `Version` 保持三段数字，不要写第四段，否则程序集版本会变五段；
- 只在 `YFrame`、`YF_Manager`、`YFrame.Tests`、`YFrame.Installer` 导入；
- 插件不导入。

---

## 14. 关键路径

```text
YFrame/
├── YFrame/
│   ├── App.xaml.cs
│   ├── MainWindow.xaml / .cs
│   ├── MainWindowViewModel.cs
│   ├── Service/*.cs
│   ├── Service/Telemetry/        # OpenTelemetry / APM
│   ├── Service/CloudBase/        # CloudBase 硬件入库
│   ├── Service/Security/         # DPAPI / 引导迁移
│   ├── View/UC/Toolbox/*.xaml
│   ├── Controls/Sparkline.cs
│   ├── Controls/VerticalText.cs
│   └── Common/Themes/*.xaml
├── YF_Manager/
│   ├── Interface/I_YF_Detail.cs
│   ├── Interface/I_YF_Command.cs
│   ├── Common/Config.cs
│   ├── Common/UpdateDefaults.cs
│   ├── Common/UpdateTrustKeys.cs
│   ├── Common/YF_Messenger.cs
│   ├── Common/Tools/YF_ConfigHelper.cs
│   ├── Common/Tools/YF_ZipHelper.cs
│   ├── Common/Tools/YF_Sha256Hasher.cs
│   ├── Common/Tools/YF_ManifestSigner.cs
│   └── Common/Interceptors/LogInterceptor.cs
├── YFrame.Pack/
├── YFrame.Sign/
├── YFrame.update/
├── YFrame.Installer/
├── YFrame.Tests/
├── Version.props
└── YFrame.sln
```

插件：

```text
C:\Users\Administrator\Desktop\code\C#\YF_AIHelper
... YF_Clicker, YF_HttpServer, YF_KMScript, YF_Paint,
    YF_Penetration, YF_PluginServer, YF_ScreenOCRTranslate,
    YF_Serialport, YF_TcpHelper
```

---

## 15. 开发约定

1. **所有代码中文注释**；函数说明功能与参数，核心逻辑说明意图。
2. 不写死路径、颜色、URL；路径用配置/`AppContext.BaseDirectory`，颜色用主题资源。
3. 不重复实现日志、配置、文件、ZIP、MD5 等能力，优先复用 `YF_Manager`。
4. AOP 方法必须 `public virtual` + `[Log]`。
5. 插件 UI 文案使用 `key_*` 资源，不硬编码中文/英文。
6. 修改 WPF 主题时同步四套主题，避免某主题缺资源键。
7. 更新相关默认地址只改 `Cos_UpdateDefaults.BaseUrl`，不要在 YFrame/Pack/update 再写一份。
8. 更新器不能在运行时覆盖自身；使用 `YFrame.update.new.exe` pending 机制。
9. 删除备份只在更新成功后执行；失败要保留。
10. 新增交付项目后，更新 `.sln`、README、AGENTS 和对应发布配置。

---

## 16. 当前边界 / 注意

- 仅 Windows x64；
- 插件 csproj 输出路径多为 Debug 绝对路径，Release 需调整/复制；
- AI 模型、PaddleOCR 模型不随仓库；
- OCR 百度翻译凭据目前硬编码，生产前需配置化；
- 更新校验使用 SHA-256 + 清单 ECDSA 签名，未做 Authenticode 验证；
- `YFrame.update` 是框架依赖单文件，需要 .NET 8 Desktop Runtime；
- 安装器是自包含单文件，体积较大；
- WPF 透明窗口、高 DPI、全屏和多显示器场景需要实机验证；
- `.sln` 的 CI 只构建核心 + 测试，不构建 Pack/update/插件；
- APM 目前只有 Trace/Error Span，没有 CPU/内存 Metric，也没有 CLS 式日志检索；
- CloudBase 使用 Publishable Key 时对应 `anon`，只授予 upsert RPC 的 `EXECUTE`，并撤销表级 INSERT/UPDATE/DELETE；
- 关闭 RLS 只关闭行级策略，不能替代表级 GRANT；
- Publishable Key 可被客户端提取，生产环境建议用云函数代理并限制写入范围。

---

## 17. 快速自检

```powershell
dotnet build YFrame.sln -c Debug
dotnet test YFrame.Tests/YFrame.Tests.csproj -c Debug
```

当前应通过：

```text
0 errors
123 tests passed
```

如果 WPF 进程占用输出文件：

```powershell
Get-Process YFrame,YFrame.update -ErrorAction SilentlyContinue | Stop-Process -Force
```