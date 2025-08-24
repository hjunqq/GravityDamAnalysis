using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Revit.Application;
using GravityDamAnalysis.Revit.Selection;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 读取Revit实体命令
/// 专门用于选择和读取Revit模型中的实体元素
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class ReadRevitEntitiesCommand : IExternalCommand
{
    private readonly ILogger<ReadRevitEntitiesCommand>? _logger;

    public ReadRevitEntitiesCommand()
    {
        _logger = DamAnalysisApplication.ServiceProvider?.GetRequiredService<ILogger<ReadRevitEntitiesCommand>>();
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger?.LogInformation("开始执行读取Revit实体命令");

            var uiApp = commandData.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 读取所有潜在的坝体实体
            var damEntities = ReadDamEntities(doc);
            
            if (!damEntities.Any())
            {
                TaskDialog.Show("提示", "未找到任何坝体实体。\n支持的实体类型：\n• 体量 (Mass)\n• 常规模型 (Generic Model)\n• 结构构件 (Structural Framing)\n• 墙体 (Wall)\n• 结构基础 (Structural Foundation)");
                return Result.Succeeded;
            }

            // 显示找到的实体信息
            DisplayEntityInfo(damEntities);

            _logger?.LogInformation($"成功读取 {damEntities.Count} 个实体");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "读取Revit实体时发生错误");
            message = $"读取失败: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 读取文档中的坝体实体
    /// </summary>
    private List<Element> ReadDamEntities(Document doc)
    {
        var entities = new List<Element>();

        try
        {
            // 定义坝体相关的类别过滤器
            var categoryFilters = new List<ElementCategoryFilter>
            {
                new ElementCategoryFilter(BuiltInCategory.OST_Mass),                    // 体量
                new ElementCategoryFilter(BuiltInCategory.OST_GenericModel),            // 常规模型
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming),       // 结构构件
                new ElementCategoryFilter(BuiltInCategory.OST_Walls),                   // 墙体
                new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation)     // 结构基础
            };

            // 创建逻辑或过滤器
            var orFilter = new LogicalOrFilter(categoryFilters.Cast<ElementFilter>().ToList());

            // 应用过滤器收集元素
            var collector = new FilteredElementCollector(doc)
                .WherePasses(orFilter)
                .WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (IsValidDamEntity(element))
                {
                    entities.Add(element);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "读取实体时发生错误");
        }

        return entities;
    }

    /// <summary>
    /// 验证是否为有效的坝体实体
    /// </summary>
    private bool IsValidDamEntity(Element element)
    {
        try
        {
            // 检查是否有几何体
            var geometry = element.get_Geometry(new Options());
            if (geometry == null) return false;

            // 检查是否有实体几何
            bool hasSolid = false;
            foreach (var geoObject in geometry)
            {
                if (geoObject is Solid solid && solid.Volume > 0)
                {
                    hasSolid = true;
                    break;
                }
                else if (geoObject is GeometryInstance instance)
                {
                    var instanceGeometry = instance.GetInstanceGeometry();
                    foreach (var instanceGeoObject in instanceGeometry)
                    {
                        if (instanceGeoObject is Solid instanceSolid && instanceSolid.Volume > 0)
                        {
                            hasSolid = true;
                            break;
                        }
                    }
                    if (hasSolid) break;
                }
            }

            return hasSolid;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 显示实体信息
    /// </summary>
    private void DisplayEntityInfo(List<Element> entities)
    {
        var info = $"找到 {entities.Count} 个坝体实体：\n\n";
        
        foreach (var entity in entities.Take(10)) // 最多显示10个
        {
            var categoryName = entity.Category?.Name ?? "未知类别";
            var elementName = entity.Name ?? "未命名";
            var elementId = entity.Id.Value;

            info += $"• ID: {elementId} | 类别: {categoryName} | 名称: {elementName}\n";
        }

        if (entities.Count > 10)
        {
            info += $"\n...还有 {entities.Count - 10} 个实体";
        }

        TaskDialog.Show("实体读取结果", info);
    }
}
