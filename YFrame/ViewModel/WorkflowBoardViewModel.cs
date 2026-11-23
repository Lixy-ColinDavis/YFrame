using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;
using YF_Manager;
using YFrame.Model;
using YFrame.Service;

namespace YFrame.ViewModel;

/// <summary>
/// 工作任务流程图 ViewModel：流程切换、节点/连线维护、自动布局和自动保存。
/// </summary>
public sealed class WorkflowBoardViewModel : ViewModelBase
{
    /// <summary>节点默认宽度。</summary>
    public const double NodeWidth = WorkflowLayout.NodeWidth;

    /// <summary>节点默认高度。</summary>
    public const double NodeHeight = WorkflowLayout.NodeHeight;

    private readonly DispatcherTimer _saveTimer;
    private WorkflowDocument _document;
    private WorkflowDefinition? _selectedFlow;
    private string _saveStatus = "已加载";

    /// <summary>创建工作流画布并载入本地数据。</summary>
    public WorkflowBoardViewModel()
    {
        _document = WorkflowStorage.Load();
        Flows = _document.Flows;
        foreach (WorkflowDefinition flow in Flows)
            AttachFlow(flow);

        _selectedFlow = Flows.FirstOrDefault(flow => flow.Id == _document.ActiveId)
                        ?? Flows.FirstOrDefault();
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveTimer.Tick += (_, _) => SaveNow();

        AddStepCommand = new YF_RelayCommand(AddStep);
        AutoLayoutCommand = new YF_RelayCommand(AutoLayout);
        ResetCompletedCommand = new YF_RelayCommand(ResetCompleted);
        DuplicateFlowCommand = new YF_RelayCommand(DuplicateFlow);
        NewFlowCommand = new YF_RelayCommand(NewFlow);
        DeleteFlowCommand = new YF_RelayCommand(DeleteFlow);

        if (Flows.Count == 0)
            NewFlow();
    }

    /// <summary>图结构变化，通知视图重新绘制。</summary>
    public event Action? GraphChanged;

    /// <summary>请求视图适配画布。</summary>
    public event Action? FitRequested;

    /// <summary>新增节点后请求打开编辑浮层。</summary>
    public event Action<WorkflowNode>? NodeEditRequested;

    /// <summary>流程集合。</summary>
    public ObservableCollection<WorkflowDefinition> Flows { get; }

    /// <summary>当前流程。</summary>
    public WorkflowDefinition? SelectedFlow
    {
        get => _selectedFlow;
        set
        {
            if (!SetProperty(ref _selectedFlow, value))
                return;

            if (value != null)
                _document.ActiveId = value.Id;
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ProgressPercent));
            GraphChanged?.Invoke();
            FitRequested?.Invoke();
            ScheduleSave();
        }
    }

    /// <summary>当前流程进度文本。</summary>
    public string ProgressText
    {
        get
        {
            if (SelectedFlow == null)
                return "0 / 0";
            int done = SelectedFlow.Nodes.Count(node => node.Done);
            return $"{done} / {SelectedFlow.Nodes.Count}";
        }
    }

    /// <summary>自动保存状态。</summary>
    public string SaveStatus
    {
        get => _saveStatus;
        set => SetProperty(ref _saveStatus, value);
    }

    /// <summary>当前流程完成百分比，供顶部进度条使用。</summary>
    public double ProgressPercent
    {
        get
        {
            if (SelectedFlow == null || SelectedFlow.Nodes.Count == 0)
                return 0;
            return SelectedFlow.Nodes.Count(node => node.Done) * 100.0 / SelectedFlow.Nodes.Count;
        }
    }

    public YF_RelayCommand AddStepCommand { get; }
    public YF_RelayCommand AutoLayoutCommand { get; }
    public YF_RelayCommand ResetCompletedCommand { get; }
    public YF_RelayCommand DuplicateFlowCommand { get; }
    public YF_RelayCommand NewFlowCommand { get; }
    public YF_RelayCommand DeleteFlowCommand { get; }

    /// <summary>添加一个步骤节点。</summary>
    public void AddStep()
    {
        if (SelectedFlow == null)
            return;

        WorkflowDefinition flow = SelectedFlow;
        double x = WorkflowLayout.PaddingX;
        double y = WorkflowLayout.PaddingY;
        if (flow.Nodes.Count > 0)
        {
            x = flow.Nodes.Max(node => node.X + NodeWidth) + WorkflowLayout.GapX;
            // 与现有步骤的最高一行对齐，避免新步骤整体比第一条向下偏移
            y = flow.Nodes.Min(node => node.Y);
        }

        var node = new WorkflowNode
        {
            Id = "n" + flow.NextNodeId++,
            Order = NextOrder(flow),
            Text = "新步骤",
            X = x,
            Y = y
        };
        flow.Nodes.Add(node);
        GraphChanged?.Invoke();
        NodeEditRequested?.Invoke(node);
        ScheduleSave();
    }

    /// <summary>在指定画布位置添加步骤。</summary>
    /// <param name="x">X 坐标</param>
    /// <param name="y">Y 坐标</param>
    public void AddStepAt(double x, double y)
    {
        if (SelectedFlow == null)
            return;

        WorkflowDefinition flow = SelectedFlow;
        var node = new WorkflowNode
        {
            Id = "n" + flow.NextNodeId++,
            Order = NextOrder(flow),
            Text = "新步骤",
            X = Math.Max(0, x - NodeWidth / 2),
            Y = Math.Max(0, y - NodeHeight / 2)
        };
        flow.Nodes.Add(node);
        GraphChanged?.Invoke();
        NodeEditRequested?.Invoke(node);
        ScheduleSave();
    }

    /// <summary>删除节点及其关联连线。</summary>
    /// <param name="node">待删除节点</param>
    public void DeleteNode(WorkflowNode node)
    {
        if (SelectedFlow == null)
            return;

        SelectedFlow.Edges.Where(edge => edge.From == node.Id || edge.To == node.Id)
            .ToList()
            .ForEach(edge => SelectedFlow.Edges.Remove(edge));
        SelectedFlow.Nodes.Remove(node);
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>添加或更新一条连线。</summary>
    /// <param name="from">起点节点</param>
    /// <param name="to">终点节点</param>
    /// <param name="fromDir">起点端口</param>
    /// <param name="toDir">终点端口</param>
    public void AddEdge(WorkflowNode from, WorkflowNode to, string fromDir, string toDir)
    {
        if (SelectedFlow == null || ReferenceEquals(from, to))
            return;

        bool exists = SelectedFlow.Edges.Any(edge =>
            edge.From == from.Id && edge.To == to.Id &&
            edge.FromDir == fromDir && edge.ToDir == toDir);
        if (exists)
            return;

        SelectedFlow.Edges.Add(new WorkflowEdge
        {
            From = from.Id,
            To = to.Id,
            FromDir = fromDir,
            ToDir = toDir
        });
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>删除连线。</summary>
    /// <param name="edge">待删除连线</param>
    public void DeleteEdge(WorkflowEdge edge)
    {
        SelectedFlow?.Edges.Remove(edge);
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>
    /// 自动布局：只按节点序号 Order 排序后蛇形排列，连线不参与排序。
    /// </summary>
    public void AutoLayout()
    {
        if (SelectedFlow == null || SelectedFlow.Nodes.Count == 0)
            return;

        WorkflowDefinition flow = SelectedFlow;

        // 关键：排序输入只有 Order，不读取 Edges。
        // 因此新增、删除或反向连线都不会改变步骤顺序与位置。
        var ordered = flow.Nodes
            .OrderBy(node => node.Order)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ToList();

        const int columns = 4;
        for (int i = 0; i < ordered.Count; i++)
        {
            WorkflowNode node = ordered[i];
            int row = i / columns;
            int column = i % columns;
            node.X = (row % 2 == 0 ? column : columns - 1 - column)
                     * (NodeWidth + WorkflowLayout.GapX) + WorkflowLayout.PaddingX;
            node.Y = row * (NodeHeight + WorkflowLayout.GapY) + WorkflowLayout.PaddingY;
        }

        // 位置确定后仅调整端口朝向，不改变节点排序。
        foreach (WorkflowEdge edge in flow.Edges)
            AssignNearestDirections(flow, edge);

        GraphChanged?.Invoke();
        FitRequested?.Invoke();
        SaveNow();
    }

    /// <summary>取当前流程的下一个步骤序号。</summary>
    /// <param name="flow">目标流程</param>
    /// <returns>当前最大序号 + 1；空流程从 1 开始</returns>
    private static int NextOrder(WorkflowDefinition flow)
        => flow.Nodes.Count == 0 ? 1 : flow.Nodes.Max(node => node.Order) + 1;

    /// <summary>
    /// 修改节点序号；若目标序号已被占用，则与原节点交换序号。
    /// </summary>
    /// <param name="node">待修改节点</param>
    /// <param name="order">新序号，最小为 1</param>
    public void SetNodeOrder(WorkflowNode node, int order)
    {
        if (SelectedFlow == null || !SelectedFlow.Nodes.Contains(node))
            return;

        order = Math.Max(1, order);
        WorkflowNode? occupied = SelectedFlow.Nodes
            .FirstOrDefault(item => !ReferenceEquals(item, node) && item.Order == order);
        if (occupied != null)
            occupied.Order = node.Order;

        node.Order = order;
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>重置当前流程所有步骤为未完成。</summary>
    public void ResetCompleted()
    {
        if (SelectedFlow == null)
            return;

        foreach (WorkflowNode node in SelectedFlow.Nodes)
            node.Done = false;
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>复制当前流程。</summary>
    public void DuplicateFlow()
    {
        if (SelectedFlow == null)
            return;

        WorkflowDefinition source = SelectedFlow;
        var copy = new WorkflowDefinition
        {
            Name = source.Name + " 副本",
            NextNodeId = source.NextNodeId
        };

        var idMap = new Dictionary<string, string>();
        foreach (WorkflowNode node in source.Nodes)
        {
            string newId = "n" + copy.NextNodeId++;
            idMap[node.Id] = newId;
            copy.Nodes.Add(new WorkflowNode
            {
                Id = newId,
                Order = node.Order,
                Text = node.Text,
                Note = node.Note,
                Done = node.Done,
                X = node.X,
                Y = node.Y
            });
        }

        foreach (WorkflowEdge edge in source.Edges)
        {
            if (idMap.TryGetValue(edge.From, out string? from) &&
                idMap.TryGetValue(edge.To, out string? to))
            {
                copy.Edges.Add(new WorkflowEdge
                {
                    From = from,
                    To = to,
                    FromDir = edge.FromDir,
                    ToDir = edge.ToDir
                });
            }
        }

        AttachFlow(copy);
        Flows.Add(copy);
        SelectedFlow = copy;
        GraphChanged?.Invoke();
        FitRequested?.Invoke();
        SaveNow();
    }

    /// <summary>新建空流程。</summary>
    public void NewFlow()
    {
        var flow = new WorkflowDefinition
        {
            Name = $"流程 {Flows.Count + 1}",
            NextNodeId = 1
        };
        AttachFlow(flow);
        Flows.Add(flow);
        SelectedFlow = flow;
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>删除当前流程；只剩一条时创建新的空流程。</summary>
    public void DeleteFlow()
    {
        if (SelectedFlow != null)
            DeleteWorkflow(SelectedFlow);
    }

    /// <summary>删除指定流程。</summary>
    /// <param name="flow">待删除流程</param>
    public void DeleteWorkflow(WorkflowDefinition flow)
    {
        if (flow == null)
            return;

        DetachFlow(flow);
        Flows.Remove(flow);

        if (Flows.Count == 0)
        {
            var replacement = new WorkflowDefinition { Name = "流程 1", NextNodeId = 1 };
            AttachFlow(replacement);
            Flows.Add(replacement);
            SelectedFlow = replacement;
        }
        else
        {
            SelectedFlow = Flows[0];
        }

        GraphChanged?.Invoke();
        FitRequested?.Invoke();
        SaveNow();
    }

    /// <summary>上移指定流程。</summary>
    /// <param name="flow">待移动流程</param>
    public void MoveUp(WorkflowDefinition flow) => MoveWorkflow(flow, -1);

    /// <summary>下移指定流程。</summary>
    /// <param name="flow">待移动流程</param>
    public void MoveDown(WorkflowDefinition flow) => MoveWorkflow(flow, 1);

    /// <summary>按偏移量移动流程并保持选中。</summary>
    private void MoveWorkflow(WorkflowDefinition flow, int offset)
    {
        int index = Flows.IndexOf(flow);
        int target = index + offset;
        if (index < 0 || target < 0 || target >= Flows.Count)
            return;

        Flows.Move(index, target);
        SelectedFlow = flow;
        SaveNow();
    }

    /// <summary>置顶指定流程。</summary>
    /// <param name="flow">待移动流程</param>
    public void MoveTop(WorkflowDefinition flow)
    {
        int index = Flows.IndexOf(flow);
        if (index > 0)
        {
            Flows.Move(index, 0);
            SelectedFlow = flow;
            SaveNow();
        }
    }

    /// <summary>置底指定流程。</summary>
    /// <param name="flow">待移动流程</param>
    public void MoveBottom(WorkflowDefinition flow)
    {
        int index = Flows.IndexOf(flow);
        if (index >= 0 && index < Flows.Count - 1)
        {
            Flows.Move(index, Flows.Count - 1);
            SelectedFlow = flow;
            SaveNow();
        }
    }

    /// <summary>节点移动后调用，刷新连线并安排保存。</summary>
    public void NotifyNodeMoved()
    {
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>节点数据修改后刷新进度并保存。</summary>
    public void NotifyNodeEdited()
    {
        SelectedFlow?.RefreshProgress();
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressPercent));
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>立即保存，供视图关闭时调用。</summary>
    public void Flush()
    {
        _saveTimer.Stop();
        SaveNow();
    }

    /// <summary>订阅流程及节点/连线变化。</summary>
    private void AttachFlow(WorkflowDefinition flow)
    {
        flow.PropertyChanged += Flow_PropertyChanged;
        flow.Nodes.CollectionChanged += Nodes_CollectionChanged;
        flow.Edges.CollectionChanged += Edges_CollectionChanged;
        foreach (WorkflowNode node in flow.Nodes)
            node.PropertyChanged += Node_PropertyChanged;
        foreach (WorkflowEdge edge in flow.Edges)
            edge.PropertyChanged += Edge_PropertyChanged;
    }

    /// <summary>取消订阅流程。</summary>
    private void DetachFlow(WorkflowDefinition flow)
    {
        flow.PropertyChanged -= Flow_PropertyChanged;
        flow.Nodes.CollectionChanged -= Nodes_CollectionChanged;
        flow.Edges.CollectionChanged -= Edges_CollectionChanged;
        foreach (WorkflowNode node in flow.Nodes)
            node.PropertyChanged -= Node_PropertyChanged;
        foreach (WorkflowEdge edge in flow.Edges)
            edge.PropertyChanged -= Edge_PropertyChanged;
    }

    private void Flow_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowDefinition.ProgressText))
            return;

        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressPercent));
        ScheduleSave();
    }

    private void Nodes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (WorkflowNode node in e.OldItems)
                node.PropertyChanged -= Node_PropertyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (WorkflowNode node in e.NewItems)
                node.PropertyChanged += Node_PropertyChanged;
        }

        SelectedFlow?.RefreshProgress();
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(ProgressPercent));
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    private void Edges_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (WorkflowEdge edge in e.OldItems)
                edge.PropertyChanged -= Edge_PropertyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (WorkflowEdge edge in e.NewItems)
                edge.PropertyChanged += Edge_PropertyChanged;
        }

        GraphChanged?.Invoke();
        ScheduleSave();
    }

    private void Node_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkflowNode.Done))
        {
            SelectedFlow?.RefreshProgress();
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(ProgressPercent));
        }

        GraphChanged?.Invoke();
        ScheduleSave();
    }

    private void Edge_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        GraphChanged?.Invoke();
        ScheduleSave();
    }

    /// <summary>根据两个节点位置选择最近端口。</summary>
    private static void AssignNearestDirections(WorkflowDefinition flow, WorkflowEdge edge)
    {
        WorkflowNode? from = flow.Nodes.FirstOrDefault(node => node.Id == edge.From);
        WorkflowNode? to = flow.Nodes.FirstOrDefault(node => node.Id == edge.To);
        if (from == null || to == null)
            return;

        double dx = (to.X + NodeWidth / 2) - (from.X + NodeWidth / 2);
        double dy = (to.Y + NodeHeight / 2) - (from.Y + NodeHeight / 2);
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            edge.FromDir = dx >= 0 ? "right" : "left";
            edge.ToDir = dx >= 0 ? "left" : "right";
        }
        else
        {
            edge.FromDir = dy >= 0 ? "bottom" : "top";
            edge.ToDir = dy >= 0 ? "top" : "bottom";
        }
    }

    /// <summary>防抖保存。</summary>
    private void ScheduleSave()
    {
        SaveStatus = "正在保存...";
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>写入 JSON。</summary>
    private void SaveNow()
    {
        _saveTimer.Stop();
        try
        {
            _document.ActiveId = SelectedFlow?.Id ?? string.Empty;
            WorkflowStorage.Save(_document);
            SaveStatus = $"已自动保存 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            SaveStatus = $"保存失败：{ex.Message}";
        }
    }
}
