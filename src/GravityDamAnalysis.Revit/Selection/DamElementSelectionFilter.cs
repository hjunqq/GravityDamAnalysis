using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace GravityDamAnalysis.Revit.Selection;

/// <summary>
/// 重力坝元素选择过滤器
/// 用于在Revit中筛选适合进行稳定性分析的坝体元素
/// </summary>
public class DamElementSelectionFilter : ISelectionFilter
{
    /// <summary>
    /// 判断元素是否可以被选择
    /// </summary>
    /// <param name="elem">要判断的元素</param>
    /// <returns>是否允许选择</returns>
    public bool AllowElement(Element elem)
    {
        if (elem == null) return false;

        // 允许的元素类别
        var allowedCategories = new[]
        {
            BuiltInCategory.OST_Walls,              // 墙体
            BuiltInCategory.OST_GenericModel,       // 常规模型
            BuiltInCategory.OST_StructuralFoundation, // 结构基础
            BuiltInCategory.OST_Floors,             // 楼板
            BuiltInCategory.OST_StructuralFraming   // 结构框架
        };

        // 检查元素类别 - 使用更兼容的比较方式
        if (elem.Category != null)
        {
            var category = elem.Category.BuiltInCategory;
            if (allowedCategories.Contains(category))
            {
                return true;
            }
        }

        // 检查是否有特定的参数标识为坝体
        var damTypeParam = elem.LookupParameter("结构类型");
        if (damTypeParam != null && !string.IsNullOrEmpty(damTypeParam.AsString()))
        {
            var structureType = damTypeParam.AsString().ToLowerInvariant();
            if (structureType.Contains("坝") || structureType.Contains("dam"))
            {
                return true;
            }
        }

        // 检查族类型名称
        if (elem is FamilyInstance familyInstance)
        {
            var familyName = familyInstance.Symbol.FamilyName.ToLowerInvariant();
            if (familyName.Contains("坝") || familyName.Contains("dam") || familyName.Contains("gravity"))
            {
                return true;
            }
        }

        // 检查元素名称
        var elementName = elem.Name?.ToLowerInvariant() ?? "";
        if (elementName.Contains("坝") || elementName.Contains("dam"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 判断引用是否可以被选择
    /// </summary>
    /// <param name="reference">引用</param>
    /// <param name="position">选择位置</param>
    /// <returns>是否允许选择</returns>
    public bool AllowReference(Reference reference, XYZ position)
    {
        // 允许所有引用
        return true;
    }
} 