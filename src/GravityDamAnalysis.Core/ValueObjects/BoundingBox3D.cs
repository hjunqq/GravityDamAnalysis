namespace GravityDamAnalysis.Core.ValueObjects;

/// <summary>
/// 三维边界框值对象
/// </summary>
public record BoundingBox3D
{
    /// <summary>
    /// 最小点 (边界框的左下后角)
    /// </summary>
    public Point3D Min { get; init; }

    /// <summary>
    /// 最大点 (边界框的右上前角)
    /// </summary>
    public Point3D Max { get; init; }

    /// <summary>
    /// 默认构造函数 - 创建空边界框
    /// </summary>
    public BoundingBox3D() : this(Point3D.Origin, Point3D.Origin) { }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="min">最小点</param>
    /// <param name="max">最大点</param>
    public BoundingBox3D(Point3D min, Point3D max)
    {
        Min = new Point3D(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z));
        Max = new Point3D(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z));
    }

    /// <summary>
    /// 使用六个坐标值构造边界框
    /// </summary>
    public BoundingBox3D(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
        : this(new Point3D(minX, minY, minZ), new Point3D(maxX, maxY, maxZ))
    {
    }

    /// <summary>
    /// 边界框宽度 (X方向)
    /// </summary>
    public double Width => Max.X - Min.X;

    /// <summary>
    /// 边界框高度 (Z方向)
    /// </summary>
    public double Height => Max.Z - Min.Z;

    /// <summary>
    /// 边界框深度 (Y方向)
    /// </summary>
    public double Depth => Max.Y - Min.Y;

    /// <summary>
    /// 边界框体积
    /// </summary>
    public double Volume => Width * Height * Depth;

    /// <summary>
    /// 边界框中心点
    /// </summary>
    public Point3D Center => new((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);

    /// <summary>
    /// 检查点是否在边界框内
    /// </summary>
    public bool Contains(Point3D point)
    {
        return point.X >= Min.X && point.X <= Max.X &&
               point.Y >= Min.Y && point.Y <= Max.Y &&
               point.Z >= Min.Z && point.Z <= Max.Z;
    }

    /// <summary>
    /// 检查边界框是否有效 (最小值小于最大值)
    /// </summary>
    public bool IsValid()
    {
        return Min.X <= Max.X && Min.Y <= Max.Y && Min.Z <= Max.Z;
    }

    /// <summary>
    /// 扩展边界框以包含指定点
    /// </summary>
    public BoundingBox3D Union(Point3D point)
    {
        return new BoundingBox3D(
            new Point3D(Math.Min(Min.X, point.X), Math.Min(Min.Y, point.Y), Math.Min(Min.Z, point.Z)),
            new Point3D(Math.Max(Max.X, point.X), Math.Max(Max.Y, point.Y), Math.Max(Max.Z, point.Z))
        );
    }

    /// <summary>
    /// 合并两个边界框
    /// </summary>
    public BoundingBox3D Union(BoundingBox3D other)
    {
        return new BoundingBox3D(
            new Point3D(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
            new Point3D(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z))
        );
    }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    public override string ToString()
    {
        return $"BoundingBox3D[Min:{Min}, Max:{Max}, Size:({Width:F2}×{Depth:F2}×{Height:F2})]";
    }
} 