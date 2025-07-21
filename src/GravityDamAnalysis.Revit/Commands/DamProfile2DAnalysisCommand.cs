using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Calculation.Models;
using GravityDamAnalysis.Calculation.Services;
using GravityDamAnalysis.Revit.SectionAnalysis;
using GravityDamAnalysis.Revit.Selection;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 重力坝二维剖面稳定性分析命令
/// 实现完整的二维剖面分析流程
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class DamProfile2DAnalysisCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            // 1. 选择坝体元素
            var damElement = SelectDamElement(uiDoc);
            if (damElement == null)
            {
                message = "未选择有效的坝体元素";
                return Result.Cancelled;
            }

            // 2. 定义剖面参数（简化版本，使用默认值）
            var sectionParams = new SectionGenerationParameters
            {
                Normal = XYZ.BasisX, // 默认X轴方向剖面
                Offset = 0.0,
                Name = $"Section_{DateTime.Now:HHmmss}",
                ShowSectionPlane = false
            };

            // 3. 生成剖面并提取几何
            Profile2D profile;
            using (var transaction = new Transaction(doc, "提取剖面几何"))
            {
                transaction.Start();

                var sectionGenerator = new SectionPlaneGenerator(null); // 简化版本，不需要日志
                var sectionPlane = sectionGenerator.CreateSectionPlane(
                    damElement, 
                    sectionParams.Normal, 
                    sectionParams.Offset);

                profile = sectionGenerator.ExtractSectionProfile(
                    damElement, 
                    sectionPlane, 
                    sectionParams.Name);

                transaction.Commit();
            }

            if (!profile.IsValid())
            {
                message = "无法提取有效的剖面几何数据";
                return Result.Failed;
            }

            // 4. 获取分析参数
            var analysisParams = GetAnalysisParameters();
            var materialProps = GetMaterialProperties(damElement);

            // 5. 执行稳定性分析
            var stabilityAnalyzer = new Profile2DStabilityAnalyzer(null); // 简化版本
            var analysisResult = stabilityAnalyzer.AnalyzeProfile2DStability(
                profile, analysisParams, materialProps);

            // 6. 显示结果
            var report = analysisResult.GenerateReport();
            TaskDialog.Show("稳定性分析结果", report);

            message = $"分析完成：抗滑系数 {analysisResult.SlidingSafetyFactor:F3}, " +
                     $"抗倾覆系数 {analysisResult.OverturnSafetyFactor:F3}";

            return Result.Succeeded;
        }
        catch (OperationCanceledException)
        {
            message = "操作被用户取消";
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            message = $"分析失败: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 选择坝体元素
    /// </summary>
    private Element SelectDamElement(UIDocument uiDoc)
    {
        try
        {
            var filter = new DamElementSelectionFilter();
            var reference = uiDoc.Selection.PickObject(
                ObjectType.Element,
                filter,
                "请选择重力坝体元素（墙体、族实例或模型类别）");

            return uiDoc.Document.GetElement(reference);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// 获取分析参数
    /// </summary>
    private AnalysisParameters GetAnalysisParameters()
    {
        // 可以从用户界面获取，这里使用默认值
        return new AnalysisParameters
        {
            UpstreamWaterLevel = 100.0,     // m
            DownstreamWaterLevel = 10.0,    // m
            WaterDensity = 9.8,             // kN/m³
            SeismicCoefficient = 0.15,      // 地震系数
            UpliftReductionFactor = 0.8,    // 扬压力折减系数
            RequiredSlidingSafetyFactor = 3.0,
            RequiredOverturnSafetyFactor = 1.5,
            ConsiderUpliftPressure = true
        };
    }

    /// <summary>
    /// 获取材料属性
    /// </summary>
    private MaterialProperties GetMaterialProperties(Element damElement)
    {
        try
        {
            // 尝试从元素参数中读取材料属性
            var materialId = damElement.GetMaterialIds(false).FirstOrDefault();
            if (materialId != null && materialId != ElementId.InvalidElementId)
            {
                var material = damElement.Document.GetElement(materialId) as Material;
                if (material != null)
                {
                    return CreateMaterialPropertiesFromRevitMaterial(material);
                }
            }

            // 如果没有材料信息，使用默认混凝土属性
            return MaterialProperties.CreateStandardConcrete("C30");
        }
        catch (Exception ex)
        {
            return MaterialProperties.CreateStandardConcrete("C30");
        }
    }

    /// <summary>
    /// 从Revit材料创建材料属性
    /// </summary>
    private MaterialProperties CreateMaterialPropertiesFromRevitMaterial(Material revitMaterial)
    {
        var props = new MaterialProperties
        {
            Name = revitMaterial.Name,
            Type = MaterialType.Concrete
        };

        // 尝试读取材料的物理属性
        try
        {
            var structuralAsset = revitMaterial.Document.GetElement(revitMaterial.StructuralAssetId) as PropertySetElement;
            if (structuralAsset != null)
            {
                // 从结构资产中读取属性
                var asset = structuralAsset.GetStructuralAsset();
                
                // 密度 (Revit中是质量密度 kg/m³，需要转换为重度 kN/m³)
                if (asset.Density > 0)
                {
                    props.Density = asset.Density * 9.81 / 1000.0; // kg/m³ * 9.81 / 1000 = kN/m³
                }

                // 弹性模量 (Pa转换为GPa)
                if (asset.YoungModulus.X > 0)
                {
                    props.ElasticModulus = asset.YoungModulus.X / 1e9; // Pa to GPa
                }

                // 泊松比
                if (asset.PoissonRatio.X > 0)
                {
                    props.PoissonRatio = asset.PoissonRatio.X;
                }

                // 抗压强度 (Pa转换为MPa)
                if (asset.MinimumYieldStress > 0)
                {
                    props.CompressiveStrength = asset.MinimumYieldStress / 1e6; // Pa to MPa
                }
            }
        }
        catch (Exception ex)
        {
            // 忽略错误，使用默认值
        }

        // 确保所有属性都有合理的默认值
        if (props.Density <= 0) props.Density = 24.0;
        if (props.ElasticModulus <= 0) props.ElasticModulus = 30.0;
        if (props.PoissonRatio <= 0) props.PoissonRatio = 0.18;
        if (props.CompressiveStrength <= 0) props.CompressiveStrength = 30.0;
        if (props.TensileStrength <= 0) props.TensileStrength = 3.0;
        if (props.FrictionCoefficient <= 0) props.FrictionCoefficient = 0.75;

        return props;
    }
} 