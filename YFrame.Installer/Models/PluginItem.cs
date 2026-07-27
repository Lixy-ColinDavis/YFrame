namespace YFrame.Installer.Models;

public class PluginItem : ViewModels.ViewModelBase
{
    private bool _isSelected = true;
    private bool _isRequired;

    public string DirectoryName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string SizeDisplay { get; set; } = string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsRequired
    {
        get => _isRequired;
        set => SetProperty(ref _isRequired, value);
    }
}
