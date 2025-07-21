using System;
using System.Collections.Generic;
using System.Linq;
using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 增强的二维剖面实体
/// 包含完整的几何、材料、边界条件信息
/// </summary>
public class EnhancedProfile2D : Profile2D
{
    /// <summary>
    /// 材料分区信息
    /// </summary>
    public List<MaterialZone> MaterialZones { get; set; } = new();
    
    /// <summary>
    /// 边界条件定义
    /// </summary>
    public BoundaryConditions BoundaryConditions { get; set; } = new();
    
    /// <summary>
    /// 几何特征点（如坝踵、坝趾、溢流面等）
    /// </summary>
    public Dictionary<string, Point2D> FeaturePoints { get; set; } = new();
    
    /// <summary>
    /// 水位线与剖面的交点
    /// </summary>
    public WaterLevelIntersections WaterLevels { get; set; } = new();
    
    /// <summary>
    /// 计算网格信息（用于有限元分析）
    /// </summary>
    public AnalysisMesh? Mesh { get; set; }
    
    /// <summary>
    /// 基础接触面轮廓
    /// </summary>
    public List<Point2D> FoundationContour { get; set; } = new List<Point2D>();
    
    /// <summary>
    /// 识别的特征字典
    /// </summary>
    public Dictionary<string, object> Features { get; set; } = new Dictionary<string, object>();
    
    /// <summary>
    /// 自动识别关键几何特征
    /// </summary>
    public void IdentifyGeometricFeatures()
    {
        try
        {
            if (!MainContour.Any()) return;

            // 识别坝踵、坝趾
            FeaturePoints["坝踵"] = FindUpstreamToe();
            FeaturePoints["坝趾"] = FindDownstreamToe();
            
            // 识别顶部边缘
            FeaturePoints["坝顶中心"] = FindCrestCenter();
            
            // 识别坡度变化点
            var slopeChanges = FindSlopeChanges();
            for (int i = 0; i < slopeChanges.Count; i++)
            {
                FeaturePoints[$"坡度变化点_{i + 1}"] = slopeChanges[i];
            }
        }
        catch (Exception)
        {
            // 如果特征识别失败，不影响主流程
        }
    }

    /// <summary>
    /// 查找上游坝踵
    /// </summary>
    private Point2D FindUpstreamToe()
    {
        if (!MainContour.Any()) return new Point2D(0, 0);
        
        // 找到最左下角的点
        return MainContour
            .OrderBy(p => p.X)
            .ThenBy(p => p.Y)
            .First();
    }

    /// <summary>
    /// 查找下游坝趾
    /// </summary>
    private Point2D FindDownstreamToe()
    {
        if (!MainContour.Any()) return new Point2D(0, 0);
        
        // 找到最右下角的点
        return MainContour
            .OrderByDescending(p => p.X)
            .ThenBy(p => p.Y)
            .First();
    }

    /// <summary>
    /// 查找坝顶中心
    /// </summary>
    private Point2D FindCrestCenter()
    {
        if (!MainContour.Any()) return new Point2D(0, 0);
        
        // 找到最高点
        var maxY = MainContour.Max(p => p.Y);
        var topPoints = MainContour.Where(p => Math.Abs(p.Y - maxY) < 0.1).ToList();
        
        if (!topPoints.Any()) return MainContour.First();
        
        var avgX = topPoints.Average(p => p.X);
        return new Point2D(avgX, maxY);
    }

    /// <summary>
    /// 查找坡度变化点
    /// </summary>
    private List<Point2D> FindSlopeChanges()
    {
        var changePoints = new List<Point2D>();
        
        if (MainContour.Count < 3) return changePoints;
        
        const double angleThreshold = 15.0; // 角度变化阈值（度）
        
        for (int i = 1; i < MainContour.Count - 1; i++)
        {
            var p1 = MainContour[i - 1];
            var p2 = MainContour[i];
            var p3 = MainContour[i + 1];
            
            var angle1 = Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
            var angle2 = Math.Atan2(p3.Y - p2.Y, p3.X - p2.X);
            
            var angleDiff = Math.Abs(angle2 - angle1) * 180.0 / Math.PI;
            
            if (angleDiff > angleThreshold)
            {
                changePoints.Add(p2);
            }
        }
        
        return changePoints;
    }

    /// <summary>
    /// 计算剖面重心（增强版本，支持材料分区）
    /// </summary>
    public new Point2D CalculateCentroid()
    {
        if (!MainContour.Any()) return new Point2D(0, 0);
        
        double area = CalculateArea();
        if (Math.Abs(area) < 1e-6) return MainContour.First();
        
        double cx = 0, cy = 0;
        
        for (int i = 0; i < MainContour.Count; i++)
        {
            int j = (i + 1) % MainContour.Count;
            double factor = MainContour[i].X * MainContour[j].Y - MainContour[j].X * MainContour[i].Y;
            cx += (MainContour[i].X + MainContour[j].X) * factor;
            cy += (MainContour[i].Y + MainContour[j].Y) * factor;
        }
        
        cx /= (6.0 * area);
        cy /= (6.0 * area);
        
        return new Point2D(cx, cy);
    }

    /// <summary>
    /// 计算剖面面积（增强版本，考虑材料分区）
    /// </summary>
    public new double CalculateArea()
    {
        if (MainContour.Count < 3) return 0;
        
        double area = 0;
        for (int i = 0; i < MainContour.Count; i++)
        {
            int j = (i + 1) % MainContour.Count;
            area += MainContour[i].X * MainContour[j].Y;
            area -= MainContour[j].X * MainContour[i].Y;
        }
        
        return Math.Abs(area) / 2.0;
    }

    // 新增验证相关属性和方法
    
    /// <summary>
    /// 验证结果
    /// </summary>
    public ProfileValidationResult? ValidationResults { get; set; }
    
    /// <summary>
    /// 发现的问题列表
    /// </summary>
    public List<GeometryIssue> Issues { get; set; } = new List<GeometryIssue>();
    
    /// <summary>
    /// 验证状态
    /// </summary>
    public ValidationStatus Status { get; set; } = ValidationStatus.Pending;
    
    /// <summary>
    /// 边界条件字典
    /// </summary>
    public Dictionary<string, BoundaryCondition> BoundaryConditionDict { get; set; } = new Dictionary<string, BoundaryCondition>();
    
    /// <summary>
    /// 用户标注
    /// </summary>
    public List<UserAnnotation> UserAnnotations { get; set; } = new List<UserAnnotation>();
    
    /// <summary>
    /// 显示设置
    /// </summary>
    public ProfileDisplaySettings DisplaySettings { get; set; } = new ProfileDisplaySettings();
    
    /// <summary>
    /// 是否通过验证
    /// </summary>
    public bool IsValidationPassed => Status == ValidationStatus.Validated || Status == ValidationStatus.CalculationReady;
    
    /// <summary>
    /// 是否有严重问题
    /// </summary>
    public bool HasCriticalIssues => Issues.Any(i => i.IsBlocking);
    
    /// <summary>
    /// 是否需要用户审查
    /// </summary>
    public bool RequiresUserReview => HasCriticalIssues || Issues.Any(i => i.Severity >= IssueSeverity.Error);
    
    /// <summary>
    /// 添加验证问题
    /// </summary>
    public void AddIssue(GeometryIssue issue)
    {
        Issues.Add(issue);
        
        // 根据问题严重程度自动更新状态
        if (issue.Severity >= IssueSeverity.Error && Status == ValidationStatus.Pending)
        {
            Status = ValidationStatus.HasIssues;
        }
    }
    
    /// <summary>
    /// 清除所有问题
    /// </summary>
    public void ClearIssues()
    {
        Issues.Clear();
        if (Status == ValidationStatus.HasIssues)
        {
            Status = ValidationStatus.Pending;
        }
    }
    
    /// <summary>
    /// 获取指定类型的问题
    /// </summary>
    public List<GeometryIssue> GetIssuesByType(IssueType type)
    {
        return Issues.Where(i => i.Type == type).ToList();
    }
    
    /// <summary>
    /// 获取指定严重程度的问题
    /// </summary>
    public List<GeometryIssue> GetIssuesBySeverity(IssueSeverity severity)
    {
        return Issues.Where(i => i.Severity == severity).ToList();
    }
}

/// <summary>
/// 边界条件定义
/// </summary>
public class BoundaryConditions
{
    /// <summary>
    /// 基底约束条件
    /// </summary>
    public BaseConstraint BaseConstraint { get; set; } = BaseConstraint.Fixed;
    
    /// <summary>
    /// 上游水压力边界
    /// </summary>
    public List<PressureBoundary> UpstreamPressure { get; set; } = new();
    
    /// <summary>
    /// 下游水压力边界
    /// </summary>
    public List<PressureBoundary> DownstreamPressure { get; set; } = new();
    
    /// <summary>
    /// 扬压力边界
    /// </summary>
    public UpliftPressureBoundary UpliftPressure { get; set; } = new();
}

/// <summary>
/// 基底约束类型
/// </summary>
public enum BaseConstraint
{
    /// <summary>
    /// 固定约束
    /// </summary>
    Fixed,
    
    /// <summary>
    /// 仅垂直约束
    /// </summary>
    VerticalOnly,
    
    /// <summary>
    /// 仅水平约束
    /// </summary>
    HorizontalOnly,
    
    /// <summary>
    /// 无约束
    /// </summary>
    Free
}

/// <summary>
/// 压力边界条件
/// </summary>
public class PressureBoundary
{
    /// <summary>
    /// 边界起点
    /// </summary>
    public Point2D StartPoint { get; set; }
    
    /// <summary>
    /// 边界终点
    /// </summary>
    public Point2D EndPoint { get; set; }
    
    /// <summary>
    /// 起点压力值 (kPa)
    /// </summary>
    public double StartPressure { get; set; }
    
    /// <summary>
    /// 终点压力值 (kPa)
    /// </summary>
    public double EndPressure { get; set; }
    
    /// <summary>
    /// 压力方向（法向量）
    /// </summary>
    public Vector2D Direction { get; set; } = new(1, 0);
}

/// <summary>
/// 扬压力边界条件
/// </summary>
public class UpliftPressureBoundary
{
    /// <summary>
    /// 上游水头 (m)
    /// </summary>
    public double UpstreamHead { get; set; }
    
    /// <summary>
    /// 下游水头 (m)
    /// </summary>
    public double DownstreamHead { get; set; }
    
    /// <summary>
    /// 折减系数
    /// </summary>
    public double ReductionFactor { get; set; } = 0.8;
    
    /// <summary>
    /// 排水效率
    /// </summary>
    public double DrainageEfficiency { get; set; } = 0.5;
}

/// <summary>
/// 材料分区定义
/// </summary>
public class MaterialZone
{
    /// <summary>
    /// 材料分区名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 分区边界
    /// </summary>
    public List<Point2D> Boundary { get; set; } = new();
    
    /// <summary>
    /// 材料属性
    /// </summary>
    public MaterialProperties Properties { get; set; } = new();
    
    /// <summary>
    /// 分区面积
    /// </summary>
    public double Area => CalculateArea();
    
    /// <summary>
    /// 分区重心
    /// </summary>
    public Point2D Centroid => CalculateCentroid();
    
    /// <summary>
    /// 计算分区面积
    /// </summary>
    private double CalculateArea()
    {
        if (Boundary.Count < 3) return 0;
        
        double area = 0;
        for (int i = 0; i < Boundary.Count; i++)
        {
            int j = (i + 1) % Boundary.Count;
            area += Boundary[i].X * Boundary[j].Y;
            area -= Boundary[j].X * Boundary[i].Y;
        }
        
        return Math.Abs(area) / 2.0;
    }
    
    /// <summary>
    /// 计算分区重心
    /// </summary>
    private Point2D CalculateCentroid()
    {
        if (!Boundary.Any()) return new Point2D(0, 0);
        
        double area = CalculateArea();
        if (Math.Abs(area) < 1e-6) return Boundary.First();
        
        double cx = 0, cy = 0;
        
        for (int i = 0; i < Boundary.Count; i++)
        {
            int j = (i + 1) % Boundary.Count;
            double factor = Boundary[i].X * Boundary[j].Y - Boundary[j].X * Boundary[i].Y;
            cx += (Boundary[i].X + Boundary[j].X) * factor;
            cy += (Boundary[i].Y + Boundary[j].Y) * factor;
        }
        
        cx /= (6.0 * area);
        cy /= (6.0 * area);
        
        return new Point2D(cx, cy);
    }
}

/// <summary>
/// 水位线交点信息
/// </summary>
public class WaterLevelIntersections
{
    /// <summary>
    /// 上游水位线交点
    /// </summary>
    public Point2D UpstreamWaterLineIntersection { get; set; }
    
    /// <summary>
    /// 下游水位线交点
    /// </summary>
    public Point2D DownstreamWaterLineIntersection { get; set; }
    
    /// <summary>
    /// 上游水位 (m)
    /// </summary>
    public double UpstreamWaterLevel { get; set; }
    
    /// <summary>
    /// 下游水位 (m)
    /// </summary>
    public double DownstreamWaterLevel { get; set; }
}

/// <summary>
/// 分析网格
/// </summary>
public class AnalysisMesh
{
    /// <summary>
    /// 网格节点
    /// </summary>
    public List<MeshNode> Nodes { get; set; } = new();
    
    /// <summary>
    /// 网格单元
    /// </summary>
    public List<MeshElement> Elements { get; set; } = new();
    
    /// <summary>
    /// 网格类型
    /// </summary>
    public MeshType Type { get; set; } = MeshType.Triangular;
}

/// <summary>
/// 网格节点
/// </summary>
public class MeshNode
{
    /// <summary>
    /// 节点ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 节点坐标
    /// </summary>
    public Point2D Position { get; set; }
    
    /// <summary>
    /// 节点约束条件
    /// </summary>
    public NodeConstraint Constraint { get; set; } = NodeConstraint.Free;
}

/// <summary>
/// 网格单元
/// </summary>
public class MeshElement
{
    /// <summary>
    /// 单元ID
    /// </summary>
    public int Id { get; set; }
    
    /// <summary>
    /// 节点索引
    /// </summary>
    public List<int> NodeIndices { get; set; } = new();
    
    /// <summary>
    /// 材料属性
    /// </summary>
    public MaterialProperties Material { get; set; } = new();
}

/// <summary>
/// 网格类型
/// </summary>
public enum MeshType
{
    /// <summary>
    /// 三角形网格
    /// </summary>
    Triangular,
    
    /// <summary>
    /// 四边形网格
    /// </summary>
    Quadrilateral,
    
    /// <summary>
    /// 混合网格
    /// </summary>
    Mixed
}

/// <summary>
/// 节点约束条件
/// </summary>
public enum NodeConstraint
{
    /// <summary>
    /// 无约束
    /// </summary>
    Free,
    
    /// <summary>
    /// 固定约束
    /// </summary>
    Fixed,
    
    /// <summary>
    /// X方向约束
    /// </summary>
    FixedX,
    
    /// <summary>
    /// Y方向约束
    /// </summary>
    FixedY
}

/// <summary>
/// 二维向量
/// </summary>
public class Vector2D
{
    public double X { get; set; }
    public double Y { get; set; }
    
    public Vector2D(double x, double y)
    {
        X = x;
        Y = y;
    }
    
    /// <summary>
    /// 向量长度
    /// </summary>
    public double Length => Math.Sqrt(X * X + Y * Y);
    
    /// <summary>
    /// 标准化向量
    /// </summary>
    public Vector2D Normalize()
    {
        double len = Length;
        return len > 0 ? new Vector2D(X / len, Y / len) : new Vector2D(0, 0);
    }
} 