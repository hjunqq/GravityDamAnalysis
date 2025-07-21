using System;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Infrastructure.Revit;
using GravityDamAnalysis.Revit.Application;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 快速重力坝识别命令
/// 在文档中自动搜索和识别潜在的重力坝实体
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class QuickDamRecognitionCommand : IExternalCommand
{
    private readonly ILogger<QuickDamRecognitionCommand> _logger;
    private readonly RevitDataExtractor _dataExtractor;

    public QuickDamRecognitionCommand()
    {
        _logger = DamAnalysisApplication.ServiceProvider.GetRequiredService<ILogger<QuickDamRecognitionCommand>>();
        _dataExtractor = DamAnalysisApplication.ServiceProvider.GetRequiredService<RevitDataExtractor>();
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger.LogInformation("开始执行快速坝体识别命令");

            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument.Document;

            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 搜索潜在的坝体元素
            var potentialDamElements = FindPotentialDamElements(doc);

            if (!potentialDamElements.Any())
            {
                TaskDialog.Show("识别结果", "在当前文档中未找到潜在的重力坝实体。\n\n" +
                    "建议检查以下元素类型：\n" +
                    "• 体量 (Mass)\n" +
                    "• 常规模型 (Generic Model)\n" +
                    "• 结构构件 (Structural Framing)\n" +
                    "• 墙体 (Wall)\n" +
                    "• 结构基础 (Structural Foundation)");
                return Result.Succeeded;
            }

            // 分析每个潜在元素
            var analysisResults = AnalyzePotentialElements(potentialDamElements);

            // 显示识别结果
            DisplayRecognitionResults(analysisResults, uiApp.ActiveUIDocument);

            _logger.LogInformation("快速坝体识别命令执行完成，找到 {Count} 个潜在坝体", analysisResults.Count);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行快速坝体识别命令时发生错误");
            message = $"识别过程中发生错误: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 查找文档中潜在的坝体元素
    /// </summary>
    private List<Element> FindPotentialDamElements(Document doc)
    {
        var potentialElements = new List<Element>();

        var targetCategories = new[]
        {
            BuiltInCategory.OST_Mass,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Columns,
            BuiltInCategory.OST_StructuralColumns
        };

        foreach (var category in targetCategories)
        {
            try
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                foreach (Element element in collector)
                {
                    if (IsPotentialDamElement(element))
                    {
                        potentialElements.Add(element);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "搜索类别 {Category} 时发生错误", category);
            }
        }

        return potentialElements;
    }

    /// <summary>
    /// 判断元素是否为潜在的坝体元素
    /// </summary>
    private bool IsPotentialDamElement(Element element)
    {
        try
        {
            // 检查元素名称
            var name = element.Name?.ToLowerInvariant() ?? "";
            var damKeywords = new[] { "坝", "dam", "重力", "gravity", "挡水", "水工" };
            
            if (damKeywords.Any(keyword => name.Contains(keyword)))
            {
                return true;
            }

            // 检查几何特征
            var geometryData = _dataExtractor.ExtractGeometry(element);
            if (geometryData != null)
            {
                // 坝体通常具有以下特征：
                // 1. 足够大的体积（>100 m³）
                // 2. 高度与底宽比在合理范围内（0.3-2.0）
                // 3. 有一定的厚度

                var heightToWidthRatio = geometryData.BaseWidth > 0 ? geometryData.Height / geometryData.BaseWidth : 0;
                
                return geometryData.Volume > 100 && 
                       geometryData.Height > 5 && 
                       heightToWidthRatio >= 0.3 && 
                       heightToWidthRatio <= 2.0;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 分析潜在元素
    /// </summary>
    private List<DamRecognitionResult> AnalyzePotentialElements(List<Element> elements)
    {
        var results = new List<DamRecognitionResult>();

        foreach (var element in elements)
        {
            try
            {
                var result = new DamRecognitionResult
                {
                    Element = element,
                    ElementId = element.Id,
                    ElementName = element.Name ?? "未命名元素",
                    Category = element.Category?.Name ?? "未知类别"
                };

                // 提取几何信息
                var geometryData = _dataExtractor.ExtractGeometry(element);
                if (geometryData != null)
                {
                    result.Volume = geometryData.Volume;
                    result.Height = geometryData.Height;
                    result.BaseWidth = geometryData.BaseWidth;
                    result.HasGeometry = true;
                }

                // 提取材料信息
                var materialData = _dataExtractor.ExtractMaterialProperties(element);
                result.MaterialName = materialData.Name;

                // 计算置信度评分
                result.ConfidenceScore = CalculateConfidenceScore(element, result);

                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "分析元素 {ElementId} 时发生错误", element.Id);
            }
        }

        return results.OrderByDescending(r => r.ConfidenceScore).ToList();
    }

    /// <summary>
    /// 计算坝体识别置信度评分
    /// </summary>
    private double CalculateConfidenceScore(Element element, DamRecognitionResult result)
    {
        double score = 0.0;

        // 名称匹配评分 (0-30分)
        var name = element.Name?.ToLowerInvariant() ?? "";
        var damKeywords = new[] { "坝", "dam", "重力", "gravity" };
        var waterKeywords = new[] { "挡水", "水工", "水利", "hydraulic" };
        
        if (damKeywords.Any(keyword => name.Contains(keyword)))
            score += 30;
        else if (waterKeywords.Any(keyword => name.Contains(keyword)))
            score += 15;

        // 几何特征评分 (0-40分)
        if (result.HasGeometry)
        {
            // 体积评分
            if (result.Volume > 1000) score += 15;
            else if (result.Volume > 500) score += 10;
            else if (result.Volume > 100) score += 5;

            // 高宽比评分
            var heightToWidthRatio = result.BaseWidth > 0 ? result.Height / result.BaseWidth : 0;
            if (heightToWidthRatio >= 0.5 && heightToWidthRatio <= 1.5) score += 15;
            else if (heightToWidthRatio >= 0.3 && heightToWidthRatio <= 2.0) score += 10;

            // 高度评分
            if (result.Height > 50) score += 10;
            else if (result.Height > 20) score += 8;
            else if (result.Height > 10) score += 5;
        }

        // 材料类型评分 (0-20分)
        var materialName = result.MaterialName.ToLowerInvariant();
        if (materialName.Contains("混凝土") || materialName.Contains("concrete"))
            score += 20;
        else if (materialName.Contains("石") || materialName.Contains("stone"))
            score += 15;
        else if (materialName.Contains("砌") || materialName.Contains("masonry"))
            score += 10;

        // 类别评分 (0-10分)
        switch (element.Category?.BuiltInCategory)
        {
            case BuiltInCategory.OST_Mass:
            case BuiltInCategory.OST_GenericModel:
                score += 10;
                break;
            case BuiltInCategory.OST_StructuralFraming:
            case BuiltInCategory.OST_StructuralFoundation:
                score += 8;
                break;
            case BuiltInCategory.OST_Walls:
                score += 6;
                break;
            default:
                score += 3;
                break;
        }

        return Math.Min(100, score);
    }

    /// <summary>
    /// 显示识别结果
    /// </summary>
    private void DisplayRecognitionResults(List<DamRecognitionResult> results, UIDocument uidoc)
    {
        var resultText = new StringBuilder();
        resultText.AppendLine("重力坝快速识别结果");
        resultText.AppendLine("========================");
        resultText.AppendLine($"共找到 {results.Count} 个潜在的重力坝实体\n");

        var highConfidenceResults = results.Where(r => r.ConfidenceScore >= 70).ToList();
        var mediumConfidenceResults = results.Where(r => r.ConfidenceScore >= 40 && r.ConfidenceScore < 70).ToList();
        var lowConfidenceResults = results.Where(r => r.ConfidenceScore < 40).ToList();

        if (highConfidenceResults.Any())
        {
            resultText.AppendLine("高置信度匹配 (≥70分):");
            foreach (var result in highConfidenceResults)
            {
                resultText.AppendLine($"• {result.ElementName} (ID: {result.ElementId.Value})");
                resultText.AppendLine($"  置信度: {result.ConfidenceScore:F1}% | 类别: {result.Category}");
                if (result.HasGeometry)
                    resultText.AppendLine($"  几何: 体积 {result.Volume:F1}m³, 高度 {result.Height:F1}m, 底宽 {result.BaseWidth:F1}m");
                resultText.AppendLine();
            }
        }

        if (mediumConfidenceResults.Any())
        {
            resultText.AppendLine("中等置信度匹配 (40-70分):");
            foreach (var result in mediumConfidenceResults)
            {
                resultText.AppendLine($"• {result.ElementName} (ID: {result.ElementId.Value}) - {result.ConfidenceScore:F1}%");
            }
            resultText.AppendLine();
        }

        if (lowConfidenceResults.Any())
        {
            resultText.AppendLine($"低置信度匹配 (<40分): {lowConfidenceResults.Count} 个元素");
        }

        var dialog = new TaskDialog("坝体识别结果")
        {
            MainInstruction = highConfidenceResults.Any() ? 
                $"识别到 {highConfidenceResults.Count} 个高置信度的重力坝实体" : 
                "未找到高置信度的重力坝实体",
            MainContent = resultText.ToString(),
            CommonButtons = TaskDialogCommonButtons.Ok
        };

        // 如果有高置信度结果，询问是否高亮显示
        if (highConfidenceResults.Any())
        {
            dialog.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No;
            dialog.MainInstruction += "\n\n是否在视图中高亮显示这些元素？";
            
            var dialogResult = dialog.Show();
            if (dialogResult == TaskDialogResult.Yes)
            {
                HighlightElements(highConfidenceResults.Select(r => r.ElementId).ToList(), uidoc);
            }
        }
        else
        {
            dialog.Show();
        }
    }

    /// <summary>
    /// 在视图中高亮显示元素
    /// </summary>
    private void HighlightElements(List<ElementId> elementIds, UIDocument uidoc)
    {
        try
        {
            uidoc.Selection.SetElementIds(elementIds);
            uidoc.ShowElements(elementIds);
            
            TaskDialog.Show("提示", $"已选中并高亮显示 {elementIds.Count} 个元素");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "高亮显示元素时发生错误");
        }
    }
}

/// <summary>
/// 坝体识别结果
/// </summary>
public class DamRecognitionResult
{
    public Element Element { get; set; }
    public ElementId ElementId { get; set; }
    public string ElementName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string MaterialName { get; set; } = string.Empty;
    public double Volume { get; set; }
    public double Height { get; set; }
    public double BaseWidth { get; set; }
    public bool HasGeometry { get; set; }
    public double ConfidenceScore { get; set; }
} 