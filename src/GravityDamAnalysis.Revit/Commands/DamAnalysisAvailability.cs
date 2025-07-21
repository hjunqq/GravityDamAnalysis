using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 重力坝分析命令可用性控制类
/// 决定命令何时在Revit界面中可用
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
public class DamAnalysisAvailability : IExternalCommandAvailability
{
    /// <summary>
    /// 判断命令是否可用
    /// </summary>
    /// <param name="applicationData">应用程序数据</param>
    /// <param name="selectedCategories">选中的类别</param>
    /// <returns>命令是否可用</returns>
    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
    {
        // 检查是否有活动文档
        if (applicationData?.ActiveUIDocument?.Document == null)
        {
            return false;
        }

        var doc = applicationData.ActiveUIDocument.Document;

        // 检查文档是否已保存（非必需，但建议）
        if (doc.IsModifiable == false && doc.IsLinked)
        {
            return false;
        }

        // 检查文档中是否包含可能的坝体元素
        return HasPotentialDamElements(doc);
    }

    /// <summary>
    /// 检查文档中是否包含潜在的坝体元素
    /// </summary>
    /// <param name="doc">Revit文档</param>
    /// <returns>是否包含潜在坝体元素</returns>
    private bool HasPotentialDamElements(Document doc)
    {
        try
        {
            // 定义潜在的坝体元素类别
            var potentialCategories = new[]
            {
                BuiltInCategory.OST_Mass,                 // 体量
                BuiltInCategory.OST_GenericModel,         // 常规模型
                BuiltInCategory.OST_StructuralFraming,    // 结构构件
                BuiltInCategory.OST_Walls,                // 墙体
                BuiltInCategory.OST_StructuralFoundation, // 结构基础
                BuiltInCategory.OST_Columns,              // 柱
                BuiltInCategory.OST_StructuralColumns     // 结构柱
            };

            foreach (var category in potentialCategories)
            {
                var collector = new FilteredElementCollector(doc)
                    .OfCategory(category)
                    .WhereElementIsNotElementType();

                // 如果找到任何这些类别的元素，认为可能包含坝体
                if (collector.Any())
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // 如果检查失败，保守地返回true以允许命令执行
            return true;
        }
    }
} 