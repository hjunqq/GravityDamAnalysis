using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Infrastructure.Revit;
using GravityDamAnalysis.Revit.Selection;
using GravityDamAnalysis.Revit.Application;
using GravityDamAnalysis.Calculation.Models;
using GravityDamAnalysis.Calculation.Services;
using GravityDamAnalysis.Revit.UI;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 重力坝稳定性分析命令
/// 实现完整的坝体分析工作流程：选择元素 → 提取数据 → 计算 → 展示结果
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class DamStabilityAnalysisCommand : IExternalCommand
{
    private readonly ILogger<DamStabilityAnalysisCommand> _logger;
    private readonly RevitDataExtractor _dataExtractor;

    public DamStabilityAnalysisCommand()
    {
        // 从应用程序的服务提供者获取服务
        _logger = DamAnalysisApplication.ServiceProvider.GetRequiredService<ILogger<DamStabilityAnalysisCommand>>();
        _dataExtractor = DamAnalysisApplication.ServiceProvider.GetRequiredService<RevitDataExtractor>();
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger.LogInformation("开始执行重力坝稳定性分析命令");

            var uiApp = commandData.Application;
            var doc = uiApp.ActiveUIDocument.Document;

            // 检查活动文档
            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 步骤1：选择重力坝元素
            var selectedElement = SelectDamElement(uiApp.ActiveUIDocument);
            if (selectedElement == null)
            {
                TaskDialog.Show("取消", "未选择任何元素，操作已取消");
                return Result.Cancelled;
            }

            // 步骤2：提取几何和材料数据
            var damData = ExtractDamData(selectedElement);
            if (damData == null)
            {
                TaskDialog.Show("错误", "无法从选定元素中提取有效的坝体数据");
                return Result.Failed;
            }

            // 步骤3：确认数据并配置参数
            var parameterConfig = ConfirmAndConfigureAnalysis(damData);
            if (!parameterConfig.IsConfirmed)
            {
                return Result.Cancelled;
            }

            // 步骤4：执行稳定性计算
            var results = Task.Run(async () => await PerformStabilityAnalysisAsync(damData, parameterConfig.Parameters)).Result;

            // 步骤5：展示计算结果
            DisplayResults(results);

            _logger.LogInformation("重力坝稳定性分析命令执行完成");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行重力坝稳定性分析命令时发生错误");
            message = $"分析过程中发生错误: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 选择重力坝元素
    /// </summary>
    private Element? SelectDamElement(UIDocument uidoc)
    {
        try
        {
            var selectionFilter = new DamElementSelectionFilter();
            var reference = uidoc.Selection.PickObject(
                ObjectType.Element,
                selectionFilter,
                "请选择重力坝实体（体量、结构构件、墙体等）");

            return uidoc.Document.GetElement(reference);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择坝体元素时发生错误");
            TaskDialog.Show("选择错误", $"选择元素时发生错误: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 提取坝体数据
    /// </summary>
    private DamAnalysisData? ExtractDamData(Element element)
    {
        try
        {
            // 提取几何信息
            var geometry = _dataExtractor.ExtractGeometry(element);
            if (geometry == null)
            {
                return null;
            }

            // 提取材料属性
            var material = _dataExtractor.ExtractMaterialProperties(element);

            return new DamAnalysisData
            {
                ElementId = element.Id,
                ElementName = element.Name ?? "未命名坝体",
                Volume = geometry.Volume,
                Height = geometry.Height,
                BaseWidth = geometry.BaseWidth,
                Density = material.Density,
                CohesionStrength = material.CohesionStrength,
                FrictionAngle = material.FrictionAngle,
                CompressiveStrength = material.CompressiveStrength
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取坝体数据时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 确认数据并配置分析参数
    /// </summary>
    private ParameterConfigResult ConfirmAndConfigureAnalysis(DamAnalysisData damData)
    {
        // 首先确认坝体信息
        var confirmDialog = new TaskDialog("坝体信息确认")
        {
            MainInstruction = "请确认提取的坝体信息",
            MainContent = $"元素名称: {damData.ElementName}\n" +
                         $"体积: {damData.Volume:F2} m³\n" +
                         $"高度: {damData.Height:F2} m\n" +
                         $"底宽: {damData.BaseWidth:F2} m\n" +
                         $"密度: {damData.Density:F2} kg/m³\n" +
                         $"摩擦角: {damData.FrictionAngle:F1}°\n" +
                         $"抗压强度: {damData.CompressiveStrength:F2} MPa",
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            DefaultButton = TaskDialogResult.Yes
        };

        var confirmResult = confirmDialog.Show();
        if (confirmResult != TaskDialogResult.Yes)
        {
            return new ParameterConfigResult { IsConfirmed = false };
        }

        // 配置分析参数
        var defaultParameters = new AnalysisParameters
        {
            // 根据坝体高度设置合理的默认水位
            UpstreamWaterLevel = Math.Min(damData.Height * 0.8, 100.0),
            DownstreamWaterLevel = Math.Min(damData.Height * 0.1, 10.0),
            FrictionCoefficient = damData.FrictionAngle > 0 ? 
                Math.Tan(damData.FrictionAngle * Math.PI / 180) : 0.75
        };

        return ParametersConfigHelper.ShowParametersDialog(defaultParameters);
    }

    /// <summary>
    /// 执行稳定性分析计算
    /// </summary>
    private async Task<StabilityAnalysisResult> PerformStabilityAnalysisAsync(DamAnalysisData damData, AnalysisParameters parameters)
    {
        _logger.LogInformation("开始执行稳定性计算");

        // 构建坝体实体
        var damGeometry = new Core.Entities.DamGeometry(
            damData.Volume, 
            new Core.ValueObjects.BoundingBox3D(
                new Core.ValueObjects.Point3D(0, 0, 0),
                new Core.ValueObjects.Point3D(damData.BaseWidth, damData.BaseWidth, damData.Height)
            )
        );

        var materialProperties = new Core.Entities.MaterialProperties(
            "提取的材料",
            damData.Density / 1000 * 9.81, // 转换为kN/m³
            30000, // 默认弹性模量
            0.18,  // 默认泊松比
            damData.CompressiveStrength,
            damData.CompressiveStrength * 0.1, // 估算抗拉强度
            parameters.FrictionCoefficient
        );

        var damEntity = new Core.Entities.DamEntity(
            Guid.NewGuid(),
            damData.ElementName,
            damGeometry,
            materialProperties
        )
        {
            RevitElementId = damData.ElementId.Value
        };

        // 使用稳定性分析服务
        var analysisService = DamAnalysisApplication.ServiceProvider.GetRequiredService<IStabilityAnalysisService>();
        
        return await analysisService.AnalyzeStabilityAsync(damEntity, parameters);
    }

    /// <summary>
    /// 展示计算结果
    /// </summary>
    private void DisplayResults(StabilityAnalysisResult results)
    {
        var stabilityStatus = results.IsOverallStable ? "稳定" : "不稳定";
        var dialog = new TaskDialog("稳定性分析结果")
        {
            MainInstruction = $"坝体稳定性分析完成 - {stabilityStatus}",
            MainContent = $"抗滑安全系数: {results.SlidingSafetyFactor:F3} (要求≥{results.Parameters.RequiredSlidingSafetyFactor:F1}) {(results.IsSlidingStable ? "✓" : "✗")}\n" +
                         $"抗倾覆安全系数: {results.OverturnSafetyFactor:F3} (要求≥{results.Parameters.RequiredOverturnSafetyFactor:F1}) {(results.IsOverturnStable ? "✓" : "✗")}\n" +
                         $"坝体重量: {results.ForceAnalysis.SelfWeight:F2} kN\n" +
                         $"水平力: {results.ForceAnalysis.HorizontalForce:F2} kN\n" +
                         $"分析用时: {results.Duration.TotalSeconds:F2} 秒\n\n" +
                         $"结论: 坝体{stabilityStatus}",
            CommonButtons = TaskDialogCommonButtons.Ok,
            ExpandedContent = results.GenerateSummary()
        };

        if (!results.IsOverallStable)
        {
            dialog.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
            dialog.ExpandedContent += "\n\n建议:\n" +
                                   "• 如抗滑安全系数不足，考虑增加坝底摩擦系数或增加坝体重量\n" +
                                   "• 如抗倾覆安全系数不足，考虑增加坝体底宽或降低重心\n" +
                                   "• 检查水位设置和材料参数是否合理";
        }

        dialog.Show();
        _logger.LogInformation($"分析结果: 抗滑系数={results.SlidingSafetyFactor:F3}, 抗倾覆系数={results.OverturnSafetyFactor:F3}");
    }
}

/// <summary>
/// 坝体分析数据
/// </summary>
public class DamAnalysisData
{
    public ElementId ElementId { get; set; }
    public string ElementName { get; set; } = string.Empty;
    public double Volume { get; set; }
    public double Height { get; set; }
    public double BaseWidth { get; set; }
    public double Density { get; set; }
    public double CohesionStrength { get; set; }
    public double FrictionAngle { get; set; }
    public double CompressiveStrength { get; set; }
} 