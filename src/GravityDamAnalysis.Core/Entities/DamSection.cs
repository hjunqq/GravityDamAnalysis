using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 重力坝断面实体
/// </summary>
public class DamSection
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">断面ID</param>
    /// <param name="name">断面名称</param>
    /// <param name="position">断面位置</param>
    /// <param name="sectionType">断面类型</param>
    /// <param name="height">断面高度</param>
    /// <param name="topWidth">顶部宽度</param>
    /// <param name="bottomWidth">底部宽度</param>
    public DamSection(
        Guid id,
        string name,
        Point3D position,
        SectionType sectionType,
        double height,
        double topWidth,
        double bottomWidth)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("断面名称不能为空", nameof(name));
            
        if (height <= 0)
            throw new ArgumentException("断面高度必须大于0", nameof(height));
            
        if (topWidth < 0)
            throw new ArgumentException("顶部宽度不能小于0", nameof(topWidth));
            
        if (bottomWidth <= 0)
            throw new ArgumentException("底部宽度必须大于0", nameof(bottomWidth));

        Id = id;
        Name = name;
        Position = position ?? throw new ArgumentNullException(nameof(position));
        SectionType = sectionType;
        Height = height;
        TopWidth = topWidth;
        BottomWidth = bottomWidth;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 断面唯一标识
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// 断面名称
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// 断面位置坐标
    /// </summary>
    public Point3D Position { get; private set; }

    /// <summary>
    /// 断面类型
    /// </summary>
    public SectionType SectionType { get; private set; }

    /// <summary>
    /// 断面高度 (m)
    /// </summary>
    public double Height { get; private set; }

    /// <summary>
    /// 顶部宽度 (m)
    /// </summary>
    public double TopWidth { get; private set; }

    /// <summary>
    /// 底部宽度 (m)
    /// </summary>
    public double BottomWidth { get; private set; }

    /// <summary>
    /// 上游坡度 (水平:垂直)
    /// </summary>
    public double UpstreamSlope { get; private set; } = 0.0;

    /// <summary>
    /// 下游坡度 (水平:垂直)
    /// </summary>
    public double DownstreamSlope { get; private set; } = 0.8;

    /// <summary>
    /// 断面面积 (m²)
    /// </summary>
    public double Area => CalculateArea();

    /// <summary>
    /// 断面重心高度 (m)
    /// </summary>
    public double CentroidHeight => CalculateCentroidHeight();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    /// <summary>
    /// 更新断面名称
    /// </summary>
    /// <param name="name">新名称</param>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("断面名称不能为空", nameof(name));

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新断面几何参数
    /// </summary>
    /// <param name="height">高度</param>
    /// <param name="topWidth">顶宽</param>
    /// <param name="bottomWidth">底宽</param>
    public void UpdateGeometry(double height, double topWidth, double bottomWidth)
    {
        if (height <= 0)
            throw new ArgumentException("断面高度必须大于0", nameof(height));
        if (topWidth < 0)
            throw new ArgumentException("顶部宽度不能小于0", nameof(topWidth));
        if (bottomWidth <= 0)
            throw new ArgumentException("底部宽度必须大于0", nameof(bottomWidth));

        Height = height;
        TopWidth = topWidth;
        BottomWidth = bottomWidth;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新坡度信息
    /// </summary>
    /// <param name="upstreamSlope">上游坡度</param>
    /// <param name="downstreamSlope">下游坡度</param>
    public void UpdateSlopes(double upstreamSlope, double downstreamSlope)
    {
        if (upstreamSlope < 0)
            throw new ArgumentException("上游坡度不能小于0", nameof(upstreamSlope));
        if (downstreamSlope < 0)
            throw new ArgumentException("下游坡度不能小于0", nameof(downstreamSlope));

        UpstreamSlope = upstreamSlope;
        DownstreamSlope = downstreamSlope;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 验证断面数据的有效性
    /// </summary>
    /// <returns>是否有效</returns>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name) &&
               Height > 0 &&
               TopWidth >= 0 &&
               BottomWidth > 0 &&
               UpstreamSlope >= 0 &&
               DownstreamSlope >= 0 &&
               Position != null;
    }

    /// <summary>
    /// 计算断面面积
    /// </summary>
    /// <returns>断面面积</returns>
    private double CalculateArea()
    {
        // 梯形断面面积计算：A = (上底 + 下底) × 高 ÷ 2
        return (TopWidth + BottomWidth) * Height / 2.0;
    }

    /// <summary>
    /// 计算断面重心高度
    /// </summary>
    /// <returns>重心高度</returns>
    private double CalculateCentroidHeight()
    {
        // 梯形重心高度计算
        if (Math.Abs(TopWidth - BottomWidth) < 1e-6)
        {
            // 矩形断面
            return Height / 2.0;
        }
        else
        {
            // 梯形断面重心高度
            return Height * (2 * BottomWidth + TopWidth) / (3 * (BottomWidth + TopWidth));
        }
    }

    /// <summary>
    /// 获取断面描述
    /// </summary>
    /// <returns>断面描述字符串</returns>
    public override string ToString()
    {
        return $"断面 {Name} - 高度: {Height:F2}m, 顶宽: {TopWidth:F2}m, 底宽: {BottomWidth:F2}m, 类型: {SectionType}";
    }
}

/// <summary>
/// 断面类型枚举
/// </summary>
public enum SectionType
{
    /// <summary>
    /// 标准非溢流断面
    /// </summary>
    Standard,

    /// <summary>
    /// 溢流断面
    /// </summary>
    Spillway,

    /// <summary>
    /// 非溢流断面
    /// </summary>
    NonOverflow,

    /// <summary>
    /// 导流底孔断面
    /// </summary>
    Outlet,

    /// <summary>
    /// 特殊断面
    /// </summary>
    Special
} 