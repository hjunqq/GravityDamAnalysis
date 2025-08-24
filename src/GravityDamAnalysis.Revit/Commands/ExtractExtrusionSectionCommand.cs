using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Models;
using GravityDamAnalysis.Revit.Application;
using GravityDamAnalysis.Revit.Selection;

namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 拉伸几何Sketch提取命令
/// 专门从拉伸几何体中提取sketch（草图轮廓），并在Revit中高亮显示提取的面
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class ExtractExtrusionSectionCommand : IExternalCommand
{
    private readonly ILogger<ExtractExtrusionSectionCommand>? _logger;
    private Document? _document;

    public ExtractExtrusionSectionCommand()
    {
        try
        {
            // 尝试从应用程序获取服务提供者
            _logger = DamAnalysisApplication.ServiceProvider.GetService<ILogger<ExtractExtrusionSectionCommand>>();
        }
        catch (InvalidOperationException)
        {
            // 服务提供者尚未初始化，这是正常情况
            // 日志记录器保持为null，程序可以继续运行
        }
        catch (Exception)
        {
            // 其他异常情况，日志记录器保持为null
        }
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger?.LogInformation("开始执行拉伸几何截面提取命令");

            var uiApp = commandData.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;
            _document = doc;

            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 选择拉伸元素
            var selectedElements = SelectExtrusionElements(uidoc);
            if (!selectedElements.Any())
            {
                TaskDialog.Show("取消", "未选择任何拉伸元素");
                return Result.Cancelled;
            }

            // 识别拉伸实体
            var extrusionData = IdentifyExtrusionGeometry(selectedElements);
            if (!extrusionData.Any())
            {
                TaskDialog.Show("提示", "所选元素中未找到拉伸几何体");
                return Result.Succeeded;
            }

            // 提取截面
            var sectionResults = ExtractSectionsFromExtrusions(extrusionData);
            
            // 高亮显示提取的面
            HighlightExtractedFaces(sectionResults, uidoc);
            
            // 显示结果
            DisplaySectionResults(sectionResults);

            _logger?.LogInformation($"成功提取 {sectionResults.Count} 个截面并高亮显示");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "提取拉伸截面时发生错误");
            message = $"截面提取失败: {ex.Message}";
            return Result.Failed;
        }
    }

    /// <summary>
    /// 选择拉伸元素
    /// </summary>
    private List<Element> SelectExtrusionElements(UIDocument uidoc)
    {
        var selectedElements = new List<Element>();

        try
        {
            // 检查预选元素
            var preSelectedIds = uidoc.Selection.GetElementIds();
            if (preSelectedIds.Any())
            {
                foreach (var id in preSelectedIds)
                {
                    var element = uidoc.Document.GetElement(id);
                    if (element != null && IsExtrusionElement(element))
                    {
                        selectedElements.Add(element);
                    }
                }
                
                if (selectedElements.Any())
                {
                    return selectedElements;
                }
            }

            // 让用户选择
            var selectionFilter = new ExtrusionElementSelectionFilter();
            var references = uidoc.Selection.PickObjects(
                ObjectType.Element,
                selectionFilter,
                "请选择拉伸元素（常规模型、体量等）");

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
            // 用户取消
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "选择拉伸元素时发生错误");
        }

        return selectedElements;
    }

    /// <summary>
    /// 判断是否为拉伸元素
    /// </summary>
    private bool IsExtrusionElement(Element element)
    {
        try
        {
            // 检查类别
            if (element.Category?.BuiltInCategory == BuiltInCategory.OST_GenericModel ||
                element.Category?.BuiltInCategory == BuiltInCategory.OST_Mass)
            {
                // 检查几何体是否包含拉伸实体
                var geometry = element.get_Geometry(new Options());
                if (geometry != null)
                {
                    foreach (var geoObject in geometry)
                    {
                        if (geoObject is Solid solid && IsExtrusionSolid(solid))
                        {
                            return true;
                        }
                        else if (geoObject is GeometryInstance instance)
                        {
                            var instanceGeometry = instance.GetInstanceGeometry();
                            foreach (var instanceGeoObject in instanceGeometry)
                            {
                                if (instanceGeoObject is Solid instanceSolid && IsExtrusionSolid(instanceSolid))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 判断是否为拉伸实体
    /// </summary>
    private bool IsExtrusionSolid(Solid solid)
    {
        try
        {
            if (solid.Volume <= 0) return false;

            var faces = solid.Faces.Cast<Face>().ToList();
            var planarFaces = faces.OfType<PlanarFace>().ToList();

            // 拉伸实体特征：至少有两个平行的平面作为起始和结束面
            if (planarFaces.Count >= 2)
            {
                for (int i = 0; i < planarFaces.Count - 1; i++)
                {
                    for (int j = i + 1; j < planarFaces.Count; j++)
                    {
                        var face1 = planarFaces[i];
                        var face2 = planarFaces[j];

                        // 检查法向量是否平行（方向相反）
                        if (face1.FaceNormal.IsAlmostEqualTo(face2.FaceNormal.Negate(), 0.1))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 识别拉伸几何体
    /// </summary>
    private List<ExtrusionData> IdentifyExtrusionGeometry(List<Element> elements)
    {
        var extrusionDataList = new List<ExtrusionData>();

        foreach (var element in elements)
        {
            try
            {
                var geometry = element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
                if (geometry == null) continue;

                var extrusionData = new ExtrusionData
                {
                    Element = element,
                    ElementId = element.Id.Value,
                    ElementName = element.Name ?? "未命名",
                    ExtrusionSolids = new List<ExtrusionSolidInfo>()
                };

                ProcessGeometry(geometry, extrusionData);

                if (extrusionData.ExtrusionSolids.Any())
                {
                    extrusionDataList.Add(extrusionData);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"处理元素 {element.Id} 的几何体时发生错误");
            }
        }

        return extrusionDataList;
    }

    /// <summary>
    /// 处理几何体
    /// </summary>
    private void ProcessGeometry(GeometryElement geometry, ExtrusionData extrusionData)
    {
        foreach (var geoObject in geometry)
        {
            switch (geoObject)
            {
                case Solid solid when IsExtrusionSolid(solid):
                    var extrusionInfo = AnalyzeExtrusionSolid(solid);
                    if (extrusionInfo != null)
                    {
                        extrusionData.ExtrusionSolids.Add(extrusionInfo);
                    }
                    break;

                case GeometryInstance instance:
                    var instanceGeometry = instance.GetInstanceGeometry();
                    ProcessGeometry(instanceGeometry, extrusionData);
                    break;
            }
        }
    }

    /// <summary>
    /// 分析拉伸实体
    /// </summary>
    private ExtrusionSolidInfo? AnalyzeExtrusionSolid(Solid solid)
    {
        try
        {
            var info = new ExtrusionSolidInfo
            {
                Solid = solid,
                Volume = solid.Volume,
                SurfaceArea = solid.SurfaceArea
            };

            var faces = solid.Faces.Cast<Face>().ToList();
            var planarFaces = faces.OfType<PlanarFace>().ToList();

            // 查找拉伸的起始和结束面
            for (int i = 0; i < planarFaces.Count - 1; i++)
            {
                for (int j = i + 1; j < planarFaces.Count; j++)
                {
                    var face1 = planarFaces[i];
                    var face2 = planarFaces[j];

                    if (face1.FaceNormal.IsAlmostEqualTo(face2.FaceNormal.Negate(), 0.1))
                    {
                        info.StartFace = face1;
                        info.EndFace = face2;
                        info.ExtrusionDirection = face1.FaceNormal;
                        
                        // 计算拉伸距离
                        var startCenter = GetFaceCenter(face1);
                        var endCenter = GetFaceCenter(face2);
                        info.ExtrusionLength = startCenter.DistanceTo(endCenter);
                        break;
                    }
                }
                if (info.StartFace != null) break;
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "分析拉伸实体时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 从拉伸几何体提取sketch
    /// </summary>
    private List<SectionResult> ExtractSectionsFromExtrusions(List<ExtrusionData> extrusionDataList)
    {
        var sectionResults = new List<SectionResult>();

        foreach (var extrusionData in extrusionDataList)
        {
            try
            {
                var sectionResult = new SectionResult
                {
                    ElementId = extrusionData.ElementId,
                    ElementName = extrusionData.ElementName,
                    ExtrusionLength = 0,
                    ExtrusionDirection = null,
                    SectionProfiles = new List<SectionProfile>(),
                    ExtractedFaces = new List<Face>()
                };

                // 尝试提取拉伸体的sketch
                var sketchProfile = ExtractExtrusionSketch(extrusionData.Element);
                if (sketchProfile != null)
                {
                    sectionResult.SectionProfiles.Add(sketchProfile);
                    
                    // 如果找到了sketch，也高亮显示对应的面
                    if (extrusionData.ExtrusionSolids.Any())
                    {
                        var firstSolid = extrusionData.ExtrusionSolids.First();
                        if (firstSolid.StartFace != null)
                        {
                            sectionResult.ExtractedFaces.Add(firstSolid.StartFace);
                        }
                    }
                }

                if (sectionResult.SectionProfiles.Any())
                {
                    sectionResults.Add(sectionResult);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"提取元素 {extrusionData.ElementId} 的sketch时发生错误");
            }
        }

        return sectionResults;
    }

    /// <summary>
    /// 高亮显示提取的面
    /// </summary>
    private void HighlightExtractedFaces(List<SectionResult> sectionResults, UIDocument uidoc)
    {
        try
        {
            if (_document == null) return;

            using (Transaction trans = new Transaction(_document, "高亮显示提取的面"))
            {
                trans.Start();

                // 收集所有需要高亮的元素ID
                var elementsToHighlight = new HashSet<ElementId>();
                var totalFacesCount = 0;
                
                foreach (var result in sectionResults)
                {
                    var elementId = new ElementId(result.ElementId);
                    elementsToHighlight.Add(elementId);
                    totalFacesCount += result.ExtractedFaces.Count;
                }

                // 创建高亮显示的图形覆盖
                if (elementsToHighlight.Any())
                {
                    // 创建绿色半透明材质用于高亮
                    var overrideGraphics = new OverrideGraphicSettings();
                    
                    // 设置表面颜色为绿色
                    overrideGraphics.SetSurfaceForegroundPatternColor(new Color(0, 255, 0));
                    overrideGraphics.SetSurfaceTransparency(30); // 30% 透明度
                    
                    // 设置轮廓线为红色加粗
                    overrideGraphics.SetProjectionLineColor(new Color(255, 0, 0));
                    overrideGraphics.SetProjectionLineWeight(5);

                    // 应用图形覆盖到视图
                    var activeView = uidoc.ActiveView;
                    foreach (var elementId in elementsToHighlight)
                    {
                        try
                        {
                            activeView.SetElementOverrides(elementId, overrideGraphics);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, $"无法为元素 {elementId} 设置图形覆盖");
                        }
                    }

                    // 显示提示信息
                    TaskDialog.Show("高亮显示", 
                        $"已高亮显示 {elementsToHighlight.Count} 个元素的Sketch提取结果。\n" +
                        $"总共识别到 {totalFacesCount} 个Sketch。\n\n" +
                        "高亮效果：\n" +
                        "• 绿色半透明表面：包含提取Sketch的元素\n" +
                        "• 红色加粗轮廓：元素边界\n\n" +
                        "提示：可以通过视图菜单的'图形设置'功能清除高亮显示。");
                }

                trans.Commit();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "高亮显示面时发生错误");
            TaskDialog.Show("错误", $"高亮显示失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 提取拉伸体的sketch
    /// </summary>
    private SectionProfile? ExtractExtrusionSketch(Element element)
    {
        try
        {
            // 通过几何体分析获取边界轮廓作为sketch
            var geometry = element.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
            if (geometry != null)
            {
                foreach (var geoObject in geometry)
                {
                    if (geoObject is Solid solid)
                    {
                        // 找到最底部的面作为sketch
                        var bottomFace = FindBottomFace(solid);
                        if (bottomFace != null)
                        {
                            return ExtractFaceProfile(bottomFace, "拉伸Sketch");
                        }
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "提取拉伸体sketch时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 找到最底部的面
    /// </summary>
    private Face? FindBottomFace(Solid solid)
    {
        try
        {
            var faces = solid.Faces.Cast<Face>().ToList();
            var planarFaces = faces.OfType<PlanarFace>().ToList();

            if (!planarFaces.Any()) return null;

            // 找到Z坐标最小的面（最底部）
            var bottomFace = planarFaces.OrderBy(f => f.Origin.Z).First();
            return bottomFace;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 提取面轮廓
    /// </summary>
    private SectionProfile? ExtractFaceProfile(Face face, string profileName)
    {
        try
        {
            var profile = new SectionProfile
            {
                Name = profileName,
                Coordinates = new List<Point2D>(),
                Area = face.Area
            };

            // 获取面的边界循环
            var edgeLoops = face.EdgeLoops;
            foreach (EdgeArray edgeLoop in edgeLoops)
            {
                foreach (Edge edge in edgeLoop)
                {
                    var curve = edge.AsCurve();
                    if (curve != null)
                    {
                        // 采样曲线上的点
                        var tessellatedCurve = curve.Tessellate();
                        foreach (var point in tessellatedCurve)
                        {
                            // 将3D点投影到面的2D坐标系
                            var point2D = ProjectTo2D(point, face);
                            profile.Coordinates.Add(point2D);
                        }
                    }
                }
            }

            return profile.Coordinates.Any() ? profile : null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "提取面轮廓时发生错误");
            return null;
        }
    }

    /// <summary>
    /// 将3D点投影到2D
    /// </summary>
    private Point2D ProjectTo2D(XYZ point3D, Face face)
    {
        try
        {
            // 使用面的参数化来获取2D坐标
            var result = face.Project(point3D);
            if (result != null)
            {
                var uv = result.UVPoint;
                return new Point2D { X = uv.U, Y = uv.V };
            }

            // 如果投影失败，使用简单的平面投影
            if (face is PlanarFace planarFace)
            {
                var origin = planarFace.Origin;
                var xAxis = planarFace.XVector;
                var yAxis = planarFace.YVector;

                var localPoint = point3D - origin;
                var x = localPoint.DotProduct(xAxis);
                var y = localPoint.DotProduct(yAxis);

                return new Point2D { X = x, Y = y };
            }

            return new Point2D { X = point3D.X, Y = point3D.Y };
        }
        catch
        {
            return new Point2D { X = point3D.X, Y = point3D.Y };
        }
    }



    /// <summary>
    /// 获取面的中心点
    /// </summary>
    private XYZ GetFaceCenter(Face face)
    {
        try
        {
            // 对于平面，使用面的参数化中心
            if (face is PlanarFace planarFace)
            {
                return planarFace.Origin;
            }

            // 对于其他类型的面，计算边界点的平均值
            return GetFaceCentroid(face);
        }
        catch
        {
            return GetFaceCentroid(face);
        }
    }

    /// <summary>
    /// 获取面的重心
    /// </summary>
    private XYZ GetFaceCentroid(Face face)
    {
        try
        {
            var edgeArray = face.EdgeLoops.get_Item(0);
            var points = new List<XYZ>();
            
            foreach (Edge edge in edgeArray)
            {
                var curve = edge.AsCurve();
                if (curve != null)
                {
                    points.Add(curve.GetEndPoint(0));
                }
            }

            if (points.Count > 0)
            {
                var centroid = new XYZ(0, 0, 0);
                foreach (var point in points)
                {
                    centroid = centroid.Add(point);
                }
                return centroid.Divide(points.Count);
            }

            return new XYZ(0, 0, 0);
        }
        catch
        {
            return new XYZ(0, 0, 0);
        }
    }

    /// <summary>
    /// 显示截面结果
    /// </summary>
    private void DisplaySectionResults(List<SectionResult> sectionResults)
    {
        if (!sectionResults.Any())
        {
            TaskDialog.Show("结果", "未能提取任何截面");
            return;
        }

        var info = $"成功从 {sectionResults.Count} 个拉伸实体中提取Sketch：\n\n";

        foreach (var result in sectionResults.Take(3)) // 显示前3个
        {
            info += $"元素 ID: {result.ElementId} ({result.ElementName})\n";
            info += $"  提取Sketch数: {result.SectionProfiles.Count}\n";
            info += $"  高亮显示面数: {result.ExtractedFaces.Count}\n";

            foreach (var profile in result.SectionProfiles)
            {
                info += $"    - {profile.Name}: {profile.Coordinates.Count} 个坐标点, 面积: {profile.Area:F2}\n";
            }
            info += "\n";
        }

        if (sectionResults.Count > 3)
        {
            info += $"...还有 {sectionResults.Count - 3} 个结果";
        }

        TaskDialog.Show("截面提取结果", info);
    }
}

/// <summary>
/// 拉伸元素选择过滤器
/// </summary>
public class ExtrusionElementSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem.Category?.BuiltInCategory == BuiltInCategory.OST_GenericModel ||
               elem.Category?.BuiltInCategory == BuiltInCategory.OST_Mass ||
               elem.Category?.BuiltInCategory == BuiltInCategory.OST_StructuralFraming;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        return false;
    }
}

/// <summary>
/// 拉伸数据类
/// </summary>
public class ExtrusionData
{
    public required Element Element { get; set; }
    public long ElementId { get; set; }
    public required string ElementName { get; set; }
    public required List<ExtrusionSolidInfo> ExtrusionSolids { get; set; }
}

/// <summary>
/// 拉伸实体信息类
/// </summary>
public class ExtrusionSolidInfo
{
    public required Solid Solid { get; set; }
    public Face? StartFace { get; set; }
    public Face? EndFace { get; set; }
    public XYZ? ExtrusionDirection { get; set; }
    public double ExtrusionLength { get; set; }
    public double Volume { get; set; }
    public double SurfaceArea { get; set; }
}

/// <summary>
/// 截面结果类
/// </summary>
public class SectionResult
{
    public long ElementId { get; set; }
    public required string ElementName { get; set; }
    public double ExtrusionLength { get; set; }
    public XYZ? ExtrusionDirection { get; set; }
    public required List<SectionProfile> SectionProfiles { get; set; }
    public required List<Face> ExtractedFaces { get; set; } = new List<Face>(); // 新增：存储提取的面
}

/// <summary>
/// 截面轮廓类
/// </summary>
public class SectionProfile
{
    public required string Name { get; set; }
    public required List<Point2D> Coordinates { get; set; }
    public double Area { get; set; }
}

/// <summary>
/// 2D点类
/// </summary>
public class Point2D
{
    public double X { get; set; }
    public double Y { get; set; }
}
