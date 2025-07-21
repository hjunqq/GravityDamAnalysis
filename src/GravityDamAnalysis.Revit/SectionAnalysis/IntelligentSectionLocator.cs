using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using GravityDamAnalysis.Calculation.Models;

namespace GravityDamAnalysis.Revit.SectionAnalysis;

/// <summary>
/// 智能剖面定位器
/// 根据坝体几何特征自动确定关键分析剖面
/// </summary>
public class IntelligentSectionLocator
{
    private readonly ILogger<IntelligentSectionLocator> _logger;
    private const double TOLERANCE = 1e-6;

    public IntelligentSectionLocator(ILogger<IntelligentSectionLocator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 识别关键剖面位置
    /// </summary>
    /// <param name="damEntity">坝体实体</param>
    /// <param name="parameters">分析参数</param>
    /// <returns>关键剖面位置列表</returns>
    public List<SectionLocation> IdentifyKeySections(DamEntity damEntity, AnalysisParameters parameters)
    {
        try
        {
            _logger.LogInformation("开始识别坝体关键剖面位置");
            var sections = new List<SectionLocation>();

            // 1. 最大截面（通常是坝体最高处）
            var maxSection = FindMaxHeightSection(damEntity);
            if (maxSection != null)
            {
                sections.Add(maxSection);
                _logger.LogInformation("识别到最大截面: {Name}", maxSection.Name);
            }

            // 2. 特征变化点（几何突变处）
            var transitionSections = FindGeometryTransitions(damEntity);
            sections.AddRange(transitionSections);
            _logger.LogInformation("识别到 {Count} 个几何变化剖面", transitionSections.Count);

            // 3. 荷载关键点（如溢洪道位置）
            var loadCriticalSections = FindLoadCriticalSections(damEntity);
            sections.AddRange(loadCriticalSections);
            _logger.LogInformation("识别到 {Count} 个荷载关键剖面", loadCriticalSections.Count);

            // 4. 用户自定义剖面（预留功能）
            // 注：CustomSectionLocations 属性待实现

            // 5. 优化剖面分布
            var optimizedSections = OptimizeSectionDistribution(sections, damEntity);
            
            _logger.LogInformation("完成剖面识别，共 {Count} 个关键剖面", optimizedSections.Count);
            return optimizedSections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "识别关键剖面位置失败");
            return new List<SectionLocation>();
        }
    }

    /// <summary>
    /// 查找最大截面（最高处）
    /// </summary>
    private SectionLocation FindMaxHeightSection(DamEntity damEntity)
    {
        try
        {
            var bbox = damEntity.Geometry.BoundingBox;
            
            // 找到坝体中心线
            var centerX = (bbox.Min.X + bbox.Max.X) / 2.0;
            var centerZ = (bbox.Min.Z + bbox.Max.Z) / 2.0;
            
            // 创建垂直于坝轴方向的剖面
            return new SectionLocation
            {
                Name = "最大截面",
                Position = new Point3D(centerX, bbox.Center.Y, centerZ),
                Normal = new Vector3D(0, 1, 0), // 垂直于坝轴方向（假设Y轴为坝轴方向）
                Priority = SectionPriority.Critical,
                Description = "坝体最高截面，通常为设计控制截面"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查找最大截面失败");
            return null;
        }
    }

    /// <summary>
    /// 查找几何特征变化点
    /// </summary>
    private List<SectionLocation> FindGeometryTransitions(DamEntity damEntity)
    {
        var sections = new List<SectionLocation>();

        try
        {
            var bbox = damEntity.Geometry.BoundingBox;
            
            // 沿坝轴方向分析几何变化
            var sampleCount = 10;
            var stepY = (bbox.Max.Y - bbox.Min.Y) / sampleCount;
            
            var previousWidth = 0.0;
            var previousHeight = 0.0;
            
            for (int i = 1; i < sampleCount; i++)
            {
                var y = bbox.Min.Y + i * stepY;
                
                // 简化计算：假设在该Y位置的坝体宽度和高度
                var currentWidth = EstimateDamWidthAtPosition(damEntity, y);
                var currentHeight = EstimateDamHeightAtPosition(damEntity, y);
                
                // 检查是否有显著变化
                if (i > 1)
                {
                    var widthChangeRatio = Math.Abs(currentWidth - previousWidth) / Math.Max(previousWidth, 1.0);
                    var heightChangeRatio = Math.Abs(currentHeight - previousHeight) / Math.Max(previousHeight, 1.0);
                    
                    if (widthChangeRatio > 0.2 || heightChangeRatio > 0.2) // 20%变化阈值
                    {
                        sections.Add(new SectionLocation
                        {
                            Name = $"几何变化点_{i}",
                            Position = new Point3D(bbox.Center.X, y, bbox.Center.Z),
                            Normal = new Vector3D(0, 1, 0),
                            Priority = SectionPriority.Important,
                            Description = $"几何特征变化处，宽度变化: {widthChangeRatio:P1}, 高度变化: {heightChangeRatio:P1}"
                        });
                    }
                }
                
                previousWidth = currentWidth;
                previousHeight = currentHeight;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查找几何变化点失败");
        }

        return sections;
    }

    /// <summary>
    /// 查找荷载关键点
    /// </summary>
    private List<SectionLocation> FindLoadCriticalSections(DamEntity damEntity)
    {
        var sections = new List<SectionLocation>();

        try
        {
            var bbox = damEntity.Geometry.BoundingBox;
            
            // 在坝体的1/4和3/4位置创建剖面（这些通常是应力集中位置）
            var quarterPositions = new[] { 0.25, 0.75 };
            
            foreach (var ratio in quarterPositions)
            {
                var y = bbox.Min.Y + ratio * (bbox.Max.Y - bbox.Min.Y);
                
                sections.Add(new SectionLocation
                {
                    Name = $"荷载关键点_{ratio:P0}",
                    Position = new Point3D(bbox.Center.X, y, bbox.Center.Z),
                    Normal = new Vector3D(0, 1, 0),
                    Priority = SectionPriority.Important,
                    Description = $"荷载关键位置（{ratio:P0}处）"
                });
            }
            
            // 如果坝体足够长，增加更多中间位置
            if (bbox.Max.Y - bbox.Min.Y > 100.0) // 大于100m的坝体
            {
                sections.Add(new SectionLocation
                {
                    Name = "中央截面",
                    Position = new Point3D(bbox.Center.X, bbox.Center.Y, bbox.Center.Z),
                    Normal = new Vector3D(0, 1, 0),
                    Priority = SectionPriority.Normal,
                    Description = "坝体中央截面"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查找荷载关键点失败");
        }

        return sections;
    }

    /// <summary>
    /// 优化剖面分布
    /// </summary>
    private List<SectionLocation> OptimizeSectionDistribution(List<SectionLocation> sections, DamEntity damEntity)
    {
        try
        {
            if (!sections.Any()) return sections;

            // 移除重复的剖面（距离过近的）
            var optimizedSections = new List<SectionLocation>();
            var minDistance = 5.0; // 最小间距5m

            // 按优先级排序
            var sortedSections = sections.OrderByDescending(s => (int)s.Priority).ToList();

            foreach (var section in sortedSections)
            {
                bool tooClose = false;
                
                foreach (var existing in optimizedSections)
                {
                    var distance = CalculateDistance(section.Position, existing.Position);
                    if (distance < minDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    optimizedSections.Add(section);
                }
            }

            // 确保至少有一个剖面
            if (!optimizedSections.Any() && sections.Any())
            {
                optimizedSections.Add(sections.First());
            }

            // 按位置排序（从下游到上游）
            return optimizedSections.OrderBy(s => s.Position.Y).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "优化剖面分布失败");
            return sections;
        }
    }

    /// <summary>
    /// 估算指定位置的坝体宽度
    /// </summary>
    private double EstimateDamWidthAtPosition(DamEntity damEntity, double yPosition)
    {
        // 简化实现：基于边界框进行线性插值
        var bbox = damEntity.Geometry.BoundingBox;
        return bbox.Width; // 实际实现中应该根据几何体进行精确计算
    }

    /// <summary>
    /// 估算指定位置的坝体高度
    /// </summary>
    private double EstimateDamHeightAtPosition(DamEntity damEntity, double yPosition)
    {
        // 简化实现：基于边界框进行线性插值
        var bbox = damEntity.Geometry.BoundingBox;
        return bbox.Height; // 实际实现中应该根据几何体进行精确计算
    }

    /// <summary>
    /// 计算两点间距离
    /// </summary>
    private double CalculateDistance(Point3D p1, Point3D p2)
    {
        var dx = p1.X - p2.X;
        var dy = p1.Y - p2.Y;
        var dz = p1.Z - p2.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>
    /// 基于Revit元素自动生成剖面位置
    /// </summary>
    /// <param name="revitElement">Revit坝体元素</param>
    /// <param name="sectionCount">期望的剖面数量</param>
    /// <returns>剖面位置列表</returns>
    public List<SectionLocation> GenerateUniformSections(Element revitElement, int sectionCount = 5)
    {
        var sections = new List<SectionLocation>();

        try
        {
            var bbox = revitElement.get_BoundingBox(null);
            if (bbox == null) return sections;

            var damLength = bbox.Max.Y - bbox.Min.Y;
            var stepSize = damLength / (sectionCount + 1);

            for (int i = 1; i <= sectionCount; i++)
            {
                var y = bbox.Min.Y + i * stepSize;
                var position = new Point3D(
                    (bbox.Min.X + bbox.Max.X) / 2.0,
                    y,
                    (bbox.Min.Z + bbox.Max.Z) / 2.0
                );

                sections.Add(new SectionLocation
                {
                    Name = $"均匀剖面_{i}",
                    Position = position,
                    Normal = new Vector3D(0, 1, 0),
                    Priority = SectionPriority.Normal,
                    Description = $"第{i}个均匀分布剖面"
                });
            }

            _logger.LogInformation("生成 {Count} 个均匀分布剖面", sections.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成均匀剖面失败");
        }

        return sections;
    }
}

/// <summary>
/// 剖面位置定义
/// </summary>
public class SectionLocation
{
    /// <summary>
    /// 剖面名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 剖面位置
    /// </summary>
    public Point3D Position { get; set; }

    /// <summary>
    /// 剖面法向量
    /// </summary>
    public Vector3D Normal { get; set; }

    /// <summary>
    /// 优先级
    /// </summary>
    public SectionPriority Priority { get; set; } = SectionPriority.Normal;

    /// <summary>
    /// 描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 剖面类型（纵剖面、横剖面等）
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 剖面方向向量（用于ViewSection创建）
    /// </summary>
    public Vector3D Direction { get; set; }

    /// <summary>
    /// 偏移距离
    /// </summary>
    public double Offset { get; set; } = 0.0;
}

/// <summary>
/// 剖面优先级
/// </summary>
public enum SectionPriority
{
    /// <summary>
    /// 普通
    /// </summary>
    Normal = 1,

    /// <summary>
    /// 重要
    /// </summary>
    Important = 2,

    /// <summary>
    /// 关键
    /// </summary>
    Critical = 3
} 