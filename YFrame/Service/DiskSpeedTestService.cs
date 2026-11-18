using System;
using System.Diagnostics;
using System.IO;

namespace YFrame.Service
{
    /// <summary>
    /// 磁盘顺序读写测速服务：在系统临时目录写入 32MB 临时文件并读回，
    /// 统计写入/读取吞吐量。仅用于当前磁盘的粗略性能参考。
    /// </summary>
    public static class DiskSpeedTestService
    {
        private const int BlockSize = 1024 * 1024;
        private const int SizeMb = 32;

        /// <summary>
        /// 在系统临时目录执行一次顺序写/读测速。
        /// </summary>
        public static bool Run(out double readMBps, out double writeMBps, out string error)
            => Run(null, out readMBps, out writeMBps, out error);

        /// <summary>
        /// 在指定卷根目录执行一次顺序写/读测速，结束后删除临时文件。
        /// </summary>
        /// <param name="volumeRoot">目标卷根目录，例如 C:\ 或 D:\；为空时使用系统临时目录</param>
        /// <param name="readMBps">读取速度（MB/s）</param>
        /// <param name="writeMBps">写入速度（MB/s）</param>
        /// <param name="error">失败说明</param>
        /// <returns>是否成功</returns>
        public static bool Run(string? volumeRoot, out double readMBps, out double writeMBps, out string error)
        {
            readMBps = 0;
            writeMBps = 0;
            error = string.Empty;
            string path;

            try
            {
                string directory = ResolveTestDirectory(volumeRoot);
                path = Path.Combine(directory, $"YFrame_disk_speed_{Guid.NewGuid():N}.tmp");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            try
            {
                byte[] buffer = new byte[BlockSize];
                new Random(20261116).NextBytes(buffer);

                // WriteThrough 绕过部分写缓存，结果更接近真实写入
                using (var writeStream = new FileStream(path, FileMode.Create, FileAccess.Write,
                           FileShare.None, BlockSize, FileOptions.WriteThrough | FileOptions.SequentialScan))
                {
                    var stopwatch = Stopwatch.StartNew();
                    for (int i = 0; i < SizeMb; i++)
                        writeStream.Write(buffer, 0, buffer.Length);
                    writeStream.Flush(true);
                    stopwatch.Stop();
                    writeMBps = SizeMb / stopwatch.Elapsed.TotalSeconds;
                }

                using (var readStream = new FileStream(path, FileMode.Open, FileAccess.Read,
                           FileShare.Read, BlockSize, FileOptions.SequentialScan))
                {
                    var stopwatch = Stopwatch.StartNew();
                    long total = 0;
                    int read;
                    while ((read = readStream.Read(buffer, 0, buffer.Length)) > 0)
                        total += read;
                    stopwatch.Stop();
                    readMBps = total / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch
                {
                    // 临时文件删除失败不影响测速结果
                }
            }
        }

        /// <summary>
        /// 选择测速临时目录：系统盘使用系统临时目录，其他盘使用对应卷根目录。
        /// </summary>
        /// <param name="volumeRoot">目标卷根目录，可为空</param>
        /// <returns>可写临时目录</returns>
        private static string ResolveTestDirectory(string? volumeRoot)
        {
            if (string.IsNullOrWhiteSpace(volumeRoot))
                return Path.GetTempPath();

            string root = Path.GetFullPath(volumeRoot);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"磁盘根目录不存在：{root}");

            string? systemRoot = Path.GetPathRoot(Path.GetTempPath());
            if (!string.IsNullOrEmpty(systemRoot) &&
                string.Equals(root.TrimEnd('\\'), systemRoot.TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetTempPath();
            }

            return root;
        }
    }
}
