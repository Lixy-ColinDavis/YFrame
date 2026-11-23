using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using YFrame.Model;
using YFrame.ViewModel;

namespace YFrame.View.UC;

/// <summary>
/// 工作任务流程画布：绘制节点/连线，处理拖动、端口连线、编辑、缩放和自动适配。
/// 参考 aaa.html 的 FlowBoard 交互，用 WPF Canvas/Path 原生实现。
/// </summary>
public partial class WorkflowBoard : UserControl
{
    private const double NodeWidth = 190;
    private const double NodeHeight = 72;

    private readonly Dictionary<string, Border> _nodeViews = new();
    private WorkflowBoardViewModel? _viewModel;
    private WorkflowNode? _editingNode;
    private WorkflowNode? _dragNode;
    private WorkflowNode? _linkSource;
    private string _linkDirection = "right";
    private Path? _tempLinkPath;
    private Point _dragStart;
    private double _dragOriginX;
    private double _dragOriginY;
    private bool _dragging;
    private bool _panning;
    private Point _panStart;
    private double _panOriginX;
    private double _panOriginY;

    /// <summary>初始化流程画布。</summary>
    public WorkflowBoard()
    {
        InitializeComponent();
        DataContextChanged += WorkflowBoard_DataContextChanged;
        Loaded += (_, _) =>
        {
            RenderGraph();
            FitBoard();
        };
    }

    /// <summary>数据上下文切换时订阅新的 ViewModel。</summary>
    private void WorkflowBoard_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.GraphChanged -= ViewModel_GraphChanged;
            _viewModel.FitRequested -= ViewModel_FitRequested;
            _viewModel.NodeEditRequested -= ViewModel_NodeEditRequested;
        }

        _viewModel = e.NewValue as WorkflowBoardViewModel;
        if (_viewModel == null)
            return;

        _viewModel.GraphChanged += ViewModel_GraphChanged;
        _viewModel.FitRequested += ViewModel_FitRequested;
        _viewModel.NodeEditRequested += ViewModel_NodeEditRequested;
        RenderGraph();
        FitBoard();
    }

    /// <summary>图变化时重绘；拖动期间由本地直接刷新，避免销毁拖动控件。</summary>
    private void ViewModel_GraphChanged()
    {
        if (_dragNode != null)
            return;

        RenderGraph();
    }

    /// <summary>请求适配视图。</summary>
    private void ViewModel_FitRequested() => FitBoard();

    /// <summary>新增步骤后打开编辑浮层。</summary>
    private void ViewModel_NodeEditRequested(WorkflowNode node) => OpenNodeEditor(node);

    /// <summary>重绘全部节点和连线。</summary>
    private void RenderGraph()
    {
        NodeCanvas.Children.Clear();
        EdgeCanvas.Children.Clear();
        _nodeViews.Clear();

        WorkflowDefinition? flow = _viewModel?.SelectedFlow;
        if (flow == null)
        {
            EmptyTip.Visibility = Visibility.Visible;
            return;
        }

        EmptyTip.Visibility = flow.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // 先画线，再画节点，保证节点在连线之上
        foreach (WorkflowEdge edge in flow.Edges)
            DrawEdge(flow, edge);

        foreach (WorkflowNode node in flow.Nodes)
            DrawNode(node);

        UpdateEditorVisibility();
    }

    /// <summary>绘制单个节点。</summary>
    private void DrawNode(WorkflowNode node)
    {
        var border = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            CornerRadius = new CornerRadius(10),
            Background = FindBrush("PanelAltBrush", Brushes.Transparent),
            BorderBrush = node.Done ? FindBrush("BorderBrush", Brushes.Gray) : FindBrush("AccentBrush", Brushes.Cyan),
            BorderThickness = new Thickness(1),
            Tag = node,
            Opacity = node.Done ? 0.7 : 1
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var textPanel = new StackPanel { Margin = new Thickness(32, 8, 8, 8) };
        textPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(node.Text) ? "未命名步骤" : node.Text,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextBrush", Brushes.White),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        textPanel.Children.Add(new TextBlock
        {
            Text = node.Note,
            FontSize = 10,
            Foreground = FindBrush("SubtleTextBrush", Brushes.Gray),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 4, 0, 0),
            Visibility = string.IsNullOrWhiteSpace(node.Note) ? Visibility.Collapsed : Visibility.Visible
        });
        Grid.SetColumn(textPanel, 0);
        grid.Children.Add(textPanel);

        var check = new CheckBox
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "标记步骤完成"
        };
        check.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new System.Windows.Data.Binding(nameof(WorkflowNode.Done))
            {
                Source = node,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
        Grid.SetColumn(check, 1);
        grid.Children.Add(check);

        border.Child = grid;
        border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
        border.MouseMove += Node_MouseMove;
        border.MouseLeftButtonUp += Node_MouseLeftButtonUp;

        AddPorts(border, node);
        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);
        NodeCanvas.Children.Add(border);
        _nodeViews[node.Id] = border;
    }

    /// <summary>给节点添加四个连接端口。</summary>
    private void AddPorts(Border border, WorkflowNode node)
    {
        AddPort(border, node, "top", HorizontalAlignment.Center, VerticalAlignment.Top);
        AddPort(border, node, "bottom", HorizontalAlignment.Center, VerticalAlignment.Bottom);
        AddPort(border, node, "left", HorizontalAlignment.Left, VerticalAlignment.Center);
        AddPort(border, node, "right", HorizontalAlignment.Right, VerticalAlignment.Center);
    }

    /// <summary>添加单个端口圆点。</summary>
    private void AddPort(Border border, WorkflowNode node, string direction,
        HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var port = new Ellipse
        {
            Width = 10,
            Height = 10,
            Fill = FindBrush("WindowBrush", Brushes.Black),
            Stroke = FindBrush("AccentBrush", Brushes.Cyan),
            StrokeThickness = 1.5,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = new Thickness(-5),
            Opacity = 0.55,
            Cursor = Cursors.Cross,
            Tag = new PortTag(node, direction),
            ToolTip = "拖动此端口创建连线"
        };
        port.MouseLeftButtonDown += Port_MouseLeftButtonDown;
        port.MouseEnter += (_, _) => port.Opacity = 1;
        port.MouseLeave += (_, _) => port.Opacity = 0.55;
        Panel.SetZIndex(port, 10);

        if (border.Child is Grid grid)
        {
            // 节点 Grid 有“内容列 + 复选框列”两列；
            // 端口必须跨两列，才能贴合整个节点外框，而不是贴在第一列右边缘。
            Grid.SetColumnSpan(port, 2);
            grid.Children.Add(port);
        }
    }

    /// <summary>绘制一条贝塞尔连线。</summary>
    private void DrawEdge(WorkflowDefinition flow, WorkflowEdge edge)
    {
        WorkflowNode? from = flow.Nodes.FirstOrDefault(node => node.Id == edge.From);
        WorkflowNode? to = flow.Nodes.FirstOrDefault(node => node.Id == edge.To);
        if (from == null || to == null)
            return;

        Point p1 = PortPoint(from, edge.FromDir);
        Point p2 = PortPoint(to, edge.ToDir);
        Path path = CreateBezierPath(p1, p2, edge.FromDir, edge.ToDir,
            FindBrush("SubtleTextBrush", Brushes.Gray), 1.7);
        path.Tag = edge;
        path.Cursor = Cursors.Hand;
        path.ToolTip = "双击删除连线";
        path.MouseLeftButtonDown += (_, args) =>
        {
            if (args.ClickCount == 2 && _viewModel?.SelectedFlow != null)
            {
                _viewModel.DeleteEdge(edge);
                args.Handled = true;
            }
        };
        EdgeCanvas.Children.Add(path);
    }

    /// <summary>根据端口方向计算节点连接点。</summary>
    private static Point PortPoint(WorkflowNode node, string direction) => direction switch
    {
        "top" => new Point(node.X + NodeWidth / 2, node.Y),
        "bottom" => new Point(node.X + NodeWidth / 2, node.Y + NodeHeight),
        "left" => new Point(node.X, node.Y + NodeHeight / 2),
        _ => new Point(node.X + NodeWidth, node.Y + NodeHeight / 2)
    };

    /// <summary>生成三次贝塞尔路径。</summary>
    private static Path CreateBezierPath(Point start, Point end, string fromDir, string toDir,
        Brush brush, double thickness)
    {
        Vector startVector = DirectionVector(fromDir);
        Vector endVector = DirectionVector(toDir);
        double distance = Math.Max(40, Math.Min(130, (end - start).Length * 0.45));
        var geometry = new StreamGeometry();
        using (StreamGeometryContext context = geometry.Open())
        {
            context.BeginFigure(start, false, false);
            context.BezierTo(
                start + startVector * distance,
                end + endVector * distance,
                end, true, false);
        }

        return new Path
        {
            Data = geometry,
            Stroke = brush,
            StrokeThickness = thickness,
            IsHitTestVisible = true
        };
    }

    /// <summary>方向对应的单位向量。</summary>
    private static Vector DirectionVector(string direction) => direction switch
    {
        "top" => new Vector(0, -1),
        "bottom" => new Vector(0, 1),
        "left" => new Vector(-1, 0),
        _ => new Vector(1, 0)
    };

    /// <summary>开始拖动节点或双击编辑。</summary>
    private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: WorkflowNode node })
            return;

        if (e.ClickCount == 2)
        {
            OpenNodeEditor(node);
            e.Handled = true;
            return;
        }

        _dragNode = node;
        _dragStart = e.GetPosition(NodeCanvas);
        _dragOriginX = node.X;
        _dragOriginY = node.Y;
        _dragging = false;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    /// <summary>拖动节点位置并实时重绘连线。</summary>
    private void Node_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragNode == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        Point current = e.GetPosition(NodeCanvas);
        double dx = current.X - _dragStart.X;
        double dy = current.Y - _dragStart.Y;
        if (Math.Abs(dx) + Math.Abs(dy) > 3)
            _dragging = true;

        if (!_dragging)
            return;

        _dragNode.X = Math.Max(0, _dragOriginX + dx);
        _dragNode.Y = Math.Max(0, _dragOriginY + dy);
        if (sender is Border border)
        {
            Canvas.SetLeft(border, _dragNode.X);
            Canvas.SetTop(border, _dragNode.Y);
        }
        RefreshEdges();
    }

    /// <summary>结束节点拖动。</summary>
    private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragNode == null)
            return;

        ((UIElement)sender).ReleaseMouseCapture();
        WorkflowNode node = _dragNode;
        bool moved = _dragging;
        _dragNode = null;
        _dragging = false;
        if (moved)
            _viewModel?.NotifyNodeMoved();
        e.Handled = true;
    }

    /// <summary>从端口开始拉线。</summary>
    private void Port_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Ellipse { Tag: PortTag tag })
            return;

        _linkSource = tag.Node;
        _linkDirection = tag.Direction;
        Point start = PortPoint(tag.Node, tag.Direction);
        _tempLinkPath = CreateBezierPath(start, start, tag.Direction, tag.Direction,
            FindBrush("AccentBrush", Brushes.Cyan), 2);
        _tempLinkPath.StrokeDashArray = new DoubleCollection { 5, 5 };
        EdgeCanvas.Children.Add(_tempLinkPath);
        BoardRoot.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>画布移动：更新拖线、节点或平移。</summary>
    private void BoardRoot_MouseMove(object sender, MouseEventArgs e)
    {
        if (_linkSource != null && _tempLinkPath != null)
        {
            Point start = PortPoint(_linkSource, _linkDirection);
            Point end = e.GetPosition(NodeCanvas);
            EdgeCanvas.Children.Remove(_tempLinkPath);
            _tempLinkPath = CreateBezierPath(start, end, _linkDirection, "left",
                FindBrush("AccentBrush", Brushes.Cyan), 2);
            _tempLinkPath.StrokeDashArray = new DoubleCollection { 5, 5 };
            _tempLinkPath.IsHitTestVisible = false;
            EdgeCanvas.Children.Add(_tempLinkPath);
            return;
        }

        if (_panning)
        {
            Point current = e.GetPosition(BoardViewport);
            BoardPan.X = _panOriginX + current.X - _panStart.X;
            BoardPan.Y = _panOriginY + current.Y - _panStart.Y;
        }
    }

    /// <summary>结束拉线或平移。</summary>
    private void BoardRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_linkSource != null)
        {
            WorkflowNode? target = FindNodeAt(e.GetPosition(NodeCanvas));
            if (target != null && _viewModel != null)
            {
                string targetDirection = NearestDirection(target, e.GetPosition(NodeCanvas));
                _viewModel.AddEdge(_linkSource, target, _linkDirection, targetDirection);
            }

            if (_tempLinkPath != null)
                EdgeCanvas.Children.Remove(_tempLinkPath);
            _tempLinkPath = null;
            _linkSource = null;
            BoardRoot.ReleaseMouseCapture();
            return;
        }

        if (_panning)
        {
            _panning = false;
            BoardRoot.ReleaseMouseCapture();
        }
    }

    /// <summary>点击画布空白：开始平移。</summary>
    private void BoardRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource != BoardRoot || e.ChangedButton != MouseButton.Left)
            return;

        if (e.ClickCount == 2)
        {
            Point point = e.GetPosition(NodeCanvas);
            _viewModel?.AddStepAt(point.X, point.Y);
            e.Handled = true;
            return;
        }

        _panning = true;
        _panStart = e.GetPosition(BoardViewport);
        _panOriginX = BoardPan.X;
        _panOriginY = BoardPan.Y;
        BoardRoot.CaptureMouse();
        e.Handled = true;
    }

    /// <summary>鼠标滚轮缩放画布。</summary>
    private void BoardRoot_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double oldScale = BoardScale.ScaleX;
        double factor = e.Delta > 0 ? 1.1 : 0.9;
        double newScale = Math.Clamp(oldScale * factor, 0.35, 1.8);
        Point mouse = e.GetPosition(BoardViewport);
        BoardPan.X = mouse.X - (mouse.X - BoardPan.X) * newScale / oldScale;
        BoardPan.Y = mouse.Y - (mouse.Y - BoardPan.Y) * newScale / oldScale;
        BoardScale.ScaleX = newScale;
        BoardScale.ScaleY = newScale;
        e.Handled = true;
    }

    /// <summary>重新计算当前流程全部连线。</summary>
    private void RefreshEdges()
    {
        if (_viewModel?.SelectedFlow == null)
            return;

        EdgeCanvas.Children.Clear();
        foreach (WorkflowEdge edge in _viewModel.SelectedFlow.Edges)
            DrawEdge(_viewModel.SelectedFlow, edge);
    }

    /// <summary>根据画布坐标寻找节点。</summary>
    private WorkflowNode? FindNodeAt(Point point)
    {
        if (_viewModel?.SelectedFlow == null)
            return null;

        return _viewModel.SelectedFlow.Nodes.LastOrDefault(node =>
            point.X >= node.X && point.X <= node.X + NodeWidth &&
            point.Y >= node.Y && point.Y <= node.Y + NodeHeight);
    }

    /// <summary>按落点选择最近的端口方向。</summary>
    private static string NearestDirection(WorkflowNode node, Point point)
    {
        double centerX = node.X + NodeWidth / 2;
        double centerY = node.Y + NodeHeight / 2;
        double dx = point.X - centerX;
        double dy = point.Y - centerY;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? "right" : "left";
        return dy >= 0 ? "bottom" : "top";
    }

    /// <summary>打开节点编辑浮层。</summary>
    private void OpenNodeEditor(WorkflowNode node)
    {
        _editingNode = node;
        NodeTitleBox.Text = node.Text;
        NodeNoteBox.Text = node.Note;
        NodeDoneBox.IsChecked = node.Done;
        EditorOverlay.Visibility = Visibility.Visible;
        NodeTitleBox.Focus();
        NodeTitleBox.SelectAll();
    }

    /// <summary>关闭编辑浮层。</summary>
    private void CloseNodeEditor()
    {
        _editingNode = null;
        EditorOverlay.Visibility = Visibility.Collapsed;
    }

    /// <summary>保存节点编辑。</summary>
    private void SaveNodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingNode == null)
            return;

        _editingNode.Text = string.IsNullOrWhiteSpace(NodeTitleBox.Text)
            ? "未命名步骤"
            : NodeTitleBox.Text.Trim();
        _editingNode.Note = NodeNoteBox.Text.Trim();
        _editingNode.Done = NodeDoneBox.IsChecked == true;
        CloseNodeEditor();
        _viewModel?.NotifyNodeEdited();
        RenderGraph();
    }

    /// <summary>取消节点编辑。</summary>
    private void CancelNodeButton_Click(object sender, RoutedEventArgs e) => CloseNodeEditor();

    /// <summary>删除当前编辑节点。</summary>
    private void DeleteNodeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_editingNode == null || _viewModel == null)
            return;

        WorkflowNode node = _editingNode;
        CloseNodeEditor();
        _viewModel.DeleteNode(node);
        RenderGraph();
    }

    /// <summary>工具栏复位视图。</summary>
    private void FitButton_Click(object sender, RoutedEventArgs e) => FitBoard();

    /// <summary>将当前流程缩放到画布可视区域。</summary>
    private void FitBoard()
    {
        WorkflowDefinition? flow = _viewModel?.SelectedFlow;
        if (flow == null || flow.Nodes.Count == 0 ||
            BoardViewport.ActualWidth <= 0 || BoardViewport.ActualHeight <= 0)
        {
            BoardScale.ScaleX = BoardScale.ScaleY = 1;
            BoardPan.X = BoardPan.Y = 0;
            return;
        }

        double minX = flow.Nodes.Min(node => node.X);
        double minY = flow.Nodes.Min(node => node.Y);
        double maxX = flow.Nodes.Max(node => node.X + NodeWidth);
        double maxY = flow.Nodes.Max(node => node.Y + NodeHeight);
        double width = Math.Max(1, maxX - minX);
        double height = Math.Max(1, maxY - minY);
        double scale = Math.Clamp(Math.Min(
            (BoardViewport.ActualWidth - 100) / width,
            (BoardViewport.ActualHeight - 100) / height), 0.35, 1.15);

        BoardScale.ScaleX = scale;
        BoardScale.ScaleY = scale;
        BoardPan.X = (BoardViewport.ActualWidth - width * scale) / 2 - minX * scale;
        BoardPan.Y = (BoardViewport.ActualHeight - height * scale) / 2 - minY * scale;
    }

    /// <summary>编辑浮层仅在有选中流程时显示。</summary>
    private void UpdateEditorVisibility()
    {
        if (_viewModel?.SelectedFlow == null)
            CloseNodeEditor();
    }

    /// <summary>从资源字典取画刷，找不到时回退。</summary>
    private Brush FindBrush(string key, Brush fallback)
        => TryFindResource(key) as Brush ?? fallback;

    /// <summary>端口携带的节点和方向。</summary>
    private sealed record PortTag(WorkflowNode Node, string Direction);
}
