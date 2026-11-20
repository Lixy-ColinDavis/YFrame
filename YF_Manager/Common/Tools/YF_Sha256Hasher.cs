using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YF_Manager
{
    /// <summary>
    /// SHA-256 哈希计算工具，支持字符串、文件和流三种来源。
    /// 文件采用分块读取，避免一次性把大文件加载到内存；返回小写十六进制。
    /// </summary>
    public static class YF_Sha256Hasher
    {
        /// <summary>文件读取缓冲区大小（1MB），兼顾大文件性能与内存占用。</summary>
        private const int BufferSize = 1024 * 1024;

        /// <summary>
        /// 计算字符串内容的 SHA-256。
        /// </summary>
        /// <param name="content">要计算哈希的文本内容</param>
        /// <returns>64 位小写十六进制 SHA-256；内容为空时返回空串</returns>
        public static string ComputeString(string? content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
            return ToHex(hash);
        }

        /// <summary>
        /// 同步计算文件的 SHA-256。
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <returns>64 位小写十六进制 SHA-256</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static string ComputeFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("计算 SHA-256 失败：文件不存在", filePath);

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, useAsync: false);
            return ComputeStream(stream);
        }

        /// <summary>
        /// 异步分块计算文件的 SHA-256，可报告进度并支持取消。
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="progress">进度回调（0.0 ~ 1.0），可为 null</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>64 位小写十六进制 SHA-256</returns>
        /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
        public static async Task<string> ComputeFileAsync(
            string filePath,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("计算 SHA-256 失败：文件不存在", filePath);

            using var sha256 = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, useAsync: true);

            long totalBytes = stream.Length;
            if (totalBytes == 0)
            {
                progress?.Report(1.0);
                return ToHex(sha256.ComputeHash(Array.Empty<byte>()));
            }

            byte[] buffer = new byte[BufferSize];
            long processed = 0;
            int bytesRead;

            // 分块读取并喂给哈希累加器，避免大文件一次性占用内存
            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                processed += bytesRead;
                progress?.Report((double)processed / totalBytes);
            }

            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            progress?.Report(1.0);
            return ToHex(sha256.Hash!);
        }

        /// <summary>
        /// 计算一个已打开的流的 SHA-256。
        /// 不关闭、不重置调用方的流，从当前位置开始读取。
        /// </summary>
        /// <param name="stream">待计算的流</param>
        /// <returns>64 位小写十六进制 SHA-256</returns>
        public static string ComputeStream(Stream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var sha256 = SHA256.Create();
            return ToHex(sha256.ComputeHash(stream));
        }

        /// <summary>
        /// 校验文件的 SHA-256 是否等于期望值。
        /// </summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="expectedSha256">期望的 SHA-256（大小写不敏感）</param>
        /// <returns>一致返回 true；文件不存在或哈希不一致返回 false</returns>
        public static bool VerifyFile(string filePath, string expectedSha256)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256))
                return false;

            try
            {
                string actual = ComputeFile(filePath);
                return string.Equals(actual, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        /// <summary>把哈希字节转换为小写十六进制。</summary>
        /// <param name="hash">SHA-256 哈希字节</param>
        /// <returns>64 位小写十六进制字符串</returns>
        private static string ToHex(byte[] hash)
            => Convert.ToHexString(hash).ToLowerInvariant();
    }
}
