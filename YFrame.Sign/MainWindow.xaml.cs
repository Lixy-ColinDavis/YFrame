using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using YFrame.Sign.Models;
using YFrame.Sign.Services;

namespace YFrame.Sign;

/// <summary>
/// Sign 主窗口：维护父目录、相对清单、密钥路径和签名操作。
/// </summary>
public partial class MainWindow : Window
{
    private readonly SignConfig _config;
    private bool _initializing = true;

    /// <summary>初始化窗口、载入本地配置并绑定事件。</summary>
    public MainWindow()
    {
        InitializeComponent();

        _config = SignConfig.Load();
        LoadConfigToUi();

        ConfigPathText.Text = $"配置：{SignConfig.ConfigPath}";
        Loaded += Window_Loaded;
        Closing += (_, _) => SaveConfig();
    }

    /// <summary>窗口加载后准备默认密钥，首次使用自动生成临时密钥。</summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _initializing = false;

        // 每次启动都自动定位 Pack output 下最新的清单目录
        RefreshLatestOutput();

        string oldDefaultKeyDirectory = Path.Combine(AppContext.BaseDirectory, "Keys");
        bool usesOldDefaultKey = KeyPathTextBox.Text.StartsWith(
            oldDefaultKeyDirectory, StringComparison.OrdinalIgnoreCase);
        // 上一版临时密钥放在 YFrame.update 目录，本版迁移到 YFrame.Pack 目录
        bool usesUpdateDefaultKey = KeyPathTextBox.Text.Contains(
            @"\YFrame.update\bin\", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(KeyPathTextBox.Text) ||
            usesOldDefaultKey ||
            usesUpdateDefaultKey)
        {
            GenerateTemporaryKey();
        }
        else
        {
            AppendLog("已载入本地配置。");
        }

        UpdateActualPath();
        SaveConfig();
    }

    /// <summary>
    /// 自动读取 Pack output 下最新的清单目录，并刷新父目录输入框。
    /// </summary>
    private void RefreshLatestOutput()
    {
        string latest = SignConfig.FindLatestOutputDirectory();
        if (string.IsNullOrWhiteSpace(latest))
        {
            AppendLog("未找到 Pack output 下的清单目录，保留当前父目录。");
            return;
        }

        if (!string.Equals(BaseDirectoryTextBox.Text.Trim(), latest,
                StringComparison.OrdinalIgnoreCase))
        {
            BaseDirectoryTextBox.Text = latest;
            AppendLog($"已自动定位最新输出目录：{latest}");
        }
    }

    /// <summary>把配置值写入界面控件。</summary>
    private void LoadConfigToUi()
    {
        BaseDirectoryTextBox.Text = _config.BaseDirectory;
        RelativeManifestTextBox.Text = _config.RelativeManifestPath;
        KeyPathTextBox.Text = _config.PrivateKeyPath;
    }

    /// <summary>从界面收集配置，不保存私钥内容。</summary>
    private SignConfig CollectConfig() => new()
    {
        BaseDirectory = BaseDirectoryTextBox.Text.Trim(),
        RelativeManifestPath = RelativeManifestTextBox.Text.Trim(),
        PrivateKeyPath = KeyPathTextBox.Text.Trim(),
        PublicKeyPath = _config.PublicKeyPath
    };

    /// <summary>保存本地配置；失败时只记录日志，不阻断签名。</summary>
    private void SaveConfig()
    {
        try
        {
            _config.BaseDirectory = BaseDirectoryTextBox.Text.Trim();
            _config.RelativeManifestPath = RelativeManifestTextBox.Text.Trim();
            _config.PrivateKeyPath = KeyPathTextBox.Text.Trim();
            _config.Save();
        }
        catch (Exception ex)
        {
            AppendLog($"配置保存失败：{ex.Message}");
        }
    }

    /// <summary>路径输入变化时刷新完整路径提示。</summary>
    private void PathInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_initializing)
            UpdateActualPath();
    }

    /// <summary>刷新实际清单路径和输出签名路径提示。</summary>
    private void UpdateActualPath()
    {
        if (TryResolveManifestPath(out string manifestPath))
        {
            ActualPathText.Text = $"清单：{manifestPath}    签名：{manifestPath}.sig";
            return;
        }

        ActualPathText.Text = "清单路径无效：请检查父目录和相对文件。";
    }

    /// <summary>
    /// 根据父目录和相对路径计算清单完整路径，并阻止越出父目录。
    /// </summary>
    /// <param name="manifestPath">计算出的清单路径</param>
    /// <returns>路径有效返回 true</returns>
    private bool TryResolveManifestPath(out string manifestPath)
    {
        manifestPath = string.Empty;

        string baseDirectory = BaseDirectoryTextBox.Text.Trim();
        string relativePath = RelativeManifestTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(relativePath))
            return false;

        try
        {
            string baseFull = Path.GetFullPath(baseDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string full = Path.GetFullPath(Path.Combine(baseFull, relativePath));
            string prefix = baseFull + Path.DirectorySeparatorChar;

            // 相对地址只能落在父目录内，避免通过 .. 读取任意文件
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            manifestPath = full;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>选择父目录。</summary>
    private void BrowseBaseDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择 YF_Version.json 所在目录",
            InitialDirectory = Directory.Exists(BaseDirectoryTextBox.Text)
                ? BaseDirectoryTextBox.Text
                : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog(this) == true)
        {
            BaseDirectoryTextBox.Text = dialog.FolderName;
            UpdateActualPath();
        }
    }

    /// <summary>选择清单文件，并转换为相对父目录的路径。</summary>
    private void BrowseManifest_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 YF_Version.json",
            Filter = "JSON 清单|*.json|所有文件|*.*",
            InitialDirectory = Directory.Exists(BaseDirectoryTextBox.Text)
                ? BaseDirectoryTextBox.Text
                : AppContext.BaseDirectory
        };

        if (dialog.ShowDialog(this) != true)
            return;

        string baseDirectory = BaseDirectoryTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(baseDirectory) || !Directory.Exists(baseDirectory))
        {
            baseDirectory = Path.GetDirectoryName(dialog.FileName) ?? AppContext.BaseDirectory;
            BaseDirectoryTextBox.Text = baseDirectory;
        }

        string baseFull = Path.GetFullPath(baseDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string fileFull = Path.GetFullPath(dialog.FileName);
        string relative = Path.GetRelativePath(baseFull, fileFull);

        if (relative.StartsWith("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
        {
            MessageBox.Show(this, "清单必须位于当前父目录内部，请重新选择父目录。",
                "路径不在父目录内", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RelativeManifestTextBox.Text = relative;
        UpdateActualPath();
    }

    /// <summary>选择私钥 PEM 文件。</summary>
    private void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 ECDSA 私钥 PEM",
            Filter = "PEM 私钥|*.pem;*.key|所有文件|*.*",
            InitialDirectory = Directory.Exists(AppContext.BaseDirectory)
                ? AppContext.BaseDirectory
                : null
        };

        if (dialog.ShowDialog(this) == true)
        {
            KeyPathTextBox.Text = dialog.FileName;
            UpdateActualPath();
        }
    }

    /// <summary>生成一份临时 ECDSA P-256 密钥对，并自动填入私钥路径。</summary>
    private void GenerateKey_Click(object sender, RoutedEventArgs e)
    {
        GenerateTemporaryKey();
    }

    /// <summary>执行临时密钥生成流程。</summary>
    private void GenerateTemporaryKey()
    {
        try
        {
            // 默认与 YFrame.Pack.exe 放在同一目录，便于预览签名流程
            string keyDirectory = SignConfig.FindPackDirectory();
            var (privatePath, publicPath) = ManifestSigner.GenerateTemporaryKey(keyDirectory);

            KeyPathTextBox.Text = privatePath;
            _config.PublicKeyPath = publicPath;
            SetStatus("临时密钥已生成");
            AppendLog($"已生成临时私钥：{privatePath}");
            AppendLog($"已生成临时公钥：{publicPath}");
            AppendLog("临时密钥仅用于本地预览，请不要用于生产发布。");
            SaveConfig();
        }
        catch (Exception ex)
        {
            SetStatus("密钥生成失败");
            AppendLog($"生成临时密钥失败：{ex.Message}");
            MessageBox.Show(this, $"生成临时密钥失败：\n{ex.Message}",
                "YFrame Sign", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>读取清单原始字节并生成 .sig 文件。</summary>
    private void Sign_Click(object sender, RoutedEventArgs e)
    {
        if (!TryResolveManifestPath(out string manifestPath))
        {
            MessageBox.Show(this, "清单路径无效，请检查父目录和相对文件。",
                "路径错误", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!File.Exists(manifestPath))
        {
            MessageBox.Show(this, $"清单不存在：\n{manifestPath}",
                "文件不存在", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string keyPath = KeyPathTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        {
            MessageBox.Show(this, "请选择有效的 ECDSA 私钥 PEM。",
                "缺少私钥", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetStatus("正在签名...");
            SignatureResult result = ManifestSigner.Sign(manifestPath, keyPath);

            SetStatus("签名完成");
            AppendLog($"清单：{manifestPath}");
            AppendLog($"SHA-256：{result.ManifestSha256}");
            AppendLog($"签名文件：{result.SignaturePath}");
            AppendLog($"密钥长度：{result.KeySize} bit");
            AppendLog($"签名 Base64：{result.SignatureBase64}");
            SaveConfig();
            UpdateActualPath();
        }
        catch (Exception ex)
        {
            SetStatus("签名失败");
            AppendLog($"签名失败：{ex.Message}");
            MessageBox.Show(this, $"签名失败：\n{ex.Message}",
                "YFrame Sign", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>打开清单所在目录。</summary>
    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        string directory;
        if (TryResolveManifestPath(out string manifestPath))
            directory = Path.GetDirectoryName(manifestPath) ?? AppContext.BaseDirectory;
        else
            directory = BaseDirectoryTextBox.Text.Trim();

        if (!Directory.Exists(directory))
        {
            MessageBox.Show(this, "清单目录不存在。", "YFrame Sign",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
        {
            UseShellExecute = true
        });
    }

    /// <summary>清空日志。</summary>
    private void ClearLog_Click(object sender, RoutedEventArgs e)
        => LogTextBox.Clear();

    /// <summary>追加带时间的日志并滚动到末尾。</summary>
    /// <param name="message">日志内容</param>
    private void AppendLog(string message)
        => LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    /// <summary>更新状态栏文本。</summary>
    /// <param name="message">状态内容</param>
    private void SetStatus(string message)
        => StatusText.Text = message;

    /// <summary>日志变化后自动滚动到末尾。</summary>
    private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
            textBox.ScrollToEnd();
    }

    /// <summary>拖动标题栏移动窗口。</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || WindowState == WindowState.Maximized)
            return;

        if (IsInsideButton(e.OriginalSource as DependencyObject))
            return;

        DragMove();
    }

    /// <summary>判断事件源是否位于按钮内，避免拖动拦截按钮点击。</summary>
    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button)
                return true;

            source = source is Visual
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return false;
    }

    /// <summary>最小化窗口。</summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    /// <summary>关闭窗口。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Close();
}
