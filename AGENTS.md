# YFrame 项目参考（AI 快速理解用）

> **编写目的：** 供 AI 在新对话中快速理解此项目，避免重复读取所有代码。
> **生成日期：** 2026-07-15

---

## 一、项目概述

YFrame 是一个基于 **.NET 8.0 + WPF** 的**模块化插件桌面框架**，采用抽屉式 IDE 风格 Shell，通过**反射动态加载插件**，为各类开发者工具提供统一运行平台。

- **项目路径：** `C:\Users\Administrator\Desktop\code\C#\YFrame`
- **解决方案：** `YFrame.sln`（Visual Studio 2022）
- **语言：** C# 12.0，nullable enabled

### 解决方案组成

| 项目 | 路径 | 类型 | 输出 |
|------|------|------|------|
| YFrame | `YFrame\` | WPF Application | `YFrame.exe`（主框架 Shell） |
| YF_Manager | `YF_Manager\` | Class Library | `YF_Manager.dll`（共享基础设施库） |

### 插件（位于上层目录 `C:\Users\Administrator\Desktop\code\C#\`）

| 插件 | 目录 | 功能 | 核心依赖 |
|------|------|------|----------|
| YF_AIHelper | `YF_AIHelper\` | 本地 LLM AI 对话助手 | LLamaSharp 0.24.0 + CUDA 12 |
| YF_Clicker | `YF_Clicker\` | 鼠标自动连点器 | WindowsInput |
| YF_HttpServer | `YF_HttpServer\` | 一键 HTTP 文件服务器 | HttpListener（内置） |
| YF_KMScript | `YF_KMScript\` | 中文 DSL 键鼠自动化脚本 | 无额外依赖 |
| YF_Penetration | `YF_Penetration\` | NAT 内网穿透（P2P 联机） | NatTraversal 自研库 |
| YF_ScreenOCRTranslate | `YF_ScreenOCRTranslate\` | 截图 OCR + 翻译 | PaddleOCRSharp + 百度翻译 API |

---

## 二、目录结构速览

```
YFrame/
├── YFrame.sln                          # VS 解决方案（含 YFrame + YF_Manager 两项目）
├── README.md                           # 详细项目文档
│
├── YFrame/                             # 主框架 WPF 项目
│   ├── App.xaml                        # 应用入口 XAML（默认主题+语言资源字典、全局Style）
│   ├── App.xaml.cs                     # 入口逻辑：ChangeTheme() / ChangeLanguage()，静态 logger
│   ├── MainWindow.xaml                 # 主窗口布局（DockPanel三栏+Menu+StatusBar）
│   ├── MainWindow.xaml.cs              # 代码后置：DataContext = MainWindowViewModel.Instance
│   ├── ViewModel/
│   │   ├── MainWindowViewModel.cs      # 核心 ViewModel（~700行，单例+AOP，面板切换+热键路由+命令）
│   │   └── Service/
│   │       ├── UserControlsService.cs  # 插件加载服务（200行，反射扫描/加载/实例化）
│   │       └── HotkeyService.cs        # 全局热键服务（Win32 RegisterHotKey，AOP单例）
│   ├── Model/
│   │   ├── PluginsModel.cs             # 插件列表模型（Name, ID, Status）
│   │   ├── CtrlDataModel.cs            # 运行时插件实例数据（UserControl, CommandHandler, Parameters）
│   │   └── CtrlParamModel.cs           # 预留参数模型（当前为空壳）
│   ├── View/UC/
│   │   └── PerformanceMonitor.xaml/.cs # LiveCharts CPU/内存图表（5秒采样，30秒窗口）
│   ├── Common/
│   │   ├── Images/Logo.png             # 应用 Logo
│   │   ├── Images/Logo.ico             # 应用图标（.ico 格式）
│   │   ├── Themes/                     # 四套主题 XAML（Dark/Cream/LightBlue/Green） + ControlStyles
│   │   └── Language/                   # zh-CN.xaml + en-US.xaml
│   └── Properties/                     # 标准 WPF 配置
│
├── YF_Manager/                         # 共享框架库
│   ├── YF_Manager.cs                   # 静态入口：YF_Manager_Main 类 + 静态 logger
│   ├── Interface/
│   │   ├── I_YF_Detail.cs              # 插件元数据接口（YF_ID, YF_Name）
│   │   └── I_YF_Command.cs             # 插件命令接口（ExecuteCommand, OnPluginCallback）
│   └── Common/
│       ├── Config.cs                   # 全局常量（LogPath, PluginPath, TCP端口, PaddlePath）
│       ├── Attributes/LogAttribute.cs  # [Log] 特性（LogLevel: Debug/Info/Warning/Error）
│       ├── Interceptors/LogInterceptor.cs  # Castle.Core IInterceptor（AOP 核心）
│       ├── Tools/
│       │   ├── YF_Manager_Log.cs       # 日志系统（HTML格式，按天/类型分文件，1MB轮转，最多999文件）
│       │   ├── YF_TcpHelper.cs         # 网络工具（GetLocalIP, GetDefaultGatewayIP）
│       │   └── YF_FileHelper.cs        # 文件工具（CopyDirectory, SetClipboardWithRetry, OpenFolder）
│       ├── YF_RelayCommand.cs          # ICommand实现（无参版 + 泛型版<T>）
│       └── YF_DelegateFunctionModel.cs # 委托类型声明（dvFunc_Vs, dvFunc_Vs_s）
│
├── Review/                             # 代码审查报告（多个HTML报告）
└── .gitignore                          # 排除 bin/obj/Log/Review 等
```

---

## 三、核心架构设计

### 3.1 依赖关系

```
YFrame.exe ──编译依赖──→ YF_Manager.dll ──→ Castle.Core 5.2.1
    │                        │
    │ 运行时反射加载（无编译依赖） │  插件编译时引用
    ▼                        ▼
plugins/              所有插件项目
  ├── YF_AIHelper.dll ──→ YF_Manager.dll + LLamaSharp
  ├── YF_Clicker.dll ──→ YF_Manager.dll + WindowsInput
  ├── YF_HttpServer.dll ──→ YF_Manager.dll
  ├── YF_KMScript.dll ──→ YF_Manager.dll
  ├── YF_Penetration.dll ──→ YF_Manager.dll + NatTraversal 自研库
  └── YF_ScreenOCRTranslate.dll ──→ YF_Manager.dll + PaddleOCRSharp
```

**关键设计：** YFrame 与插件间**零编译时依赖**，完全通过 `I_YF_Detail` + `I_YF_Command` 接口契约通信。

### 3.2 插件契约接口

```csharp
// I_YF_Detail — 插件身份标识
interface I_YF_Detail { string YF_ID { get; } string YF_Name { get; } }

// I_YF_Command — 命令执行 + 事件回调
interface I_YF_Command {
    void ExecuteCommand(string command, object parameter = null);
    event EventHandler<PluginEventArgs> OnPluginCallback;
}
```

### 3.3 插件发现与加载流程

```
应用启动 → MainWindowViewModel.Init()
  → InitUI() → UserControlsService.LoadAndShowUserControl()
    1. 扫描 plugins/ 下所有子文件夹
    2. 查找 YF_*.dll 文件
    3. 过滤掉 YF_Manager.dll
    4. Assembly.LoadFrom(dllPath)
    5. 获取约定类型: {pluginName}.MainControl + {pluginName}.MainControlViewModel
    6. Activator.CreateInstance() 创建 ViewModel → 验证接口 → 存入 DctControls 字典
```

**懒加载：** 启动时仅扫描元数据（ID+Name），用户点击"显示"后才实例化 UserControl。

### 3.4 插件开发规范

| 约定项 | 规范 |
|--------|------|
| 命名空间 | 与 DLL 文件名相同（`YF_AIHelper.dll` → namespace `YF_AIHelper`） |
| 入口控件 | `{命名空间}.MainControl`，继承 `UserControl` |
| 视图模型 | `{命名空间}.MainControlViewModel`，实现 `I_YF_Detail` + `I_YF_Command` |
| 输出目录 | 编译到 `plugins/{命名空间}/` 目录下 |
| 必须引用 | `YF_Manager.dll` |

### 3.5 单例 + AOP 代理（最核心模式）

所有关键组件使用此模式：

```csharp
// YF_Manager 中对 Castle.Core 的 static 引用保证优先初始化（避免日志系统空引用）
private static readonly Lazy<T> _instance = new Lazy<T>(
    () => new ProxyGenerator().CreateClassProxy<T>(new LogInterceptor())
);
public static T Instance => _instance.Value;
```

**要求：** 被代理的方法必须声明为 `virtual`，有 `[Log]` 特性才被拦截记录。

**采用此模式的类：**
- YFrame: `MainWindowViewModel`, `UserControlsService`, `HotkeyService`
- YF_Manager: `YF_FileHelper`
- YF_AIHelper: `MainControlViewModel`
- YF_Clicker: `MainControlViewModel`
- YF_HttpServer: `MainControlViewModel`, `HttpService`
- YF_KMScript: `MainControlViewModel`
- YF_Penetration: `MainControlViewModel`
- YF_ScreenOCRTranslate: `MainControlViewModel`, `ScreenShotViewModel`, `TranslateService`

### 3.6 日志系统

| 日志类型 | 目录 | 内容 |
|----------|------|------|
| InfoLog | `Log/InfoLog/` | 所有 Info 级别聚合 |
| DebugLog | `Log/DebugLog/` | 插件扫描、加载过程 |
| ErrorLog | `Log/ErrorLog/` | 异常详情、堆栈 |
| CommandLog | `Log/CommandLog/` | 框架→插件命令记录 |
| TcpLog | `Log/TcpLog/` | 网络通信日志 |
| InterceptorsLog | `Log/InterceptorsLog/` | AOP 拦截日志 |

- 文件格式：HTML（`.htm`），按日期命名（`yyyy-MM-dd.htm`）
- 单文件上限：1MB，超出自动轮转（`*_1.htm` ... `*_999.htm`）
- 线程安全：静态 `_fileLock`
- UI 回显：静态委托 `d_LogWrite` → `MainWindowViewModel.Show_Log()`，最多缓存 500 行
- **日志面板可清除：** `ClearLog()` 清空内存缓存 + UI 显示

### 3.7 全局热键系统（2026-07 新增）

```
MainWindow 加载
    │
    ▼
HotkeyService.Instance.Initialize(window)
    │  WindowInteropHelper → HwndSource.AddHook(WndProc)
    │
    ▼
用户点击工具栏"热键监控" → ToggleHotkeyCommand
    │
    ├── 开启 → HotkeyService.Instance.Register()
    │             RegisterHotKey(hWnd, HOTKEY_ID, MOD_CONTROL, VK_Y)
    │             状态栏显示 "热键Ctrl+Y: 已开启"
    │
    └── 关闭 → HotkeyService.Instance.Unregister()
                  UnregisterHotKey(hWnd, HOTKEY_ID)
                  状态栏显示 "热键Ctrl+Y: 已关闭"

Ctrl+Y 按下 → WndProc 拦截 WM_HOTKEY → OnHotkeyPressed 事件
    │
    ▼
MainWindowViewModel.OnHotkeyPressed()
    │  根据当前激活插件 ID 分发命令:
    ├── "YF_ScreenOCRTranslate" → ExecuteCommand("CaptureScreen")
    ├── "YF_Clicker" → ExecuteCommand("ToggleClick")
    └── 其他插件 → ExecuteCommand("HotkeyTrigger")
```

**HotkeyService** 位于 `YFrame\ViewModel\Service\HotkeyService.cs`，遵循 AOP 单例模式。热键由**框架统一管理**而非各插件独立注册，避免热键冲突。

### 3.8 面板切换（2026-07 新增）

主窗口的左右侧边栏支持**多标签页切换**：

| 面板 | 标签页 | 索引 | 说明 |
|------|--------|------|------|
| **左侧面板** | 插件列表 | 0 | VS Code 风格活动栏，垂直旋转文字，选中竖线指示 |
| **左侧面板** | 工具箱 | 1 | 工具箱内容区（待扩展） |
| **右侧面板** | 日志 | 0 | 实时日志输出，支持清除 |
| **右侧面板** | 参数 | 1 | 参数面板（待扩展） |

切换通过 `SwitchLeftPanelCommand` / `SwitchRightPanelCommand` 命令绑定，ViewModel 维护 `ActiveLeftPanel` / `ActiveRightPanel` 属性。

---

## 四、主题与多语言

### 主题（4套）
| 主题 | 文件 | 主色调 |
|------|------|--------|
| 炭火暗夜 | `DarkGrayTheme.xaml` | 背景 #1E1E1E，强调色 #0078D4 |
| 素火明昼 | `CreamWhiteTheme.xaml` | 背景 #FFF5F5F8，强调色 #D94A1A |
| 冰火深蓝 | `LightBlueTheme.xaml` | 背景 #0B1526，强调色 #00CCF0 |
| 翠火青绿 | `GreenWhiteTheme.xaml` | 背景 #0A1410，强调色 #00E676 |

全局控件样式：`ControlStyles.xaml`（Button/TextBox/Label）

### 语言（2种）
- `zh-CN.xaml` 简体中文（默认）
- `en-US.xaml` English

切换方式：`App.ChangeLanguage("zh"/"en")` 或 `App.ChangeTheme("path")`，通过 `MergedDictionaries` 替换实现。

---

## 五、重要配置常量

| 常量 | 值 | 位置 |
|------|-----|------|
| 目标框架 | `net8.0-windows` | .csproj |
| 日志根目录 | `Log` | Config.cs |
| 插件目录 | `Plugins`（代码实际用 `"plugins"`） | Config.cs / UserControlsService.cs |
| 插件匹配 | `YF_*.dll` | UserControlsService.cs |
| 日志文件上限 | 1MB（`1024 * 1024`） | YF_Manager_Log.cs |
| 日志轮转上限 | 999 个文件 | YF_Manager_Log.cs |
| UI 日志行数 | 500 行 | MainWindowViewModel.cs |
| 性能采样周期 | 5 秒 | PerformanceMonitor.cs |
| 图表数据窗口 | 6 点（30 秒） | PerformanceMonitor.cs |
| 全局热键 | Ctrl+Y (MOD_CONTROL=0x0002, VK_Y=0x59) | HotkeyService.cs |
| 热键 ID | 9001 | HotkeyService.cs |
| TCP 端口 | 服务器 8021，客户端 8022 | Config.cs |
| PaddleOCR 路径 | `plugins\YF_ScreenOCRTranslate\inference` | Config.cs |
| 窗口尺寸 | 800 x 1200 | MainWindow.xaml |
| 窗口标题 | "YF Tools" | MainWindow.xaml |
| C# 可空 | enabled | .csproj |
| 剪贴板重试 | 2 次 | YF_FileHelper.cs |

---

## 六、插件详情速查

### YF_AIHelper（AI 助手）
- **ID:** YF_AIHelper，名称："AI 助手"
- **模型:** DeepSeek-R1-Distill-Qwen-7B-Q4_K_M.gguf（本地 GGUF）
- **配置:** ContextSize=1024, GpuLayerCount=20
- **核心:** LLamaWeights → LLamaContext → InteractiveExecutor → 流式 InferAsync
- **UI:** 聊天界面，AI左/用户右气泡，消息自动滚动
- **初始化分离:** `Init(false)` 仅返回元数据不加载模型（避免文件占用），`Init(true)` 完整加载
- **已知问题:** KeyDown 事件未绑定到 XAML（Enter/Ctrl+Enter 功能不生效），`ExecuteCommand` 未区分命令类型，`Microsoft.Extensions.Configuration` 包已添加但未使用

### YF_Clicker（鼠标连点器）（2026-07 新增）
- **ID:** YF_Clicker，名称："鼠标连点器"
- **核心依赖:** WindowsInput（InputSimulator 模拟鼠标点击）
- **功能:** 可设定点击间隔(ms)，后台线程连续点击，10 秒自动停止
- **启停方式:** 手动点击 UI 按钮 / 框架 Ctrl+Y 热键切换
- **状态:** UI 显示运行状态（绿/灰圆形指示器）+ 点击次数统计
- **回调:** 启停/完成时通过 `OnPluginCallback` 通知框架

### YF_HttpServer（Http 文件助手）
- **ID:** YF_HttpServer，名称："Http 文件助手"
- **核心:** HttpListener 监听端口（默认8000），后台线程运行
- **功能:** GET目录浏览+文件下载（含MIME），POST上传（X-FileName头）
- **UI:** IP/端口/路径配置 + 启停按钮 + 拖拽上传 + 防火墙/curl命令一键复制
- **自定义附加行为:** `DragDropBehavior` 实现MVVM拖放绑定
- **已知问题:** `_YF_FileHelper` 字段未初始化（OpenFolderCommand 会空引用），`ExecuteCommand` 未区分命令类型，`Run()` 中有调试残留代码 `var a = ex.GetType().Name;`

### YF_KMScript（脚本编辑器）
- **ID:** YF_KMScript，名称："脚本编辑器"
- **核心:** 自研中文DSL解释器（`ScriptInterpreter`，独立在 `Services/` 目录中，416行），OpenCV 模板匹配（`ImageMatcher`）
- **语法:** 定义/找图/点击/等待/循环/如果/否则/截图/输出，`//` 注释，Python 风格缩进（Tab 或 2 空格）
- **找图:** OpenCvSharp4 `CCOEFF_NORMED` 算法，`匹配阈值` 变量控制相似度
- **特性:** 后台执行 + `_shouldStop` 可中断 + 嵌套循环 + if/else 条件判断 + 区域截图选择窗口
- **新增服务:** `ImageMatcher`（OpenCV 找图）、`LogEntry`（日志模型）、`RegionSelectionWindow`（截图选区）、`ScreenCapture`（截图工具）
- **文件格式:** `.ys` 脚本文件
- **内部 RelayCommand:** 自定义简单 RelayCommand，未使用框架的 `YF_RelayCommand`

### YF_Penetration（NAT 内网穿透）（2026-07 新增）
- **ID:** YF_Penetration，名称："NAT 内网穿透"
- **核心依赖:** 自研 NatTraversal 库（Client + Server + Shared 三层）
- **功能:** Host 创建房间 → 生成加入码 → Player 加入 → P2P 中继转发
- **模式:** Host / Player 两种角色，TCP / UDP 双协议中继
- **UI:** 双标签页切换（Host/Player），连接状态指示器，统计面板
- **状态:** 开发中，已具备基本房间创建/加入/中继功能

### YF_ScreenOCRTranslate（OCR 实时翻译）
- **ID:** YF_ScreenOCRTranslate，名称："OCR 实时翻译"
- **工作流:** Ctrl+Y热键 → 全屏截图选区 → PaddleOCR识别 → 百度翻译API → Canvas叠加显示
- **Win32:** （热键现已由框架 HotkeyService 统一管理）
- **DPI:** 1.25倍缩放补偿（硬编码）
- **置信度:** >0.6过滤
- **模型:** PP-OCRv5 mobile（det/rec/cls）
- **已知问题:** 百度 API 密钥硬编码在源码中（appId/secretKey），`PaddleOCREngine` 未释放可能导致 GPU 内存泄漏，翻译 API 使用 HTTP 而非 HTTPS

---

## 七、关键文件路径映射

| 文件 | 路径 |
|------|------|
| 解决方案 | `YFrame/YFrame.sln` |
| README | `YFrame/README.md` |
| AGENTS | `YFrame/AGENTS.md` |
| App 入口 | `YFrame/YFrame/App.xaml.cs` |
| 主窗口 XAML | `YFrame/YFrame/MainWindow.xaml` |
| 核心 ViewModel | `YFrame/YFrame/ViewModel/MainWindowViewModel.cs` |
| 插件加载服务 | `YFrame/YFrame/ViewModel/Service/UserControlsService.cs` |
| 热键服务 | `YFrame/YFrame/ViewModel/Service/HotkeyService.cs` |
| 插件元数据模型 | `YFrame/YFrame/Model/PluginsModel.cs` |
| 插件实例模型 | `YFrame/YFrame/Model/CtrlDataModel.cs` |
| 性能监视器 | `YFrame/YFrame/View/UC/PerformanceMonitor.xaml.cs` |
| 接口定义 | `YFrame/YF_Manager/Interface/I_YF_Detail.cs` + `I_YF_Command.cs` |
| AOP 拦截器 | `YFrame/YF_Manager/Common/Interceptors/LogInterceptor.cs` |
| 日志系统 | `YFrame/YF_Manager/Common/Tools/YF_Manager_Log.cs` |
| 文件工具 | `YFrame/YF_Manager/Common/Tools/YF_FileHelper.cs` |
| 日志特性 | `YFrame/YF_Manager/Common/Attributes/LogAttribute.cs` |
| 全局常量 | `YFrame/YF_Manager/Common/Config.cs` |
| RelayCommand | `YFrame/YF_Manager/Common/YF_RelayCommand.cs` |
| 工具类 | `YFrame/YF_Manager/Common/Tools/YF_TcpHelper.cs` |
| 主题 | `YFrame/YFrame/Common/Themes/*.xaml` |
| 语言 | `YFrame/YFrame/Common/Language/*.xaml` |

---

## 八、开发注意事项

1. **添加新插件：** 在 `C:\Users\Administrator\Desktop\code\C#\` 下创建 `YF_XXX` 目录，按约定编写 `MainControl.xaml` + `MainControlViewModel.cs`，输出到 `plugins/YF_XXX/`
2. **修改主框架：** 编辑 `YFrame/` 和 `YF_Manager/` 下的代码，用 Visual Studio 2022 打开 `YFrame.sln`
3. **当前只支持 x64 / Any CPU** 配置
4. **AOP 代理要求方法为 virtual** 且有 `[Log]` 特性
5. **全局热键 Ctrl+Y 由框架 HotkeyService 统一管理**，插件无需各自注册热键，只需实现 `ExecuteCommand("ToggleClick" / "CaptureScreen" / "HotkeyTrigger")`
6. **已知问题：**
   - `CtrlParamModel.cs` 为空壳
   - `DarkGrayTheme.xaml` 缺少图表相关资源键（其他三个主题有）
   - 目录名 `Plugins` vs `plugins` 大小写不一致
   - YF_AIHelper: KeyDown 未绑定到 XAML（Enter/Ctrl+Enter 不生效）
   - YF_HttpServer: `_YF_FileHelper` 未初始化（OpenFolderCommand 空引用）
   - YF_ScreenOCRTranslate: 百度 API 密钥硬编码，PaddleOCREngine 未释放
7. **Logo.png 引用绝对路径**（`.csproj` 第22行），移植时需注意
8. **YF_FileHelper 现采用 AOP 单例模式**，新增 `OpenFolder()` 方法可打开任意文件夹（不存在则自动创建）
