using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
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
    private WorkflowNode? _hoveredNode;
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

    // 单击切换完成、双击打开编辑：用短延时区分单击与双击
    private readonly DispatcherTimer _nodeClickTimer;
    private WorkflowNode? _pendingClickNode;

    /// <summary>初始化流程画布。</summary>
    public WorkflowBoard()
    {
        InitializeComponent();
        _nodeClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(280) };
        _nodeClickTimer.Tick += NodeClickTimer_Tick;
        DataContextChanged += WorkflowBoard_DataContextChanged;

        // 节点被拖到 BoardRoot 边界外后，空白点击会落到 BoardViewport，
        // 所以平移入口必须同时覆盖视口层，否则画布无法继续拖动。
        BoardViewport.MouseLeftButtonDown += BoardViewport_MouseLeftButtonDown;
        BoardViewport.PreviewMouseWheel += BoardViewport_PreviewMouseWheel;
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
        _nodeClickTimer.Stop();
        _pendingClickNode = null;
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
        bool done = node.Done;
        var border = new Border
        {
            Width = NodeWidth,
            Height = NodeHeight,
            CornerRadius = new CornerRadius(10),
            Background = done
                ? FindBrush("PanelBrush", Brushes.Transparent)
                : FindBrush("PanelAltBrush", Brushes.Transparent),
            BorderBrush = done
                ? FindBrush("BorderBrush", Brushes.Gray)
                : FindBrush("AccentBrush", Brushes.Cyan),
            BorderThickness = new Thickness(done ? 1 : 1.6),
            Tag = node,
            Cursor = Cursors.Hand,
            ToolTip = "单击切换完成状态，双击编辑步骤"
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });

        // 左侧序号圆点：完成时变灰，未完成时使用强调色
        var numberBorder = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            Background = done
                ? FindBrush("PanelAltBrush", Brushes.Transparent)
                : FindBrush("AccentSoftBrush", Brushes.Transparent),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        numberBorder.Child = new TextBlock
        {
            Text = node.Order > 0 ? node.Order.ToString() : "?",
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = done
                ? FindBrush("SubtleTextBrush", Brushes.Gray)
                : FindBrush("AccentBrush", Brushes.Cyan),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(numberBorder, 0);
        grid.Children.Add(numberBorder);

        var textPanel = new StackPanel
        {
            Margin = new Thickness(0, 8, 4, 8),
            VerticalAlignment = VerticalAlignment.Center
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(node.Text) ? "未命名步骤" : node.Text,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = done
                ? FindBrush("SubtleTextBrush", Brushes.Gray)
                : FindBrush("TextBrush", Brushes.White),
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
        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        // 右侧完成勾：仅完成状态显示，颜色与主框架成功色一致
        var checkBorder = new Border
        {
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(10),
            Background = FindBrush("SuccessBrush", Brushes.Green),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = done ? Visibility.Visible : Visibility.Hidden
        };
        checkBorder.Child = new TextBlock
        {
            Text = "✓",
            FontSize = 11,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(checkBorder, 2);
        grid.Children.Add(checkBorder);

        border.Child = grid;
        border.MouseLeftButtonDown += Node_MouseLeftButtonDown;
        border.MouseMove += Node_MouseMove;
        border.MouseLeftButtonUp += Node_MouseLeftButtonUp;

        List<Ellipse> ports = AddPorts(border, node);
        border.MouseEnter += (_, _) =>
        {
            _hoveredNode = node;
            // 连接点只在鼠标悬停步骤时显示
            foreach (Ellipse port in ports)
                port.Opacity = 1;

            // 让画布拿到键盘焦点，悬停时 Delete 才能删除当前步骤
            if (EditorOverlay.Visibility != Visibility.Visible)
                Focus();
        };
        border.MouseLeave += (_, _) =>
        {
            if (ReferenceEquals(_hoveredNode, node))
                _hoveredNode = null;

            foreach (Ellipse port in ports)
                port.Opacity = 0;
        };

        Canvas.SetLeft(border, node.X);
        Canvas.SetTop(border, node.Y);
        NodeCanvas.Children.Add(border);
        _nodeViews[node.Id] = border;
    }

    /// <summary>给节点添加四个连接端口。</summary>
    /// <returns>端口圆点列表，用于悬停显示/隐藏</returns>
    private List<Ellipse> AddPorts(Border border, WorkflowNode node)
    {
        return new List<Ellipse>
        {
            AddPort(border, node, "top", HorizontalAlignment.Center, VerticalAlignment.Top),
            AddPort(border, node, "bottom", HorizontalAlignment.Center, VerticalAlignment.Bottom),
            AddPort(border, node, "left", HorizontalAlignment.Left, VerticalAlignment.Center),
            AddPort(border, node, "right", HorizontalAlignment.Right, VerticalAlignment.Center)
        };
    }

    /// <summary>添加单个端口圆点。</summary>
    /// <returns>创建的端口圆点</returns>
    private Ellipse AddPort(Border border, WorkflowNode node, string direction,
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
            Opacity = 0,
            Cursor = Cursors.Cross,
            Tag = new PortTag(node, direction),
            ToolTip = "拖动此端口创建连线"
        };
        port.MouseLeftButtonDown += Port_MouseLeftButtonDown;
        Panel.SetZIndex(port, 10);

        if (border.Child is Grid grid)
        {
            // 端口跨全部列，中心才能落在节点外框上，而不是第一列边缘
            Grid.SetColumnSpan(port, grid.ColumnDefinitions.Count);
            grid.Children.Add(port);
        }

        return port;
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
            // 双击取消待执行的单击切换，只打开编辑浮层
            _nodeClickTimer.Stop();
            _pendingClickNode = null;
            OpenNodeEditor(node);
            e.Handled = true;
            return;
        }

        _nodeClickTimer.Stop();
        _pendingClickNode = null;
        _dragNode = node;
        // 使用视口坐标计算拖动距离，避免自动平移改变 NodeCanvas 坐标后
        // 下一次 MouseMove 把平移量重复计入，造成突然飞出几个屏幕。
        _dragStart = e.GetPosition(BoardViewport);
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

        Point current = e.GetPosition(BoardViewport);
        double scale = Math.Max(0.05, BoardScale.ScaleX);
        double dx = (current.X - _dragStart.X) / scale;
        double dy = (current.Y - _dragStart.Y) / scale;
        if (Math.Abs(dx) + Math.Abs(dy) > 3)
            _dragging = true;

        if (!_dragging)
            return;

        _dragNode.X = _dragOriginX + dx;
        _dragNode.Y = _dragOriginY + dy;
        if (sender is Border border)
        {
            Canvas.SetLeft(border, _dragNode.X);
            Canvas.SetTop(border, _dragNode.Y);

            // 拖到视口左上边缘时自动平移画布，使节点可以继续向左/上移动；
            // 这样默认步骤仍靠左上显示，同时不会被画布原点卡住。
            Point screenPoint = border.TranslatePoint(new Point(0, 0), BoardViewport);
            const double safeMargin = 36;
            if (screenPoint.X < safeMargin)
                BoardPan.X += safeMargin - screenPoint.X;
            if (screenPoint.Y < safeMargin)
                BoardPan.Y += safeMargin - screenPoint.Y;
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
        {
            _viewModel?.NotifyNodeMoved();
        }
        else
        {
            // 没有拖动视为单击：延时执行，避免双击编辑时先切换完成状态
            _pendingClickNode = node;
            _nodeClickTimer.Stop();
            _nodeClickTimer.Start();
        }
        e.Handled = true;
    }

    /// <summary>单击步骤后切换完成状态。</summary>
    private void NodeClickTimer_Tick(object? sender, EventArgs e)
    {
        _nodeClickTimer.Stop();
        WorkflowNode? node = _pendingClickNode;
        _pendingClickNode = null;
        if (node == null)
            return;

        node.Done = !node.Done;
        _viewModel?.NotifyNodeEdited();
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
        if (e.ChangedButton != MouseButton.Left)
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

    /// <summary>视口层空白点击：当 BoardRoot 被平移出可视区域时仍能拖动画布。</summary>
    private void BoardViewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left ||
            EditorOverlay.Visibility == Visibility.Visible ||
            _panning)
        {
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
    /// <summary>在整个画布视口上响应滚轮缩放，鼠标位于空白或节点上都有效。</summary>
    private void BoardViewport_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // 编辑浮层内的 TextBox 需要正常滚动，不能被画布抢走滚轮
        if (EditorOverlay.Visibility == Visibility.Visible)
            return;

        ZoomAt(e.GetPosition(BoardViewport), e.Delta);
        e.Handled = true;
    }

    /// <summary>以鼠标位置为中心缩放画布。</summary>
    /// <param name="mouse">鼠标在视口中的位置</param>
    /// <param name="delta">滚轮增量</param>
    private void ZoomAt(Point mouse, int delta)
    {
        double oldScale = BoardScale.ScaleX;
        double factor = delta > 0 ? 1.1 : 0.9;
        double newScale = Math.Clamp(oldScale * factor, 0.2, 1.8);
        BoardPan.X = mouse.X - (mouse.X - BoardPan.X) * newScale / oldScale;
        BoardPan.Y = mouse.Y - (mouse.Y - BoardPan.Y) * newScale / oldScale;
        BoardScale.ScaleX = newScale;
        BoardScale.ScaleY = newScale;
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
        NodeOrderBox.Text = node.Order.ToString();
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

        if (int.TryParse(NodeOrderBox.Text, out int order) && order > 0 &&
            order != _editingNode.Order)
        {
            _viewModel?.SetNodeOrder(_editingNode, order);
        }

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

    /// <summary>
    /// 画布键盘操作：编辑浮层中 Esc 关闭；悬停步骤时 Delete 删除该步骤。
    /// </summary>
    private void WorkflowBoard_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && EditorOverlay.Visibility == Visibility.Visible)
        {
            CloseNodeEditor();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete &&
            EditorOverlay.Visibility != Visibility.Visible &&
            Keyboard.FocusedElement is not TextBox &&
            _hoveredNode != null &&
            _viewModel != null)
        {
            WorkflowNode node = _hoveredNode;
            _hoveredNode = null;
            _viewModel.DeleteNode(node);
            e.Handled = true;
        }
    }

    /// <summary>工具栏添加步骤：按当前可视区域左上角定位，而不是固定画布坐标。</summary>
    private void AddStepButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null)
            return;

        // 已有步骤时放到最右侧步骤右边，避免连续添加全部叠在视口左上角；
        // 空流程第一条仍按可视区域左上角放置。
        if (_viewModel.SelectedFlow is { Nodes.Count: > 0 })
        {
            _viewModel.AddStep();
            return;
        }

        // 取可视窗口左上角向内 36px 的位置，转换为 NodeCanvas 世界坐标
        Point viewportTopLeft = new(36, 36);
        Point worldPoint = NodeCanvas.PointFromScreen(
            BoardViewport.PointToScreen(viewportTopLeft));

        _viewModel.AddStepAt(
            worldPoint.X + NodeWidth / 2,
            worldPoint.Y + NodeHeight / 2);
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
            (BoardViewport.ActualHeight - 100) / height), 0.05, 1.15);

        BoardScale.ScaleX = scale;
        BoardScale.ScaleY = scale;

        // 内容从可视区域左上角开始显示，不再整体居中，
        // 避免新流程/新步骤看起来向右下偏移。
        const double viewportMargin = 40;
        BoardPan.X = viewportMargin - minX * scale;
        BoardPan.Y = viewportMargin - minY * scale;
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
