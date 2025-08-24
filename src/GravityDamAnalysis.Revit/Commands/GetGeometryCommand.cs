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
/// 获取几何命令
/// 从选定的Revit元素中提取详细的几何信息
/// </summary>
[Transaction(TransactionMode.ReadOnly)]
[Regeneration(RegenerationOption.Manual)]
public class GetGeometryCommand : IExternalCommand
{
    private readonly ILogger<GetGeometryCommand>? _logger;

    public GetGeometryCommand()
    {
        _logger = DamAnalysisApplication.ServiceProvider?.GetRequiredService<ILogger<GetGeometryCommand>>();
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger?.LogInformation("开始执行获取几何命令");

            var uiApp = commandData.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 选择元素
            var selectedElements = SelectElements(uidoc);
            if (!selectedElements.Any())
            {
                TaskDialog.Show("取消", "未选择任何元素");
                return Result.Cancelled;
            }

            // 提取几何信息
            var geometryResults = new List<GeometryInfo>();
            foreach (var element in selectedElements)
            {
                var geometryInfo = ExtractGeometryInfo(element);
                if (geometryInfo != null)
                {
                    geometryResults.Add(geometryInfo);
                }
            }

            // 显示几何信息
            DisplayGeometryInfo(geometryResults);

            _logger?.LogInformation($"成功获取 {geometryResults.Count} 个元素的几何信息");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "获取几何信息时发生错误");
            message = $"获取几何失败: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 选择元素
    /// </summary>
    private List<Element> SelectElements(UIDocument uidoc)
    {
        var selectedElements = new List<Element>();

        try
        {
            // 首先检查是否已有预选元素
            var preSelectedIds = uidoc.Selection.GetElementIds();
            if (preSelectedIds.Any())
            {
                foreach (var id in preSelectedIds)
                {
                    var element = uidoc.Document.GetElement(id);
                    if (element != null)
                    {
                        selectedElements.Add(element);
                    }
                }
                return selectedElements;
            }

            // 如果没有预选，则让用户选择
            var selectionFilter = new DamElementSelectionFilter();
            var references = uidoc.Selection.PickObjects(
                ObjectType.Element,
                selectionFilter,
                "请选择要获取几何信息的元素（可多选）");

            foreach (var reference in references)
            {
                var element = uidoc.Document.GetElement(reference);
                if (element != null)
                {
                    selectedElements.Add(element);
                }
            }
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            // 用户取消选择
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "选择元素时发生错误");
        }

        return selectedElements;
    }

    /// <summary>
    /// 提取元素的几何信息
    /// </summary>
    private GeometryInfo ExtractGeometryInfo(Element element)
    {
        try
        {
            var geometryInfo = new GeometryInfo
            {
                ElementId = element.Id.Value,
                ElementName = element.Name ?? "未命名",
                CategoryName = element.Category?.Name ?? "未知类别"
            };

            // 获取边界框
            var boundingBox = element.get_BoundingBox(null);
            if (boundingBox != null)
            {
                geometryInfo.BoundingBoxMin = boundingBox.Min;
                geometryInfo.BoundingBoxMax = boundingBox.Max;
                geometryInfo.Width = boundingBox.Max.X - boundingBox.Min.X;
                geometryInfo.Height = boundingBox.Max.Z - boundingBox.Min.Z;
                geometryInfo.Depth = boundingBox.Max.Y - boundingBox.Min.Y;
            }

            // 获取几何体
            var options = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            var geometry = element.get_Geometry(options);
            if (geometry != null)
            {
                ExtractDetailedGeometry(geometry, geometryInfo);
            }

            return geometryInfo;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, $"提取元素 {element.Id} 的几何信息失败");
            return null;
        }
    }

    /// <summary>
    /// 提取详细几何信息
    /// </summary>
    private void ExtractDetailedGeometry(GeometryElement geometry, GeometryInfo geometryInfo)
    {
        foreach (var geoObject in geometry)
        {
            switch (geoObject)
            {
                case Solid solid when solid.Volume > 0:
                    geometryInfo.SolidCount++;
                    geometryInfo.TotalVolume += solid.Volume;
                    geometryInfo.TotalSurfaceArea += solid.SurfaceArea;
                    
                    // 检查是否为拉伸实体
                    if (IsExtrusionSolid(solid))
                    {
                        geometryInfo.IsExtrusion = true;
                        geometryInfo.ExtrusionInfo = GetExtrusionInfo(solid);
                    }
                    break;

                case Face face:
                    geometryInfo.FaceCount++;
                    break;

                case Edge edge:
                    geometryInfo.EdgeCount++;
                    break;

                case Curve curve:
                    geometryInfo.CurveCount++;
                    break;

                case GeometryInstance instance:
                    var instanceGeometry = instance.GetInstanceGeometry();
                    ExtractDetailedGeometry(instanceGeometry, geometryInfo);
                    break;
            }
        }
    }

    /// <summary>
    /// 检查是否为拉伸实体
    /// </summary>
    private bool IsExtrusionSolid(Solid solid)
    {
        try
        {
            // 简单判断：检查面的数量和类型
            var faces = solid.Faces.Cast<Face>().ToList();
            
            // 拉伸实体通常有规律的面结构
            var planarFaces = faces.OfType<PlanarFace>().ToList();
            var cylindricalFaces = faces.OfType<CylindricalFace>().ToList();
            
            // 如果大部分是平面且有规律的对称面，可能是拉伸体
            return planarFaces.Count >= 2 && faces.Count >= 4;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取拉伸信息
    /// </summary>
    private ExtrusionInfo GetExtrusionInfo(Solid solid)
    {
        var extrusionInfo = new ExtrusionInfo();
        
        try
        {
            var faces = solid.Faces.Cast<Face>().ToList();
            var planarFaces = faces.OfType<PlanarFace>().ToList();
            
            if (planarFaces.Count >= 2)
            {
                // 查找平行的面作为拉伸的起始和结束面
                for (int i = 0; i < planarFaces.Count - 1; i++)
                {
                    for (int j = i + 1; j < planarFaces.Count; j++)
                    {
                        var face1 = planarFaces[i];
                        var face2 = planarFaces[j];
                        
                        // 检查法向量是否平行但方向相反
                        if (face1.FaceNormal.IsAlmostEqualTo(face2.FaceNormal.Negate()))
                        {
                            extrusionInfo.StartFace = face1;
                            extrusionInfo.EndFace = face2;
                            extrusionInfo.ExtrusionDirection = face1.FaceNormal;
                            
                            // 计算拉伸距离
                            var distance = Math.Abs(face1.Origin.DistanceTo(face2.Origin));
                            extrusionInfo.ExtrusionLength = distance;
                            break;
                        }
                    }
                    if (extrusionInfo.StartFace != null) break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "获取拉伸信息时发生错误");
        }

        return extrusionInfo;
    }

    /// <summary>
    /// 显示几何信息
    /// </summary>
    private void DisplayGeometryInfo(List<GeometryInfo> geometryResults)
    {
        if (!geometryResults.Any())
        {
            TaskDialog.Show("结果", "未能获取任何几何信息");
            return;
        }

        var info = $"共获取 {geometryResults.Count} 个元素的几何信息：\n\n";

        foreach (var geo in geometryResults.Take(5)) // 显示前5个
        {
            info += $"元素 ID: {geo.ElementId}\n";
            info += $"  名称: {geo.ElementName}\n";
            info += $"  类别: {geo.CategoryName}\n";
            info += $"  尺寸: {geo.Width:F2} × {geo.Depth:F2} × {geo.Height:F2}\n";
            info += $"  体积: {geo.TotalVolume:F2} 立方单位\n";
            info += $"  表面积: {geo.TotalSurfaceArea:F2} 平方单位\n";
            info += $"  实体数: {geo.SolidCount}, 面数: {geo.FaceCount}\n";
            
            if (geo.IsExtrusion)
            {
                info += $"  ✓ 拉伸实体 (长度: {geo.ExtrusionInfo?.ExtrusionLength:F2})\n";
            }
            
            info += "\n";
        }

        if (geometryResults.Count > 5)
        {
            info += $"...还有 {geometryResults.Count - 5} 个元素的信息";
        }

        TaskDialog.Show("几何信息", info);
    }
}

/// <summary>
/// 几何信息类
/// </summary>
public class GeometryInfo
{
    public long ElementId { get; set; }
    public string ElementName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    
    public XYZ? BoundingBoxMin { get; set; }
    public XYZ? BoundingBoxMax { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }
    
    public int SolidCount { get; set; }
    public int FaceCount { get; set; }
    public int EdgeCount { get; set; }
    public int CurveCount { get; set; }
    
    public double TotalVolume { get; set; }
    public double TotalSurfaceArea { get; set; }
    
    public bool IsExtrusion { get; set; }
    public ExtrusionInfo? ExtrusionInfo { get; set; }
}

/// <summary>
/// 拉伸信息类
/// </summary>
public class ExtrusionInfo
{
    public Face? StartFace { get; set; }
    public Face? EndFace { get; set; }
    public XYZ? ExtrusionDirection { get; set; }
    public double ExtrusionLength { get; set; }
}
