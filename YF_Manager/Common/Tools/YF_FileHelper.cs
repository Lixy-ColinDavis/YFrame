using System;
using System.IO;

namespace YF_Manager
{
    public class YF_FileHelper
    {
        // AOP 日志拦截，采用 ProxyGenerator 模式：
        private static readonly Lazy<YF_FileHelper> _instance =
            new Lazy<YF_FileHelper>(() =>
                new Castle.DynamicProxy.ProxyGenerator()
                    .CreateClassProxy<YF_FileHelper>(new LogInterceptor()));

        public static YF_FileHelper Instance => _instance.Value;

        public YF_FileHelper() { }

        /// <summary>
        /// 剪贴板写入重试次数
        /// </summary>
        private const int ClipboardRetryCount = 2;

        /// <summary>
        /// 递归复制目录
        /// </summary>
        /// <returns>复制是否成功</returns>
        [Log(Level = LogLevel.Info, Message = "递归复制目录")]
        public virtual bool CopyDirectory(string sourceDir, string destDir)
        {
            if (string.IsNullOrEmpty(sourceDir))
            {
                YF_Manager_Main.logger?.ErrorInfo("CopyDirectory", "sourceDir 为空");
                return false;
            }
            if (string.IsNullOrEmpty(destDir))
            {
                YF_Manager_Main.logger?.ErrorInfo("CopyDirectory", "destDir 为空");
                return false;
            }
            if (!Directory.Exists(sourceDir))
            {
                YF_Manager_Main.logger?.ErrorInfo("CopyDirectory", $"源目录不存在: {sourceDir}");
                return false;
            }

            try
            {
                Directory.CreateDirectory(destDir);
                foreach (var file in Directory.GetFiles(sourceDir))
                    File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
                foreach (var dir in Directory.GetDirectories(sourceDir))
                    CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
                return true;
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("CopyDirectory", "递归复制目录异常 " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 带重试的剪贴板写入，解决 CLIPBRD_E_CANT_OPEN 异常
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "剪贴板写入")]
        public virtual void SetClipboardWithRetry(string text)
        {
            for (int i = 0; i < ClipboardRetryCount; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch (System.Runtime.InteropServices.COMException ex)
                {
                    System.Threading.Thread.Sleep(20);
                    YF_Manager_Main.logger?.ErrorInfo("SetClipboardWithRetry", "剪贴板复制异常 " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 打开指定文件夹（如不存在则创建后打开）
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "打开文件夹")]
        public virtual void OpenFolder(string absolutePath)
        {
            try
            {
                if (!Directory.Exists(absolutePath))
                    Directory.CreateDirectory(absolutePath);
                System.Diagnostics.Process.Start("explorer.exe", absolutePath);
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("OpenFolder", "打开文件夹异常 " + ex.Message);
            }
        }
    }
}
