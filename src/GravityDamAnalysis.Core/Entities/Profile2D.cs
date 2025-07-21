using System.ComponentModel.DataAnnotations;
using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 二维剖面数据结构
/// 用于存储从Revit 3D模型提取的2D几何剖面信息
/// </summary>
public class Profile2D
{
    /// <summary>
    /// 剖面ID
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// 剖面名称
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 主轮廓（外边界）
    /// </summary>
    public List<Point2D> MainContour { get; set; } = new();

    /// <summary>
    /// 内部轮廓集合（孔洞等）
    /// </summary>
    public List<List<Point2D>> InnerContours { get; set; } = new();

    /// <summary>
    /// 剖面平面法向量（在原3D坐标系中）
    /// </summary>
    public Vector3D SectionNormal { get; set; } = new();

    /// <summary>
    /// 剖面平面原点（在原3D坐标系中）
    /// </summary>
    public Point3D SectionOrigin { get; set; } = new();

    /// <summary>
    /// 剖面的2D坐标系X轴方向（在原3D坐标系中）
    /// </summary>
    public Vector3D LocalXAxis { get; set; } = new();

    /// <summary>
    /// 剖面的2D坐标系Y轴方向（在原3D坐标系中）
    /// </summary>
    public Vector3D LocalYAxis { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 添加内部轮廓
    /// </summary>
    /// <param name="contour">内部轮廓点集</param>
    public void AddInnerContour(List<Point2D> contour)
    {
        if (contour != null && contour.Count >= 3)
        {
            InnerContours.Add(contour);
        }
    }

    /// <summary>
    /// 计算剖面面积
    /// </summary>
    /// <returns>剖面净面积（扣除孔洞）</returns>
    public double CalculateArea()
    {
        var mainArea = CalculatePolygonArea(MainContour);
        var holeAreas = InnerContours.Sum(contour => CalculatePolygonArea(contour));
        return mainArea - holeAreas;
    }

    /// <summary>
    /// 计算剖面形心
    /// </summary>
    /// <returns>形心坐标</returns>
    public Point2D CalculateCentroid()
    {
        // 简化计算：仅考虑主轮廓的形心
        if (MainContour.Count < 3) return new Point2D(0, 0);

        double area = 0.0;
        double centroidX = 0.0;
        double centroidY = 0.0;

        for (int i = 0; i < MainContour.Count; i++)
        {
            var current = MainContour[i];
            var next = MainContour[(i + 1) % MainContour.Count];

            double crossProduct = current.X * next.Y - next.X * current.Y;
            area += crossProduct;
            centroidX += (current.X + next.X) * crossProduct;
            centroidY += (current.Y + next.Y) * crossProduct;
        }

        area /= 2.0;
        centroidX /= (6.0 * area);
        centroidY /= (6.0 * area);

        return new Point2D(centroidX, centroidY);
    }

    /// <summary>
    /// 获取剖面边界框
    /// </summary>
    /// <returns>2D边界框</returns>
    public BoundingBox2D GetBoundingBox()
    {
        if (MainContour.Count == 0)
            return new BoundingBox2D();

        var minX = MainContour.Min(p => p.X);
        var maxX = MainContour.Max(p => p.X);
        var minY = MainContour.Min(p => p.Y);
        var maxY = MainContour.Max(p => p.Y);

        return new BoundingBox2D(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// 计算多边形面积（使用鞋带公式）
    /// </summary>
    private double CalculatePolygonArea(List<Point2D> polygon)
    {
        if (polygon.Count < 3) return 0.0;

        double area = 0.0;
        for (int i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];
            area += current.X * next.Y - next.X * current.Y;
        }

        return Math.Abs(area) / 2.0;
    }

    /// <summary>
    /// 验证剖面数据有效性
    /// </summary>
    public bool IsValid()
    {
        return MainContour.Count >= 3 && 
               !string.IsNullOrEmpty(Name) &&
               SectionNormal.Length > 0;
    }

    /// <summary>
    /// 获取剖面描述信息
    /// </summary>
    public override string ToString()
    {
        return $"Profile2D: {Name}, Points: {MainContour.Count}, Area: {CalculateArea():F2} m²";
    }
}

/// <summary>
/// 二维点结构
/// </summary>
public struct Point2D
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point2D(double x, double y)
    {
        X = x;
        Y = y;
    }

    public double DistanceTo(Point2D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public override string ToString() => $"({X:F3}, {Y:F3})";
}

/// <summary>
/// 三维向量结构
/// </summary>
public struct Vector3D
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>
    /// 检查是否为零向量（所有分量都接近零）
    /// </summary>
    public bool IsZero(double tolerance = 1e-6) => Length < tolerance;

    /// <summary>
    /// 检查是否为有效的非零向量
    /// </summary>
    public bool IsValid(double tolerance = 1e-6) => Length >= tolerance;

    public Vector3D Normalize()
    {
        var length = Length;
        return length > 0 ? new Vector3D(X / length, Y / length, Z / length) : this;
    }

    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
}

/// <summary>
/// 二维边界框
/// </summary>
public struct BoundingBox2D
{
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }

    public BoundingBox2D(double minX, double minY, double maxX, double maxY)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public double Width => MaxX - MinX;
    public double Height => MaxY - MinY;
    public Point2D Center => new((MinX + MaxX) / 2, (MinY + MaxY) / 2);

    public override string ToString() => $"BBox2D: ({MinX:F2}, {MinY:F2}) to ({MaxX:F2}, {MaxY:F2})";
} 