# YFrame

> **一个把「插件框架 + 桌面工具 + 增量发布」串成完整闭环的 .NET 8 / WPF 桌面平台。**
>
> 不是单个工具，而是一套可扩展的桌面应用基础设施：主框架负责插件生命周期、主题、日志、热键和工具箱；插件以独立 DLL 交付；Pack 与 Update 负责把版本差异安全地送到用户机器上。

<p>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF-0C54C2">
  <img alt="Plugins" src="https://img.shields.io/badge/plugins-10-4F46E5">
  <img alt="Tests" src="https://img.shields.io/badge/tests-115-059669">
  <img alt="Themes" src="https://img.shields.io/badge/themes-4-7C3AED">
</p>

---

## 面试官速览：这个项目值得看什么

| 看点 | 具体实现 | 体现的能力 |
|---|---|---|
| **零编译依赖插件体系** | 插件只依赖 `YF_Manager` 契约，主框架通过反射扫描 `plugins/`、懒加载 `MainControl` 与 ViewModel | 插件化架构、反射、接口隔离、生命周期设计 |
| **DI + AOP + Mediator 组合** | `Microsoft.Extensions.DependencyInjection` 管理服务；Castle DynamicProxy 拦截 `[Log]` 方法；`YF_Messenger` 解耦跨组件通信 | 依赖倒置、面向切面、消息驱动、可测试性 |
| **完整的桌面 Shell** | 抽屉式三栏布局、四套主题、双语资源、手风琴首页、自绘 Sparkline、垂直文字控件、高 DPI 适配 | WPF 深度定制、MVVM、动态资源、性能优化 |
| **系统级硬件诊断** | WMI 读取 CPU/GPU/内存/磁盘/主板/BIOS/OS；注册表修正显存；PCIe 链路属性；WinMM 音频测试；按卷磁盘测速；多显示器枚举 | Win32 / WMI 互操作、设备树理解、异步采集 |
| **增量发布闭环** | Pack 比较两个 ZIP，生成 `current.zip`、`increment.zip`、`YF_Version.json`；Update 下载、校验、备份、替换、删除、重启；支持更新器自身延迟接替 | 发布工程、文件校验、进程/文件占用处理、回滚意识 |
| **工程化** | 6 个解决方案项目、10 个插件、115 个 xUnit 测试、GitHub Actions / GitLab CI、统一版本文件 | 测试、CI、版本治理、文档与可维护性 |

---

## 30 秒了解项目

YFrame 是一个 Windows 桌面开发者工具箱平台：

```text
┌──────────────────────────────────────────────────────────────┐
│                        YFrame Shell                          │
│  标题栏 / 菜单 / 插件列表 / 工具箱 / 日志 / 性能监视器        │
│  四套主题 · 中英双语 · 全局热键 · 系统托盘                    │
└──────────────────────────────┬───────────────────────────────┘
                               │ 反射加载（无编译依赖）
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
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
        │               │                │
   YFrame.Pack     YFrame.update    YFrame.Installer
   增量包构建       自动更新器        单文件安装器
```

**核心数字：**

- 目标框架：`.NET 8.0-windows`
- 解决方案项目：**6 个**
- 插件：**10 个**
- 主题：**4 套**（暗夜科技、云白简约、复古终端、玻璃拟态）
- 语言：**中文 / English**
- 单元测试：**115 个**
- 当前产品版本：**2.1.0**

---

## 功能矩阵

### 主框架

- **插件中心**：扫描、加载、显示、热重载、卸载当前插件；
- **插件管理器**：连接插件服务器，浏览远程插件并下载安装；
- **工具箱**：取色器、正则测试、MD5 校验、硬件检查；
- **日志系统**：AOP 自动记录 + 分类 HTML 日志 + UI 实时回显；
- **性能监视器**：CPU / 内存采样 + 自绘 Sparkline；
- **主题与语言**：运行时切换，不重启；
- **全局热键**：`Ctrl+Y`，按当前插件路由；
- **系统托盘**：显示/退出/快捷操作；
- **启动更新检查**：UAT / PDT 环境、三天跳过、更新提示。

### 工具箱

| 工具 | 能力 |
|---|---|
| **屏幕取色器** | 框选屏幕区域、缩放预览、点击取色、复制色值 |
| **正则测试** | 模式校验、选项、匹配列表、多行锚点归一化 |
| **MD5 校验** | 单文件哈希、双文件对比、异步分块与进度 |
| **硬件检查** | CPU/GPU/内存/磁盘/主板/BIOS/OS/音频/显示器，一屏卡片式展示 |

### 硬件检查（重点功能）

- CPU：型号、核心/线程、主频、插槽；
- GPU：独显/核显、显存（注册表 64 位修正）、驱动版本；
- 内存：总容量、频率、通道/插槽；
- 磁盘：型号、容量、接口、固件、PCIe 链路、按盘符独立测速；
- 主板 / BIOS / 操作系统；
- 音频：默认输出/输入设备、播放测试、3 秒输入电平与倒计时；
- 显示器：多屏逐台展示，型号、尺寸、分辨率、刷新率、厂商/序列号。

### 交付工具链

| 工具 | 作用 |
|---|---|
| **YFrame.Pack** | 读取两个 `YF-*.zip`，生成增量包、完整包副本、版本清单 |
| **YFrame.update** | 自动下载、MD5/大小校验、备份、替换、删除、重启、日志 |
| **YFrame.Installer** | 将框架本体打包为单文件安装器，嵌入 payload.zip |

---

## 技术架构

### 分层

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

### 解决方案组成

| 项目 | 类型 | 职责 |
|---|---|---|
| `YFrame` | WPF Application | Shell、主 ViewModel、插件运行时、工具箱、更新检查 |
| `YF_Manager` | Class Library | 插件契约、AOP 日志、DI 辅助、配置、Mediator、通用工具 |
| `YFrame.Tests` | xUnit | 核心服务、模型、工具类测试 |
| `YFrame.Installer` | WPF Application | 向导式安装器 + 单文件发布 |
| `YFrame.Pack` | WPF Application | 增量包与版本清单构建工具 |
| `YFrame.update` | WPF Application | 自动更新执行器 |

### 技术栈

| 层面 | 技术选择 |
|---|---|
| 运行时 | `.NET 8.0-windows`、C#、WPF |
| 架构 | MVVM、DI、Castle DynamicProxy AOP、Mediator |
| UI | 原生 WPF、XAML `DynamicResource`、自绘 `Sparkline` / `VerticalText` |
| 配置 | `Config/config.conf` 键值配置 + `System.Text.Json` 局部 JSON |
| 系统能力 | WMI、Win32 P/Invoke（user32 / winmm / cfgmgr32 / gdi32） |
| 压缩与传输 | `System.IO.Compression`、`HttpListener`、`HttpClient`、`System.Net.Sockets` |
| AI / OCR | LLamaSharp 0.27 + CUDA 12、PaddleOCRSharp 6.1 |
| 串口 | `System.IO.Ports` 8.0 |
| 测试 | xUnit 2.5.3、coverlet |
| CI | GitHub Actions / GitLab CI（Windows Runner） |

**刻意不使用第三方 UI 框架**：Shell、卡片、下拉框、进度条、曲线、竖排文字均为 WPF 原生/自绘实现，便于统一主题和展示 WPF 深度定制能力。

插件位于上层目录 `..\YF_*`，独立编译并输出到主框架的 `plugins/` 下。

### 启动时序

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

### 插件契约

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

### AOP 日志

- 被代理方法必须是 `public virtual`；
- 标记 `[Log(Level = ..., Message = "...")]`；
- Castle `LogInterceptor` 记录方法名、参数、耗时、返回值/异常；
- 统一写入 HTML 日志分类目录，并同步到 UI 日志面板。

### Mediator 解耦

`YF_Messenger` 提供 8 类消息：

- `LogAppendMessage` / `LogClearMessage`
- `PluginShownMessage` / `HotkeyTriggeredMessage`
- `ScriptCommandMessage`
- `PanelSwitchMessage`
- `ThemeChangedMessage` / `LanguageChangedMessage`

组件之间不直接互相引用，便于单测和替换。

### UI 与主题

- 完全自绘 WPF Shell，不依赖第三方 UI 框架；
- 四套主题通过 `MergedDictionaries` 动态替换；
- 字体统一 `DengXian`；
- 首页四卡手风琴：宽度展开、内容滑入、背景层悬浮、错峰入场；
- `Sparkline` 自绘曲线 + 渐变面积，无图表库；
- `VerticalText` 自绘竖排文字；
- `app.manifest` 声明 PerMonitorV2 DPI 感知；
- 悬浮使用内容下方背景层 + 立即透明度切换，避免遮挡文字和重绘整卡。

### 性能取舍

- 移除窗口根级 DropShadowEffect，避免整窗重绘；
- 首页动画使用 `FillBehavior.Stop`，结束后清除动画时钟与位移层；
- 背景光晕使用 `CacheMode=BitmapCache`；
- 悬浮高亮只改变独立背景层，不覆盖文字；
- 硬件采集、下载、哈希等耗时工作全部异步化。

---

## 插件生态

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

### 代表性插件

**YF_AIHelper**

- 本地模型加载：`LLamaWeights.LoadFromFile` → `LLamaContext` → `ChatSession`；
- `ContextSize=4096`、`GpuLayerCount=40`；
- 模型路径来自 `Config.AIModelpath`，模型文件不随仓库分发；
- 流式输出 + 多轮对话 + GPU 加速。

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

底层由 `ScriptInterpreter` 解析缩进语法树，OpenCV `CCOEFF_NORMED` 做模板匹配，后台线程可中断执行。

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

## 增量发布与自动更新

这是项目从“桌面工具”走向“可交付产品”的关键闭环。

### 1. YFrame.Pack

输入两个版本包：

```text
YF-2.0.0.zip
YF-2.0.1.zip
```

按文件内容 MD5 比较，输出：

```text
output/20261118-153000/
  current.zip           最新完整包副本
  increment.zip         只含新增/修改文件
  YF_Version.json       版本清单
```

`YF_Version.json` 中：

- `current_version` / `pervious_version`；
- `current`: 完整包 URL、size、md5；
- `pervious`: 增量包 URL、size、md5；
- `removed`: 新版本删除的文件路径。

Pack 支持 URL 编辑、本地配置、时间戳输出目录、差异统计和失败提示。

### 2. YFrame.update

启动后自动：

```text
读取清单 → 版本判断 → 下载 → 校验 size/MD5 → 解压校验路径
→ 等待 YFrame 退出 → 备份 → 替换 → 删除 removed
→ 清理 Backup → 启动 YFrame → 退出
```

特点：

- 增量包路径穿越校验；
- 备份后可恢复；
- 更新成功后自动清理 `Backup`；
- 失败保留备份与日志；
- `Log/Update/yyyy-MM-dd.log` 记录完整流程；
- 新包包含 `YFrame.update.exe` 时，不覆盖运行中的自身，而是写为 `YFrame.update.new.exe`，由 YFrame 下次启动接替。

### 3. 更新环境

`Config/config.conf`：

```ini
Env=uat
UpdateBaseUrl=https://yfcos-1256976024.cos.ap-beijing.myqcloud.com/YFrame/update
```

派生：

```text
{BaseUrl}/{Env}/YF_Version.json
{BaseUrl}/{Env}/current.zip
{BaseUrl}/{Env}/increment.zip
```

- `Env=uat`：UAT 环境；
- `Env=pdt`：生产环境；
- 缺省或非法值回退 `uat`；
- 默认根地址只在 `YF_Manager/Common/UpdateDefaults.cs` 中维护一份。

### 4. 安装器

`YFrame.Installer` 将主框架核心文件收集为 `payload.zip` 并嵌入安装器，使用 Release 单文件发布：

```powershell
dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release
```

输出：

```text
Installer/YFrame.Installer.exe
```

---

## 工程化

### 版本管理

统一入口：

```text
Version.props
```

- `Version=2.1.0`；
- `AssemblyVersion` / `FileVersion` 第四位自动取“当前日期距 2000-01-01 的天数”；
- 标题栏显示 `v2.1`；
- 关于窗口显示完整 `2.1.0.<day>`；
- 安装器、测试共用版本；
- 插件不导入该文件，保持独立默认版本。

### 测试

```powershell
dotnet test YFrame.Tests/YFrame.Tests.csproj
```

覆盖：

- `RelayCommand` / 泛型命令；
- 配置常量；
- 日志缓冲区；
- 插件调度与命令路由；
- 文件 / TCP / MD5 / 正则工具；
- 插件模型与实例模型。

当前：**115 个测试全部通过**。

### CI

- `.github/workflows/ci.yml`
- `.gitlab-ci.yml`

在 Windows Runner 上还原、编译核心项目并运行测试；WPF 项目不能使用 Linux Runner。

---

## 快速开始

### 环境

- Windows 10/11 x64；
- .NET 8 SDK（可用 .NET 9 SDK 构建 `net8.0-windows`）；
- Visual Studio 2022（可选）。

### 构建与运行

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

### 构建插件

插件不在解决方案内，需要单独构建。插件项目默认输出到：

```text
YFrame/bin/Debug/net8.0-windows/plugins/{插件名}/
```

例如：

```powershell
dotnet build ..\YF_Clicker\YF_Clicker\YF_Clicker.csproj -c Debug
```

插件 DLL、依赖和 `plugin.json` 会被复制到主框架的插件目录。

### 发布工具

```powershell
# 安装器（自包含单文件，约 150MB）
dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release -p:PublishProfile=SingleFile

# 更新器（框架依赖单文件，约 0.6MB）
dotnet publish YFrame.update/YFrame.update.csproj -c Release -p:PublishProfile=SingleFile

# 增量包工具
dotnet build YFrame.Pack/YFrame.Pack.csproj -c Release
```

---

## 目录结构

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

## 开发一个插件

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

## 配置速查

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

## 已知边界

- 仅支持 Windows / x64；
- 插件项目默认输出路径写死为 Debug 输出目录，Release 部署需单独复制；
- AI 模型、PaddleOCR 模型体积较大，不随仓库分发；
- OCR 翻译依赖百度翻译 API，当前凭据硬编码在源码中，公开部署前应改为配置或服务端代理；
- 自动更新目前使用 size + MD5 校验，生产环境建议再增加 Authenticode 签名校验；
- 更新器为框架依赖单文件，目标机器需要 .NET 8 Desktop Runtime；安装器为自包含单文件。

---

## 界面预览

仓库历史设计稿截图：

![Home](https://raw.gitcode.com/user-images/assets/7353928/a7fe3ea5-5883-4906-a3ce-4bf435607487/image.png)
![Plugins](https://raw.gitcode.com/user-images/assets/7353928/7a3d49cd-7d16-47dd-96a7-b6e05b69dca7/image.png)
![Tools](https://raw.gitcode.com/user-images/assets/7353928/1e6301c3-30cb-4fcc-85cd-61ad3e98a220/image.png)

---

## 仓库

- GitCode：<https://gitcode.com/Colin_Davis/YFrame>
- 解决方案：`YFrame.sln`
- 文档：[README.md](README.md) · [AGENTS.md](AGENTS.md)
