using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace YFrame.Installer.Services;

public static class PayloadExtractor
{
    private const string PayloadResourceName = "YFrame.Installer.Resources.payload.zip";
    private static string? _extractedPath;

    /// <summary>
    /// 获取 payload 根目录路径（优先从嵌入资源解压，文件系统为备用方案）
    /// </summary>
    public static string GetPayloadPath()
    {
        if (_extractedPath != null && Directory.Exists(_extractedPath))
            return _extractedPath;

        // 1. 优先从嵌入资源提取 payload.zip（Debug 和 Release 均嵌入）
        try
        {
            ExtractFromResource();
            if (_extractedPath != null && Directory.Exists(_extractedPath))
                return _extractedPath;
        }
        catch
        {
            // 嵌入资源提取失败，尝试文件系统后备方案
        }

        // 2. exe 同目录下的 payload/（备用：手动放置场景）
        var exeDir = AppContext.BaseDirectory;
        var filePayload = Path.Combine(exeDir, "payload");
        if (Directory.Exists(filePayload))
        {
            _extractedPath = filePayload;
            return filePayload;
        }

        // 3. 向上查找项目源码目录下的 payload/（备用：VS 直接运行场景）
        var baseDir = exeDir;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(baseDir, "payload");
            if (Directory.Exists(candidate))
            {
                _extractedPath = candidate;
                return candidate;
            }
            var parent = Path.GetDirectoryName(baseDir);
            if (parent == null || parent == baseDir) break;
            baseDir = parent;
        }

        // 4. 兜底：在 exe 目录创建空 payload 目录
        _extractedPath = filePayload;
        Directory.CreateDirectory(filePayload);
        return filePayload;
    }

    /// <summary>
    /// 从嵌入资源解压 payload.zip 到临时目录
    /// </summary>
    private static void ExtractFromResource()
    {
        var assembly = Assembly.GetExecutingAssembly();

        Stream? stream = assembly.GetManifestResourceStream(PayloadResourceName);
        if (stream == null)
        {
            var names = assembly.GetManifestResourceNames();
            var match = names.FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
            if (match != null)
                stream = assembly.GetManifestResourceStream(match);
        }

        if (stream == null)
            throw new InvalidOperationException("Embedded payload.zip not found. Resources: " + string.Join(", ", assembly.GetManifestResourceNames()));

        var tempDir = Path.Combine(Path.GetTempPath(), "YFrame_Setup_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        using (stream)
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            archive.ExtractToDirectory(tempDir, true);
        }

        _extractedPath = tempDir;
    }

    /// <summary>
    /// 清理临时目录
    /// </summary>
    public static void Cleanup()
    {
        if (_extractedPath != null && _extractedPath.StartsWith(Path.GetTempPath()) && Directory.Exists(_extractedPath))
        {
            try { Directory.Delete(_extractedPath, true); } catch { }
        }
    }
}
