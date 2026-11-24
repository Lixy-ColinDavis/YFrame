using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YFrame.upload.cos.ViewModels;

/// <summary>
/// MVVM 属性通知基类。
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    /// <summary>属性变化事件。</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>通知属性变化。</summary>
    /// <param name="propertyName">属性名</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>设置字段并在值变化时通知。</summary>
    /// <typeparam name="T">字段类型</typeparam>
    /// <param name="field">字段引用</param>
    /// <param name="value">新值</param>
    /// <param name="propertyName">属性名</param>
    /// <returns>是否变化</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
