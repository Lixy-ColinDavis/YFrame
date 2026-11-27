using System;
using System.IO;
using System.IO.Compression;

namespace YF_Manager
{
    /// <summary>
    /// ZIP 压缩 / 解压工具类（AOP 单例）
    /// 供框架及插件统一调用，避免各模块重复实现
    /// </summary>
    public class YF_ZipHelper
    {
        #region AOP 单例

        /// <summary>
        /// 单例模式 + AOP 日志拦截代理
        /// </summary>
        private static readonly Lazy<YF_ZipHelper> _instance =
            new Lazy<YF_ZipHelper>(() =>
                new Castle.DynamicProxy.ProxyGenerator()
                    .CreateClassProxy<YF_ZipHelper>(new LogInterceptor()));

        public static YF_ZipHelper Instance => _instance.Value;

        public YF_ZipHelper() { }

        #endregion

        #region 压缩

        /// <summary>
        /// 将指定目录压缩为 ZIP 文件
        /// </summary>
        /// <param name="sourceDir">源目录路径</param>
        /// <param name="destinationZip">目标 ZIP 文件路径（含 .zip 扩展名）</param>
        /// <param name="includeBaseDirectory">是否在 ZIP 中包含源目录根文件夹（默认 false，仅打包内部文件）</param>
        /// <returns>压缩成功返回 true，失败返回 false</returns>
        [Log(Level = LogLevel.Info, Message = "压缩目录为 ZIP")]
        public virtual bool CompressDirectory(string sourceDir, string destinationZip, bool includeBaseDirectory = false)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                return false;

            try
            {
                // 删除可能存在的旧文件
                if (File.Exists(destinationZip))
                    File.Delete(destinationZip);

                // 确保目标目录存在
                var destDir = Path.GetDirectoryName(destinationZip);                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                    Directory.CreateDirectory(destDir);

                ZipFile.CreateFromDirectory(sourceDir, destinationZip, CompressionLevel.Optimal, includeBaseDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 解压

        /// <summary>
        /// 将 ZIP 文件解压到指定目录。
        /// 失败时抛出异常，不再静默返回 false。
        /// 插件安装请使用带路径校验和 staging 的专用安装器。
        /// </summary>
        /// <param name="zipPath">ZIP 文件路径</param>
        /// <param name="destinationDir">目标解压目录（不存在则自动创建）</param>
        /// <returns>解压成功返回 true；失败抛出异常</returns>
        /// <exception cref="FileNotFoundException">ZIP 文件不存在</exception>
        /// <exception cref="InvalidDataException">ZIP 损坏、路径非法或解压失败</exception>
        [Obsolete("ExtractToDirectory 不再静默失败，请改用带路径校验和 staging 的插件安装器。")]
        [Log(Level = LogLevel.Info, Message = "解压 ZIP 文件")]
        public virtual bool ExtractToDirectory(string zipPath, string destinationDir)
        {
            if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                throw new FileNotFoundException("解压 ZIP 失败：文件不存在。", zipPath);
            if (string.IsNullOrWhiteSpace(destinationDir))
                throw new ArgumentException("解压目标目录不能为空。", nameof(destinationDir));

            try
            {
                // 确保目标目录存在
                if (!Directory.Exists(destinationDir))
                    Directory.CreateDirectory(destinationDir);

                ZipFile.ExtractToDirectory(zipPath, destinationDir, overwriteFiles: true);
                return true;
            }
            catch (Exception ex) when (ex is not InvalidDataException)
            {
                // 统一转换为解压失败异常，保留原始异常便于定位
                throw new InvalidDataException("解压 ZIP 失败。", ex);
            }
        }

        #endregion
    }
}
