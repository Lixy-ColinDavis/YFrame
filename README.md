# YFrame

> **YFrame** 是一个基于 **.NET 8 + WPF** 的模块化 Windows 桌面工具平台，由插件框架、系统工具箱、工作任务画布、增量发布工具链和 COS 上传工具组成。

YFrame 将常用开发者工具统一到一个可扩展的 Shell 中：主框架负责插件加载、主题、日志、热键、性能监控、工作任务和工具箱；插件以独立 DLL 交付；`YFrame.Pack` 与 `YFrame.update` 负责版本差异打包和自动更新；`YFrame.Sign` 与 `YFrame.upload.cos` 负责清单签名与腾讯云对象存储（COS）上传。

<p>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8.0-512BD4">
  <img alt="WPF" src="https://img.shields.io/badge/UI-WPF-0C54C2">
  <img alt="Plugins" src="https://img.shields.io/badge/plugins-10-4F46E5">
  <img alt="Tests" src="https://img.shields.io/badge/tests-129-059669">
  <img alt="Themes" src="https://img.shields.io/badge/themes-4-7C3AED">
</p>

---

## 目录

1. [项目概览](#1-项目概览)
2. [功能矩阵](#2-功能矩阵)
3. [技术架构](#3-技术架构)
4. [核心设计](#4-核心设计)
5. [插件生态](#5-插件生态)
6. [硬件检查、腾讯云应用性能监控(APM)、云数据库CloudBase云端同步](#6-硬件检查与云端同步)
7. [发布、腾讯云对象存储(COS) 上传与自动更新](#7-上传与自动更新)
8. [工程化与版本治理](#8-工程化与版本治理)
9. [快速开始](#9-快速开始)
10. [目录结构](#10-目录结构)
11. [插件开发](#11-插件开发)
12. [配置速查](#12-配置速查)
13. [已知边界](#13-已知边界)

---

## 1. 项目概览

YFrame 工具集，个人项目展示

- 新工具可以只写一个独立插件 DLL，不需要改主框架；
- 插件与主框架没有编译时依赖，通过 `YF_Manager` 契约通信；
- 新版本以增量 ZIP 形式发布，客户端自动完成下载、校验、备份、替换与重启；
- 主框架、插件、安装器、Pack、Update 都有明确职责边界。

**关键数字：**

| 项目 | 数量/说明 |
|---|---|
| 目标框架 | `.NET 8.0-windows` |
| 解决方案项目 | 8 个 |
| 插件 | 10 个 |
| 主题 | 4 套 |
| 语言 | 中文 / English |
| 单元测试 | 129 个 |
| 当前版本 | 2.1.6 |

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
   YFrame.Pack     YFrame.Sign      YFrame.update
   增量包构建       清单签名          自动更新器
        │
   YFrame.upload.cos                 YFrame.Installer
   COS 上传 / 插件打包                单文件安装器
```

---

## 2. 功能矩阵

### 2.1 主框架

- **插件中心**：扫描、加载、显示、热重载、卸载当前插件；
- **插件管理器**：连接插件服务器，浏览远程插件并下载安装；
- **工具箱**：取色器、正则测试、MD5 校验、硬件检查；
- **工作任务**：多流程看板、步骤节点、连线、自动布局、完成进度、自动保存；
- **日志系统**：AOP 自动记录、分类 HTML 日志、UI 实时回显；
- **APM 可观测性**：OpenTelemetry Trace、硬件扫描标签、Error.<类型> Span、OTLP 上报；
- **CloudBase 同步**：硬件扫描结果通过 PostgreSQL RPC 按 `device_name` upsert；
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
| `YFrame.Sign` | 选择清单与 ECDSA 私钥，生成 YF_Version.json.sig |
| `YFrame.upload.cos` | 上传更新包文件、压缩并上传插件 ZIP 与 plugins.json |
| `YFrame.update` | 下载、校验、备份、替换、删除、重启、日志 |
| `YFrame.Installer` | 将框架本体打包为单文件安装器 |

### 2.4 工作任务

- **多流程**：新建、重命名、复制、删除、上移/下移/置顶/置底；
- **步骤节点**：添加、编辑、删除、拖拽、完成状态、序号、备注；
- **连线**：悬停显示端口，拖拽端口连线，双击连线删除；
- **自动布局**：按步骤序号四列蛇形排列，连线不参与排序；
- **画布操作**：滚轮缩放 0.2～1.8、拖拽平移、复位适配；
- **持久化**：450ms 防抖自动保存到 `Config/Workflows/workflows.json`。

---

## 3. 技术架构

### 3.1 技术栈

| 层面 | 技术 |
|---|---|
| 运行时 | `.NET 8.0-windows`、C#、WPF |
| 架构 | MVVM、DI、Castle DynamicProxy AOP、Mediator |
| UI | 原生 WPF、XAML `DynamicResource`、自绘 `Sparkline` / `VerticalText` |
| 配置 | `Config/config.conf` 键值配置 + `System.Text.Json` |
| 密钥 | Windows DPAPI `CurrentUser` + 安装/更新引导文件 |
| 可观测性 | OpenTelemetry Trace/Logs + OTLP → 腾讯云 APM |
| 云端同步 | CloudBase PostgreSQL PostgREST RPC |
| COS | `Tencent.QCloud.Cos.Sdk` 简单上传 |
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
| `YFrame.Sign` | WPF Application | 清单签名、临时密钥、路径配置 |
| `YFrame.update` | WPF Application | 自动下载、校验、备份、替换、重启 |
| `YFrame.upload.cos` | WPF Application | 腾讯云对象存储（COS）、更新文件上传、插件打包与上传 |

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
│ WorkflowBoard · TelemetryService · CloudBaseHardwareService │
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

1. 处理卸载参数与单实例互斥锁；
2. 初始化日志；
3. 读取根目录 `YFrame.secrets.bootstrap`，首次启动迁移到 DPAPI；
4. 初始化 APM OpenTelemetry；
5. 接替 `YFrame.update.new.exe`（如果存在）；
6. 恢复主题；
7. 读取 `Config.Env`，请求 `YF_Version.json`；
8. 按版本规则提示更新、跳过或继续；
9. 构建 DI 容器；
10. 解析主窗口、加载插件元数据、显示 Shell。

---

## 4. 核心设计

### 4.1 插件契约

插件契约由两个最小接口组成，插件只引用 `YF_Manager.dll`，不引用 `YFrame.exe`。

| 接口 | 关键成员 | 作用 |
|---|---|---|
| `I_YF_Detail` | `YF_ID`、`YF_Name` | 提供插件标识与显示名称，供扫描和列表展示 |
| `I_YF_Command` | `ExecuteCommand`、`OnPluginCallback` | 接收框架命令，向框架回传插件事件 |

契约刻意保持很小：

- 不暴露主窗口、DI 容器或具体服务类型；
- 插件与框架只通过字符串命令和事件对象通信；
- 插件可以独立升级，不需要与主框架一起编译；
- 命令参数使用 `object?`，由插件自行解释，避免契约频繁变更。

目录约定为 `plugins/{PluginName}/`，其中包含 `{PluginName}.dll`、依赖 DLL 和可选的 `plugin.json`。

入口控件和 ViewModel 通过命名约定定位：`{Namespace}.MainControl` 与 `{Namespace}.MainControlViewModel`。

加载流程：

1. 扫描 `plugins/*/YF_*.dll`，过滤 `YF_Manager.dll`；
2. 使用 `Assembly.LoadFrom` 加载程序集；
3. 读取 `I_YF_Detail` 元数据，但暂不实例化 ViewModel；
4. 用户点击插件后，再创建 ViewModel 和 `MainControl`；
5. 绑定 `DataContext`，加入插件显示区。

这种“元数据预扫描 + 实例懒加载”的方式降低了启动时的程序集加载和 UI 构造开销，也让单个插件损坏不会阻断整个 Shell。

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

### 4.5 工作任务（工作流）

入口为左侧第三栏“工作任务”，选中后主工作区显示 `WorkflowBoard`。

数据模型分为三层：

| 层级 | 内容 | 说明 |
|---|---|---|
| 文档 | `WorkflowDocument` | 保存全部流程与当前选中流程 ID |
| 流程 | `WorkflowDefinition` | 名称、节点集合、连线集合、下一个节点编号 |
| 图元 | `WorkflowNode` / `WorkflowEdge` | 节点保存文本、备注、完成状态、序号与坐标；连线保存起止节点和四向端口 |

核心交互：

- 流程名称、步骤内容、序号、备注可直接编辑；
- 双击画布空白处或点击“添加步骤”新增节点；
- 拖拽节点移动位置，拖拽节点四向端口创建连线；
- 双击连线删除，悬停节点后按 `Delete` 删除，`Esc` 关闭编辑浮层；
- 单击节点切换完成状态；使用 280ms 定时器区分单击与拖拽；
- 顶部显示完成数量与进度条；
- 自动布局只按 `Order` 排序，四列蛇形排列，连线仅调整端口朝向；
- 缩放范围 0.2～1.8，支持拖拽平移与“复位”适配。

持久化到 `Config/Workflows/workflows.json`，修改后 450ms 防抖保存，使用临时文件原子替换；首次运行生成“发布流程”“紧急修复”示例。

工作流的自动化布局不读取连线拓扑，而是以显式 `Order` 作为唯一排序输入。这样删除、反向连线或临时调整连接关系都不会改变步骤顺序，避免“图形拓扑变化导致流程顺序跳动”的常见问题。

### 4.6 可靠性与安全取舍

YFrame 的可靠性设计：

- 插件加载隔离：单个插件程序集损坏或类型缺失，只影响该插件，不阻断 Shell；
- AOP 只做旁路记录：日志失败降级为 `Debug` 输出，不改变业务异常和返回值；
- 更新链先验证后替换：ECDSA 清单签名、包 SHA-256、staging 解压、路径穿越检查、备份与失败保留；
- 更新器自我替换：运行中的 `YFrame.update.exe` 不直接覆盖，写为 pending 文件，下次启动接替；
- 上传器幂等顺序：先上传 ZIP，再上传签名，最后上传 `plugins.json`，避免客户端读到半成品；
- 工作流自动保存：450ms 防抖 + 临时文件原子替换，降低频繁写盘和半写文件风险。

安全边界：

- APM Endpoint 是公开配置，Token 是秘密；
- CloudBase 客户端只持有 Publishable Key，数据库写权限收敛到 RPC；
- DPAPI 只保护落盘静态数据，不防同用户运行中的进程读取；
- `YFrame.secrets.bootstrap` 是安装/更新下发通道，不是强保密容器；
- 公开分发场景仍建议使用云函数代理或服务端签发短期凭证。

### 4.7 插件供应链安全

插件是会被 `Assembly.LoadFrom` 执行的代码，因此不能只依赖 HTTPS。

信任链：

```text
plugins.json 原始字节
→ plugins.json.sig 验签
→ 读取每个 ZIP 的 size + sha256
→ 下载并校验 ZIP
→ staging 解压
→ 原子替换旧插件
→ 写安装记录
→ 加载前校验入口 DLL
```

清单要求：

| 字段 | 作用 |
|---|---|
| `schemaVersion` | 当前为 2，客户端拒绝旧格式 |
| `keyId` | 选择内置插件公钥 |
| `plugins[].id` | 必须等于插件目录名 |
| `plugins[].version` | 安装记录与兼容性约束 |
| `plugins[].size` | ZIP 字节数 |
| `plugins[].sha256` | ZIP 内容哈希 |
| `plugins[].url` | 仅允许 HTTPS |

签名与密钥：

```text
算法：ECDSA P-256 + SHA-256
域分离：YFRAME-PLUGIN-MANIFEST-V1
私钥：SigningKeys\plugins\plugin-manifest-2026-01.private.pem
公钥：YFrame\Service\Security\PluginTrustKeys.cs
```

插件签名密钥与更新签名密钥分离。私钥只在签名端使用，不进入客户端、源码或 `upload.settings.json`。

安装约束：

- 先验签清单，再反序列化；
- 下载后校验大小和 SHA-256；
- 只解压到 `.staging`，校验绝对路径、`..`、盘符和符号链接；
- 成功后先备份旧目录，再原子替换；失败恢复旧插件；
- 写 `.yframe-plugin.json` 记录入口 DLL 哈希；
- 加载前校验哈希，不匹配拒绝加载；
- 没有安装记录的旧插件按 legacy 处理并提示重新安装；
- TCP 插件源没有签名清单，当前直接拒绝安装，避免留下无校验旁路。

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
- 后台线程连续点击，预期时间/次数 自动停止；
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

中文 DSL 围绕“定义、找图、点击、等待、循环、条件、截图、输出”组织，缩进表达嵌套层级；解释器先构建语法树，再在后台线程执行，使用 OpenCV `CCOEFF_NORMED` 做模板匹配，并支持中断。

**YF_Paint**

交互链路为：全局热键触发全屏 BitBlt → 框选区域 → 标注画板 → 复制、保存或钉图。坐标体系统一为 DIP，导出时按 DPI 换算为物理像素，并支持多显示器虚拟屏幕。

**YF_PluginServer + PluginManager**

- `GET /api/plugins`：插件元数据列表；
- `GET /api/plugins/{id}/download`：插件 ZIP 流式下载；
- 框架内置插件管理器可连接服务器、下载并解压安装。

---

## 6. 硬件检查与云端同步

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

### 6.5 应用性能监控(APM) 可观测性


![b98d2056af47a91ec78b83b4e92902ee.png](https://raw.gitcode.com/user-images/assets/7353928/ff32d604-b2a0-4bcc-bc3e-8ffe5dc418b6/b98d2056af47a91ec78b83b4e92902ee.png 'b98d2056af47a91ec78b83b4e92902ee.png')

框架通过 OpenTelemetry OTLP 接入腾讯云 APM，默认开启。

设计上把配置和密钥分开：

- APM Endpoint 属于非敏感常量，维护在 `YFrame/Service/Telemetry/ApmBuiltInSettings.cs`；
- `ApmEnabled`、`ApmServiceName`、`ApmEnvironment` 属于普通配置，放在 `config.conf`；
- APM Token 不进入源码和配置，保存到 `%LocalAppData%\YFrame\secrets.dat`，由 DPAPI `CurrentUser` 保护；
- 安装包或更新包可在程序根目录放置 `YFrame.secrets.bootstrap`，新版首次启动自动迁移到 DPAPI。

当前 Trace 语义：

| Span | 触发时机 | 技术含义 |
|---|---|---|
| `app.startup` | 应用启动 | 启动主流程耗时 |
| `hardware.scan` | 打开/刷新硬件检查 | WMI 采集耗时与硬件快照摘要 |
| `cloudbase.upload` | 硬件扫描后上传 CloudBase | RPC 调用结果、HTTP 状态与重试上下文 |
| `Error.<类型>` | 任意 Error 日志 | 错误分类 Span |

`hardware.scan` 采用结构化标签而不是整段 JSON，便于 APM 侧做低基数筛选：

- CPU 名称、核心数、线程数；
- 内存容量；
- GPU 数量、全部 GPU 名称及逐卡名称；
- 磁盘数量、显示器数量、操作系统。

错误可观测性采用“日志转 Span”策略：

- 只有 `[Error]` 级日志进入 APM；
- Error Span 名称直接使用异常类型名并移除 `Exception` 后缀，例如 `NullReferenceException` → `Error.NullReference`；
- 没有异常对象时，按来源和消息兜底，例如 CloudBase 上传失败归类为 `Error.upload`；
- AOP 捕获的异常会携带原始类型和堆栈，因此框架方法失败也能进入 Error Span；
- 日志正文保留在本地分类日志中，APM 只接收适合聚合的错误语义。

当前没有自定义 Metric、CPU/内存指标曲线和 CLS 式日志检索，因此 APM 的定位是“链路与错误可观测”，不是完整日志仓库。

### 6.6 CloudBase 硬件入库
![173464c6d7100295339705b8a37c075d.png](https://raw.gitcode.com/user-images/assets/7353928/be2df9a1-bf8e-443c-a42f-058b939911fb/173464c6d7100295339705b8a37c075d.png '173464c6d7100295339705b8a37c075d.png')

硬件扫描完成后，后台调用 CloudBase PostgreSQL RPC，不允许匿名用户直接操作云数据库。客户端只调用 `upsert_yframe_hardware`，数据库函数内部使用 `INSERT ... ON CONFLICT (device_name)` 完成 upsert。

设计核心：

- 客户端不持有表级 INSERT/UPDATE/DELETE 权限；
- `anon` 只拥有函数 `EXECUTE` 权限；
- `device_name` 唯一索引保证 upsert 语义；
- 字段长度和写入逻辑固定在数据库函数中，客户端无法直接拼接 SQL；
- 即使 Publishable Key 被提取，也只能调用受控函数，不能任意改表。

字段映射：

| 表字段 | 内容 |
|---|---|
| `device_name` | 计算机名 |
| `cpu` | CPU 名称、核心/线程、频率 |
| `gpu` | 全部 GPU 名称，多个用 ` | ` 分隔 |
| `memory` | 内存容量与内存条数量 |
| `disk` | 全部磁盘型号、容量、接口、介质 |
| `updatetime` | 由数据库函数更新 |
| `id` / `created_at` | 使用数据库默认值 |

配置项：

| 键 | 说明 |
|---|---|
| `CloudBaseEnabled` | 是否启用硬件入库 |
| `CloudBaseEnvId` | CloudBase 环境 ID |
| `CloudBaseHardwareTable` | 硬件表名，默认 `yframe_hardware` |
| `CloudBaseHardwareRpc` | upsert RPC 名，默认 `upsert_yframe_hardware` |

CloudBase API Key 同样不写入配置，保存到 `%LocalAppData%\YFrame\secrets.dat`，由 DPAPI 保护；安装/更新引导文件可自动完成迁移。

---

## 7. 发布、COS 上传与自动更新

### 7.1 YFrame.Pack

输入两个版本 ZIP，按文件路径和内容 SHA-256 比较，生成时间戳输出目录。

输出目录包含四类工件：

| 文件 | 作用 |
|---|---|
| `current.zip` | 最新完整包副本 |
| `increment.zip` | 仅包含新增/修改文件 |
| `YF_Version.json` | 版本清单 |
| `YF_Version.json.sig` | 清单 ECDSA 签名 |

签名文件必须与清单一起上传；更新器先验签，再读取清单，缺失或验签失败时拒绝更新。

版本清单的核心字段：

| 字段 | 含义 |
|---|---|
| `product` | 产品名 |
| `current_version` | 最新版本 |
| `previous_version` | 上一版本 |
| `updatetime` | 构建时间 |
| `current` | 完整包 URL、大小、SHA-256 |
| `previous` | 增量包 URL、大小、SHA-256 |
| `removed` | 新版本删除的文件列表 |

输出目录自动附加时间戳；`YFrame.Pack.config.json` 只保存输出根目录和 URL，不保存输入 ZIP 路径；启动时自动扫描 exe 目录中版本最高的两个 ZIP。

### 7.2 YFrame.update

更新器采用“先验证完整性，再替换文件”的策略，版本选择规则如下：

| 本地版本 | 行为 |
|---|---|
| 等于 `current_version` | 已是最新，不更新 |
| 等于 `previous_version` 且增量包可用 | 下载 `increment.zip`，增量更新 |
| 低于 `current_version` 的其他版本 | 下载 `current.zip`，全量更新 |
| 增量包缺失但全量包可用 | 回退全量更新 |
| 高于 `current_version` | 不降级 |


全量更新规则：

- `current.zip` 中的框架文件覆盖到安装目录；
- `Config/`、`Log/`、`plugins/`、`Backup/`、`File/` 等用户目录不覆盖、不清理；
- 根目录旧 `.dll` / `.exe` / `.json` / `.config` / `.pdb` 和 `runtimes/`、`Common/` 中不属于新包的文件会清理；
- `YFrame.Pack.exe`、`YFrame.Pack.config.json`、`YFrame.Installer.exe` 保留；
- 新包中的 `YFrame.update.exe` 写为 `YFrame.update.new.exe`，由下次启动接替。

稳定性设计：

- 备份失败或替换失败时保留备份，成功后清理整个 `Backup`；
- 日志写入 `Log/Update/yyyy-MM-dd.log`；
- 不覆盖运行中的更新器，使用 pending 文件接替；
- YFrame 下次启动先接替 pending 更新器，再做版本检查；
- 文件占用时重试，失败不阻断主程序启动。

### 7.3 更新环境

更新环境由 `Config/config.conf` 中的 `Env` 控制：`uat` 或缺失时使用 UAT，`pdt` 使用生产环境，非法值回退 `uat`。

实际地址由 `UpdateBaseUrl` 与 `Env` 拼接，分别指向版本清单、完整包和增量包。默认根地址只在 `YF_Manager/Common/UpdateDefaults.cs` 的 `Cos_UpdateDefaults.BaseUrl` 维护一份，避免 Pack、Update 和主程序出现多份地址。

### 7.4 YFrame.Installer

安装器通过 `CollectPayload.ps1` 收集 YFrame 核心文件与 `runtimes/`，打包成 `Resources/payload.zip` 后嵌入单文件安装器。向导流程为“欢迎 → 路径/快捷方式 → 进度 → 完成”，发布使用 `dotnet publish YFrame.Installer/YFrame.Installer.csproj -c Release -p:PublishProfile=SingleFile`，输出 `Installer/YFrame.Installer.exe`。

### 7.5 YFrame.upload.cos

独立 WPF 上传器，已加入 `YFrame.sln`，负责两类互不混合的上传任务。

![image.png](https://raw.gitcode.com/user-images/assets/7353928/13037241-7c9e-4241-8ee2-9f243c100148/image.png 'image.png')

**A. 更新包四文件上传**

输入目录必须同时包含 `current.zip`、`increment.zip`、`YF_Version.json`、`YF_Version.json.sig`。

| 步骤 | 行为 |
|---|---|
| 扫描 | 自动寻找最新包含四文件的时间戳目录 |
| 选择环境 | UAT 或 PDT |
| 读取凭证 | 从本地 `SecretKey.csv` 读取 SecretId/SecretKey，仅驻留内存 |
| 上传 | 按 `current.zip → increment.zip → YF_Version.json.sig → YF_Version.json` 顺序执行 |

COS 对象键统一为 `YFrame/update/{uat|pdt}/{文件名}`。上传器固定要求四文件齐全，缺一个不允许上传；签名文件先于清单上传，清单最后上传，避免客户端提前读到不完整清单。COS 简单上传在目标对象已存在时覆盖。

`SecretKey.csv` 首行为 `SecretId,SecretKey`，支持带引号字段；凭证不会写入 `upload.settings.json`。

**B. 插件包压缩与上传**

插件源目录下每个子目录视为一个插件。上传器扫描后显示插件名称、ZIP 大小、公开 URL 和状态，并支持逐个勾选。

发布分两步：

1. “仅压缩”：为每个勾选插件生成 `{FolderName}.zip`，计算 SHA-256，并生成带 `schemaVersion/keyId` 的 `plugins.json`；
2. “上传已压缩包”：把 ZIP 上传到 `YFrame/plugins/file/`，把 `plugins.json.sig` 和 `plugins.json` 上传到 `YFrame/plugins/`。

设计要点：

- 压缩与上传解耦，上传阶段不会重新压缩；
- 勾选状态保存到 `SelectedPlugins`，下次启动恢复；旧配置没有该字段时默认全选，空集合表示全部取消；
- ZIP 按插件目录结构打包，使用相对路径与前向斜杠；
- `plugins.json` 包含 `id`、`name`、`version`、`fileName`、`url`、`size`、`sha256`；
- 清单使用插件私钥签名，生成 `plugins.json.sig`；签名覆盖清单原始字节；
- 签名文件先上传，清单最后上传，保证客户端不会看到“清单已存在但 ZIP/签名不全”的中间状态。

**配置与输出**

配置位于 `YFrame.Pack/bin/{Debug|Release}/net8.0-windows/Config/upload.settings.json`，主要字段覆盖 Pack 输出目录、SecretKey.csv、Bucket/Region/Environment、插件源目录与 ZIP 输出目录、插件 ZIP/清单的 COS 前缀与公开地址、清单文件名、`SelectedPlugins`、`PluginPrivateKeyFile` 和 `PluginKeyId`。

插件签名私钥默认路径：

```text
C:\Users\Administrator\Desktop\code\C#\YFrame\SigningKeys\plugins\plugin-manifest-2026-01.private.pem
```

私钥只用于上传器签名，不能上传到 COS、不能提交 Git、不能复制到客户端。

上传器默认输出目录与 `YFrame.Sign` 一致，均为 `YFrame.Pack/bin/{Debug|Release}/net8.0-windows/`。构建命令为 `dotnet build YFrame.upload.cos/YFrame.upload.cos.csproj -c Release`。

---

## 8. 工程化与版本治理

### 8.1 统一版本

`Version.props` 是主框架、YF_Manager、测试和安装器的统一版本源，集中定义产品版本、构建天数、程序集版本、文件版本和信息版本。

当前产品版本为 `2.1.6`。标题栏显示主次版本 `v2.1`；关于窗口显示 `2.1.6.<day>`；exe 文件版本同样包含构建天数。`Version` 必须保持三段数字，避免程序集版本变成五段。插件不导入该文件，保持独立默认版本。

### 8.2 测试

当前 129 个测试全部通过，覆盖命令、配置、日志、插件调度、文件、TCP、MD5、正则、插件清单签名和安全安装等核心能力。测试项目通过 `dotnet test YFrame.Tests/YFrame.Tests.csproj` 执行。

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
dotnet build YFrame.sln -c Debug
dotnet test YFrame.Tests/YFrame.Tests.csproj -c Debug
dotnet run --project YFrame/YFrame.csproj
```

构建完成后可直接运行 `YFrame/bin/Debug/net8.0-windows/YFrame.exe`。

### 9.3 构建插件

插件不在解决方案内，需要单独构建，默认输出到 `YFrame/bin/Debug/net8.0-windows/plugins/{PluginName}/`。例如构建 `YF_Clicker` 时执行 `dotnet build ..\YF_Clicker\YF_Clicker\YF_Clicker.csproj -c Debug`。


---

## 10. 目录结构

```text
YFrame/
├── YFrame/                       # WPF Shell
│   ├── MainWindow.xaml/.cs
│   ├── ViewModel/
│   │   └── WorkflowBoardViewModel.cs
│   ├── Model/
│   │   ├── WorkflowModels.cs
│   │   └── WorkflowLayout.cs
│   ├── Service/
│   │   ├── Telemetry/           # OpenTelemetry / APM
│   │   ├── CloudBase/           # 硬件表上传
│   │   ├── Security/            # DPAPI / 引导迁移
│   │   └── WorkflowStorage.cs
│   ├── View/UC/
│   │   └── WorkflowBoard.xaml/.cs
│   ├── View/WorkflowConfirmWindow.xaml/.cs
│   ├── View/UC/Toolbox/
│   ├── Controls/                 # Sparkline / VerticalText
│   └── Common/                   # Themes / Language / Images
├── YF_Manager/                   # 契约、AOP、配置、Mediator、工具
├── YFrame.Tests/                 # xUnit
├── YFrame.Installer/             # 向导式安装器
├── YFrame.Pack/                  # 增量包构建工具
├── YFrame.Sign/                  # 清单签名工具
├── YFrame.update/                # 自动更新器
├── YFrame.upload.cos/            # COS 上传 / 插件打包上传
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

ViewModel 只需实现插件标识、显示名称、命令入口和回调事件；框架按约定类型创建实例并绑定 `DataContext`，不需要插件引用主程序。

约定：

- 命名空间 = DLL 文件名；
- 入口控件 = `{命名空间}.MainControl`；
- 需要 AOP 的方法必须 `virtual` 且标记 `[Log]`；
- UI 文案放到 `Common/Language/zh-CN.xaml` / `en-US.xaml`；
- 不在插件中重复实现日志、配置、文件、ZIP 等通用能力。

---

## 12. 配置速查

主框架配置文件位于 `YFrame/bin/Debug/net8.0-windows/Config/config.conf`。

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
| `ApmEnabled` | 是否启用 APM OpenTelemetry，默认 `true` |
| `ApmServiceName` | APM 服务名，默认 `YFrame` |
| `ApmEnvironment` | APM 环境，默认 `uat` |
| `CloudBaseEnabled` | 是否上传硬件信息，默认 `true` |
| `CloudBaseEnvId` | CloudBase 环境 ID |
| `CloudBaseHardwareTable` | 硬件表名，默认 `yframe_hardware` |
| `CloudBaseHardwareRpc` | 硬件 upsert RPC 名，默认 `upsert_yframe_hardware` |

其他本地数据：

- 工作任务：`Config/Workflows/workflows.json`
- COS 上传器：`YFrame.Pack/bin/{Debug|Release}/net8.0-windows/Config/upload.settings.json`

COS 上传器不会把 SecretId/SecretKey 写入配置，只在运行时从 `SecretKey.csv` 读取。

---

## 13. 已知边界

- 仅支持 Windows / x64；
- 插件项目默认输出路径写死为 Debug 输出目录，Release 部署需单独复制；
- AI 模型、PaddleOCR 模型体积较大，不随仓库分发；
- OCR 翻译依赖百度翻译 API，当前凭据硬编码在源码中，公开部署前应改为配置或服务端代理；
- 自动更新已内置清单签名校验（ECDSA P-256 + SHA-256）和包 SHA-256 校验；生产环境仍建议增加 Authenticode 校验；
- 更新器为框架依赖单文件，目标机器需要 .NET 8 Desktop Runtime；安装器为自包含单文件；
- APM 当前只上报 Trace 和 Error Span，不包含 CPU/内存 Metric；APM 控制台没有 CLS 式日志检索，完整日志需接 CLS；
- CloudBase 使用 Publishable Key 时对应 `anon` 角色，只授予 upsert RPC 的 `EXECUTE`，并撤销表级 INSERT/UPDATE/DELETE；Publishable Key 可被客户端提取，生产环境建议改用云函数代理。
- 工作任务目前只做本地 JSON 持久化，不做多端同步或协作；
- `YFrame.upload.cos` 依赖本地 `SecretKey.csv`，不会保存 SecretId/SecretKey；公开分发前应改用临时凭证或服务端上传；
- 上传器输出目录与 `YFrame.Sign` 一致，均在 `YFrame.Pack/bin/{Configuration}/net8.0-windows/`。

---

## 界面预览

仓库历史设计稿截图：
![image.png](https://raw.gitcode.com/user-images/assets/7353928/a47310a3-bcfc-4a9f-b603-a4f9a7318e95/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/1a729d2e-4bf0-4533-ad92-8fbd58c776b8/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/0986b125-1eb2-4f47-814d-1918151fef77/image.png 'image.png')
![image.png](https://raw.gitcode.com/user-images/assets/7353928/c9279382-d132-4a48-9fdd-8c2f005548b9/image.png 'image.png')
---

## 仓库

- GitCode：<https://gitcode.com/Colin_Davis/YFrame>
- 解决方案：`YFrame.sln`
- 文档：[README.md](README.md) · [AGENTS.md](AGENTS.md)