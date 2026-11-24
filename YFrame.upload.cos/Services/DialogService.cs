using System.Windows;
using Microsoft.Win32;

namespace YFrame.upload.cos.Services;

/// <summary>上传器弹窗服务接口，便于 ViewModel 与具体 UI 解耦。</summary>
public interface IDialogService
{
    /// <summary>选择文件夹。</summary>
    /// <param name="title">标题</param>
    /// <param name="initialDirectory">初始目录</param>
    /// <returns>选中的目录；取消时返回 null</returns>
    string? PickFolder(string title, string? initialDirectory);

    /// <summary>选择文件。</summary>
    /// <param name="title">标题</param>
    /// <param name="filter">文件过滤器</param>
    /// <param name="initialDirectory">初始目录</param>
    /// <returns>选中的文件；取消时返回 null</returns>
    string? PickFile(string title, string filter, string? initialDirectory);

    /// <summary>显示错误。</summary>
    /// <param name="title">标题</param>
    /// <param name="message">内容</param>
    void ShowError(string title, string message);
}

/// <summary>基于 WPF 对话框的实现。</summary>
public sealed class DialogService : IDialogService
{
    /// <summary>选择文件夹。</summary>
    public string? PickFolder(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory ?? string.Empty
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <summary>选择文件。</summary>
    public string? PickFile(string title, string filter, string? initialDirectory)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = filter,
            CheckFileExists = true,
            InitialDirectory = initialDirectory ?? string.Empty
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>显示错误。</summary>
    public void ShowError(string title, string message)
        => MessageBox.Show(Application.Current.MainWindow, message, title,
            MessageBoxButton.OK, MessageBoxImage.Warning);
}
