using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YF_Manager.Common
{
    public class YF_FileHelper
    {
        /// <summary>
        /// 递归复制目录
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "递归复制目录")]
        public virtual void CopyDirectory(string sourceDir, string destDir)
        {
            try
            {
                Directory.CreateDirectory(destDir);
                foreach (var file in Directory.GetFiles(sourceDir))
                    File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
                foreach (var dir in Directory.GetDirectories(sourceDir))
                    CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
            }
            catch (Exception ex)
            {
                YF_Manager_Main.logger?.ErrorInfo("CopyDirectory", "递归复制目录异常 " + ex.Message);
            }
        }

        /// <summary>
        /// 带重试的剪贴板写入，解决 CLIPBRD_E_CANT_OPEN 异常
        /// </summary>
        [Log(Level = LogLevel.Info, Message = "剪贴板写入")]
        public virtual void SetClipboardWithRetry(string text)
        {
            for (int i = 0; i < 2; i++)
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

        [Log(Level = LogLevel.Info, Message = "打开文件夹")]
        public virtual void OpenFolder(string AbsolutePath)
        {
            string absPath = AbsolutePath;
            if (!Directory.Exists(absPath))
                Directory.CreateDirectory(absPath);
            System.Diagnostics.Process.Start("explorer.exe", absPath);
        }
    }
}
