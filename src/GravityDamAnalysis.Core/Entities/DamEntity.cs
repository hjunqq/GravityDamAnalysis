using GravityDamAnalysis.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 重力坝实体 - 代表一个完整的重力坝结构
/// </summary>
public class DamEntity
{
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public DamEntity() { }

    /// <summary>
    /// 参数化构造函数
    /// </summary>
    /// <param name="id">坝体ID</param>
    /// <param name="name">坝体名称</param>
    /// <param name="geometry">几何信息</param>
    /// <param name="materialProperties">材料属性</param>
    public DamEntity(Guid id, string name, DamGeometry geometry, MaterialProperties materialProperties)
    {
        Id = id;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        MaterialProperties = materialProperties ?? throw new ArgumentNullException(nameof(materialProperties));
        
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("坝体名称不能为空", nameof(name));
            
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 坝体唯一标识符
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Revit元素ID
    /// </summary>
    public long RevitElementId { get; set; }

    /// <summary>
    /// 坝体名称
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 坝体几何信息
    /// </summary>
    public DamGeometry Geometry { get; set; } = new();

    /// <summary>
    /// 材料属性
    /// </summary>
    public MaterialProperties MaterialProperties { get; set; } = new();

    /// <summary>
    /// 坝体分类
    /// </summary>
    public DamClassification Classification { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 坝体断面集合
    /// </summary>
    public List<DamSection> Sections { get; set; } = new();

    /// <summary>
    /// 更新坝体名称
    /// </summary>
    /// <param name="name">新名称</param>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("坝体名称不能为空", nameof(name));
            
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新几何信息
    /// </summary>
    /// <param name="geometry">新的几何信息</param>
    public void UpdateGeometry(DamGeometry geometry)
    {
        Geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 更新材料属性
    /// </summary>
    /// <param name="materialProperties">新的材料属性</param>
    public void UpdateMaterialProperties(MaterialProperties materialProperties)
    {
        MaterialProperties = materialProperties ?? throw new ArgumentNullException(nameof(materialProperties));
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 验证坝体实体的有效性
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Name) &&
               Geometry.IsValid() &&
               MaterialProperties.IsValid();
    }

    /// <summary>
    /// 获取坝体描述信息
    /// </summary>
    public string GetDescription()
    {
        return $"{Name} - 高度: {Geometry.Height:F2}m, 底宽: {Geometry.BaseWidth:F2}m, 类型: {Classification.Type}";
    }
} 