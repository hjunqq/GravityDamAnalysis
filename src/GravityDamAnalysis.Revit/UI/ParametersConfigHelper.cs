using System;
using System.Text;
using Autodesk.Revit.UI;
using GravityDamAnalysis.Calculation.Models;

namespace GravityDamAnalysis.Revit.UI;

/// <summary>
/// 分析参数配置助手类
/// 使用Revit原生UI组件进行参数配置
/// </summary>
public static class ParametersConfigHelper
{
    /// <summary>
    /// 显示参数配置对话框
    /// </summary>
    /// <param name="currentParameters">当前参数</param>
    /// <returns>配置结果</returns>
    public static ParameterConfigResult ShowParametersDialog(AnalysisParameters currentParameters)
    {
        var result = new ParameterConfigResult();
        
        // 构建参数显示文本
        var parameterText = BuildParameterDisplayText(currentParameters);
        
        var dialog = new TaskDialog("稳定性分析参数配置")
        {
            MainInstruction = "请确认分析参数设置",
            MainContent = parameterText,
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No | TaskDialogCommonButtons.Cancel,
            DefaultButton = TaskDialogResult.Yes,
            ExpandedContent = GetParameterDescription()
        };

        // 添加命令按钮
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "使用当前参数", "使用显示的参数值进行分析");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "快速调整参数", "使用预设的参数模板");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "手动输入参数", "通过输入框逐个设置参数");

        var dialogResult = dialog.Show();
        
        switch (dialogResult)
        {
            case TaskDialogResult.CommandLink1:
                result.IsConfirmed = true;
                result.Parameters = currentParameters;
                break;
                
            case TaskDialogResult.CommandLink2:
                result = ShowQuickParameterSelection(currentParameters);
                break;
                
            case TaskDialogResult.CommandLink3:
                result = ShowManualParameterInput(currentParameters);
                break;
                
            default:
                result.IsConfirmed = false;
                break;
        }
        
        return result;
    }

    /// <summary>
    /// 显示快速参数选择对话框
    /// </summary>
    private static ParameterConfigResult ShowQuickParameterSelection(AnalysisParameters currentParameters)
    {
        var result = new ParameterConfigResult();
        
        var dialog = new TaskDialog("快速参数配置")
        {
            MainInstruction = "选择预设的参数模板",
            MainContent = "根据工程实际情况选择合适的参数模板：",
            CommonButtons = TaskDialogCommonButtons.Cancel,
            DefaultButton = TaskDialogResult.Cancel
        };

        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "常规重力坝", 
            "水位: 100m/10m, 摩擦系数: 0.75, 无地震");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "高坝大库", 
            "水位: 200m/20m, 摩擦系数: 0.8, 地震系数: 0.1");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "中低坝", 
            "水位: 50m/5m, 摩擦系数: 0.7, 无地震");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "抗震设计", 
            "水位: 100m/10m, 摩擦系数: 0.75, 地震系数: 0.2");

        var dialogResult = dialog.Show();
        
        switch (dialogResult)
        {
            case TaskDialogResult.CommandLink1:
                result.Parameters = CreatePresetParameters("Regular");
                result.IsConfirmed = true;
                break;
            case TaskDialogResult.CommandLink2:
                result.Parameters = CreatePresetParameters("HighDam");
                result.IsConfirmed = true;
                break;
            case TaskDialogResult.CommandLink3:
                result.Parameters = CreatePresetParameters("LowDam");
                result.IsConfirmed = true;
                break;
            case TaskDialogResult.CommandLink4:
                result.Parameters = CreatePresetParameters("Seismic");
                result.IsConfirmed = true;
                break;
            default:
                result.IsConfirmed = false;
                break;
        }
        
        return result;
    }

    /// <summary>
    /// 显示手动参数输入对话框（简化版）
    /// </summary>
    private static ParameterConfigResult ShowManualParameterInput(AnalysisParameters currentParameters)
    {
        var result = new ParameterConfigResult();
        
        // 由于Revit TaskDialog功能有限，这里提供一个简化的手动输入
        // 实际项目中可以考虑使用WPF窗口
        var dialog = new TaskDialog("手动参数输入")
        {
            MainInstruction = "参数手动调整",
            MainContent = "选择要调整的参数类别：",
            CommonButtons = TaskDialogCommonButtons.Cancel
        };

        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "调整水位参数", 
            $"当前: 上游{currentParameters.UpstreamWaterLevel}m, 下游{currentParameters.DownstreamWaterLevel}m");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "调整物理参数", 
            $"当前: 摩擦系数{currentParameters.FrictionCoefficient}, 地震系数{currentParameters.SeismicCoefficient}");
        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "调整安全系数", 
            $"当前: 抗滑{currentParameters.RequiredSlidingSafetyFactor}, 抗倾覆{currentParameters.RequiredOverturnSafetyFactor}");

        var dialogResult = dialog.Show();
        
        // 这里可以进一步扩展实现具体的参数调整逻辑
        // 目前返回原参数
        result.Parameters = currentParameters;
        result.IsConfirmed = dialogResult != TaskDialogResult.Cancel;
        
        return result;
    }

    /// <summary>
    /// 创建预设参数
    /// </summary>
    private static AnalysisParameters CreatePresetParameters(string presetType)
    {
        return presetType switch
        {
            "Regular" => new AnalysisParameters
            {
                UpstreamWaterLevel = 100.0,
                DownstreamWaterLevel = 10.0,
                WaterDensity = 9.8,
                FrictionCoefficient = 0.75,
                SeismicCoefficient = 0.0,
                RequiredSlidingSafetyFactor = 3.0,
                RequiredOverturnSafetyFactor = 1.5,
                ConsiderUpliftPressure = true,
                UpliftReductionFactor = 0.8
            },
            "HighDam" => new AnalysisParameters
            {
                UpstreamWaterLevel = 200.0,
                DownstreamWaterLevel = 20.0,
                WaterDensity = 9.8,
                FrictionCoefficient = 0.8,
                SeismicCoefficient = 0.1,
                RequiredSlidingSafetyFactor = 3.5,
                RequiredOverturnSafetyFactor = 2.0,
                ConsiderUpliftPressure = true,
                UpliftReductionFactor = 0.75
            },
            "LowDam" => new AnalysisParameters
            {
                UpstreamWaterLevel = 50.0,
                DownstreamWaterLevel = 5.0,
                WaterDensity = 9.8,
                FrictionCoefficient = 0.7,
                SeismicCoefficient = 0.0,
                RequiredSlidingSafetyFactor = 2.5,
                RequiredOverturnSafetyFactor = 1.3,
                ConsiderUpliftPressure = false,
                UpliftReductionFactor = 0.8
            },
            "Seismic" => new AnalysisParameters
            {
                UpstreamWaterLevel = 100.0,
                DownstreamWaterLevel = 10.0,
                WaterDensity = 9.8,
                FrictionCoefficient = 0.75,
                SeismicCoefficient = 0.2,
                RequiredSlidingSafetyFactor = 2.5,
                RequiredOverturnSafetyFactor = 1.3,
                ConsiderUpliftPressure = true,
                UpliftReductionFactor = 0.8
            },
            _ => new AnalysisParameters()
        };
    }

    /// <summary>
    /// 构建参数显示文本
    /// </summary>
    private static string BuildParameterDisplayText(AnalysisParameters parameters)
    {
        var text = new StringBuilder();
        
        text.AppendLine("水位参数:");
        text.AppendLine($"  上游水位: {parameters.UpstreamWaterLevel:F2} m");
        text.AppendLine($"  下游水位: {parameters.DownstreamWaterLevel:F2} m");
        text.AppendLine();
        
        text.AppendLine("物理参数:");
        text.AppendLine($"  水的重度: {parameters.WaterDensity:F2} kN/m³");
        text.AppendLine($"  摩擦系数: {parameters.FrictionCoefficient:F3}");
        text.AppendLine($"  地震系数: {parameters.SeismicCoefficient:F3}");
        text.AppendLine();
        
        text.AppendLine("安全系数要求:");
        text.AppendLine($"  抗滑安全系数: {parameters.RequiredSlidingSafetyFactor:F2}");
        text.AppendLine($"  抗倾覆安全系数: {parameters.RequiredOverturnSafetyFactor:F2}");
        text.AppendLine();
        
        text.AppendLine("其他设置:");
        text.AppendLine($"  考虑扬压力: {(parameters.ConsiderUpliftPressure ? "是" : "否")}");
        if (parameters.ConsiderUpliftPressure)
        {
            text.AppendLine($"  扬压力折减系数: {parameters.UpliftReductionFactor:F2}");
        }
        
        return text.ToString();
    }

    /// <summary>
    /// 获取参数说明
    /// </summary>
    private static string GetParameterDescription()
    {
        return @"参数说明：

水位参数：
• 上游水位：库水位，影响静水压力大小
• 下游水位：下游水位，影响净水头

物理参数：
• 水的重度：一般取9.8 kN/m³
• 摩擦系数：坝基接触面摩擦系数，一般0.6-0.8
• 地震系数：地震影响系数，0为不考虑地震

安全系数要求：
• 抗滑安全系数：一般要求≥3.0（基本组合）
• 抗倾覆安全系数：一般要求≥1.5（基本组合）

扬压力设置：
• 考虑扬压力：是否计算坝底扬压力
• 扬压力折减系数：考虑排水措施的折减";
    }
}

/// <summary>
/// 参数配置结果
/// </summary>
public class ParameterConfigResult
{
    /// <summary>
    /// 是否确认配置
    /// </summary>
    public bool IsConfirmed { get; set; }
    
    /// <summary>
    /// 配置的参数
    /// </summary>
    public AnalysisParameters Parameters { get; set; } = new();
} 