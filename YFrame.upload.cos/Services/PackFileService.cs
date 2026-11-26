using System.IO;
using YFrame.upload.cos.Models;

namespace YFrame.upload.cos.Services;

/// <summary>
/// 扫描 YFrame.Pack 输出目录，定位最新一个包含四个发布文件的子目录。
/// </summary>
public sealed class PackFileService
{
    private static readonly string[] RequiredFiles =
    {
        "current.zip",
        "increment.zip",
        "YF_Version.json.sig",
        "YF_Version.json"
    };

    /// <summary>查找最新完整 Pack 目录并生成待上传文件。</summary>
    /// <param name="outputRoot">Pack 输出根目录</param>
    /// <param name="environment">uat 或 pdt</param>
    /// <returns>Pack 目录解析结果</returns>
    public PackDirectoryResult FindLatest(string outputRoot, string environment)
    {
        if (string.IsNullOrWhiteSpace(outputRoot) || !Directory.Exists(outputRoot))
            return PackDirectoryResult.Fail("Pack 输出目录不存在。");

        string env = string.Equals(environment, "pdt", StringComparison.OrdinalIgnoreCase)
            ? "pdt"
            : "uat";

        // 允许用户直接选中带四个文件的时间戳目录，也允许选中 output 根目录
        if (ContainsAllFiles(outputRoot))
            return BuildResult(outputRoot, env);

        foreach (string directory in Directory.GetDirectories(outputRoot)
                     .OrderByDescending(Directory.GetLastWriteTime)
                     .ThenByDescending(Path.GetFileName))
        {
            if (ContainsAllFiles(directory))
                return BuildResult(directory, env);
        }

        return PackDirectoryResult.Fail(
            "未找到包含 current.zip、increment.zip、YF_Version.json、YF_Version.json.sig 的目录。");
    }

    /// <summary>判断目录是否包含四个必需文件。</summary>
    /// <param name="directory">目录路径</param>
    /// <returns>是否完整</returns>
    private static bool ContainsAllFiles(string directory)
        => RequiredFiles.All(name => File.Exists(Path.Combine(directory, name)));

    /// <summary>构建文件列表。</summary>
    /// <param name="directory">时间戳目录</param>
    /// <param name="environment">发布环境</param>
    /// <returns>解析结果</returns>
    private static PackDirectoryResult BuildResult(string directory, string environment)
    {
        var files = new List<PackFileItem>();
        foreach (string name in RequiredFiles)
        {
            string path = Path.Combine(directory, name);
            long length = new FileInfo(path).Length;
            files.Add(new PackFileItem
            {
                Name = name,
                FullPath = path,
                Length = length,
                ObjectKey = $"YFrame/update/{environment}/{name}"
            });
        }

        return new PackDirectoryResult
        {
            DirectoryPath = directory,
            Files = files
        };
    }
}

/// <summary>Pack 目录解析结果。</summary>
public sealed class PackDirectoryResult
{
    /// <summary>选中的时间戳目录。</summary>
    public string? DirectoryPath { get; init; }

    /// <summary>四个待上传文件。</summary>
    public IReadOnlyList<PackFileItem> Files { get; init; } = Array.Empty<PackFileItem>();

    /// <summary>错误文本；成功时为空。</summary>
    public string? Error { get; init; }

    /// <summary>是否成功。</summary>
    public bool Success => Error == null && DirectoryPath != null && Files.Count == 4;

    /// <summary>创建失败结果。</summary>
    /// <param name="error">错误文本</param>
    /// <returns>失败结果</returns>
    public static PackDirectoryResult Fail(string error)
        => new() { Error = error };
}
