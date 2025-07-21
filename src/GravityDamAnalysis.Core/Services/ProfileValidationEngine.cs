using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Core.Services;

/// <summary>
/// 剖面验证引擎
/// 负责对二维剖面进行几何一致性、工程合理性和边界条件完整性验证
/// </summary>
public class ProfileValidationEngine
{
    private readonly ILogger<ProfileValidationEngine> _logger;
    
    // 工程标准参数
    private const double MIN_DAM_HEIGHT = 5.0;          // 最小坝高 (m)
    private const double MAX_DAM_HEIGHT = 300.0;        // 最大坝高 (m)
    private const double MIN_UPSTREAM_SLOPE = 0.05;     // 最小上游坡度
    private const double MAX_UPSTREAM_SLOPE = 1.0;      // 最大上游坡度
    private const double MIN_DOWNSTREAM_SLOPE = 0.7;    // 最小下游坡度
    private const double MAX_DOWNSTREAM_SLOPE = 1.5;    // 最大下游坡度
    private const double GEOMETRIC_TOLERANCE = 0.001;   // 几何容差 (m)
    
    public ProfileValidationEngine(ILogger<ProfileValidationEngine> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 执行完整的剖面验证
    /// 包括几何验证、工程验证和边界条件验证
    /// </summary>
    public ProfileValidationResult ValidateProfile(EnhancedProfile2D profile)
    {
        _logger.LogInformation("开始验证剖面 {ProfileName}", profile.Name);
        
        var result = new ProfileValidationResult();
        
        try
        {
            // 几何一致性检查
            result.GeometryValidation = ValidateGeometry(profile);
            
            // 工程合理性检查
            result.EngineeringValidation = ValidateEngineering(profile);
            
            // 边界条件检查
            result.BoundaryConditionValidation = ValidateBoundaryConditions(profile);
            
            // 综合评估
            result.OverallStatus = DetermineOverallStatus(result);
            result.OverallScore = CalculateOverallScore(result);
            
            _logger.LogInformation("剖面验证完成，总体评分: {Score:P}", result.OverallScore);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "剖面验证过程中发生错误");
            
            result.GeometryValidation.Issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Critical,
                Description = $"验证过程发生错误: {ex.Message}",
                SuggestedFix = "检查剖面数据完整性或联系技术支持",
                CanAutoFix = false
            });
            
            result.OverallStatus = ValidationStatus.HasIssues;
            return result;
        }
    }
    
    /// <summary>
    /// 几何一致性验证
    /// 检查剖面几何的基本正确性和完整性
    /// </summary>
    public GeometryValidationResult ValidateGeometry(EnhancedProfile2D profile)
    {
        _logger.LogDebug("开始几何验证");
        
        var issues = new List<GeometryIssue>();
        var metrics = new Dictionary<string, object>();
        
        // 1. 检查轮廓闭合性
        ValidateContourClosure(profile, issues);
        
        // 2. 检查尺寸合理性
        ValidateDimensions(profile, issues, metrics);
        
        // 3. 检查坡度合理性
        ValidateSlopes(profile, issues, metrics);
        
        // 4. 检查几何连续性
        ValidateGeometricContinuity(profile, issues);
        
        // 5. 检查自相交
        ValidateSelfIntersection(profile, issues);
        
        var result = new GeometryValidationResult
        {
            Issues = issues,
            HasCriticalIssues = issues.Any(i => i.Severity == IssueSeverity.Critical),
            PassedValidation = !issues.Any(i => i.Severity >= IssueSeverity.Error),
            GeometryScore = CalculateGeometryScore(issues, metrics),
            Metrics = metrics
        };
        
        _logger.LogDebug("几何验证完成，发现 {IssueCount} 个问题", issues.Count);
        return result;
    }
    
    /// <summary>
    /// 工程合理性验证
    /// 检查剖面是否符合重力坝工程设计标准
    /// </summary>
    public EngineeringValidationResult ValidateEngineering(EnhancedProfile2D profile)
    {
        _logger.LogDebug("开始工程合理性验证");
        
        var issues = new List<GeometryIssue>();
        var applicableStandards = new List<string> { "SL 319-2018", "DL/T 5108-2020" };
        
        // 1. 检查基础接触面
        ValidateFoundation(profile, issues);
        
        // 2. 检查排水系统
        ValidateDrainageSystem(profile, issues);
        
        // 3. 检查材料分区
        ValidateMaterialZones(profile, issues);
        
        // 4. 检查结构稳定性指标
        ValidateStructuralStability(profile, issues);
        
        // 5. 检查工程构造要求
        ValidateConstructionRequirements(profile, issues);
        
        var result = new EngineeringValidationResult
        {
            Issues = issues,
            EngineeringStandardsCompliant = !issues.Any(i => i.Severity >= IssueSeverity.Error),
            RequiresEngineerReview = issues.Any(i => i.Severity >= IssueSeverity.Warning),
            EngineeringScore = CalculateEngineeringScore(issues),
            ApplicableStandards = applicableStandards
        };
        
        _logger.LogDebug("工程验证完成，发现 {IssueCount} 个问题", issues.Count);
        return result;
    }
    
    /// <summary>
    /// 边界条件验证
    /// 检查分析所需的边界条件是否完整和合理
    /// </summary>
    public BoundaryConditionValidationResult ValidateBoundaryConditions(EnhancedProfile2D profile)
    {
        _logger.LogDebug("开始边界条件验证");
        
        var issues = new List<GeometryIssue>();
        var missingConditions = new List<string>();
        
        // 1. 检查水位设置
        ValidateWaterLevels(profile, issues, missingConditions);
        
        // 2. 检查基础约束
        ValidateFoundationConstraints(profile, issues, missingConditions);
        
        // 3. 检查荷载条件
        ValidateLoadConditions(profile, issues, missingConditions);
        
        // 4. 检查材料参数
        ValidateMaterialParameters(profile, issues, missingConditions);
        
        var completenessScore = CalculateCompletenessScore(missingConditions.Count, issues);
        
        var result = new BoundaryConditionValidationResult
        {
            Issues = issues,
            BoundaryConditionsComplete = missingConditions.Count == 0 && !issues.Any(i => i.Severity >= IssueSeverity.Error),
            CompletenessScore = completenessScore,
            MissingConditions = missingConditions
        };
        
        _logger.LogDebug("边界条件验证完成，缺失 {MissingCount} 个条件", missingConditions.Count);
        return result;
    }
    
    #region 几何验证具体实现
    
    /// <summary>
    /// 验证轮廓闭合性
    /// </summary>
    private void ValidateContourClosure(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        if (profile.MainContour == null || profile.MainContour.Count < 3)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.OpenContour,
                Severity = IssueSeverity.Critical,
                Description = "主轮廓无效或点数不足",
                SuggestedFix = "确保剖面包含至少3个有效点",
                CanAutoFix = false
            });
            return;
        }
        
        var first = profile.MainContour.First();
        var last = profile.MainContour.Last();
        var distance = Math.Sqrt(Math.Pow(first.X - last.X, 2) + Math.Pow(first.Y - last.Y, 2));
        
        if (distance > GEOMETRIC_TOLERANCE)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.OpenContour,
                Severity = IssueSeverity.Error,
                Description = $"坝体轮廓未完全闭合，起止点距离 {distance:F3}m",
                Location = new Point2D((first.X + last.X) / 2, (first.Y + last.Y) / 2),
                SuggestedFix = "检查几何提取参数，确保切割平面完全穿过坝体",
                CanAutoFix = true
            });
        }
    }
    
    /// <summary>
    /// 验证尺寸合理性
    /// </summary>
    private void ValidateDimensions(EnhancedProfile2D profile, List<GeometryIssue> issues, Dictionary<string, object> metrics)
    {
        if (profile.MainContour == null || !profile.MainContour.Any()) return;
        
        var minX = profile.MainContour.Min(p => p.X);
        var maxX = profile.MainContour.Max(p => p.X);
        var minY = profile.MainContour.Min(p => p.Y);
        var maxY = profile.MainContour.Max(p => p.Y);
        
        var damWidth = maxX - minX;
        var damHeight = maxY - minY;
        
        metrics["DamWidth"] = damWidth;
        metrics["DamHeight"] = damHeight;
        metrics["AspectRatio"] = damWidth / Math.Max(damHeight, 0.1);
        
        // 检查坝高
        if (damHeight < MIN_DAM_HEIGHT)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Warning,
                Description = $"坝高 {damHeight:F1}m 低于常规最小值 {MIN_DAM_HEIGHT}m",
                SuggestedFix = "检查模型单位设置或几何提取范围",
                CanAutoFix = false
            });
        }
        
        if (damHeight > MAX_DAM_HEIGHT)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Warning,
                Description = $"坝高 {damHeight:F1}m 超过常规最大值 {MAX_DAM_HEIGHT}m",
                SuggestedFix = "确认是否为超高坝，如是请调整验证参数",
                CanAutoFix = false
            });
        }
        
        // 检查宽高比
        var aspectRatio = damWidth / Math.Max(damHeight, 0.1);
        if (aspectRatio < 0.5 || aspectRatio > 15.0)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Warning,
                Description = $"坝体宽高比 {aspectRatio:F2} 可能不合理",
                SuggestedFix = "检查剖面提取方向或坝轴线定义",
                CanAutoFix = false
            });
        }
    }
    
    /// <summary>
    /// 验证坡度合理性
    /// </summary>
    private void ValidateSlopes(EnhancedProfile2D profile, List<GeometryIssue> issues, Dictionary<string, object> metrics)
    {
        if (profile.MainContour == null || profile.MainContour.Count < 3) return;
        
        var centerX = profile.MainContour.Average(p => p.X);
        var upstreamSlope = CalculateUpstreamSlope(profile.MainContour, centerX);
        var downstreamSlope = CalculateDownstreamSlope(profile.MainContour, centerX);
        
        metrics["UpstreamSlope"] = upstreamSlope;
        metrics["DownstreamSlope"] = downstreamSlope;
        
        // 验证上游面坡度
        if (upstreamSlope < MIN_UPSTREAM_SLOPE || upstreamSlope > MAX_UPSTREAM_SLOPE)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidSlope,
                Severity = IssueSeverity.Warning,
                Description = $"上游面坡度 {upstreamSlope:F3} 超出推荐范围 ({MIN_UPSTREAM_SLOPE:F2}-{MAX_UPSTREAM_SLOPE:F2})",
                SuggestedFix = "检查坝体几何设计或验证坡度计算方法",
                CanAutoFix = false
            });
        }
        
        // 验证下游面坡度
        if (downstreamSlope < MIN_DOWNSTREAM_SLOPE || downstreamSlope > MAX_DOWNSTREAM_SLOPE)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidSlope,
                Severity = IssueSeverity.Warning,
                Description = $"下游面坡度 {downstreamSlope:F3} 超出推荐范围 ({MIN_DOWNSTREAM_SLOPE:F2}-{MAX_DOWNSTREAM_SLOPE:F2})",
                SuggestedFix = "检查坝体几何设计或验证坡度计算方法",
                CanAutoFix = false
            });
        }
    }
    
    /// <summary>
    /// 验证几何连续性
    /// </summary>
    private void ValidateGeometricContinuity(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        if (profile.MainContour == null || profile.MainContour.Count < 2) return;
        
        const double maxSegmentLength = 50.0; // 最大线段长度
        const double minSegmentLength = 0.01; // 最小线段长度
        
        for (int i = 0; i < profile.MainContour.Count - 1; i++)
        {
            var p1 = profile.MainContour[i];
            var p2 = profile.MainContour[i + 1];
            var segmentLength = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));
            
            if (segmentLength > maxSegmentLength)
            {
                issues.Add(new GeometryIssue
                {
                    Type = IssueType.OpenContour,
                    Severity = IssueSeverity.Warning,
                    Description = $"存在过长的几何线段 ({segmentLength:F2}m)，可能影响精度",
                    Location = new Point2D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2),
                    SuggestedFix = "增加几何提取精度或检查原始模型",
                    CanAutoFix = false
                });
            }
            
            if (segmentLength < minSegmentLength)
            {
                issues.Add(new GeometryIssue
                {
                    Type = IssueType.OpenContour,
                    Severity = IssueSeverity.Info,
                    Description = $"存在极短的几何线段 ({segmentLength:F4}m)，可以优化",
                    Location = new Point2D((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2),
                    SuggestedFix = "简化冗余点或优化几何精度",
                    CanAutoFix = true
                });
            }
        }
    }
    
    /// <summary>
    /// 验证自相交
    /// </summary>
    private void ValidateSelfIntersection(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        if (profile.MainContour == null || profile.MainContour.Count < 4) return;
        
        var contour = profile.MainContour;
        
        for (int i = 0; i < contour.Count - 1; i++)
        {
            for (int j = i + 2; j < contour.Count - 1; j++)
            {
                // 避免检查相邻线段
                if (Math.Abs(i - j) <= 1 || (i == 0 && j == contour.Count - 2)) continue;
                
                var p1 = contour[i];
                var p2 = contour[i + 1];
                var p3 = contour[j];
                var p4 = contour[j + 1];
                
                if (LineSegmentsIntersect(p1, p2, p3, p4, out Point2D intersection))
                {
                    issues.Add(new GeometryIssue
                    {
                        Type = IssueType.OpenContour,
                        Severity = IssueSeverity.Error,
                        Description = "检测到轮廓自相交，可能导致计算错误",
                        Location = intersection,
                        SuggestedFix = "检查几何提取算法或原始模型的完整性",
                        CanAutoFix = false
                    });
                }
            }
        }
    }
    
    #endregion
    
    #region 工程验证具体实现
    
    /// <summary>
    /// 验证基础接触面
    /// </summary>
    private void ValidateFoundation(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        // 检查基础线是否存在
        if (profile.FoundationContour == null || profile.FoundationContour.Count < 2)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.MissingFoundation,
                Severity = IssueSeverity.Error,
                Description = "缺少基础接触面信息",
                SuggestedFix = "确保Revit模型包含基础元素，或手动标注基础线",
                CanAutoFix = false
            });
            return;
        }
        
        // 检查基础线与坝体轮廓的连接
        var foundationStart = profile.FoundationContour.First();
        var foundationEnd = profile.FoundationContour.Last();
        
        var damPoints = profile.MainContour;
        var minDistanceStart = damPoints.Min(p => Math.Sqrt(Math.Pow(p.X - foundationStart.X, 2) + Math.Pow(p.Y - foundationStart.Y, 2)));
        var minDistanceEnd = damPoints.Min(p => Math.Sqrt(Math.Pow(p.X - foundationEnd.X, 2) + Math.Pow(p.Y - foundationEnd.Y, 2)));
        
        if (minDistanceStart > GEOMETRIC_TOLERANCE || minDistanceEnd > GEOMETRIC_TOLERANCE)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.MissingFoundation,
                Severity = IssueSeverity.Warning,
                Description = "基础线与坝体轮廓连接不良",
                SuggestedFix = "检查基础与坝体的几何连接或调整提取精度",
                CanAutoFix = true
            });
        }
    }
    
    /// <summary>
    /// 验证排水系统
    /// </summary>
    private void ValidateDrainageSystem(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        if (!profile.Features.ContainsKey("DrainageSystem") && !profile.Features.ContainsKey("排水系统"))
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.MissingDrainageSystem,
                Severity = IssueSeverity.Warning,
                Description = "未检测到排水系统特征",
                SuggestedFix = "验证排水廊道、排水孔等构造是否正确建模",
                CanAutoFix = false
            });
        }
    }
    
    /// <summary>
    /// 验证材料分区
    /// </summary>
    private void ValidateMaterialZones(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        if (profile.MaterialZones.Count == 0)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.MaterialZoneOverlap,
                Severity = IssueSeverity.Warning,
                Description = "未定义材料分区",
                SuggestedFix = "为坝体不同部位指定材料属性",
                CanAutoFix = true
            });
            return;
        }
        
        // 检查材料分区重叠
        for (int i = 0; i < profile.MaterialZones.Count; i++)
        {
            for (int j = i + 1; j < profile.MaterialZones.Count; j++)
            {
                var zone1 = profile.MaterialZones[i];
                var zone2 = profile.MaterialZones[j];
                
                if (CheckZoneOverlap(zone1, zone2))
                {
                    issues.Add(new GeometryIssue
                    {
                        Type = IssueType.MaterialZoneOverlap,
                        Severity = IssueSeverity.Warning,
                        Description = $"材料分区 '{zone1.Name}' 与 '{zone2.Name}' 存在重叠",
                        SuggestedFix = "调整材料分区边界或合并重叠区域",
                        CanAutoFix = false
                    });
                }
            }
        }
    }
    
    /// <summary>
    /// 验证结构稳定性指标
    /// </summary>
    private void ValidateStructuralStability(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        // 简化的稳定性检查 - 检查基本的几何比例
        if (profile.MainContour == null || !profile.MainContour.Any()) return;
        
        var damHeight = profile.MainContour.Max(p => p.Y) - profile.MainContour.Min(p => p.Y);
        var baseWidth = profile.MainContour.Max(p => p.X) - profile.MainContour.Min(p => p.X);
        
        var heightToBaseRatio = damHeight / Math.Max(baseWidth, 0.1);
        
        // 重力坝的高宽比通常应在合理范围内
        if (heightToBaseRatio > 2.0)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Warning,
                Description = $"坝体高宽比 {heightToBaseRatio:F2} 可能过大，需验证稳定性",
                SuggestedFix = "进行详细的稳定性分析或调整坝体几何",
                CanAutoFix = false
            });
        }
    }
    
    /// <summary>
    /// 验证工程构造要求
    /// </summary>
    private void ValidateConstructionRequirements(EnhancedProfile2D profile, List<GeometryIssue> issues)
    {
        // 检查坝顶宽度
        if (profile.MainContour != null && profile.MainContour.Any())
        {
            var maxY = profile.MainContour.Max(p => p.Y);
            var topPoints = profile.MainContour.Where(p => Math.Abs(p.Y - maxY) < GEOMETRIC_TOLERANCE).ToList();
            
            if (topPoints.Count >= 2)
            {
                var topWidth = topPoints.Max(p => p.X) - topPoints.Min(p => p.X);
                
                if (topWidth < 3.0) // 坝顶最小宽度通常为3m
                {
                    issues.Add(new GeometryIssue
                    {
                        Type = IssueType.InvalidDimensions,
                        Severity = IssueSeverity.Warning,
                        Description = $"坝顶宽度 {topWidth:F1}m 可能小于规范要求",
                        SuggestedFix = "检查坝顶设计是否符合相关规范要求",
                        CanAutoFix = false
                    });
                }
            }
        }
    }
    
    #endregion
    
    #region 边界条件验证具体实现
    
    /// <summary>
    /// 验证水位设置
    /// </summary>
    private void ValidateWaterLevels(EnhancedProfile2D profile, List<GeometryIssue> issues, List<string> missingConditions)
    {
        if (profile.WaterLevels.UpstreamWaterLevel <= 0)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Warning,
                Description = "上游水位未设置或设置不合理",
                SuggestedFix = "设置合理的上游水位值",
                CanAutoFix = true
            });
            missingConditions.Add("上游水位");
        }
        
        if (profile.WaterLevels.DownstreamWaterLevel < 0)
        {
            issues.Add(new GeometryIssue
            {
                Type = IssueType.InvalidDimensions,
                Severity = IssueSeverity.Info,
                Description = "下游水位未设置，将按无水计算",
                SuggestedFix = "如有下游水位，请设置相应值",
                CanAutoFix = true
            });
        }
        
        // 检查水位逻辑合理性
        if (profile.WaterLevels.UpstreamWaterLevel > 0 && profile.WaterLevels.DownstreamWaterLevel > 0)
        {
            if (profile.WaterLevels.UpstreamWaterLevel <= profile.WaterLevels.DownstreamWaterLevel)
            {
                issues.Add(new GeometryIssue
                {
                    Type = IssueType.InvalidDimensions,
                    Severity = IssueSeverity.Error,
                    Description = "上游水位应高于下游水位",
                    SuggestedFix = "检查水位设置的正确性",
                    CanAutoFix = false
                });
            }
        }
    }
    
    /// <summary>
    /// 验证基础约束
    /// </summary>
    private void ValidateFoundationConstraints(EnhancedProfile2D profile, List<GeometryIssue> issues, List<string> missingConditions)
    {
        if (!profile.BoundaryConditionDict.ContainsKey("Foundation") && 
            !profile.BoundaryConditionDict.ContainsKey("基础约束"))
        {
            missingConditions.Add("基础约束条件");
            
            issues.Add(new GeometryIssue
            {
                Type = IssueType.MissingFoundation,
                Severity = IssueSeverity.Warning,
                Description = "未定义基础约束条件",
                SuggestedFix = "设置基础的约束类型（固定、法向或弹性约束）",
                CanAutoFix = true
            });
        }
    }
    
    /// <summary>
    /// 验证荷载条件
    /// </summary>
    private void ValidateLoadConditions(EnhancedProfile2D profile, List<GeometryIssue> issues, List<string> missingConditions)
    {
        // 检查是否定义了基本荷载
        var hasGravityLoad = profile.Features.ContainsKey("自重") || profile.Features.ContainsKey("Gravity");
        var hasWaterLoad = profile.WaterLevels.UpstreamWaterLevel > 0;
        
        if (!hasGravityLoad)
        {
            missingConditions.Add("结构自重");
        }
        
        if (!hasWaterLoad)
        {
            missingConditions.Add("水压力荷载");
        }
    }
    
    /// <summary>
    /// 验证材料参数
    /// </summary>
    private void ValidateMaterialParameters(EnhancedProfile2D profile, List<GeometryIssue> issues, List<string> missingConditions)
    {
        if (profile.MaterialZones.Count == 0)
        {
            missingConditions.Add("材料参数");
            return;
        }
        
        foreach (var zone in profile.MaterialZones)
        {
            if (zone.Properties.Density <= 0)
            {
                issues.Add(new GeometryIssue
                {
                    Type = IssueType.MaterialZoneOverlap,
                    Severity = IssueSeverity.Warning,
                    Description = $"材料分区 '{zone.Name}' 缺少有效的密度参数",
                    SuggestedFix = "设置材料的密度、弹性模量等参数",
                    CanAutoFix = true
                });
            }
            
            if (zone.Properties.ElasticModulus <= 0)
            {
                issues.Add(new GeometryIssue
                {
                    Type = IssueType.MaterialZoneOverlap,
                    Severity = IssueSeverity.Warning,
                    Description = $"材料分区 '{zone.Name}' 缺少有效的弹性模量参数",
                    SuggestedFix = "设置材料的弹性模量参数",
                    CanAutoFix = true
                });
            }
        }
    }
    
    #endregion
    
    #region 辅助计算方法
    
    /// <summary>
    /// 计算上游面坡度
    /// </summary>
    private double CalculateUpstreamSlope(List<Point2D> contour, double centerX)
    {
        var upstreamPoints = contour.Where(p => p.X < centerX).ToList();
        if (upstreamPoints.Count < 2) return 0;
        
        var deltaY = upstreamPoints.Max(p => p.Y) - upstreamPoints.Min(p => p.Y);
        var deltaX = upstreamPoints.Max(p => p.X) - upstreamPoints.Min(p => p.X);
        
        return deltaX > 0 ? deltaY / deltaX : 0;
    }
    
    /// <summary>
    /// 计算下游面坡度
    /// </summary>
    private double CalculateDownstreamSlope(List<Point2D> contour, double centerX)
    {
        var downstreamPoints = contour.Where(p => p.X > centerX).ToList();
        if (downstreamPoints.Count < 2) return 0;
        
        var deltaY = downstreamPoints.Max(p => p.Y) - downstreamPoints.Min(p => p.Y);
        var deltaX = downstreamPoints.Max(p => p.X) - downstreamPoints.Min(p => p.X);
        
        return deltaX > 0 ? deltaY / deltaX : 0;
    }
    
    /// <summary>
    /// 检查两条线段是否相交
    /// </summary>
    private bool LineSegmentsIntersect(Point2D p1, Point2D p2, Point2D p3, Point2D p4, out Point2D intersection)
    {
        intersection = new Point2D(0, 0);
        
        var d1 = (p4.Y - p3.Y) * (p2.X - p1.X) - (p4.X - p3.X) * (p2.Y - p1.Y);
        var d2 = (p4.X - p3.X) * (p1.Y - p3.Y) - (p4.Y - p3.Y) * (p1.X - p3.X);
        var d3 = (p2.X - p1.X) * (p1.Y - p3.Y) - (p2.Y - p1.Y) * (p1.X - p3.X);
        
        if (Math.Abs(d1) < GEOMETRIC_TOLERANCE) return false; // 平行线
        
        var t1 = d2 / d1;
        var t2 = d3 / d1;
        
        if (t1 >= 0 && t1 <= 1 && t2 >= 0 && t2 <= 1)
        {
            intersection = new Point2D(
                p1.X + t1 * (p2.X - p1.X),
                p1.Y + t1 * (p2.Y - p1.Y)
            );
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// 检查材料分区重叠
    /// </summary>
    private bool CheckZoneOverlap(MaterialZone zone1, MaterialZone zone2)
    {
        // 简化的重叠检查 - 实际应用中需要更复杂的几何算法
        var bbox1 = GetBoundingBox(zone1.Boundary);
        var bbox2 = GetBoundingBox(zone2.Boundary);
        
        return !(bbox1.MaxX < bbox2.MinX || bbox2.MaxX < bbox1.MinX ||
                bbox1.MaxY < bbox2.MinY || bbox2.MaxY < bbox1.MinY);
    }
    
    /// <summary>
    /// 获取边界框
    /// </summary>
    private (double MinX, double MinY, double MaxX, double MaxY) GetBoundingBox(List<Point2D> points)
    {
        if (!points.Any()) return (0, 0, 0, 0);
        
        return (
            points.Min(p => p.X),
            points.Min(p => p.Y),
            points.Max(p => p.X),
            points.Max(p => p.Y)
        );
    }
    
    /// <summary>
    /// 计算几何评分
    /// </summary>
    private double CalculateGeometryScore(List<GeometryIssue> issues, Dictionary<string, object> metrics)
    {
        double score = 1.0;
        
        foreach (var issue in issues)
        {
            switch (issue.Severity)
            {
                case IssueSeverity.Critical:
                    score -= 0.5;
                    break;
                case IssueSeverity.Error:
                    score -= 0.2;
                    break;
                case IssueSeverity.Warning:
                    score -= 0.1;
                    break;
                case IssueSeverity.Info:
                    score -= 0.02;
                    break;
            }
        }
        
        return Math.Max(0, score);
    }
    
    /// <summary>
    /// 计算工程评分
    /// </summary>
    private double CalculateEngineeringScore(List<GeometryIssue> issues)
    {
        double score = 1.0;
        
        foreach (var issue in issues)
        {
            switch (issue.Severity)
            {
                case IssueSeverity.Critical:
                    score -= 0.4;
                    break;
                case IssueSeverity.Error:
                    score -= 0.15;
                    break;
                case IssueSeverity.Warning:
                    score -= 0.08;
                    break;
                case IssueSeverity.Info:
                    score -= 0.02;
                    break;
            }
        }
        
        return Math.Max(0, score);
    }
    
    /// <summary>
    /// 计算完整性评分
    /// </summary>
    private double CalculateCompletenessScore(int missingCount, List<GeometryIssue> issues)
    {
        double score = 1.0 - (missingCount * 0.2);
        
        foreach (var issue in issues)
        {
            if (issue.Severity >= IssueSeverity.Error)
            {
                score -= 0.1;
            }
        }
        
        return Math.Max(0, score);
    }
    
    /// <summary>
    /// 计算总体评分
    /// </summary>
    private double CalculateOverallScore(ProfileValidationResult result)
    {
        var geometryWeight = 0.4;
        var engineeringWeight = 0.4;
        var boundaryWeight = 0.2;
        
        return geometryWeight * result.GeometryValidation.GeometryScore +
               engineeringWeight * result.EngineeringValidation.EngineeringScore +
               boundaryWeight * result.BoundaryConditionValidation.CompletenessScore;
    }
    
    /// <summary>
    /// 确定总体验证状态
    /// </summary>
    private ValidationStatus DetermineOverallStatus(ProfileValidationResult result)
    {
        if (result.HasCriticalIssues)
        {
            return ValidationStatus.HasIssues;
        }
        
        if (result.RequiresUserReview)
        {
            return ValidationStatus.NeedsAdjustment;
        }
        
        if (result.OverallScore >= 0.9)
        {
            return ValidationStatus.CalculationReady;
        }
        
        if (result.OverallScore >= 0.7)
        {
            return ValidationStatus.Validated;
        }
        
        return ValidationStatus.Pending;
    }
    
    #endregion
} 