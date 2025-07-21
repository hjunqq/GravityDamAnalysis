using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.Services;
using GravityDamAnalysis.Core.ValueObjects;
using GravityDamAnalysis.UI.ViewModels;
using GravityDamAnalysis.UI.Services;
using System.Windows.Controls.Primitives;

namespace GravityDamAnalysis.UI.Views;

/// <summary>
/// ProfileValidationWindow.xaml 的交互逻辑
/// </summary>
public partial class ProfileValidationWindow : Window
{
    private readonly ILogger<ProfileValidationWindow> _logger;
    private ProfileValidationViewModel _viewModel;
    private bool _isDragging;
    private Point _lastPanPosition;
    private bool _isShownAsDialog = false; // 添加标志跟踪对话框状态
    
    // 绘制相关
    private readonly Dictionary<string, SolidColorBrush> _colorMap;
    private double _canvasScale = 1.0;
    private Point _canvasOffset = new Point(0, 0);

    public ProfileValidationWindow()
    {
        InitializeComponent();
        
        // 创建简单的Logger
        var loggerFactory = LoggerFactory.Create(builder => { });
        _logger = loggerFactory.CreateLogger<ProfileValidationWindow>();
        
        // 创建验证引擎
        var validationLogger = loggerFactory.CreateLogger<ProfileValidationEngine>();
        var validationEngine = new ProfileValidationEngine(validationLogger);
        
        // 创建模拟的Revit集成
        var revitIntegration = new Services.MockRevitIntegration();
        
        // 创建ViewModel
        var vmLogger = loggerFactory.CreateLogger<ProfileValidationViewModel>();
        _viewModel = new ProfileValidationViewModel(vmLogger, validationEngine, revitIntegration);
        DataContext = _viewModel;
        
        // 注意：事件订阅将在Loaded事件中进行，避免过早设置DialogResult
        
        // 初始化颜色映射
        _colorMap = new Dictionary<string, SolidColorBrush>
        {
            ["Dam"] = new SolidColorBrush(Colors.LightBlue),
            ["Foundation"] = new SolidColorBrush(Colors.Brown),
            ["WaterUpstream"] = new SolidColorBrush(Colors.Blue),
            ["WaterDownstream"] = new SolidColorBrush(Colors.LightBlue),
            ["DrainageSystem"] = new SolidColorBrush(Colors.Gray),
            ["ValidationIssue"] = new SolidColorBrush(Colors.Red)
        };
        
        Loaded += OnWindowLoaded;
    }
    
    /// <summary>
    /// 设置要验证的剖面
    /// </summary>
    public void SetProfile(EnhancedProfile2D profile)
    {
        if (IsLoaded)
        {
            // 窗口已加载，可以直接设置
            _viewModel.Profile = profile;
            DrawProfile();
        }
        else
        {
            // 窗口未加载，延迟设置直到加载完成
            Loaded += (sender, e) =>
            {
                _viewModel.Profile = profile;
                DrawProfile();
            };
        }
    }
    
    /// <summary>
    /// 获取验证结果
    /// </summary>
    public EnhancedProfile2D GetValidatedProfile()
    {
        return _viewModel.Profile;
    }

    #region 事件处理

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _logger.LogInformation("剖面验证窗口已加载");
            
            // 在窗口加载完成后订阅ViewModel事件，确保窗口已正确初始化
            _viewModel.ProfileUpdated += OnProfileUpdated;
            _viewModel.ValidationConfirmed += OnValidationConfirmed;
            _viewModel.ValidationCancelled += OnValidationCancelled;
            
            // 如果有剖面数据，立即绘制
            if (_viewModel.Profile != null)
            {
                DrawProfile();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "窗口加载时发生错误");
        }
    }

    private void OnProfileUpdated(object sender, EventArgs e)
    {
        try
        {
            DrawProfile();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新剖面显示时发生错误");
        }
    }

    private void OnValidationConfirmed(object sender, ProfileValidationEventArgs e)
    {
        try
        {
            // 使用try-catch安全设置DialogResult
            this.DialogResult = true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("无法设置DialogResult，窗口可能未作为对话框显示: {Message}", ex.Message);
            // 如果无法设置DialogResult，直接关闭窗口
            this.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确认验证时发生错误");
        }
    }

    private void OnValidationCancelled(object sender, EventArgs e)
    {
        try
        {
            // 使用try-catch安全设置DialogResult
            this.DialogResult = false;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("无法设置DialogResult，窗口可能未作为对话框显示: {Message}", ex.Message);
            // 如果无法设置DialogResult，直接关闭窗口
            this.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消验证时发生错误");
        }
    }

    #endregion

    #region Canvas绘制

    /// <summary>
    /// 绘制剖面
    /// </summary>
    private void DrawProfile()
    {
        try
        {
            if (_viewModel?.Profile == null)
            {
                _logger.LogWarning("没有剖面数据可绘制");
                return;
            }

            var canvas = FindName("DrawingCanvas") as Canvas;
            if (canvas == null)
            {
                _logger.LogError("找不到绘图Canvas");
                return;
            }

            // 清空画布
            canvas.Children.Clear();

            var profile = _viewModel.Profile;
            
            // 输出调试信息
            _logger.LogInformation("开始绘制剖面: {ProfileName}", profile.Name);
            _logger.LogInformation("主轮廓点数: {PointCount}", profile.MainContour?.Count ?? 0);
            if (profile.MainContour?.Any() == true)
            {
                var firstPoint = profile.MainContour.First();
                var lastPoint = profile.MainContour.Last();
                _logger.LogInformation("坐标范围: X[{MinX:F2}, {MaxX:F2}], Y[{MinY:F2}, {MaxY:F2}]", 
                    profile.MainContour.Min(p => p.X), profile.MainContour.Max(p => p.X),
                    profile.MainContour.Min(p => p.Y), profile.MainContour.Max(p => p.Y));
            }

            // 绘制网格（如果启用）
            if (_viewModel.ShowGrid)
            {
                DrawGrid(canvas);
            }

            // 绘制坝体轮廓
            if (profile.MainContour?.Any() == true)
            {
                DrawContour(canvas, profile.MainContour, _colorMap["Dam"], "坝体轮廓");
            }
            else
            {
                _logger.LogWarning("主轮廓为空，无法绘制坝体");
            }

            // 绘制基础轮廓
            if (profile.FoundationContour?.Any() == true)
            {
                DrawContour(canvas, profile.FoundationContour, _colorMap["Foundation"], "基础轮廓");
            }
            else
            {
                _logger.LogWarning("基础轮廓为空，无法绘制基础线");
            }

            // 绘制水位线
            DrawWaterLevels(canvas, profile);

            // 绘制排水系统
            if (profile.Features.ContainsKey("DrainageSystem") && 
                profile.Features["DrainageSystem"] is List<GravityDamAnalysis.Core.Entities.Point2D> drainageSystem)
            {
                DrawDrainageSystem(canvas, drainageSystem);
            }

            // 绘制验证标记（如果启用）
            if (profile.Issues?.Any() == true)
            {
                DrawValidationMarkers(canvas, profile.Issues);
            }

            // 绘制尺寸标注（如果启用）
            if (_viewModel.ShowDimensions)
            {
                DrawDimensions(canvas, profile);
            }

            _logger.LogInformation("剖面绘制完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "绘制剖面时发生错误");
            MessageBox.Show($"绘制剖面时发生错误: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 绘制网格
    /// </summary>
    private void DrawGrid(Canvas canvas)
    {
        var gridBrush = new SolidColorBrush(Colors.LightGray) { Opacity = 0.3 };
        var gridSize = 20.0; // 网格大小

        // 绘制垂直线
        for (double x = 0; x < canvas.ActualWidth; x += gridSize)
        {
            var line = new Line
            {
                X1 = x, Y1 = 0,
                X2 = x, Y2 = canvas.ActualHeight,
                Stroke = gridBrush,
                StrokeThickness = 0.5
            };
            canvas.Children.Add(line);
        }

        // 绘制水平线
        for (double y = 0; y < canvas.ActualHeight; y += gridSize)
        {
            var line = new Line
            {
                X1 = 0, Y1 = y,
                X2 = canvas.ActualWidth, Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5
            };
            canvas.Children.Add(line);
        }
    }

    /// <summary>
    /// 绘制轮廓
    /// </summary>
    private void DrawContour(Canvas canvas, IEnumerable<Point2D> points, SolidColorBrush fillBrush, string name)
    {
        var pointList = points.ToList();
        if (pointList.Count < 3) 
        {
            _logger.LogWarning("轮廓点数不足，无法绘制 {Name}: {Count} 个点", name, pointList.Count);
            return;
        }

        _logger.LogDebug("开始绘制轮廓 {Name}，包含 {Count} 个点", name, pointList.Count);

        var polygon = new Polygon
        {
            Fill = fillBrush,
            Stroke = new SolidColorBrush(Colors.Black),
            StrokeThickness = 2,
            Opacity = 0.7
        };

        var pointCollection = new PointCollection();
        foreach (var point in pointList)
        {
            var canvasPoint = TransformToCanvas(point);
            pointCollection.Add(canvasPoint);
            
            // 添加调试信息
            _logger.LogDebug("轮廓点转换: 原始({OrigX:F2}, {OrigY:F2}) -> Canvas({CanvasX:F2}, {CanvasY:F2})", 
                point.X, point.Y, canvasPoint.X, canvasPoint.Y);
        }

        polygon.Points = pointCollection;
        canvas.Children.Add(polygon);

        // 添加标签
        if (pointList.Any())
        {
            var labelPosition = TransformToCanvas(pointList.First());
            var label = new TextBlock
            {
                Text = name,
                Foreground = new SolidColorBrush(Colors.Black),
                FontSize = 10,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(label, labelPosition.X + 5);
            Canvas.SetTop(label, labelPosition.Y - 20);
            canvas.Children.Add(label);
        }
        
        _logger.LogDebug("轮廓 {Name} 绘制完成", name);
    }

    /// <summary>
    /// 绘制水位线
    /// </summary>
    private void DrawWaterLevels(Canvas canvas, EnhancedProfile2D profile)
    {
        // 上游水位线
        if (profile.FeaturePoints.ContainsKey("UpstreamWaterLevel"))
        {
            var upstreamPoint = profile.FeaturePoints["UpstreamWaterLevel"];
            DrawWaterLevel(canvas, upstreamPoint.Y, _colorMap["WaterUpstream"], "上游水位");
        }

        // 下游水位线
        if (profile.FeaturePoints.ContainsKey("DownstreamWaterLevel"))
        {
            var downstreamPoint = profile.FeaturePoints["DownstreamWaterLevel"];
            DrawWaterLevel(canvas, downstreamPoint.Y, _colorMap["WaterDownstream"], "下游水位");
        }
    }

    /// <summary>
    /// 绘制单条水位线
    /// </summary>
    private void DrawWaterLevel(Canvas canvas, double waterLevel, SolidColorBrush brush, string label)
    {
        var y = TransformYToCanvas(waterLevel);
        
        var line = new Line
        {
            X1 = 0,
            Y1 = y,
            X2 = canvas.ActualWidth,
            Y2 = y,
            Stroke = brush,
            StrokeThickness = 3,
            StrokeDashArray = new DoubleCollection { 10, 5 }
        };
        
        canvas.Children.Add(line);

        // 添加标签
        var labelText = new TextBlock
        {
            Text = $"{label}: {waterLevel:F2}m",
            Foreground = brush,
            FontSize = 10,
            FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(labelText, 10);
        Canvas.SetTop(labelText, y - 20);
        canvas.Children.Add(labelText);
    }

    /// <summary>
    /// 绘制排水系统
    /// </summary>
    private void DrawDrainageSystem(Canvas canvas, IEnumerable<Point2D> drainagePoints)
    {
        foreach (var point in drainagePoints)
        {
            var canvasPoint = TransformToCanvas(point);
            var circle = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = _colorMap["DrainageSystem"],
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 1
            };
            
            Canvas.SetLeft(circle, canvasPoint.X - 3);
            Canvas.SetTop(circle, canvasPoint.Y - 3);
            canvas.Children.Add(circle);
        }
    }

    /// <summary>
    /// 绘制验证标记
    /// </summary>
    private void DrawValidationMarkers(Canvas canvas, IEnumerable<GeometryIssue> issues)
    {
        foreach (var issue in issues)
        {
            if (issue.Location == null) continue;

            var canvasPoint = TransformToCanvas(issue.Location.Value);
            var marker = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = GetIssueBrush(issue.Severity),
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 2
            };

            Canvas.SetLeft(marker, canvasPoint.X - 6);
            Canvas.SetTop(marker, canvasPoint.Y - 6);
            canvas.Children.Add(marker);

            // 添加工具提示
            marker.ToolTip = $"{issue.Type}: {issue.Description}";
        }
    }

    /// <summary>
    /// 绘制尺寸标注
    /// </summary>
    private void DrawDimensions(Canvas canvas, EnhancedProfile2D profile)
    {
        // 简单示例：绘制坝高标注
        if (profile.MainContour?.Any() == true)
        {
            var contour = profile.MainContour.ToList();
            var maxY = contour.Max(p => p.Y);
            var minY = contour.Min(p => p.Y);
            var height = maxY - minY;

            var rightMostPoint = contour.OrderByDescending(p => p.X).First();
            var canvasPoint = TransformToCanvas(rightMostPoint);

            var dimensionText = new TextBlock
            {
                Text = $"坝高: {height:F2}m",
                Foreground = new SolidColorBrush(Colors.Red),
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };

            Canvas.SetLeft(dimensionText, canvasPoint.X + 10);
            Canvas.SetTop(dimensionText, canvasPoint.Y);
            canvas.Children.Add(dimensionText);
        }
    }

    #endregion

    #region 坐标转换

    /// <summary>
    /// 将工程坐标转换为Canvas坐标
    /// </summary>
    private Point TransformToCanvas(Point2D engineeringPoint)
    {
        // 获取当前剖面类型以确定正确的坐标转换方式
        var profile = _viewModel?.Profile;
        if (profile == null)
        {
            // 默认转换
            var localScale = 100.0;
            return new Point(
                (engineeringPoint.X * localScale * _canvasScale) + _canvasOffset.X + 50,
                400 - (engineeringPoint.Y * localScale * _canvasScale) + _canvasOffset.Y
            );
        }

        // 智能坐标转换，根据剖面类型和法向量调整
        var scale = CalculateOptimalScale(profile);
        
        // 检查剖面类型
        var sectionType = GetSectionTypeFromProfile(profile);
        
        double canvasX, canvasY;
        
        switch (sectionType)
        {
            case "vertical":
            case "竖直剖面":
            case "竖直":
                // 竖直剖面：X-Y平面投影
                canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y;
                break;
                
            case "longitudinal":
            case "纵剖面":
                // 纵剖面：X-Z平面投影
                canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y; // Y在剖面中代表高度
                break;
                
            case "transverse":
            case "横剖面":
                // 横剖面：Y-Z平面投影
                canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y; // Y在剖面中代表高度
                break;
                
            default:
                // 根据剖面法向量智能判断
                if (profile.SectionNormal.IsValid())
                {
                    var normal = profile.SectionNormal;
                    
                    if (Math.Abs(normal.Z) > 0.8) // 主要沿Z轴（竖直剖面）
                    {
                        canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                        canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y;
                    }
                    else if (Math.Abs(normal.Y) > 0.8) // 主要沿Y轴（纵剖面）
                    {
                        canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                        canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y;
                    }
                    else // 主要沿X轴（横剖面）
                    {
                        canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                        canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y;
                    }
                }
                else
                {
                    // 默认处理
                    canvasX = (engineeringPoint.X * scale * _canvasScale) + _canvasOffset.X + 50;
                    canvasY = (400 - (engineeringPoint.Y * scale * _canvasScale)) + _canvasOffset.Y;
                }
                break;
        }
        
        // 添加调试输出
        _logger.LogDebug("坐标转换: 工程坐标({EngX:F2}, {EngY:F2}) -> Canvas坐标({CanvasX:F2}, {CanvasY:F2}), 缩放: {Scale:F2}", 
            engineeringPoint.X, engineeringPoint.Y, canvasX, canvasY, scale);
        
        return new Point(canvasX, canvasY);
    }
    
    /// <summary>
    /// 将工程Y坐标转换为Canvas Y坐标（用于水位线等水平线的绘制）
    /// </summary>
    private double TransformYToCanvas(double engineeringY)
    {
        // 使用相同的转换逻辑，但只处理Y坐标
        var profile = _viewModel?.Profile;
        double scale;
        
        if (profile == null)
        {
            scale = 100.0;
        }
        else
        {
            scale = CalculateOptimalScale(profile);
        }
        
        // Y轴翻转：Canvas的Y=0在顶部，工程坐标的Y=0通常在底部
        return 400 - (engineeringY * scale * _canvasScale) + _canvasOffset.Y;
    }
    
    /// <summary>
    /// 计算最优缩放比例
    /// </summary>
    private double CalculateOptimalScale(EnhancedProfile2D profile)
    {
        if (!profile.MainContour.Any())
        {
            _logger.LogWarning("主轮廓为空，使用默认缩放比例");
            return 100.0;
        }
            
        // 计算剖面的实际尺寸
        var minX = profile.MainContour.Min(p => p.X);
        var maxX = profile.MainContour.Max(p => p.X);
        var minY = profile.MainContour.Min(p => p.Y);
        var maxY = profile.MainContour.Max(p => p.Y);
        
        var width = maxX - minX;
        var height = maxY - minY;
        
        _logger.LogDebug("剖面尺寸: 宽度={Width:F2}, 高度={Height:F2}", width, height);
        
        // 如果尺寸为0或负数，使用默认值
        if (width <= 0 || height <= 0)
        {
            _logger.LogWarning("剖面尺寸无效，使用默认缩放比例");
            return 100.0;
        }
        
        // 假设Canvas尺寸约为800x600，留出边距
        var targetCanvasWidth = 700.0;
        var targetCanvasHeight = 500.0;
        
        var scaleX = targetCanvasWidth / width;
        var scaleY = targetCanvasHeight / height;
        
        // 选择较小的缩放比例以确保完全显示
        var optimalScale = Math.Min(scaleX, scaleY);
        
        // 限制缩放范围，避免过小或过大
        optimalScale = Math.Max(10.0, Math.Min(1000.0, optimalScale));
        
        _logger.LogDebug("计算缩放比例: X缩放={ScaleX:F2}, Y缩放={ScaleY:F2}, 最优缩放={OptimalScale:F2}", 
            scaleX, scaleY, optimalScale);
        
        return optimalScale;
    }
    
    /// <summary>
    /// 从剖面数据中推断剖面类型
    /// </summary>
    private string GetSectionTypeFromProfile(EnhancedProfile2D profile)
    {
        // 首先检查名称
        if (!string.IsNullOrEmpty(profile.Name))
        {
            var name = profile.Name.ToLower();
            if (name.Contains("竖直") || name.Contains("vertical"))
                return "vertical";
            if (name.Contains("纵剖面") || name.Contains("longitudinal"))
                return "longitudinal";
            if (name.Contains("横剖面") || name.Contains("transverse"))
                return "transverse";
        }
        
        // 然后检查法向量
        if (profile.SectionNormal.IsValid())
        {
            var normal = profile.SectionNormal;
            
            if (Math.Abs(normal.Z) > 0.8)
                return "vertical";
            else if (Math.Abs(normal.Y) > 0.8)
                return "longitudinal";
            else if (Math.Abs(normal.X) > 0.8)
                return "transverse";
        }
        
        // 默认返回竖直剖面
        return "vertical";
    }

    #endregion

    #region 鼠标交互

    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    {
        var canvas = sender as Canvas;
        var position = e.GetPosition(canvas);
        
        // 更新坐标显示
        var statusBarItem = FindName("CoordinateStatus") as StatusBarItem;
        if (statusBarItem != null)
        {
            statusBarItem.Content = $"坐标: ({position.X:F0}, {position.Y:F0})";
        }

        // 处理拖拽
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var deltaX = position.X - _lastPanPosition.X;
            var deltaY = position.Y - _lastPanPosition.Y;
            
            _canvasOffset.X += deltaX;
            _canvasOffset.Y += deltaY;
            
            _lastPanPosition = position;
            DrawProfile();
        }
    }

    private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var canvas = sender as Canvas;
        _isDragging = true;
        _lastPanPosition = e.GetPosition(canvas);
        canvas?.CaptureMouse();
    }

    private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var canvas = sender as Canvas;
        _isDragging = false;
        canvas?.ReleaseMouseCapture();
    }

    #endregion

    #region 工具栏事件

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _canvasScale = Math.Min(_canvasScale * 1.25, 5.0);
        DrawProfile();
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _canvasScale = Math.Max(_canvasScale / 1.25, 0.1);
        DrawProfile();
    }

    private void FitToView_Click(object sender, RoutedEventArgs e)
    {
        _canvasScale = 1.0;
        _canvasOffset = new Point(0, 0);
        DrawProfile();
    }

    private void Measure_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("测量功能待实现", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Annotate_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("标注功能待实现", "信息", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void RunValidation_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RunValidationCommand.Execute(null);
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 根据问题严重程度获取画刷
    /// </summary>
    private SolidColorBrush GetIssueBrush(IssueSeverity severity)
    {
        return severity switch
        {
            IssueSeverity.Critical => new SolidColorBrush(Colors.Red),
            IssueSeverity.Error => new SolidColorBrush(Colors.Orange),
            IssueSeverity.Warning => new SolidColorBrush(Colors.Yellow),
            _ => new SolidColorBrush(Colors.LightBlue)
        };
    }



    #endregion

    #region 窗口清理

    protected override void OnClosed(EventArgs e)
    {
        // 取消事件订阅
        if (_viewModel != null)
        {
            _viewModel.ProfileUpdated -= OnProfileUpdated;
            _viewModel.ValidationConfirmed -= OnValidationConfirmed;
            _viewModel.ValidationCancelled -= OnValidationCancelled;
        }

        base.OnClosed(e);
    }

    #endregion
} 