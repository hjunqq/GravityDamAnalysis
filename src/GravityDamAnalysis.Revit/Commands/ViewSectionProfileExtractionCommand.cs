using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using GravityDamAnalysis.Revit.SectionAnalysis;
using System.Text;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// ViewSection剖面提取命令
/// 演示如何使用标准的Revit ViewSection流程从坝体中提取二维剖面
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ViewSectionProfileExtractionCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiapp = commandData.Application;
        var uidoc = uiapp.ActiveUIDocument;
        var doc = uidoc.Document;
        
        try
        {
            // 创建Logger
            var loggerFactory = LoggerFactory.Create(builder => { });
            var logger = loggerFactory.CreateLogger<ViewSectionProfileExtractionCommand>();
            
            logger.LogInformation("开始ViewSection剖面提取演示");
            
            // 步骤1：选择坝体元素
            var damElements = SelectDamElements(uidoc, logger);
            if (!damElements.Any())
            {
                TaskDialog.Show("提示", "未选择任何坝体元素。请选择坝体后重试。");
                return Result.Cancelled;
            }
            
            // 步骤2：定义剖面位置
            var sectionLocations = DefineSectionLocations(damElements, logger);
            
                         // 步骤3：使用ViewSection方法提取剖面
            var extractorLogger = loggerFactory.CreateLogger<AdvancedSectionExtractor>();
            var extractor = new AdvancedSectionExtractor(extractorLogger);
            var extractedProfiles = new List<EnhancedProfile2D>();
            
            foreach (var location in sectionLocations)
            {
                logger.LogInformation("提取剖面: {SectionName}", location.Name);
                
                var profile = Task.Run(async () => await extractor.ExtractProfile(doc, damElements, location)).Result;
                
                if (profile != null && profile.MainContour.Any())
                {
                    extractedProfiles.Add(profile);
                    logger.LogInformation("成功提取剖面 {ProfileName}，包含 {PointCount} 个点", 
                        profile.Name, profile.MainContour.Count);
                }
                else
                {
                    logger.LogWarning("剖面 {SectionName} 提取失败或为空", location.Name);
                }
            }
            
            // 步骤4：显示结果
            ShowExtractionResults(extractedProfiles, logger);
            
            logger.LogInformation("ViewSection剖面提取演示完成，成功提取 {Count} 个剖面", extractedProfiles.Count);
            
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = $"剖面提取过程中发生错误: {ex.Message}";
            TaskDialog.Show("错误", message);
            return Result.Failed;
        }
    }
    
    /// <summary>
    /// 选择坝体元素
    /// </summary>
    private List<Element> SelectDamElements(UIDocument uidoc, ILogger logger)
    {
        try
        {
            // 方法1：尝试从当前选择获取
            var selection = uidoc.Selection;
            var selectedIds = selection.GetElementIds();
            
            if (selectedIds.Any())
            {
                var selectedElements = selectedIds
                    .Select(id => uidoc.Document.GetElement(id))
                    .Where(elem => elem != null && IsDamElement(elem))
                    .ToList();
                
                if (selectedElements.Any())
                {
                    logger.LogInformation("从当前选择中找到 {Count} 个坝体元素", selectedElements.Count);
                    return selectedElements;
                }
            }
            
            // 方法2：自动搜索坝体元素
            logger.LogInformation("当前选择中无坝体元素，自动搜索项目中的坝体");
            
            var collector = new FilteredElementCollector(uidoc.Document);
            var damElements = collector
                .WhereElementIsNotElementType()
                .Where(elem => IsDamElement(elem))
                .ToList();
                
            if (damElements.Any())
            {
                logger.LogInformation("自动找到 {Count} 个坝体元素", damElements.Count);
                return damElements;
            }
            
            // 方法3：提示用户手动选择
            TaskDialog.Show("提示", "未找到坝体元素。请手动选择坝体后重试。");
            return new List<Element>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "选择坝体元素时发生错误");
            return new List<Element>();
        }
    }
    
    /// <summary>
    /// 判断是否为坝体元素
    /// </summary>
    private bool IsDamElement(Element element)
    {
        if (element == null) return false;
        
        // 检查元素类别
        var category = element.Category;
        if (category == null) return false;
        
        var categoryName = category.Name?.ToLower();
        if (categoryName != null && (
            categoryName.Contains("wall") || 
            categoryName.Contains("generic") ||
            categoryName.Contains("mass") ||
            categoryName.Contains("structural")))
        {
            // 检查元素名称或类型名称
            var elementName = element.Name?.ToLower() ?? "";
            var typeName = element.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM)?.AsValueString()?.ToLower() ?? "";
            
            return elementName.Contains("dam") || 
                   elementName.Contains("坝") ||
                   typeName.Contains("dam") ||
                   typeName.Contains("坝") ||
                   elementName.Contains("concrete") ||
                   elementName.Contains("混凝土");
        }
        
        return false;
    }
    
    /// <summary>
    /// 定义剖面位置
    /// </summary>
    private List<SectionLocation> DefineSectionLocations(List<Element> damElements, ILogger logger)
    {
        var locations = new List<SectionLocation>();
        
        try
        {
            // 计算坝体的整体边界框
            BoundingBoxXYZ overallBBox = null;
            foreach (var element in damElements)
            {
                var elementBBox = element.get_BoundingBox(null);
                if (elementBBox != null)
                {
                    if (overallBBox == null)
                    {
                        overallBBox = elementBBox;
                    }
                    else
                    {
                        // 扩展边界框
                        overallBBox.Min = new XYZ(
                            Math.Min(overallBBox.Min.X, elementBBox.Min.X),
                            Math.Min(overallBBox.Min.Y, elementBBox.Min.Y),
                            Math.Min(overallBBox.Min.Z, elementBBox.Min.Z)
                        );
                        overallBBox.Max = new XYZ(
                            Math.Max(overallBBox.Max.X, elementBBox.Max.X),
                            Math.Max(overallBBox.Max.Y, elementBBox.Max.Y),
                            Math.Max(overallBBox.Max.Z, elementBBox.Max.Z)
                        );
                    }
                }
            }
            
            if (overallBBox == null)
            {
                logger.LogError("无法获取坝体边界框");
                return locations;
            }
            
            var center = (overallBBox.Min + overallBBox.Max) / 2.0;
            
            // 创建纵剖面（沿坝轴向）
            locations.Add(new SectionLocation
            {
                Name = "纵剖面_中心线",
                Type = "longitudinal",
                Position = new Point3D(center.X, center.Y, center.Z),
                Normal = new Vector3D(0, 1, 0), // Y轴法向量
                Direction = new Vector3D(0, 1, 0), // 沿Y轴方向观看
                Priority = SectionPriority.Normal,
                Description = "坝体中心线纵剖面，用于分析坝体整体稳定性"
            });
            
            // 创建横剖面（垂直于坝轴向）
            var width = overallBBox.Max.X - overallBBox.Min.X;
            var quarterWidth = width / 4.0;
            
            // 左侧横剖面
            locations.Add(new SectionLocation
            {
                Name = "横剖面_左侧",
                Type = "transverse",
                Position = new Point3D(overallBBox.Min.X + quarterWidth, center.Y, center.Z),
                Normal = new Vector3D(1, 0, 0), // X轴法向量
                Direction = new Vector3D(1, 0, 0), // 沿X轴方向观看
                Priority = SectionPriority.Normal,
                Description = "坝体左侧横剖面"
            });
            
            // 中央横剖面
            locations.Add(new SectionLocation
            {
                Name = "横剖面_中央",
                Type = "transverse",
                Position = new Point3D(center.X, center.Y, center.Z),
                Normal = new Vector3D(1, 0, 0),
                Direction = new Vector3D(1, 0, 0),
                Priority = SectionPriority.Normal,
                Description = "坝体中央横剖面，用于分析典型断面"
            });
            
            // 右侧横剖面
            locations.Add(new SectionLocation
            {
                Name = "横剖面_右侧",
                Type = "transverse",
                Position = new Point3D(overallBBox.Max.X - quarterWidth, center.Y, center.Z),
                Normal = new Vector3D(1, 0, 0),
                Direction = new Vector3D(1, 0, 0),
                Priority = SectionPriority.Normal,
                Description = "坝体右侧横剖面"
            });
            
            // 添加竖直剖面（沿高度方向切割）
            var height = overallBBox.Max.Z - overallBBox.Min.Z;
            var quarterHeight = height / 4.0;
            
            // 下部竖直剖面
            locations.Add(new SectionLocation
            {
                Name = "竖直剖面_下部",
                Type = "vertical",
                Position = new Point3D(center.X, center.Y, overallBBox.Min.Z + quarterHeight),
                Normal = new Vector3D(0, 0, 1), // Z轴法向量
                Direction = new Vector3D(0, 0, 1), // 沿Z轴方向观看
                Priority = SectionPriority.Normal,
                Description = "坝体下部竖直剖面，用于分析基础连接"
            });
            
            // 中部竖直剖面
            locations.Add(new SectionLocation
            {
                Name = "竖直剖面_中部",
                Type = "vertical",
                Position = new Point3D(center.X, center.Y, center.Z),
                Normal = new Vector3D(0, 0, 1),
                Direction = new Vector3D(0, 0, 1),
                Priority = SectionPriority.Normal,
                Description = "坝体中部竖直剖面，用于分析主体结构"
            });
            
            // 上部竖直剖面
            locations.Add(new SectionLocation
            {
                Name = "竖直剖面_上部",
                Type = "vertical",
                Position = new Point3D(center.X, center.Y, overallBBox.Max.Z - quarterHeight),
                Normal = new Vector3D(0, 0, 1),
                Direction = new Vector3D(0, 0, 1),
                Priority = SectionPriority.Normal,
                Description = "坝体上部竖直剖面，用于分析顶部结构"
            });
            
            logger.LogInformation("定义了 {Count} 个剖面位置（包括 {VerticalCount} 个竖直剖面）", 
                locations.Count, locations.Count(l => l.Type == "vertical"));
            
            return locations;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "定义剖面位置时发生错误");
            return locations;
        }
    }
    
    /// <summary>
    /// 显示提取结果
    /// </summary>
    private void ShowExtractionResults(List<EnhancedProfile2D> profiles, ILogger logger)
    {
        try
        {
            if (!profiles.Any())
            {
                TaskDialog.Show("提取结果", "未成功提取任何剖面。请检查坝体选择和模型完整性。");
                return;
            }
            
            var resultText = new StringBuilder();
            resultText.AppendLine("ViewSection剖面提取结果：");
            resultText.AppendLine($"成功提取了 {profiles.Count} 个剖面\n");
            
            // 分类统计
            var longitudinalCount = profiles.Count(p => p.Name.Contains("纵剖面"));
            var transverseCount = profiles.Count(p => p.Name.Contains("横剖面"));
            var verticalCount = profiles.Count(p => p.Name.Contains("竖直剖面"));
            
            resultText.AppendLine("剖面类型统计：");
            resultText.AppendLine($"• 纵剖面：{longitudinalCount} 个");
            resultText.AppendLine($"• 横剖面：{transverseCount} 个");
            resultText.AppendLine($"• 竖直剖面：{verticalCount} 个");
            resultText.AppendLine();
            
            // 详细信息
            foreach (var profile in profiles)
            {
                var contourCount = profile.MainContour?.Count ?? 0;
                var innerContourCount = profile.InnerContours?.Count ?? 0;
                var sectionType = GetSectionType(profile.Name);
                var viewDirection = GetViewDirectionInfo(profile);
                
                resultText.AppendLine($"剖面：{profile.Name}");
                resultText.AppendLine($"  类型：{sectionType}");
                resultText.AppendLine($"  视图方向：{viewDirection}");
                resultText.AppendLine($"  主轮廓点数：{contourCount}");
                resultText.AppendLine($"  内部轮廓数：{innerContourCount}");
                resultText.AppendLine($"  创建时间：{profile.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                
                if (profile.SectionNormal.IsValid())
                {
                    resultText.AppendLine($"  剖面法向量：({profile.SectionNormal.X:F3}, {profile.SectionNormal.Y:F3}, {profile.SectionNormal.Z:F3})");
                }
                
                // 特别标注竖直剖面
                if (profile.Name.Contains("竖直"))
                {
                    resultText.AppendLine("  ⭐ 竖直剖面特殊说明：");
                    resultText.AppendLine("     - 视图方向沿Z轴，观察水平截面");
                    resultText.AppendLine("     - 适用于分析坝体水平分层结构");
                    resultText.AppendLine("     - 坐标系：X-Y平面投影");
                }
                
                resultText.AppendLine();
            }
            
            // 应用建议
            resultText.AppendLine("应用建议：");
            if (verticalCount > 0)
            {
                resultText.AppendLine("✓ 竖直剖面已包含，可用于：");
                resultText.AppendLine("  - 分析坝体水平分层");
                resultText.AppendLine("  - 检查施工缝结构");
                resultText.AppendLine("  - 分析温度荷载影响");
            }
            if (longitudinalCount > 0)
            {
                resultText.AppendLine("✓ 纵剖面可用于整体稳定性分析");
            }
            if (transverseCount > 0)
            {
                resultText.AppendLine("✓ 横剖面可用于典型断面计算");
            }
            
            resultText.AppendLine("\n技术要点：");
            resultText.AppendLine("• 使用标准Revit ViewSection API，确保精度");
            resultText.AppendLine("• 竖直剖面增强：支持Z轴方向切割");
            resultText.AppendLine("• 智能坐标转换：根据剖面类型自动调整");
            resultText.AppendLine("• 预览页面修正：解决坐标系显示问题");
            
            var dialog = new TaskDialog("ViewSection剖面提取完成")
            {
                MainInstruction = "剖面提取成功完成",
                MainContent = resultText.ToString(),
                ExpandedContent = "下一步可以使用这些剖面进行稳定性分析或应力计算。" +
                               "特别注意竖直剖面的应用场景和坐标系定义。",
                CommonButtons = TaskDialogCommonButtons.Ok
            };
            
            dialog.Show();
            
            logger.LogInformation("剖面提取结果展示完成，包含 {VerticalCount} 个竖直剖面", verticalCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "显示提取结果时发生错误");
            TaskDialog.Show("错误", $"显示结果时发生错误: {ex.Message}");
        }
    }
    
    /// <summary>
    /// 获取剖面类型描述
    /// </summary>
    private string GetSectionType(string profileName)
    {
        if (profileName.Contains("竖直"))
            return "竖直剖面（Z轴方向切割）";
        else if (profileName.Contains("纵剖面"))
            return "纵剖面（沿坝轴向）";
        else if (profileName.Contains("横剖面"))
            return "横剖面（垂直于坝轴向）";
        else
            return "未知类型";
    }
    
    /// <summary>
    /// 获取视图方向信息
    /// </summary>
    private string GetViewDirectionInfo(EnhancedProfile2D profile)
    {
        if (profile.SectionNormal.IsValid())
        {
            var normal = profile.SectionNormal;
            
            if (Math.Abs(normal.Z) > 0.8)
                return "向下俯视 (Z轴负方向)";
            else if (Math.Abs(normal.Y) > 0.8)
                return "沿坝轴观看 (Y轴方向)";
            else if (Math.Abs(normal.X) > 0.8)
                return "横向观看 (X轴方向)";
            else
                return $"自定义方向 ({normal.X:F2}, {normal.Y:F2}, {normal.Z:F2})";
        }
        
        return "未知方向";
    }
} 