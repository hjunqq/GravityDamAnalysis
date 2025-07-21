using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using Autodesk.Revit.DB;

namespace GravityDamAnalysis.Infrastructure.Revit;

/// <summary>
/// Revit数据提取器 - 模拟实现（等待Revit SDK配置完成后替换）
/// </summary>
public class RevitDataExtractor
{
    private readonly ILogger<RevitDataExtractor> _logger;
    
    public RevitDataExtractor(ILogger<RevitDataExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 模拟从Revit文档中提取重力坝实体
    /// </summary>
    /// <param name="documentPath">文档路径</param>
    /// <returns>重力坝实体</returns>
    public async Task<DamEntity?> ExtractDamEntityAsync(string documentPath)
    {
        _logger.LogInformation("模拟从Revit文档提取重力坝实体: {DocumentPath}", documentPath);
        
        await Task.Delay(100); // 模拟异步操作
        
        // 创建模拟的重力坝实体
        var geometry = new DamGeometry(
            1500.0, // 体积
            new BoundingBox3D(
                new Point3D(0, 0, 0),
                new Point3D(20, 20, 100)
            )
        );
        
        var materialProperties = new MaterialProperties(
            "C30混凝土",
            2400.0, // 密度
            30000.0, // 弹性模量
            0.18, // 泊松比
            30.0, // 抗压强度
            3.0, // 抗拉强度
            0.75 // 摩擦系数
        );
        
        var damEntity = new DamEntity(
            Guid.NewGuid(),
            "模拟重力坝",
            geometry,
            materialProperties
        );
        
        _logger.LogInformation("成功创建模拟重力坝实体");
        return damEntity;
    }
    
    /// <summary>
    /// 从Revit元素中提取几何信息
    /// </summary>
    /// <param name="element">Revit元素</param>
    /// <returns>几何信息</returns>
    public GeometryData? ExtractGeometry(Element element)
    {
        try
        {
            _logger.LogInformation("从元素 {ElementId} 提取几何信息", element.Id);
            
            var options = new Options 
            { 
                DetailLevel = ViewDetailLevel.Coarse,
                IncludeNonVisibleObjects = false 
            };
            
            var geometryElement = element.get_Geometry(options);
            if (geometryElement == null)
            {
                _logger.LogWarning("元素 {ElementId} 没有几何信息", element.Id);
                return null;
            }

            double totalVolume = 0;
            BoundingBoxXYZ? overallBoundingBox = null;

            // 遍历几何对象
            foreach (GeometryObject geoObj in geometryElement)
            {
                if (geoObj is Solid solid && solid.Volume > 0.001)
                {
                    totalVolume += solid.Volume;
                    
                    var bbox = solid.GetBoundingBox();
                    if (bbox != null)
                    {
                        if (overallBoundingBox == null)
                        {
                            overallBoundingBox = bbox;
                        }
                        else
                        {
                            overallBoundingBox = ExpandBoundingBox(overallBoundingBox, bbox);
                        }
                    }
                }
                else if (geoObj is GeometryInstance instance)
                {
                    var instGeometry = instance.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instGeometry)
                    {
                        if (instObj is Solid instSolid && instSolid.Volume > 0.001)
                        {
                            totalVolume += instSolid.Volume;
                            
                            var bbox = instSolid.GetBoundingBox();
                            if (bbox != null)
                            {
                                if (overallBoundingBox == null)
                                {
                                    overallBoundingBox = bbox;
                                }
                                else
                                {
                                    overallBoundingBox = ExpandBoundingBox(overallBoundingBox, bbox);
                                }
                            }
                        }
                    }
                }
            }

            if (totalVolume <= 0 || overallBoundingBox == null)
            {
                _logger.LogWarning("元素 {ElementId} 没有有效的实体几何", element.Id);
                return null;
            }

            // 计算尺寸（转换为米）
            var height = Math.Abs(overallBoundingBox.Max.Z - overallBoundingBox.Min.Z) * 0.3048;
            var width = Math.Abs(overallBoundingBox.Max.X - overallBoundingBox.Min.X) * 0.3048;
            var depth = Math.Abs(overallBoundingBox.Max.Y - overallBoundingBox.Min.Y) * 0.3048;
            
            // 体积转换为立方米
            var volumeM3 = totalVolume * Math.Pow(0.3048, 3);

            // 假设底宽是X方向的最大尺寸
            var baseWidth = Math.Max(width, depth);

            var geometryData = new GeometryData
            {
                Volume = volumeM3,
                Height = height,
                BaseWidth = baseWidth,
                Width = width,
                Depth = depth
            };

            _logger.LogInformation("成功提取几何信息: 体积={Volume:F2}m³, 高度={Height:F2}m, 底宽={BaseWidth:F2}m", 
                geometryData.Volume, geometryData.Height, geometryData.BaseWidth);

            return geometryData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取几何信息时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 从Revit元素中提取材料属性
    /// </summary>
    /// <param name="element">Revit元素</param>
    /// <returns>材料属性</returns>
    public MaterialData ExtractMaterialProperties(Element element)
    {
        try
        {
            _logger.LogInformation("从元素 {ElementId} 提取材料属性", element.Id);

            // 尝试从元素获取材料
            ElementId materialId = ElementId.InvalidElementId;
            
            // 对于不同类型的元素，获取材料的方式不同
            if (element is FamilyInstance familyInstance)
            {
                var material = familyInstance.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (material?.HasValue == true)
                {
                    materialId = material.AsElementId();
                }
            }
            else if (element is Wall wall)
            {
                var material = wall.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (material?.HasValue == true)
                {
                    materialId = material.AsElementId();
                }
            }

            MaterialData materialData;

            if (materialId != ElementId.InvalidElementId)
            {
                var materialElement = element.Document.GetElement(materialId) as Material;
                if (materialElement != null)
                {
                    materialData = ExtractFromRevitMaterial(materialElement);
                }
                else
                {
                    materialData = GetDefaultMaterialProperties();
                }
            }
            else
            {
                materialData = GetDefaultMaterialProperties();
            }

            _logger.LogInformation("成功提取材料属性: 密度={Density:F2}kg/m³, 摩擦角={FrictionAngle:F1}°", 
                materialData.Density, materialData.FrictionAngle);

            return materialData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取材料属性时发生错误，使用默认值");
            return GetDefaultMaterialProperties();
        }
    }

    /// <summary>
    /// 从Revit材料中提取属性
    /// </summary>
    private MaterialData ExtractFromRevitMaterial(Material material)
    {
        // 从Revit材料中提取属性（这里使用一些假设值，实际应用中需要根据具体的材料属性进行调整）
        var density = 2400.0; // 默认混凝土密度
        var compressiveStrength = 30.0; // 默认抗压强度 MPa
        var frictionAngle = 35.0; // 默认摩擦角
        var cohesionStrength = 0.5; // 默认黏聚力 MPa

        // 尝试从材料属性中获取密度
        try
        {
            var structuralAsset = material.Document.GetElement(material.StructuralAssetId) as PropertySetElement;
            if (structuralAsset != null)
            {
                // 这里可以根据实际的结构资产属性来提取更准确的值
                // 例如：density = structuralAsset.GetPropertyValue("Density");
            }
        }
        catch
        {
            // 使用默认值
        }

        return new MaterialData
        {
            Name = material.Name,
            Density = density,
            CompressiveStrength = compressiveStrength,
            FrictionAngle = frictionAngle,
            CohesionStrength = cohesionStrength
        };
    }

    /// <summary>
    /// 获取默认材料属性
    /// </summary>
    private MaterialData GetDefaultMaterialProperties()
    {
        return new MaterialData
        {
            Name = "默认混凝土",
            Density = 2400.0, // kg/m³
            CompressiveStrength = 30.0, // MPa
            FrictionAngle = 35.0, // 度
            CohesionStrength = 0.5 // MPa
        };
    }

    /// <summary>
    /// 扩展边界框以包含另一个边界框
    /// </summary>
    /// <param name="existing">现有边界框</param>
    /// <param name="toInclude">要包含的边界框</param>
    /// <returns>扩展后的边界框</returns>
    private BoundingBoxXYZ ExpandBoundingBox(BoundingBoxXYZ existing, BoundingBoxXYZ toInclude)
    {
        var expandedBox = new BoundingBoxXYZ();
        
        // 计算最小点
        expandedBox.Min = new XYZ(
            Math.Min(existing.Min.X, toInclude.Min.X),
            Math.Min(existing.Min.Y, toInclude.Min.Y),
            Math.Min(existing.Min.Z, toInclude.Min.Z)
        );
        
        // 计算最大点
        expandedBox.Max = new XYZ(
            Math.Max(existing.Max.X, toInclude.Max.X),
            Math.Max(existing.Max.Y, toInclude.Max.Y),
            Math.Max(existing.Max.Z, toInclude.Max.Z)
        );
        
        return expandedBox;
    }

    /// <summary>
    /// 模拟验证Revit文档
    /// </summary>
    /// <param name="documentPath">文档路径</param>
    /// <returns>是否有效</returns>
    public bool ValidateRevitDocument(string documentPath)
    {
        _logger.LogInformation("模拟验证Revit文档: {DocumentPath}", documentPath);
        return !string.IsNullOrEmpty(documentPath) && documentPath.EndsWith(".rvt");
    }
}

/// <summary>
/// 几何数据传输对象
/// </summary>
public class GeometryData
{
    public double Volume { get; set; }
    public double Height { get; set; }
    public double BaseWidth { get; set; }
    public double Width { get; set; }
    public double Depth { get; set; }
}

/// <summary>
/// 材料数据传输对象
/// </summary>
public class MaterialData
{
    public string Name { get; set; } = string.Empty;
    public double Density { get; set; }
    public double CompressiveStrength { get; set; }
    public double FrictionAngle { get; set; }
    public double CohesionStrength { get; set; }
} 