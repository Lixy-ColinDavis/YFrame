using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using YF_Manager;

namespace YFrame.Model;

/// <summary>
/// 工作任务中的一条流程定义。参考 FlowBoard：流程标题、节点、连线。
/// </summary>
public sealed class WorkflowDefinition : ViewModelBase
{
    private string _id = Guid.NewGuid().ToString("N");
    private string _name = string.Empty;
    private int _nextNodeId = 1;

    /// <summary>流程唯一标识。</summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>流程名称，显示在左侧工作任务列表。</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                OnPropertyChanged(nameof(ProgressText));
        }
    }

    /// <summary>下一个节点编号，避免新增节点 ID 重复。</summary>
    public int NextNodeId
    {
        get => _nextNodeId;
        set => SetProperty(ref _nextNodeId, value);
    }

    /// <summary>流程节点。</summary>
    public ObservableCollection<WorkflowNode> Nodes { get; set; } = new();

    /// <summary>流程连线。</summary>
    public ObservableCollection<WorkflowEdge> Edges { get; set; } = new();

    /// <summary>左侧列表显示的完成进度，例如 2/5。</summary>
    [JsonIgnore]
    public string ProgressText
    {
        get
        {
            int completed = Nodes.Count(node => node.Done);
            return $"{completed}/{Nodes.Count}";
        }
    }

    /// <summary>刷新进度文本。</summary>
    public void RefreshProgress() => OnPropertyChanged(nameof(ProgressText));
}

/// <summary>流程中的一个步骤节点。</summary>
public sealed class WorkflowNode : ViewModelBase
{
    private string _id = string.Empty;
    private string _text = string.Empty;
    private string _note = string.Empty;
    private bool _done;
    private double _x;
    private double _y;

    /// <summary>节点 ID，例如 n1。</summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>步骤内容，显示在节点标题。</summary>
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    /// <summary>步骤备注，显示在节点下方。</summary>
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    /// <summary>是否完成。</summary>
    public bool Done
    {
        get => _done;
        set => SetProperty(ref _done, value);
    }

    /// <summary>画布 X 坐标。</summary>
    public double X
    {
        get => _x;
        set => SetProperty(ref _x, value);
    }

    /// <summary>画布 Y 坐标。</summary>
    public double Y
    {
        get => _y;
        set => SetProperty(ref _y, value);
    }
}

/// <summary>流程中的一条连线。方向使用 top/right/bottom/left。</summary>
public sealed class WorkflowEdge : ViewModelBase
{
    private string _from = string.Empty;
    private string _to = string.Empty;
    private string _fromDir = "right";
    private string _toDir = "left";

    /// <summary>起点节点 ID。</summary>
    public string From
    {
        get => _from;
        set => SetProperty(ref _from, value);
    }

    /// <summary>终点节点 ID。</summary>
    public string To
    {
        get => _to;
        set => SetProperty(ref _to, value);
    }

    /// <summary>起点端口方向。</summary>
    public string FromDir
    {
        get => _fromDir;
        set => SetProperty(ref _fromDir, value);
    }

    /// <summary>终点端口方向。</summary>
    public string ToDir
    {
        get => _toDir;
        set => SetProperty(ref _toDir, value);
    }
}
