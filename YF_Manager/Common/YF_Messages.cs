namespace YF_Manager
{
    /// <summary>
    /// 追加日志消息 — 任意组件可发送此消息将文本显示到日志面板
    /// </summary>
    public record LogAppendMessage(string Text);

    /// <summary>
    /// 清除日志消息 — 请求清空日志面板
    /// </summary>
    public record LogClearMessage();

    /// <summary>
    /// 插件已显示消息 — 当用户切换显示的插件时发送
    /// 接收者：热键路由服务、日志服务等需要知道当前激活插件的组件
    /// </summary>
    public record PluginShownMessage(string PluginId);

    /// <summary>
    /// 热键触发消息 — 当 Ctrl+Y 全局热键被按下时发送
    /// </summary>
    public record HotkeyTriggeredMessage();

    /// <summary>
    /// 脚本命令消息 — 新建/打开/保存脚本操作
    /// Command 取值: "New", "Open", "Save"
    /// </summary>
    public record ScriptCommandMessage(string Command);

    /// <summary>
    /// 面板切换消息 — 左侧/右侧面板标签页切换
    /// Side 取值: "Left", "Right"; TabIndex: 标签页索引
    /// </summary>
    public record PanelSwitchMessage(string Side, int TabIndex);

    /// <summary>
    /// 主题切换消息 — 用户切换主题时发送
    /// ThemeName 为中文主题名
    /// </summary>
    public record ThemeChangedMessage(string ThemeName, string ThemePath);

    /// <summary>
    /// 语言切换消息 — 用户切换语言时发送
    /// LangCode 取值: "zh", "en"
    /// </summary>
    public record LanguageChangedMessage(string LangCode);
}
