# YFrame

> **YFrame** 是一个基于 **.NET 8 + WPF** 的模块化 Windows 桌面工具平台，由插件框架、系统工具箱、增量发布工具链三部分组成。

YFrame 将常用开发者工具统一到一个可扩展的 Shell 中：主框架负责插件加载、主题、日志、热键、性能监控和工具箱；插件以独立 DLL 交付；`YFrame.Pack` 与 `YFrame.update` 负责版本差异打包和自动更新。

<p>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF-0C54C2">
  <img alt="Plugins" src="https://img.shields.io/badge/plugins-10-4F46E5">
  <img alt="Tests" src="https://img.shields.io/badge/tests-119-059669">
  <img alt="Themes" src="https://img.shields.io/badge/themes-4-7C3AED">
</p>

---

## 目录

1. [项目概览](#1-项目概览)
2. [功能矩阵](#2-功能矩阵)
3. [技术架构](#3-技术架构)
4. [核心设计](#4-核心设计)
5. [插件生态](#5-插件生态)
6. [硬件检查](#6-硬件检查)
7. [发布与自动更新](#7-发布与自动更新)
8. [工程化与版本治理](#8-工程化与版本治理)
9. [快速开始](#9-快速开始)
10. [目录结构](#10-目录结构)
11. [插件开发](#11-插件开发)
12. [配置速查](#12-配置速查)
13. [已知边界](#13-已知边界)

---

## 1. 项目概览

YFrame 的目标不是“再写一个工具箱”，而是提供一套可复用的桌面应用基础设施：

- 新工具可以只写一个独立插件 DLL，不需要改主框架；
- 插件与主框架没有编译时依赖，通过 `YF_Manager` 契约通信；
- 新版本以增量 ZIP 形式发布，客户端自动完成下载、校验、备份、替换与重启；
- 主框架、插件、安装器、Pack、Update 都有明确职责边界。

**关键数字：**

| 项目 | 数量/说明 |
|---|---|
| 目标框架 | `.NET 8.0-windows` |
| 解决方案项目 | 6 个 |
| 插件 | 10 个 |
| 主题 | 4 套 |
| 语言 | 中文 / English |
| 单元测试 | 119 个 |
| 当前版本 | 2.1.0 |

```text
┌──────────────────────────────────────────────────────────────┐
│                        YFrame Shell                          │
│  标题栏 / 菜单 / 插件列表 / 工具箱 / 日志 / 性能监视器        │
│  四套主题 · 中英双语 · 全局热键 · 系统托盘                    │
└──────────────────────────────┬───────────────────────────────┘
                               │ 反射加载（无编译依赖）
        ┌──────────────────────┼──────────────────────┐
   YF_AIHelper           YF_ScreenOCRTranslate    YF_Paint
   YF_Clicker            YF_Serialport            YF_TcpHelper
   YF_HttpServer          YF_PluginServer          YF_KMScript
   YF_Penetration
        │
        └───────────────┬──────────────────────────────┘
                        │
              ┌─────────▼─────────┐
              │     YF_Manager     │
              │ 契约 / AOP / 配置  │
              │ 日志 / Mediator    │
              └─────────┬─────────┘
                        │
        ┌───────────────┼────────────────┐
   YFrame.Pack     YFrame.update    YFrame.Installer
   增量包构建       自动更新器        单文件安装器
```

---

## 2. 功能矩阵

### 2.1 主框架

- **插件中心**：扫描、加载、显示、热重载、卸载当前插件；
- **插件管理器**：连接插件服务器，浏览远程插件并下载安装；
- **工具箱**：取色器、正则测试、MD5 校验、硬件检查；
- **日志系统**：AOP 自动记录、分类 HTML 日志、UI 实时回显；
- **性能监视器**：CPU / 内存采样、自绘 Sparkline；
- **主题与语言**：运行时切换，无需重启；
- **全局热键**：`Ctrl+Y`，按当前插件路由；
- **系统托盘**：显示/隐藏/退出；
- **更新检查**：腾讯云对象存储（COS）、UAT/PDT 环境、三天跳过、更新提示。

### 2.2 工具箱

| 工具 | 能力 |
|---|---|
| 屏幕取色器 | 框选屏幕区域、缩放预览、点击取色、复制色值 |
| 正则测试 | 模式校验、选项、匹配列表、多行锚点归一化 |
| MD5 校验 | 单文件哈希、双文件对比、异步分块与进度 |
| 硬件检查 | CPU/GPU/内存/磁盘/主板/BIOS/OS/音频/显示器 |

### 2.3 交付工具

| 工具 | 作用 |
|---|---|
| `YFrame.Pack` | 比较两个版本 ZIP，生成增量包、完整包副本和版本清单 |
| `YFrame.update` | 下载、校验、备份、替换、删除、重启、日志 |
| `YFrame.Installer` | 将框架本体打包为单文件安装器 |

---

## 3. 技术架构

### 3.1 技术栈

| 层面 | 技术 |
|---|---|
| 运行时 | `.NET 8.0-windows`、C#、WPF |
| 架构 | MVVM、DI、Castle DynamicProxy AOP、Mediator |
| UI | 原生 WPF、XAML `DynamicResource`、自绘 `Sparkline` / `VerticalText` |
| 配置 | `Config/config.conf` 键值配置 + `System.Text.Json` |
| 系统能力 | WMI、Win32 P/Invoke（user32 / winmm / cfgmgr32 / gdi32） |
| 压缩/网络 | `System.IO.Compression`、`HttpClient`、`HttpListener`、`System.Net.Sockets` |
| AI / OCR | LLamaSharp 0.27 + CUDA 12、PaddleOCRSharp 6.1 |
| 串口 | `System.IO.Ports` 8.0 |
| 测试 | xUnit 2.5.3、coverlet |
| CI | GitHub Actions / GitLab CI（Windows Runner） |

项目不使用第三方 UI 框架：Shell、卡片、下拉框、进度条、曲线、竖排文字均为 WPF 原生/自绘实现。

### 3.2 解决方案组成

| 项目 | 类型 | 职责 |
|---|---|---|
| `YFrame` | WPF Application | Shell、主 ViewModel、插件运行时、工具箱、更新检查 |
| `YF_Manager` | Class Library | 插件契约、AOP、配置、Mediator、日志、通用工具 |
| `YFrame.Tests` | xUnit | 核心服务、模型、工具类测试 |
| `YFrame.Installer` | WPF Application | 向导式安装器、payload.zip、单文件发布 |
| `YFrame.Pack` | WPF Application | ZIP 差异、增量包、版本清单 |
| `YFrame.update` | WPF Application | 自动下载、校验、备份、替换、重启 |

10 个插件位于上层目录 `..\YF_*`，独立编译并输出到主框架的 `plugins/` 下，不属于 `YFrame.sln`。

### 3.3 分层

```text
┌─────────────────────────────────────────────────────────────┐
│ Presentation                                                │
│ MainWindow / PluginManagerWindow / AboutWindow / Toolbox    │
│ XAML + MVVM + DynamicResource + 自绘控件                     │
├─────────────────────────────────────────────────────────────┤
│ Application Services                                        │
│ MainWindowViewModel（薄门面）                                │
│ LogService · PluginService · ToolboxService · UpdateService │
├─────────────────────────────────────────────────────────────┤
│ Cross-cutting                                               │
│ DI 容器 · Castle AOP · YF_Messenger · Config · Logging       │
├─────────────────────────────────────────────────────────────┤
│ Shared Contracts & Infrastructure                           │
│ YF_Manager：I_YF_Detail · I_YF_Command · 日志 · 工具类       │
├─────────────────────────────────────────────────────────────┤
│ Plugin Runtime                                              │
│ 反射扫描 plugins/ → Assembly.LoadFrom → 懒加载实例           │
└─────────────────────────────────────────────────────────────┘
```

### 3.4 启动时序

```text
YFrame.exe
  → --uninstall 检查
  → 初始化日志
  → 接替 YFrame.update.new.exe（如果有）
  → 恢复主题
  → 读取 Config.Env（uat/pdt）
  → 请求 YF_Version.json
  → 版本匹配：提示更新 / 跳过 / 继续
  → 构建 DI 容器
  → 解析 MainWindow
  → 加载插件元数据
  → 显示 Shell
```

---

## 4. 核心设计

### 4.1 插件契约

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

插件不引用 `YFrame.exe`，只引用 `YF_Manager.dll`。主框架通过约定类型加载：

```text
plugins/{PluginName}/{PluginName}.dll
  ├── {PluginName}.MainControl        （UserControl）
  └── {PluginName}.MainControlViewModel（契约实现）
```

加载流程：

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

### 4.2 DI / AOP / Mediator

**DI**

- `App.ConfigureServices` 是组合根；
- 非 AOP 服务使用构造函数注入；
- AOP 代理类通过 `InitializeDependencies` 属性注入；
- `YF_Di.Provider` 供插件/底层库解析服务。

**AOP**

- Castle `ProxyGenerator.CreateClassProxy<T>(new LogInterceptor())`；
- 被代理方法必须 `public virtual`；
- 标记 `[Log(Level = ..., Message = "...")]`；
- 拦截器记录方法名、参数、耗时、返回值/异常；
- 业务异常记录后必须原样抛出，AOP 不改变调用方语义；
- 日志同时写入分类 HTML 文件并推送到 UI 日志面板。

**Mediator**

`YF_Messenger` 提供 8 类消息：

- `LogAppendMessage` / `LogClearMessage`
- `PluginShownMessage` / `HotkeyTriggeredMessage`
- `ScriptCommandMessage`
- `PanelSwitchMessage`
- `ThemeChangedMessage` / `LanguageChangedMessage`

组件之间不直接互相引用，新增跨组件通信优先使用 Messenger。

> 当前 DI 仍保留了历史兼容层：`YF_Di.Provider` 是服务定位器，AOP 类使用属性注入，`YF_Manager` 部分工具保留静态 `Lazy<T>` 单例。这是为了兼容插件 API 和 Castle 类代理的折中；核心服务优先构造函数注入。

### 4.3 UI 与主题

- 完全自绘 WPF Shell，不依赖第三方 UI 框架；
- 四套主题通过 `MergedDictionaries` 动态替换；
- 字体统一 `DengXian`；
- 首页四卡手风琴：宽度展开、内容滑入、背景层悬浮、错峰入场；
- `Sparkline` 自绘曲线 + 渐变面积，无图表库；
- `VerticalText` 自绘竖排文字；
- `app.manifest` 声明 PerMonitorV2 DPI 感知；
- 悬浮使用内容下方背景层 + 立即透明度切换，避免遮挡文字和重绘整卡。

### 4.4 性能取舍

- 移除窗口根级 DropShadowEffect，避免整窗重绘；
- 首页动画使用 `FillBehavior.Stop`，结束后清除动画时钟与位移层；
- 背景光晕使用 `CacheMode=BitmapCache`；
- 悬浮高亮只改变独立背景层，不覆盖文字；
- 硬件采集、下载、哈希等耗时工作全部异步化。

---

## 5. 插件生态

| 插件 | 名称 | 技术栈 | 功能 |
|---|---|---|---|
| `YF_AIHelper` | AI 助手 | LLamaSharp 0.27 + CUDA 12 | 本地 GGUF 模型对话、GPU 推理 |
| `YF_Clicker` | 鼠标连点器 | H.InputSimulator 1.5 | 可配置间隔、热键启停、10 秒自动停止 |
| `YF_HttpServer` | HTTP 文件助手 | `HttpListener` | 目录浏览、下载、上传、拖拽、curl 命令 |
| `YF_KMScript` | 脚本编辑器 | OpenCvSharp4 4.13 | 中文 DSL 键鼠自动化、模板找图、区域截图 |
| `YF_Paint` | 截图标注 | GDI BitBlt P/Invoke | 截图选区、文字/画笔/图形标注、钉图、撤销重做 |
| `YF_Penetration` | NAT 内网穿透 | NatTraversal | Host/Player、房间加入码、TCP/UDP 中继 |
| `YF_PluginServer` | 插件服务器 | `HttpListener` | 插件列表 API、ZIP 下载、端口配置持久化 |
| `YF_ScreenOCRTranslate` | OCR 实时翻译 | PaddleOCRSharp 6.1 + 百度翻译 | 截图 OCR、翻译、原位叠加 |
| `YF_Serialport` | 串口通讯助手 | System.IO.Ports 8.0 | 文本/HEX 收发、预设命令、JSON 持久化 |
| `YF_TcpHelper` | TCP 通讯助手 | `System.Net.Sockets` | TCP 服务端/客户端、UDP 广播发现、自动反连 |

### 5.1 插件说明

**YF_AIHelper**

- 本地 GGUF 模型推理，不依赖云端；
- LLamaSharp 0.27 + CUDA 12；
- `ContextSize=4096`、`GpuLayerCount=40`；
- 模型路径来自 `Config.AIModelpath`，模型不随仓库分发。

**YF_Clicker**

- H.InputSimulator 模拟鼠标左键；
- 后台线程连续点击，10 秒自动停止；
- 手动按钮 / 框架 `Ctrl+Y` 启停；
- 运行状态、点击次数通过回调上报。

**YF_HttpServer**

- `HttpListener` 一键启动；
- GET 目录浏览、文件下载、MIME 识别；
- POST 上传（`X-FileName` 头）；
- 拖拽上传、过滤/复制 `curl` 命令。

**YF_KMScript**

- 中文关键字 DSL：定义/找图/点击/等待/循环/如果/截图/输出；
- 缩进语法树解析，OpenCV `CCOEFF_NORMED` 模板匹配；
- 后台执行、可中断、嵌套循环、if/else；
- `.ys` 脚本文件 + 区域截图选择。

**YF_Paint**

- GDI BitBlt 截取虚拟屏幕；
- DIP 框选 → 物理像素裁剪；
- 画笔、直线、矩形、椭圆、文字；
- 文字拖动/双击修改，撤销重做；
- 复制、保存、钉图置顶。

**YF_Penetration**

- 自研 NatTraversal（Client/Server/Shared）；
- Host 创建房间/加入码，Player 加入；
- TCP/UDP 中继、本地端口转发；
- Host/Player 双标签、连接状态与统计。

**YF_PluginServer**

- 扫描 `plugins/` 并提供 HTTP API；
- `GET /api/plugins` 列表；
- `GET /api/plugins/{id}/download` ZIP 下载；
- 端口持久化到 `Config.PluginServerPort`。

**YF_ScreenOCRTranslate**

- PaddleOCR PP-OCRv5 识别；
- 百度翻译 API 英译中；
- 截图选区、Canvas 原位叠加；
- 依赖百度 API 凭据，生产环境需配置化。

**YF_Serialport**

- `System.IO.Ports` 串口参数配置；
- 文本/HEX 双模式收发；
- 预设命令 CRUD + JSON 持久化；
- 接收按行缓冲、连接状态管理。

**YF_TcpHelper**

- TCP 服务端多客户端管理 + 广播；
- TCP 客户端连接/收发；
- UDP 广播自动发现；
- 自动反向连接、500 行接收缓冲。

### 代表性插件

**YF_AIHelper**

- `LLamaWeights.LoadFromFile` → `LLamaContext` → `ChatSession`；
- `ContextSize=4096`、`GpuLayerCount=40`；
- 模型路径来自 `Config.AIModelpath`，模型文件不随仓库分发；
- 流式输出 + 多轮对话 + CUDA 加速。

**YF_KMScript**

中文 DSL 示例：

```text
定义 目标 = "login.png"
找图 目标
如果 目标位置 存在
    点击 目标位置
否则
    输出 "未找到目标"
结束
```

`ScriptInterpreter` 解析缩进语法树，OpenCV `CCOEFF_NORMED` 做模板匹配，后台线程可中断。

**YF_Paint**

```text
Ctrl+Y → 全屏 BitBlt → 框选 → 标注画板 → 复制/保存/钉图
```

坐标体系统一为 DIP，导出时按 DPI 换算为物理像素；支持多显示器虚拟屏幕。

**YF_PluginServer + PluginManager**

- `GET /api/plugins`：插件元数据列表；
- `GET /api/plugins/{id}/download`：插件 ZIP 流式下载；
- 框架内置插件管理器可连接服务器、下载并解压安装。

---

## 6. 硬件检查

硬件检查是框架内置的系统级工具，不使用第三方硬件库，基于 WMI、注册表和 Win32 P/Invoke。

### 6.1 采集源

| 组件 | 来源 |
|---|---|
| CPU | `Win32_Processor` |
| GPU | `Win32_VideoController` |
| 显存 | 注册表 `HardwareInformation.qwMemorySize`（修正 WMI 32 位溢出） |
| 内存 | `Win32_PhysicalMemory` |
| 磁盘 | `Win32_DiskDrive` + `MSFT_PhysicalDisk` |
| 盘符 | `Win32_DiskDriveToDiskPartition` + `Win32_LogicalDiskToPartition` |
| PCIe | 父 PCIe 设备 `DEVPKEY_PciDevice_CurrentLinkSpeed/Width` |
| 显示器 | `WmiMonitorID` + `EnumDisplayMonitors` + `GetMonitorInfo` |
| 音频 | `Win32_PnPEntity` AudioEndpoint |
| 主板/BIOS/OS | `Win32_BaseBoard` / `Win32_BIOS` / `Win32_OperatingSystem` |

### 6.2 磁盘

- 两块及以上磁盘逐块展示型号、容量、接口、固件；
- NVMe 显示 `PCIe 4.0 x4` 等链路信息；
- 每块盘独立“测速”按钮，写入该盘所在卷的 32MB 临时文件，统计顺序读写，完成后删除；
- 无盘符/不可写磁盘显示不可测；
- 硬盘行固定高度 + 内部滚动，避免挤压 GPU/主板卡片。

### 6.3 音频

- 默认输出设备播放 1 秒 440Hz 测试音；
- 默认输入设备录音 3 秒，实时显示电平；
- 倒计时位于进度条左侧，固定 16px 高容器，进度条不上下移动。

### 6.4 显示器

- 多屏逐台展示；
- 每台两行：名称+厂商/序列号，尺寸/分辨率/刷新率；
- 顶部对齐，最大化后不会在顶部出现大块空白；
- 超过两台时卡片内部滚动。

---

## 7. 发布与自动更新

### 7.1 YFrame.Pack

输入两个版本包：

```text
YF-2.0.0.zip
YF-2.0.1.zip
```

按文件路径 + 内容 SHA-256 比较，输出：

```text
output/20261118-153000/
  current.zip           最新完整包副本
  increment.zip         只包含新增/修改文件
  YF_Version.json       版本清单
```

`YF_Version.json`：

```json
{
  "product": "YFrame",
  "current_version": "2.0.1",
  "pervious_version": "2.0.0",
  "updatetime": "2026-11-18T15-30-00",
  "current": { "url": ".../current.zip", "size": 0, "sha256": "..." },
  "pervious": { "url": ".../increment.zip", "size": 0, "sha256": "..." },
  "removed": []
}
```

- `pervious` 是历史拼写，必须保持；
- `removed` 记录新版本删除的文件；
- 输出目录自动附加时间戳；
- 配置 `YFrame.Pack.config.json` 保存输出根目录和 URL，输入 ZIP 路径不保存；
- 启动时自动扫描 exe 目录版本最高的两个 ZIP。

### 7.2 YFrame.update

更新器按本地版本自动选择包：

```text
本地 == current_version
  → 已是最新，不更新

本地 == pervious_version 且增量包可用
  → 下载 increment.zip，执行增量更新

本地低于 current_version 的其他版本
  → 下载 current.zip，执行全量更新

增量包缺失但全量包可用
  → 回退全量更新

本地高于 current_version
  → 不降级
```

执行流程：

```text
读取 --manifest 或 Config/config.conf
→ 选择增量包 / 全量包
→ 下载
→ 校验 size + SHA-256
→ 解压到 staging，校验路径穿越
→ 等待 YFrame 退出
→ 备份
→ 替换
→ 删除 removed（全量更新额外清理旧框架文件）
→ 删除整个 Backup
→ 启动 YFrame
→ 关闭自身
```

全量更新规则：

- `current.zip` 中的框架文件覆盖到安装目录；
- `Config/`、`Log/`、`plugins/`、`Backup/`、`File/` 等用户目录不覆盖、不清理；
- 根目录旧 `.dll` / `.exe` / `.json` / `.config` / `.pdb` 和 `runtimes/`、`Common/` 中不属于新包的文件会清理；
- `YFrame.Pack.exe`、`YFrame.Pack.config.json`、`YFrame.Installer.exe` 保留；
- 新包中的 `YFrame.update.exe` 仍写为 `YFrame.update.new.exe`，由下次启动接替。

稳定性设计：

- 备份失败/替换失败保留备份，成功后清理整个 `Backup`；
- 日志写入 `Log/Update/yyyy-MM-dd.log`；
- 新包包含 `YFrame.update.exe` 时，不覆盖运行中的自身，而是写为 `YFrame.update.new.exe`；
- YFrame 下次启动先接替 pending 更新器，再做版本检查；
- 文件占用重试，失败不阻断主程序启动。

### 7.3 更新环境

`Config/config.conf`：

```ini
Env=uat
UpdateBaseUrl=https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/update
```

地址由程序拼接：

```text
{BaseUrl}/{Env}/YF_Version.json
{BaseUrl}/{Env}/current.zip
{BaseUrl}/{Env}/increment.zip
```

- `Env=uat` 或缺失：UAT；
- `Env=pdt`：生产；
- 非法值回退 `uat`；
- 默认根地址只在 `YF_Manager/Common/UpdateDefaults.cs` 的 `Cos_UpdateDefaults.BaseUrl` 维护一份；
- 项目开源、COS 成本低，配置中明文存储并保留注释说明。

### 7.4 YFrame.Installer

- `CollectPayload.ps1` 收集 YFrame 核心文件与 `runtimes/`；
- 打包为 `Resources/payload.zip` 并嵌入安装器；
- 向导式安装：欢迎 → 路径/快捷方式 → 进度 → 完成；
- 单文件发布：

```powershell
dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release -p:PublishProfile=SingleFile
```

输出：

```text
Installer/YFrame.Installer.exe
```

---

## 8. 工程化与版本治理

### 8.1 统一版本

`Version.props` 是主框架、YF_Manager、测试、安装器的统一版本源：

```xml
<Version>2.1.0</Version>
<BuildDay>当前日期距 2000-01-01 的天数</BuildDay>
<AssemblyVersion>$(Version).$(BuildDay)</AssemblyVersion>
<FileVersion>$(Version).$(BuildDay)</FileVersion>
<InformationalVersion>$(Version)</InformationalVersion>
```

- 标题栏：`v2.1`；
- 关于窗口：完整 `2.1.0.<day>`；
- exe 文件版本：`2.1.0.<day>`；
- 产品版本：`2.1.0`；
- `Version` 必须保持三段数字；
- 插件不导入该文件，保持独立默认版本。

### 8.2 测试

```powershell
dotnet test YFrame.Tests/YFrame.Tests.csproj
```

当前 119 个测试全部通过，覆盖命令、配置、日志、插件调度、文件/TCP/MD5/正则工具等。

### 8.3 CI

- `.github/workflows/ci.yml`
- `.gitlab-ci.yml`

Windows Runner 上还原、编译 YF_Manager + YFrame + YFrame.Tests 并运行测试。CI 目前不构建 Pack/update/插件。

---

## 9. 快速开始

### 9.1 环境

- Windows 10/11 x64；
- .NET 8 SDK（可用 .NET 9 SDK 构建 `net8.0-windows`）；
- Visual Studio 2022（可选）。

### 9.2 构建与运行

```powershell
git clone https://gitcode.com/Colin_Davis/YFrame.git
cd YFrame

dotnet build YFrame.sln -c Debug
dotnet test YFrame.Tests/YFrame.Tests.csproj -c Debug

dotnet run --project YFrame/YFrame.csproj
```

或直接运行：

```text
YFrame/bin/Debug/net8.0-windows/YFrame.exe
```

### 9.3 构建插件

插件不在解决方案内，需要单独构建，默认输出到：

```text
YFrame/bin/Debug/net8.0-windows/plugins/{PluginName}/
```

例如：

```powershell
dotnet build ..\YF_Clicker\YF_Clicker\YF_Clicker.csproj -c Debug
```

### 9.4 发布工具

```powershell
# 安装器：自包含单文件
dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release -p:PublishProfile=SingleFile

# 更新器：框架依赖单文件，需 .NET 8 Desktop Runtime
dotnet publish YFrame.update/YFrame.update.csproj -c Release -p:PublishProfile=SingleFile

# 增量包工具
dotnet build YFrame.Pack/YFrame.Pack.csproj -c Release
```

---

## 10. 目录结构

```text
YFrame/
├── YFrame/                       # WPF Shell
│   ├── MainWindow.xaml/.cs
│   ├── ViewModel/
│   ├── Service/
│   ├── View/UC/Toolbox/
│   ├── Controls/                 # Sparkline / VerticalText
│   └── Common/                   # Themes / Language / Images
├── YF_Manager/                   # 契约、AOP、配置、Mediator、工具
├── YFrame.Tests/                 # xUnit
├── YFrame.Installer/             # 向导式安装器
├── YFrame.Pack/                  # 增量包构建工具
├── YFrame.update/                # 自动更新器
├── Version.props                 # 统一版本
├── YFrame.sln
├── README.md
└── AGENTS.md
```

插件目录：

```text
../
├── YF_AIHelper/
├── YF_Clicker/
├── YF_HttpServer/
├── YF_KMScript/
├── YF_Paint/
├── YF_Penetration/
├── YF_PluginServer/
├── YF_ScreenOCRTranslate/
├── YF_Serialport/
└── YF_TcpHelper/
```

---

## 11. 插件开发

1. 新建类库，引用 `YF_Manager.dll`；
2. 实现 `I_YF_Detail` + `I_YF_Command`；
3. 创建 `MainControl.xaml` 和 `MainControlViewModel`；
4. 输出到 `plugins/{PluginName}/`；
5. 启动框架后自动发现。

最小 ViewModel：

```csharp
public class MainControlViewModel : ViewModelBase, I_YF_Detail, I_YF_Command
{
    public string YF_ID => "YF_Example";
    public string YF_Name => "示例插件";

    public event EventHandler<PluginEventArgs>? OnPluginCallback;

    public void ExecuteCommand(string command, object? parameter = null)
    {
        // 处理框架热键 / 脚本命令
    }
}
```

约定：

- 命名空间 = DLL 文件名；
- 入口控件 = `{命名空间}.MainControl`；
- 需要 AOP 的方法必须 `virtual` 且标记 `[Log]`；
- UI 文案放到 `Common/Language/zh-CN.xaml` / `en-US.xaml`；
- 不在插件中重复实现日志、配置、文件、ZIP 等通用能力。

---

## 12. 配置速查

配置文件：

```text
YFrame/bin/Debug/net8.0-windows/Config/config.conf
```

| 键 | 说明 |
|---|---|
| `Theme` | 当前主题路径 |
| `ScreenScale` | 截图/取色坐标缩放比例，默认 125 |
| `LogPath` | 日志目录 |
| `PluginPath` | 插件目录 |
| `PluginServerPort` | 插件服务器端口，默认 9000 |
| `PluginManagerServerURL` | 插件管理器服务器地址 |
| `Paddlepath` | OCR 模型目录 |
| `AIModelpath` | 本地 AI 模型路径 |
| `Env` | 更新环境：`uat` / `pdt` |
| `UpdateBaseUrl` | COS 更新根地址 |

---

## 13. 已知边界

- 仅支持 Windows / x64；
- 插件项目默认输出路径写死为 Debug 输出目录，Release 部署需单独复制；
- AI 模型、PaddleOCR 模型体积较大，不随仓库分发；
- OCR 翻译依赖百度翻译 API，当前凭据硬编码在源码中，公开部署前应改为配置或服务端代理；
- 自动更新目前使用 size + SHA-256 校验，生产环境建议再增加签名清单与 Authenticode 校验；
- 更新器为框架依赖单文件，目标机器需要 .NET 8 Desktop Runtime；安装器为自包含单文件。

---

## 界面预览

仓库历史设计稿截图：
![image.png](https://raw.gitcode.com/user-images/assets/7353928/f73e9633-9a73-499d-9afc-1481da4414bd/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/e57fdc0c-3cc2-4dab-9d0a-cbe19999df79/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/7a41a185-693e-4c09-a42e-3ff5012f3bc8/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/a2b14f27-4cfd-4c54-a2e4-ffa2e98f71d6/image.png 'image.png')

---

## 仓库

- GitCode：<https://gitcode.com/Colin_Davis/YFrame>
- 解决方案：`YFrame.sln`
- 文档：[README.md](README.md) · [AGENTS.md](AGENTS.md)