namespace YFrame.Model;

/// <summary>
/// 工作任务画布的统一布局参数。
/// PaddingX/PaddingY 是新建流程/自动布局的默认起点，让步骤默认贴近可视区域左上角；
/// 拖动时通过视口自动平移获得更大的左/上移动范围，不把默认起点当成拖动下限。
/// </summary>
public static class WorkflowLayout
{
    /// <summary>新建流程默认起点 X。</summary>
    public const double PaddingX = 60;

    /// <summary>新建流程默认起点 Y。</summary>
    public const double PaddingY = 60;

    /// <summary>节点默认宽度。</summary>
    public const double NodeWidth = 190;

    /// <summary>节点默认高度。</summary>
    public const double NodeHeight = 72;

    /// <summary>节点水平间距。</summary>
    public const double GapX = 66;

    /// <summary>节点垂直间距。</summary>
    public const double GapY = 62;
}
