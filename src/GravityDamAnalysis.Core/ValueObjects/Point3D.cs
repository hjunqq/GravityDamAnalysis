namespace GravityDamAnalysis.Core.ValueObjects;

/// <summary>
/// 三维点值对象
/// </summary>
public record Point3D
{
    /// <summary>
    /// X坐标
    /// </summary>
    public double X { get; init; }

    /// <summary>
    /// Y坐标
    /// </summary>
    public double Y { get; init; }

    /// <summary>
    /// Z坐标
    /// </summary>
    public double Z { get; init; }

    /// <summary>
    /// 默认构造函数 - 创建原点
    /// </summary>
    public Point3D() : this(0, 0, 0) { }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="y">Y坐标</param>
    /// <param name="z">Z坐标</param>
    public Point3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>
    /// 原点常量
    /// </summary>
    public static readonly Point3D Origin = new(0, 0, 0);

    /// <summary>
    /// 计算到另一个点的距离
    /// </summary>
    public double DistanceTo(Point3D other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 点的加法运算
    /// </summary>
    public static Point3D operator +(Point3D a, Point3D b)
    {
        return new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    /// <summary>
    /// 点的减法运算
    /// </summary>
    public static Point3D operator -(Point3D a, Point3D b)
    {
        return new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    }

    /// <summary>
    /// 点的标量乘法
    /// </summary>
    public static Point3D operator *(Point3D point, double scalar)
    {
        return new Point3D(point.X * scalar, point.Y * scalar, point.Z * scalar);
    }

    /// <summary>
    /// 标量与点的乘法
    /// </summary>
    public static Point3D operator *(double scalar, Point3D point)
    {
        return point * scalar;
    }

    /// <summary>
    /// 转换为字符串表示
    /// </summary>
    public override string ToString()
    {
        return $"({X:F3}, {Y:F3}, {Z:F3})";
    }
} 