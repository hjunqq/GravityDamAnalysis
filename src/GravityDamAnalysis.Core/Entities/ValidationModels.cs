using System;
using System.Collections.Generic;
using System.Linq;
using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Core.Entities;

/// <summary>
/// 验证状态枚举
/// </summary>
public enum ValidationStatus
{
    Pending,           // 待验证
    UserReviewing,     // 用户审查中
    NeedsAdjustment,   // 需要调整
    HasIssues,         // 存在问题
    Validated,         // 已验证
    CalculationReady   // 计算就绪
}

/// <summary>
/// 问题类型枚举
/// </summary>
public enum IssueType
{
    OpenContour,           // 轮廓未闭合
    InvalidSlope,          // 坡度不合理
    MissingFoundation,     // 缺少基础线
    InvalidDimensions,     // 尺寸不合理
    MissingDrainageSystem, // 缺少排水系统
    MaterialZoneOverlap    // 材料分区重叠
}

/// <summary>
/// 问题严重程度枚举
/// </summary>
public enum IssueSeverity
{
    Info,        // 信息提示
    Warning,     // 警告
    Error,       // 错误
    Critical     // 严重错误
}

/// <summary>
/// 几何问题实体类
/// 记录在二维剖面验证过程中发现的几何问题
/// </summary>
public class GeometryIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public IssueType Type { get; set; }
    public IssueSeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public Point2D? Location { get; set; }
    public string SuggestedFix { get; set; } = string.Empty;
    public bool CanAutoFix { get; set; }
    public DateTime DiscoveredAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// 是否为阻塞性问题（需要用户处理才能继续）
    /// </summary>
    public bool IsBlocking => Severity >= IssueSeverity.Error;
}

/// <summary>
/// 边界条件类型
/// </summary>
public class BoundaryCondition
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // 固定约束、法向约束、弹性约束等
    public Point2D StartPoint { get; set; }
    public Point2D EndPoint { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// 用户标注类
/// 用户在验证过程中添加的标注信息
/// </summary>
public class UserAnnotation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Text { get; set; } = string.Empty;
    public Point2D Position { get; set; }
    public string Type { get; set; } = "General"; // General, Dimension, Warning等
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = Environment.UserName;
}

/// <summary>
/// 剖面显示设置
/// 控制2D剖面的可视化显示选项
/// </summary>
public class ProfileDisplaySettings
{
    public bool ShowDimensions { get; set; } = true;
    public bool ShowMaterialZones { get; set; } = true;
    public bool ShowBoundaryConditions { get; set; } = true;
    public bool ShowGrid { get; set; } = false;
    public bool ShowValidationMarkers { get; set; } = true;
    public double ZoomLevel { get; set; } = 1.0;
    public Point2D ViewCenter { get; set; } = new Point2D(0, 0);
}

/// <summary>
/// 几何验证结果
/// </summary>
public class GeometryValidationResult
{
    public List<GeometryIssue> Issues { get; set; } = new List<GeometryIssue>();
    public bool HasCriticalIssues { get; set; }
    public bool PassedValidation { get; set; }
    public double GeometryScore { get; set; } // 0-1之间，1表示完美
    public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
}

/// <summary>
/// 工程验证结果
/// </summary>
public class EngineeringValidationResult
{
    public List<GeometryIssue> Issues { get; set; } = new List<GeometryIssue>();
    public bool EngineeringStandardsCompliant { get; set; }
    public bool RequiresEngineerReview { get; set; }
    public double EngineeringScore { get; set; } // 0-1之间，1表示完全符合工程标准
    public List<string> ApplicableStandards { get; set; } = new List<string>();
}

/// <summary>
/// 边界条件验证结果
/// </summary>
public class BoundaryConditionValidationResult
{
    public List<GeometryIssue> Issues { get; set; } = new List<GeometryIssue>();
    public bool BoundaryConditionsComplete { get; set; }
    public double CompletenessScore { get; set; } // 0-1之间，1表示边界条件完整
    public List<string> MissingConditions { get; set; } = new List<string>();
}

/// <summary>
/// 完整的剖面验证结果
/// </summary>
public class ProfileValidationResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime ValidationTime { get; set; } = DateTime.Now;
    public GeometryValidationResult GeometryValidation { get; set; } = new GeometryValidationResult();
    public EngineeringValidationResult EngineeringValidation { get; set; } = new EngineeringValidationResult();
    public BoundaryConditionValidationResult BoundaryConditionValidation { get; set; } = new BoundaryConditionValidationResult();
    public ValidationStatus OverallStatus { get; set; } = ValidationStatus.Pending;
    public double OverallScore { get; set; } // 综合评分
    
    /// <summary>
    /// 获取所有问题
    /// </summary>
    public List<GeometryIssue> GetAllIssues()
    {
        var allIssues = new List<GeometryIssue>();
        allIssues.AddRange(GeometryValidation.Issues);
        allIssues.AddRange(EngineeringValidation.Issues);
        allIssues.AddRange(BoundaryConditionValidation.Issues);
        return allIssues;
    }
    
    /// <summary>
    /// 是否有阻塞性问题
    /// </summary>
    public bool HasCriticalIssues => GetAllIssues().Any(i => i.IsBlocking);
    
    /// <summary>
    /// 是否需要用户审查
    /// </summary>
    public bool RequiresUserReview => HasCriticalIssues || 
        EngineeringValidation.RequiresEngineerReview ||
        OverallScore < 0.8;
}

/// <summary>
/// 自动修正建议
/// </summary>
public class AutoCorrectSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // FillGap, AdjustSlope, AddBoundary等
    public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
    public double ConfidenceLevel { get; set; } // 0-1之间，建议的可信度
    public bool RequiresUserConfirmation { get; set; } = true;
}

/// <summary>
/// 提取质量度量
/// </summary>
public class ExtractionQualityMetrics
{
    public double OverallScore { get; set; } // 总体质量评分
    public double GeometricAccuracy { get; set; } // 几何精度
    public double FeatureCompleteness { get; set; } // 特征完整性
    public double DataConsistency { get; set; } // 数据一致性
    public double ProcessingEfficiency { get; set; } // 处理效率
    public TimeSpan ExtractionTime { get; set; } // 提取耗时
    public Dictionary<string, double> DetailedMetrics { get; set; } = new Dictionary<string, double>();
}

/// <summary>
/// 带验证的剖面提取结果
/// </summary>
public class ProfileExtractionResult
{
    public EnhancedProfile2D Profile { get; set; } = new EnhancedProfile2D();
    public ProfileValidationResult ValidationResults { get; set; } = new ProfileValidationResult();
    public bool RequiresUserReview { get; set; }
    public List<AutoCorrectSuggestion> AutoCorrectSuggestions { get; set; } = new List<AutoCorrectSuggestion>();
    public ExtractionQualityMetrics QualityMetrics { get; set; } = new ExtractionQualityMetrics();
    public DateTime ExtractionTime { get; set; } = DateTime.Now;
    public string ExtractionMethod { get; set; } = string.Empty;
} 