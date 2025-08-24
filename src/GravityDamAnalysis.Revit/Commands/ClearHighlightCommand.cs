using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Revit.Application;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 清除高亮显示命令
/// 清除视图中所有元素的图形覆盖设置
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ClearHighlightCommand : IExternalCommand
{
    private readonly ILogger<ClearHighlightCommand>? _logger;

    public ClearHighlightCommand()
    {
        _logger = DamAnalysisApplication.ServiceProvider?.GetRequiredService<ILogger<ClearHighlightCommand>>();
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger?.LogInformation("开始执行清除高亮显示命令");

            var uiApp = commandData.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 确认是否清除
            var result = TaskDialog.Show("确认清除", 
                "是否要清除当前视图中所有元素的高亮显示？\n\n这将重置所有图形覆盖设置。", 
                TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                TaskDialogResult.No);

            if (result != TaskDialogResult.Yes)
            {
                return Result.Cancelled;
            }

            // 清除高亮显示
            var clearedCount = ClearAllHighlights(doc, uidoc.ActiveView);

            TaskDialog.Show("清除完成", $"已清除 {clearedCount} 个元素的高亮显示。");

            _logger?.LogInformation($"成功清除 {clearedCount} 个元素的高亮显示");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "清除高亮显示时发生错误");
            message = $"清除高亮失败: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 清除所有高亮显示
    /// </summary>
    private int ClearAllHighlights(Document doc, View activeView)
    {
        int clearedCount = 0;

        using (Transaction trans = new Transaction(doc, "清除高亮显示"))
        {
            trans.Start();

            try
            {
                // 获取所有可见元素
                var collector = new FilteredElementCollector(doc, activeView.Id)
                    .WhereElementIsNotElementType();

                foreach (Element element in collector)
                {
                    try
                    {
                        // 检查元素是否有图形覆盖
                        var currentOverrides = activeView.GetElementOverrides(element.Id);
                        
                        // 如果有任何覆盖设置，清除它们
                        if (HasAnyOverrides(currentOverrides))
                        {
                            var defaultOverrides = new OverrideGraphicSettings();
                            activeView.SetElementOverrides(element.Id, defaultOverrides);
                            clearedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"清除元素 {element.Id} 的覆盖设置时发生错误");
                    }
                }

                trans.Commit();
            }
            catch (Exception ex)
            {
                trans.RollBack();
                _logger?.LogError(ex, "清除高亮显示事务失败");
                throw;
            }
        }

        return clearedCount;
    }

    /// <summary>
    /// 检查是否有任何图形覆盖设置
    /// </summary>
    private bool HasAnyOverrides(OverrideGraphicSettings overrides)
    {
        try
        {
            // 检查各种覆盖设置
            return overrides.ProjectionLineColor.IsValid ||
                   overrides.ProjectionLineWeight != -1 ||
                   overrides.SurfaceForegroundPatternColor.IsValid ||
                   overrides.CutLineColor.IsValid ||
                   overrides.CutLineWeight != -1;
        }
        catch
        {
            // 如果检查失败，假设有覆盖设置
            return true;
        }
    }
}
