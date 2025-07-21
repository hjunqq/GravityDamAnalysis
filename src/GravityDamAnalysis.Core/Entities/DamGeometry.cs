using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 坝体几何信息
/// </summary>
public class DamGeometry
{
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public DamGeometry() { }

    /// <summary>
    /// 参数化构造函数
    /// </summary>
    /// <param name="volume">体积</param>
    /// <param name="boundingBox">边界框</param>
    public DamGeometry(double volume, BoundingBox3D boundingBox)
    {
        Volume = volume;
        BoundingBox = boundingBox ?? throw new ArgumentNullException(nameof(boundingBox));
        
        // 从边界框计算基本尺寸
        Height = boundingBox.Height;
        BaseWidth = boundingBox.Depth;
        Length = boundingBox.Width;
        
        // 估算坝顶宽度 (简化为底宽的30%)
        CrestWidth = BaseWidth * 0.3;
        
        // 设置中心点
        CenterPoint = boundingBox.Center;
    }

    /// <summary>
    /// 坝体体积 (m³)
    /// </summary>
    public double Volume { get; set; }

    /// <summary>
    /// 坝体高度 (m)
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 坝底宽度 (m)
    /// </summary>
    public double BaseWidth { get; set; }

    /// <summary>
    /// 坝顶宽度 (m)
    /// </summary>
    public double CrestWidth { get; set; }

    /// <summary>
    /// 坝轴方向长度 (m)
    /// </summary>
    public double Length { get; set; }

    /// <summary>
    /// 上游坡度 (垂直:水平)
    /// </summary>
    public double UpstreamSlope { get; set; }

    /// <summary>
    /// 下游坡度 (垂直:水平)
    /// </summary>
    public double DownstreamSlope { get; set; } = 0.8;

    /// <summary>
    /// 坝体中心点
    /// </summary>
    public Point3D CenterPoint { get; set; } = new(0, 0, 0);

    /// <summary>
    /// 边界框
    /// </summary>
    public BoundingBox3D BoundingBox { get; set; } = new();

    /// <summary>
    /// 验证几何信息有效性
    /// </summary>
    public bool IsValid()
    {
        return Volume > 0 && 
               Height > 0 && 
               BaseWidth > 0 && 
               Length > 0;
    }

    /// <summary>
    /// 计算高宽比
    /// </summary>
    public double GetHeightToBaseRatio()
    {
        return BaseWidth > 0 ? Height / BaseWidth : 0;
    }

    /// <summary>
    /// 计算坝体表面积（简化计算）
    /// </summary>
    public double GetSurfaceArea()
    {
        // 简化的表面积计算：前后面 + 上下面 + 左右面
        var frontBack = 2 * Height * BaseWidth;
        var topBottom = 2 * Length * BaseWidth;
        var leftRight = 2 * Height * Length;
        
        return frontBack + topBottom + leftRight;
    }

    /// <summary>
    /// 获取几何描述
    /// </summary>
    public override string ToString()
    {
        return $"体积: {Volume:F1}m³, 高度: {Height:F1}m, 底宽: {BaseWidth:F1}m, 长度: {Length:F1}m";
    }
} 