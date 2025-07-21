using GravityDamAnalysis.Core.Entities;

namespace GravityDamAnalysis.Core.Services;

/// <summary>
/// 重力坝实体识别服务接口
/// 负责从Revit元素中识别和验证重力坝实体
/// </summary>
public interface IDamEntityRecognitionService
{
    /// <summary>
    /// 从选中的元素识别重力坝实体
    /// </summary>
    /// <param name="elementId">Revit元素ID</param>
    /// <returns>识别的坝体实体，如果识别失败返回null</returns>
    Task<DamEntity?> RecognizeSelectedElementAsync(int elementId);

    /// <summary>
    /// 在文档中查找所有重力坝实体
    /// </summary>
    /// <returns>找到的坝体实体列表</returns>
    Task<List<DamEntity>> FindAllDamEntitiesAsync();

    /// <summary>
    /// 验证元素是否为有效的重力坝实体
    /// </summary>
    /// <param name="elementId">Revit元素ID</param>
    /// <returns>验证结果</returns>
    Task<ValidationResult> ValidateElementAsync(int elementId);

    /// <summary>
    /// 对坝体实体进行分类
    /// </summary>
    /// <param name="damEntity">坝体实体</param>
    /// <returns>分类结果</returns>
    Task<DamClassification> ClassifyDamAsync(DamEntity damEntity);
}

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 是否有效
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// 错误消息列表
    /// </summary>
    public List<string> ErrorMessages { get; set; } = new();

    /// <summary>
    /// 警告消息列表
    /// </summary>
    public List<string> WarningMessages { get; set; } = new();

    /// <summary>
    /// 添加错误消息
    /// </summary>
    public void AddError(string message)
    {
        ErrorMessages.Add(message);
        IsValid = false;
    }

    /// <summary>
    /// 添加警告消息
    /// </summary>
    public void AddWarning(string message)
    {
        WarningMessages.Add(message);
    }
}

/// <summary>
/// 坝体分类信息
/// </summary>
public class DamClassification
{
    /// <summary>
    /// 坝体类型
    /// </summary>
    public DamType Type { get; set; } = DamType.Gravity;

    /// <summary>
    /// 几何形式
    /// </summary>
    public DamGeometryType GeometryType { get; set; } = DamGeometryType.Trapezoidal;

    /// <summary>
    /// 建造材料
    /// </summary>
    public DamMaterial Material { get; set; } = DamMaterial.Concrete;

    /// <summary>
    /// 规模等级
    /// </summary>
    public DamScale Scale { get; set; } = DamScale.Medium;

    /// <summary>
    /// 分类置信度 (0-1)
    /// </summary>
    public double Confidence { get; set; } = 1.0;
}

/// <summary>
/// 坝体类型枚举
/// </summary>
public enum DamType
{
    /// <summary>重力坝</summary>
    Gravity,
    /// <summary>拱坝</summary>
    Arch,
    /// <summary>土石坝</summary>
    Embankment,
    /// <summary>支墩坝</summary>
    Buttress
}

/// <summary>
/// 坝体几何类型枚举
/// </summary>
public enum DamGeometryType
{
    /// <summary>矩形</summary>
    Rectangular,
    /// <summary>梯形</summary>
    Trapezoidal,
    /// <summary>三角形</summary>
    Triangular,
    /// <summary>复合形状</summary>
    Complex
}

/// <summary>
/// 坝体材料类型枚举
/// </summary>
public enum DamMaterial
{
    /// <summary>混凝土</summary>
    Concrete,
    /// <summary>石砌</summary>
    Masonry,
    /// <summary>土料</summary>
    Earth,
    /// <summary>混合材料</summary>
    Composite
}

/// <summary>
/// 坝体规模等级枚举
/// </summary>
public enum DamScale
{
    /// <summary>小型 (高度 &lt; 30m)</summary>
    Small,
    /// <summary>中型 (30m ≤ 高度 &lt; 70m)</summary>
    Medium,
    /// <summary>大型 (70m ≤ 高度 &lt; 200m)</summary>
    Large,
    /// <summary>特大型 (高度 ≥ 200m)</summary>
    ExtraLarge
} 