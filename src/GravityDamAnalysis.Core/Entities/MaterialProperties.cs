using System.ComponentModel.DataAnnotations;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 材料属性 - 包含重力坝材料的物理和力学特性
/// </summary>
public class MaterialProperties
{
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public MaterialProperties() { }

    /// <summary>
    /// 参数化构造函数
    /// </summary>
    /// <param name="name">材料名称</param>
    /// <param name="density">密度</param>
    /// <param name="elasticModulus">弹性模量</param>
    /// <param name="poissonRatio">泊松比</param>
    /// <param name="compressiveStrength">抗压强度</param>
    /// <param name="tensileStrength">抗拉强度</param>
    /// <param name="frictionCoefficient">摩擦系数</param>
    public MaterialProperties(
        string name,
        double density,
        double elasticModulus,
        double poissonRatio,
        double compressiveStrength,
        double tensileStrength,
        double frictionCoefficient)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Density = density;
        ElasticModulus = elasticModulus;
        PoissonRatio = poissonRatio;
        CompressiveStrength = compressiveStrength;
        TensileStrength = tensileStrength;
        FrictionCoefficient = frictionCoefficient;
    }

    /// <summary>
    /// 材料名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = "默认混凝土";

    /// <summary>
    /// 材料密度 (kN/m³)
    /// </summary>
    [Range(15.0, 35.0, ErrorMessage = "混凝土密度应在15-35 kN/m³范围内")]
    public double Density { get; set; } = 24.0;

    /// <summary>
    /// 抗压强度 (MPa)
    /// </summary>
    [Range(10.0, 100.0, ErrorMessage = "混凝土抗压强度应在10-100 MPa范围内")]
    public double CompressiveStrength { get; set; } = 30.0;

    /// <summary>
    /// 抗拉强度 (MPa)
    /// </summary>
    [Range(1.0, 10.0, ErrorMessage = "混凝土抗拉强度应在1-10 MPa范围内")]
    public double TensileStrength { get; set; } = 3.0;

    /// <summary>
    /// 弹性模量 (GPa)
    /// </summary>
    [Range(20.0, 50.0, ErrorMessage = "混凝土弹性模量应在20-50 GPa范围内")]
    public double ElasticModulus { get; set; } = 30.0;

    /// <summary>
    /// 泊松比
    /// </summary>
    [Range(0.1, 0.3, ErrorMessage = "混凝土泊松比应在0.1-0.3范围内")]
    public double PoissonRatio { get; set; } = 0.18;

    /// <summary>
    /// 摩擦系数 (用于坝基接触面)
    /// </summary>
    [Range(0.5, 1.2, ErrorMessage = "混凝土摩擦系数应在0.5-1.2范围内")]
    public double FrictionCoefficient { get; set; } = 0.75;

    /// <summary>
    /// 材料等级 (如 C30, C40等)
    /// </summary>
    public string Grade { get; set; } = "C30";

    /// <summary>
    /// 材料类型
    /// </summary>
    public MaterialType Type { get; set; } = MaterialType.Concrete;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 验证材料属性有效性
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name) &&
               Density > 0 &&
               CompressiveStrength > 0 &&
               TensileStrength > 0 &&
               ElasticModulus > 0 &&
               PoissonRatio > 0 &&
               FrictionCoefficient > 0;
    }

    /// <summary>
    /// 获取材料特征强度（设计值）
    /// </summary>
    /// <param name="safetyFactor">安全系数</param>
    /// <returns>设计抗压强度</returns>
    public double GetDesignCompressiveStrength(double safetyFactor = 1.4)
    {
        return CompressiveStrength / safetyFactor;
    }

    /// <summary>
    /// 获取材料描述
    /// </summary>
    public override string ToString()
    {
        return $"{Name} - 密度: {Density:F1} kN/m³, 抗压: {CompressiveStrength:F1} MPa, 弹模: {ElasticModulus:F1} GPa";
    }

    /// <summary>
    /// 创建标准混凝土材料
    /// </summary>
    /// <param name="grade">混凝土等级</param>
    /// <returns>标准混凝土材料属性</returns>
    public static MaterialProperties CreateStandardConcrete(string grade)
    {
        var properties = new MaterialProperties
        {
            Name = $"{grade}混凝土",
            Grade = grade,
            Type = MaterialType.Concrete,
            Density = 24.0,
            PoissonRatio = 0.18,
            FrictionCoefficient = 0.75
        };

        // 根据等级设置强度和模量
        switch (grade.ToUpperInvariant())
        {
            case "C20":
                properties.CompressiveStrength = 20.0;
                properties.TensileStrength = 2.0;
                properties.ElasticModulus = 25.5;
                break;
            case "C25":
                properties.CompressiveStrength = 25.0;
                properties.TensileStrength = 2.5;
                properties.ElasticModulus = 28.0;
                break;
            case "C30":
                properties.CompressiveStrength = 30.0;
                properties.TensileStrength = 3.0;
                properties.ElasticModulus = 30.0;
                break;
            case "C35":
                properties.CompressiveStrength = 35.0;
                properties.TensileStrength = 3.5;
                properties.ElasticModulus = 31.5;
                break;
            case "C40":
                properties.CompressiveStrength = 40.0;
                properties.TensileStrength = 4.0;
                properties.ElasticModulus = 32.5;
                break;
            default:
                properties.CompressiveStrength = 30.0;
                properties.TensileStrength = 3.0;
                properties.ElasticModulus = 30.0;
                break;
        }

        return properties;
    }
}

/// <summary>
/// 材料类型枚举
/// </summary>
public enum MaterialType
{
    /// <summary>
    /// 混凝土
    /// </summary>
    Concrete,

    /// <summary>
    /// 砌石
    /// </summary>
    Masonry,

    /// <summary>
    /// 土石
    /// </summary>
    EarthRock,

    /// <summary>
    /// 钢材
    /// </summary>
    Steel,

    /// <summary>
    /// 复合材料
    /// </summary>
    Composite
} 