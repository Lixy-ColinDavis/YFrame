using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using Microsoft.Win32;

namespace YFrame.upload.cos;

/// <summary>
/// COS 简单上传 Demo：只上传单个 YF_Version.json 到 UAT/PDT 固定对象键。
/// </summary>
public partial class MainWindow : Window
{
    private const string BucketName = "yfcos-1256976024";
    private const string RegionName = "ap-beijing";

    /// <summary>初始化上传窗口。</summary>
    public MainWindow()
    {
        InitializeComponent();
        BucketBox.Text = BucketName;
        RegionBox.Text = RegionName;
        UpdateObjectKey();
    }

    /// <summary>拖动自定义标题栏。</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    /// <summary>关闭窗口。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>选择本地 YF_Version.json。</summary>
    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择 YF_Version.json",
            Filter = "YF_Version.json|YF_Version.json|JSON 文件|*.json|所有文件|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            FilePathBox.Text = dialog.FileName;
            AppendLog($"已选择文件：{dialog.FileName}");
            StatusText.Text = "文件已选择";
        }
    }

    /// <summary>环境变化时刷新对象键。</summary>
    private void EnvironmentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ObjectKeyText != null)
            UpdateObjectKey();
    }

    /// <summary>上传单个 YF_Version.json。</summary>
    private async void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        string filePath = FilePathBox.Text.Trim();
        string bucket = BucketBox.Text.Trim();
        string region = RegionBox.Text.Trim();
        string secretId = SecretIdBox.Text.Trim();
        string secretKey = SecretKeyBox.Password;
        string environment = GetEnvironment();

        if (!ValidateInput(filePath, bucket, region, secretId, secretKey))
            return;

        string objectKey = BuildObjectKey(environment);
        UploadButton.IsEnabled = false;
        UploadProgress.Value = 0;
        StatusText.Text = "正在上传...";
        AppendLog($"开始上传：{filePath}");
        AppendLog($"目标：cos://{bucket}/{objectKey}");

        try
        {
            // 先本地校验 JSON，避免把损坏的清单上传到 COS
            using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(filePath)))
            {
                AppendLog($"JSON 根节点：{document.RootElement.ValueKind}");
            }

            var config = new CosXmlConfig.Builder()
                .SetRegion(region)
                .Build();
            QCloudCredentialProvider provider = new DefaultQCloudCredentialProvider(secretId, secretKey, 600);
            var cosXml = new CosXmlServer(config, provider);

            var request = new PutObjectRequest(bucket, objectKey, filePath);
            request.SetCosProgressCallback((completed, total) =>
            {
                double percent = total <= 0 ? 0 : completed * 100.0 / total;
                Dispatcher.BeginInvoke(() =>
                {
                    UploadProgress.Value = percent;
                    StatusText.Text = $"正在上传 {percent:0.0}%";
                });
            });

            // SDK 的 PutObject 是同步阻塞调用，放到后台线程避免卡住 UI
            PutObjectResult result = await Task.Run(() => cosXml.PutObject(request));

            UploadProgress.Value = 100;
            StatusText.Text = "上传成功";
            AppendLog("上传成功。");
            AppendLog(result.GetResultInfo());
        }
        catch (COSXML.CosException.CosClientException ex)
        {
            StatusText.Text = "上传失败";
            AppendLog($"客户端异常：{ex.Message}");
        }
        catch (COSXML.CosException.CosServerException ex)
        {
            StatusText.Text = "上传失败";
            AppendLog($"服务端异常：{ex.GetInfo()}");
        }
        catch (Exception ex)
        {
            StatusText.Text = "上传失败";
            AppendLog($"异常：{ex.Message}");
        }
        finally
        {
            UploadButton.IsEnabled = true;
        }
    }

    /// <summary>校验界面输入。</summary>
    /// <param name="filePath">本地文件路径</param>
    /// <param name="bucket">存储桶</param>
    /// <param name="region">地域</param>
    /// <param name="secretId">SecretId</param>
    /// <param name="secretKey">SecretKey</param>
    /// <returns>输入是否完整有效</returns>
    private bool ValidateInput(string filePath, string bucket, string region, string secretId, string secretKey)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            ShowValidationError("请先选择 YF_Version.json 文件。");
            return false;
        }

        if (!string.Equals(Path.GetFileName(filePath), "YF_Version.json", StringComparison.OrdinalIgnoreCase))
        {
            ShowValidationError("当前 Demo 只允许上传名为 YF_Version.json 的文件。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(region))
        {
            ShowValidationError("Bucket 和 Region 不能为空。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(secretId) || string.IsNullOrWhiteSpace(secretKey))
        {
            ShowValidationError("SecretId 和 SecretKey 不能为空。");
            return false;
        }

        return true;
    }

    /// <summary>获取当前选择的环境。</summary>
    private string GetEnvironment()
        => (EnvironmentBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Trim().ToLowerInvariant() ?? "uat";

    /// <summary>刷新对象键预览。</summary>
    private void UpdateObjectKey()
        => ObjectKeyText.Text = BuildObjectKey(GetEnvironment());

    /// <summary>生成 COS 对象键。</summary>
    /// <param name="environment">uat 或 pdt</param>
    /// <returns>对象键</returns>
    private static string BuildObjectKey(string environment)
        => $"YFrame/update/{environment}/YF_Version.json";

    /// <summary>显示输入校验错误。</summary>
    private void ShowValidationError(string message)
    {
        StatusText.Text = "输入不完整";
        AppendLog(message);
        MessageBox.Show(this, message, "上传参数", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>向日志区域追加一行。</summary>
    private void AppendLog(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        LogBox.ScrollToEnd();
    }
}
