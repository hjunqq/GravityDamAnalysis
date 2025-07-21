using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;

namespace GravityDamAnalysis.Revit.SectionAnalysis;

/// <summary>
/// 高级剖面提取器
/// 实现直接Face-Plane求交的高效几何切割算法
/// </summary>
public class AdvancedSectionExtractor
{
    private readonly ILogger<AdvancedSectionExtractor> _logger;
    private const double TOLERANCE = 1e-6; // 几何容差，单位：英尺
    private const double CONNECTION_TOLERANCE = 1e-3; // 曲线连接容差

    public AdvancedSectionExtractor(ILogger<AdvancedSectionExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 提取剖面（适配新的验证工作流）
    /// 使用标准的Revit ViewSection流程提取剖面
    /// </summary>
    public async Task<EnhancedProfile2D> ExtractProfile(
        Document doc, 
        List<Element> damElements, 
        SectionLocation location)
    {
        try
        {
            if (!damElements.Any())
            {
                _logger.LogWarning("未提供坝体元素");
                return new EnhancedProfile2D { Name = location.Name ?? "空剖面" };
            }

            _logger.LogInformation("开始使用ViewSection方法提取剖面: {SectionName}", location.Name);
            
            // 使用标准的Revit ViewSection流程
            var profile = await ExtractProfileUsingViewSection(doc, damElements, location);
            
            // 如果ViewSection方法失败，回退到直接几何切割方法
            if (profile == null || !profile.MainContour.Any())
            {
                _logger.LogWarning("ViewSection方法失败，回退到直接几何切割方法");
                var primaryElement = damElements.First();
                var sectionPlane = CreateSectionPlane(location);
                profile = ExtractSectionProfileAdvanced(primaryElement, sectionPlane, location.Name);
            }
            
            // 设置基础属性
            profile.Name = location.Name ?? $"剖面_{DateTime.Now:HHmmss}";
            
            _logger.LogInformation("剖面提取完成: {ProfileName}", profile.Name);
            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取剖面时发生错误");
            return new EnhancedProfile2D 
            { 
                Name = location.Name ?? "错误剖面"
            };
        }
    }

    /// <summary>
    /// 从剖面位置创建剖面平面
    /// </summary>
    private Plane CreateSectionPlane(SectionLocation location)
    {
        // 使用剖面位置信息创建平面
        var origin = new XYZ(location.Position.X, location.Position.Y, location.Position.Z);
        var normal = new XYZ(location.Normal.X, location.Normal.Y, location.Normal.Z);
        
        return Plane.CreateByNormalAndOrigin(normal, origin);
    }

    /// <summary>
    /// 从坝体元素提取指定剖面的2D轮廓（改进算法）
    /// 使用直接Face-Plane求交，避免创建辅助几何体
    /// </summary>
    /// <param name="damElement">坝体元素</param>
    /// <param name="sectionPlane">剖面平面</param>
    /// <param name="sectionName">剖面名称</param>
    /// <returns>增强的二维剖面数据</returns>
    public EnhancedProfile2D ExtractSectionProfileAdvanced(Element damElement, Plane sectionPlane, string sectionName = "")
    {
        try
        {
            _logger.LogInformation("开始高级剖面提取: 元素ID {ElementId}", damElement.Id);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // 获取元素几何数据
            var geometryElement = GetOptimizedGeometry(damElement);
            if (geometryElement == null)
            {
                throw new InvalidOperationException("无法获取元素几何数据");
            }

            // 提取所有相交曲线
            var intersectionCurves = ExtractIntersectionCurves(geometryElement, sectionPlane);
            
            if (!intersectionCurves.Any())
            {
                _logger.LogWarning("未找到与剖面相交的几何体");
                return CreateEmptyEnhancedProfile(sectionName, sectionPlane);
            }

            // 组织曲线为闭合循环
            var curveLoops = OrganizeCurvesIntoCurveLoops(intersectionCurves);
            
            // 转换为增强的2D剖面
            var enhancedProfile = ConvertToEnhancedProfile2D(curveLoops, sectionPlane, sectionName);
            
            // 自动识别几何特征
            enhancedProfile.IdentifyGeometricFeatures();
            
            // 提取材料分区信息
            ExtractMaterialZones(enhancedProfile, damElement);
            
            // 设置边界条件
            SetupBoundaryConditions(enhancedProfile);

            stopwatch.Stop();
            _logger.LogInformation("剖面提取完成，用时: {Elapsed:F2}ms, 包含 {ContourCount} 个轮廓", 
                stopwatch.Elapsed.TotalMilliseconds, 1 + enhancedProfile.InnerContours.Count);

            return enhancedProfile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "高级剖面提取失败");
            throw;
        }
    }

    /// <summary>
    /// 提取几何体与平面的相交曲线（核心改进算法）
    /// </summary>
    private List<Curve> ExtractIntersectionCurves(GeometryElement geometryElement, Plane sectionPlane)
    {
        var intersectionCurves = new List<Curve>();

        foreach (var geomObj in geometryElement)
        {
            var curves = ProcessGeometryObject(geomObj, sectionPlane);
            intersectionCurves.AddRange(curves);
        }

        return intersectionCurves;
    }

    /// <summary>
    /// 处理单个几何对象
    /// </summary>
    private List<Curve> ProcessGeometryObject(GeometryObject geomObj, Plane sectionPlane)
    {
        var curves = new List<Curve>();

        switch (geomObj)
        {
            case Solid solid when solid.Volume > TOLERANCE:
                curves.AddRange(ExtractCurvesFromSolid(solid, sectionPlane));
                break;

            case GeometryInstance instance:
                curves.AddRange(ProcessGeometryInstance(instance, sectionPlane));
                break;

            case Mesh mesh:
                curves.AddRange(ExtractCurvesFromMesh(mesh, sectionPlane));
                break;
        }

        return curves;
    }

    /// <summary>
    /// 从实体中提取与平面相交的曲线（改进算法）
    /// </summary>
    private List<Curve> ExtractCurvesFromSolid(Solid solid, Plane sectionPlane)
    {
        var curves = new List<Curve>();

        try
        {
            // 遍历实体的每个面
            foreach (Face face in solid.Faces)
            {
                // 获取面的边界曲线并检查是否在平面上
                var edgeCurves = GetFaceEdgeCurves(face);
                
                // 筛选在平面上或接近平面的曲线
                var planeCurves = edgeCurves.Where(curve => IsOnPlane(curve, sectionPlane, TOLERANCE));
                curves.AddRange(planeCurves);
                
                // 如果没有找到在平面上的曲线，尝试计算面与平面的交线
                if (!planeCurves.Any())
                {
                    var intersectionCurves = ComputeFacePlaneIntersection(face, sectionPlane);
                    curves.AddRange(intersectionCurves);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从实体提取曲线时发生错误");
        }

        return curves;
    }

    /// <summary>
    /// 计算面与平面的交线
    /// </summary>
    private List<Curve> ComputeFacePlaneIntersection(Face face, Plane plane)
    {
        var intersectionCurves = new List<Curve>();

        try
        {
            // 获取面的UV边界
            var uvBounds = face.GetBoundingBox();
            if (uvBounds == null) return intersectionCurves;

            // 在面上采样点并检测与平面的交线
            var samplePoints = GenerateFaceSamplePoints(face, uvBounds, 20); // 20x20网格
            var intersectionPoints = new List<XYZ>();

            for (int i = 0; i < samplePoints.Count - 1; i++)
            {
                var p1 = samplePoints[i];
                var p2 = samplePoints[i + 1];
                
                var intersection = ComputeLineSegmentPlaneIntersection(p1, p2, plane);
                if (intersection != null)
                {
                    intersectionPoints.Add(intersection);
                }
            }

            // 将交点连接成曲线
            if (intersectionPoints.Count >= 2)
            {
                var curve = ConnectPointsToCurve(intersectionPoints);
                if (curve != null)
                {
                    intersectionCurves.Add(curve);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "计算面平面交线时发生错误");
        }

        return intersectionCurves;
    }

    /// <summary>
    /// 在面上生成采样点
    /// </summary>
    private List<XYZ> GenerateFaceSamplePoints(Face face, BoundingBoxUV uvBounds, int resolution)
    {
        var points = new List<XYZ>();
        
        double uStep = (uvBounds.Max.U - uvBounds.Min.U) / resolution;
        double vStep = (uvBounds.Max.V - uvBounds.Min.V) / resolution;

        for (int i = 0; i <= resolution; i++)
        {
            for (int j = 0; j <= resolution; j++)
            {
                double u = uvBounds.Min.U + i * uStep;
                double v = uvBounds.Min.V + j * vStep;
                
                try
                {
                    var point = face.Evaluate(new UV(u, v));
                    points.Add(point);
                }
                catch
                {
                    // 忽略超出面边界的UV坐标
                }
            }
        }

        return points;
    }

    /// <summary>
    /// 计算线段与平面的交点
    /// </summary>
    private XYZ ComputeLineSegmentPlaneIntersection(XYZ p1, XYZ p2, Plane plane)
    {
        var d1 = DistancePointToPlane(p1, plane);
        var d2 = DistancePointToPlane(p2, plane);

        // 检查线段是否与平面相交
        if (Math.Sign(d1) == Math.Sign(d2) && Math.Abs(d1) > TOLERANCE && Math.Abs(d2) > TOLERANCE)
        {
            return null; // 线段在平面同一侧
        }

        // 计算交点
        var t = Math.Abs(d1) / (Math.Abs(d1) + Math.Abs(d2));
        return p1 + t * (p2 - p1);
    }

    /// <summary>
    /// 将点集连接成曲线
    /// </summary>
    private Curve ConnectPointsToCurve(List<XYZ> points)
    {
        if (points.Count < 2) return null;

        try
        {
            if (points.Count == 2)
            {
                return Line.CreateBound(points[0], points[1]);
            }
            else
            {
                // 创建样条曲线
                var curvePoints = points.Distinct().ToList();
                if (curvePoints.Count >= 2)
                {
                    return Line.CreateBound(curvePoints.First(), curvePoints.Last());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "连接点为曲线失败");
        }

        return null;
    }

    /// <summary>
    /// 获取面的边界曲线
    /// </summary>
    private List<Curve> GetFaceEdgeCurves(Face face)
    {
        var curves = new List<Curve>();

        try
        {
            var edgeLoops = face.GetEdgesAsCurveLoops();
            foreach (var loop in edgeLoops)
            {
                foreach (Curve curve in loop)
                {
                    curves.Add(curve);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "获取面边界曲线失败");
        }

        return curves;
    }

    /// <summary>
    /// 处理几何实例
    /// </summary>
    private List<Curve> ProcessGeometryInstance(GeometryInstance instance, Plane sectionPlane)
    {
        var curves = new List<Curve>();

        try
        {
            var transform = instance.Transform;
            var instanceGeometry = instance.GetInstanceGeometry();

            foreach (var geomObj in instanceGeometry)
            {
                var instanceCurves = ProcessGeometryObject(geomObj, sectionPlane);
                
                // 应用变换
                foreach (var curve in instanceCurves)
                {
                    var transformedCurve = curve.CreateTransformed(transform);
                    curves.Add(transformedCurve);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理几何实例失败");
        }

        return curves;
    }

    /// <summary>
    /// 从网格中提取曲线
    /// </summary>
    private List<Curve> ExtractCurvesFromMesh(Mesh mesh, Plane sectionPlane)
    {
        var curves = new List<Curve>();

        try
        {
            // 检查三角形与平面的交线
            for (int i = 0; i < mesh.NumTriangles; i++)
            {
                var triangle = mesh.get_Triangle(i);
                var intersectionCurve = ComputeTrianglePlaneIntersection(triangle, sectionPlane);
                if (intersectionCurve != null)
                {
                    curves.Add(intersectionCurve);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "从网格提取曲线失败");
        }

        return curves;
    }

    /// <summary>
    /// 计算三角形与平面的交线
    /// </summary>
    private Curve ComputeTrianglePlaneIntersection(MeshTriangle triangle, Plane plane)
    {
        var vertices = new[] 
        { 
            triangle.get_Vertex(0), 
            triangle.get_Vertex(1), 
            triangle.get_Vertex(2) 
        };

        var intersectionPoints = new List<XYZ>();

        // 检查每条边与平面的交点
        for (int i = 0; i < 3; i++)
        {
            var p1 = vertices[i];
            var p2 = vertices[(i + 1) % 3];
            
            var intersection = ComputeLineSegmentPlaneIntersection(p1, p2, plane);
            if (intersection != null)
            {
                intersectionPoints.Add(intersection);
            }
        }

        // 如果有两个交点，创建线段
        if (intersectionPoints.Count == 2)
        {
            try
            {
                return Line.CreateBound(intersectionPoints[0], intersectionPoints[1]);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// 将离散曲线组织成连续的闭合循环（改进算法）
    /// </summary>
    public List<CurveLoop> OrganizeCurvesIntoCurveLoops(List<Curve> curves)
    {
        var curveLoops = new List<CurveLoop>();
        var unusedCurves = new List<Curve>(curves);

        while (unusedCurves.Count > 0)
        {
            var loop = BuildCurveLoop(unusedCurves);
            if (loop != null && loop.Count > 2)
            {
                try
                {
                    var curveLoop = CurveLoop.Create(loop);
                    curveLoops.Add(curveLoop);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "创建曲线循环失败，跳过该循环");
                }
            }
        }

        return curveLoops;
    }

    /// <summary>
    /// 构建单个曲线循环
    /// </summary>
    private List<Curve> BuildCurveLoop(List<Curve> unusedCurves)
    {
        if (!unusedCurves.Any()) return null;

        var loop = new List<Curve>();
        var currentCurve = unusedCurves[0];
        unusedCurves.RemoveAt(0);
        loop.Add(currentCurve);

        var endPoint = currentCurve.GetEndPoint(1);
        var startPoint = currentCurve.GetEndPoint(0);

        // 寻找连接的曲线
        while (unusedCurves.Count > 0)
        {
            var nextCurve = FindConnectedCurve(endPoint, unusedCurves, CONNECTION_TOLERANCE);
            if (nextCurve == null) break;

            unusedCurves.Remove(nextCurve);
            loop.Add(nextCurve);
            endPoint = nextCurve.GetEndPoint(1);

            // 检查是否形成闭合循环
            if (endPoint.DistanceTo(startPoint) < CONNECTION_TOLERANCE)
            {
                break;
            }
        }

        return loop;
    }

    /// <summary>
    /// 查找连接的曲线
    /// </summary>
    private Curve FindConnectedCurve(XYZ point, List<Curve> curves, double tolerance)
    {
        foreach (var curve in curves)
        {
            if (point.DistanceTo(curve.GetEndPoint(0)) < tolerance)
            {
                return curve;
            }
            if (point.DistanceTo(curve.GetEndPoint(1)) < tolerance)
            {
                return curve.CreateReversed();
            }
        }
        return null;
    }

    /// <summary>
    /// 检查曲线是否在指定平面上
    /// </summary>
    private bool IsOnPlane(Curve curve, Plane plane, double tolerance)
    {
        var startDistance = Math.Abs(DistancePointToPlane(curve.GetEndPoint(0), plane));
        var endDistance = Math.Abs(DistancePointToPlane(curve.GetEndPoint(1), plane));
        return startDistance < tolerance && endDistance < tolerance;
    }

    /// <summary>
    /// 计算点到平面的距离
    /// </summary>
    private double DistancePointToPlane(XYZ point, Plane plane)
    {
        var vectorToPoint = point - plane.Origin;
        return vectorToPoint.DotProduct(plane.Normal);
    }

    /// <summary>
    /// 获取优化的几何数据
    /// </summary>
    private GeometryElement GetOptimizedGeometry(Element element)
    {
        var options = new Options
        {
            ComputeReferences = false,
            DetailLevel = ViewDetailLevel.Fine,
            IncludeNonVisibleObjects = false
        };

        return element.get_Geometry(options);
    }

    /// <summary>
    /// 转换为增强的2D剖面
    /// </summary>
    private EnhancedProfile2D ConvertToEnhancedProfile2D(List<CurveLoop> curveLoops, Plane sectionPlane, string sectionName)
    {
        var profile = new EnhancedProfile2D
        {
            Name = string.IsNullOrEmpty(sectionName) ? $"Section_{DateTime.Now:HHmmss}" : sectionName,
            SectionNormal = new Vector3D(sectionPlane.Normal.X, sectionPlane.Normal.Y, sectionPlane.Normal.Z),
            SectionOrigin = new Point3D(sectionPlane.Origin.X, sectionPlane.Origin.Y, sectionPlane.Origin.Z),
            LocalXAxis = new Vector3D(sectionPlane.XVec.X, sectionPlane.XVec.Y, sectionPlane.XVec.Z),
            LocalYAxis = new Vector3D(sectionPlane.YVec.X, sectionPlane.YVec.Y, sectionPlane.YVec.Z)
        };

        // 建立2D坐标系
        var origin = sectionPlane.Origin;
        var xAxis = sectionPlane.XVec;
        var yAxis = sectionPlane.YVec;

        // 处理主轮廓
        if (curveLoops.Count > 0)
        {
            profile.MainContour = ConvertCurveLoopTo2D(curveLoops[0], origin, xAxis, yAxis);
        }

        // 处理内部轮廓
        for (int i = 1; i < curveLoops.Count; i++)
        {
            var innerContour = ConvertCurveLoopTo2D(curveLoops[i], origin, xAxis, yAxis);
            profile.AddInnerContour(innerContour);
        }

        return profile;
    }

    /// <summary>
    /// 将曲线循环转换为2D点集
    /// </summary>
    private List<Point2D> ConvertCurveLoopTo2D(CurveLoop curveLoop, XYZ origin, XYZ xAxis, XYZ yAxis)
    {
        var points2D = new List<Point2D>();

        foreach (Curve curve in curveLoop)
        {
            // 对曲线进行离散化
            var discretizedPoints = DiscretizeCurve(curve, 10); // 每条曲线10个点
            foreach (var point3D in discretizedPoints)
            {
                var point2D = ProjectTo2D(point3D, origin, xAxis, yAxis);
                points2D.Add(point2D);
            }
        }

        return points2D;
    }

    /// <summary>
    /// 离散化曲线
    /// </summary>
    private List<XYZ> DiscretizeCurve(Curve curve, int numPoints)
    {
        var points = new List<XYZ>();
        
        for (int i = 0; i <= numPoints; i++)
        {
            double parameter = i / (double)numPoints;
            var point = curve.Evaluate(parameter, false);
            points.Add(point);
        }

        return points;
    }

    /// <summary>
    /// 将3D点投影到2D坐标系
    /// </summary>
    private Point2D ProjectTo2D(XYZ point3D, XYZ origin, XYZ xAxis, XYZ yAxis)
    {
        var localVector = point3D - origin;
        var x = localVector.DotProduct(xAxis);
        var y = localVector.DotProduct(yAxis);
        return new Point2D(x, y);
    }

    /// <summary>
    /// 提取材料分区信息
    /// </summary>
    private void ExtractMaterialZones(EnhancedProfile2D profile, Element damElement)
    {
        try
        {
            var materialIds = damElement.GetMaterialIds(false);
            
            foreach (var materialId in materialIds)
            {
                var material = damElement.Document.GetElement(materialId) as Material;
                if (material != null)
                {
                    var materialZone = new MaterialZone
                    {
                        Name = material.Name,
                        Boundary = profile.MainContour, // 简化处理，使用主轮廓
                        Properties = ExtractMaterialProperties(material)
                    };
                    
                    profile.MaterialZones.Add(materialZone);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "提取材料分区信息失败");
        }
    }

    /// <summary>
    /// 提取材料属性
    /// </summary>
    private MaterialProperties ExtractMaterialProperties(Material material)
    {
        // 从Revit材料中提取属性（使用默认值，确保兼容性）
        return new MaterialProperties(
            material.Name,
            24000, // 默认混凝土密度 (N/m³)
            30000, // 默认弹性模量 (MPa)
            0.18,  // 默认泊松比
            25.0,  // 默认抗压强度 (MPa)
            2.5,   // 默认抗拉强度 (MPa)
            0.75   // 默认摩擦系数
        );
    }

    /// <summary>
    /// 设置边界条件
    /// </summary>
    private void SetupBoundaryConditions(EnhancedProfile2D profile)
    {
        profile.BoundaryConditions = new BoundaryConditions
        {
            BaseConstraint = BaseConstraint.Fixed,
            UpstreamPressure = new List<PressureBoundary>(),
            DownstreamPressure = new List<PressureBoundary>(),
            UpliftPressure = new UpliftPressureBoundary()
        };
    }

    /// <summary>
    /// 创建空的增强剖面
    /// </summary>
    private EnhancedProfile2D CreateEmptyEnhancedProfile(string sectionName, Plane sectionPlane)
    {
        return new EnhancedProfile2D
        {
            Name = string.IsNullOrEmpty(sectionName) ? "Empty_Section" : sectionName,
            SectionNormal = new Vector3D(sectionPlane.Normal.X, sectionPlane.Normal.Y, sectionPlane.Normal.Z),
            SectionOrigin = new Point3D(sectionPlane.Origin.X, sectionPlane.Origin.Y, sectionPlane.Origin.Z),
            LocalXAxis = new Vector3D(sectionPlane.XVec.X, sectionPlane.XVec.Y, sectionPlane.XVec.Z),
            LocalYAxis = new Vector3D(sectionPlane.YVec.X, sectionPlane.YVec.Y, sectionPlane.YVec.Z)
        };
    }

    /// <summary>
    /// 使用Revit ViewSection标准流程提取剖面
    /// 遵循官方推荐的视图驱动几何提取方法
    /// </summary>
    private async Task<EnhancedProfile2D> ExtractProfileUsingViewSection(
        Document doc, 
        List<Element> damElements, 
        SectionLocation location)
    {
        ViewSection sectionView = null;
        
        try
        {
            _logger.LogInformation("开始标准ViewSection剖面提取流程");
            
            // 第一步：定义剖切范围 (BoundingBoxXYZ)
            var sectionBox = CreateSectionBoundingBox(damElements, location);
            if (sectionBox == null)
            {
                _logger.LogError("无法创建剖面边界框");
                return null;
            }
            
            // 第二步：创建剖面视图 (ViewSection)
            sectionView = await CreateSectionViewAsync(doc, sectionBox, location.Name);
            if (sectionView == null)
            {
                _logger.LogError("无法创建剖面视图");
                return null;
            }
            
            // 第三步：从新视图中提取几何体
            var sectionProfiles = ExtractGeometryFromSectionView(damElements, sectionView);
            if (!sectionProfiles.Any())
            {
                _logger.LogWarning("未从剖面视图中提取到几何轮廓");
                return null;
            }
            
            // 第四步：处理和解析剖面几何
            var enhancedProfile = ProcessSectionGeometry(sectionProfiles, location, sectionView);
            
            _logger.LogInformation("成功使用ViewSection方法提取剖面: {ProfileName}", enhancedProfile.Name);
            return enhancedProfile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ViewSection剖面提取过程发生错误");
            return null;
        }
        finally
        {
            // 第五步：清理临时视图
            if (sectionView != null)
            {
                await CleanupTemporarySectionView(doc, sectionView);
            }
        }
    }
    
    /// <summary>
    /// 创建剖面边界框
    /// </summary>
    private BoundingBoxXYZ CreateSectionBoundingBox(List<Element> damElements, SectionLocation location)
    {
        try
        {
            // 计算坝体整体边界框
            BoundingBoxXYZ overallBBox = null;
            foreach (var element in damElements)
            {
                var elementBBox = element.get_BoundingBox(null);
                if (elementBBox != null)
                {
                    if (overallBBox == null)
                    {
                        overallBBox = elementBBox;
                    }
                    else
                    {
                        overallBBox.Min = new XYZ(
                            Math.Min(overallBBox.Min.X, elementBBox.Min.X),
                            Math.Min(overallBBox.Min.Y, elementBBox.Min.Y),
                            Math.Min(overallBBox.Min.Z, elementBBox.Min.Z)
                        );
                        overallBBox.Max = new XYZ(
                            Math.Max(overallBBox.Max.X, elementBBox.Max.X),
                            Math.Max(overallBBox.Max.Y, elementBBox.Max.Y),
                            Math.Max(overallBBox.Max.Z, elementBBox.Max.Z)
                        );
                    }
                }
            }

            if (overallBBox == null)
            {
                _logger.LogError("无法获取坝体边界框");
                return null;
            }
            
            // 计算边界框中心作为剖面原点
            var center = (overallBBox.Min + overallBBox.Max) / 2.0;
            
            // 根据剖面位置定义剖面方向 - 修正版本
            XYZ viewDirection;
            XYZ upDirection;
            
            // 根据剖面位置确定视图方向
            switch (location.Type?.ToLower())
            {
                case "longitudinal":
                case "纵剖面":
                    viewDirection = XYZ.BasisY; // 沿Y轴方向观看
                    upDirection = XYZ.BasisZ;   // 上方向为Z轴
                    break;
                case "transverse":
                case "横剖面":
                    viewDirection = XYZ.BasisX; // 沿X轴方向观看
                    upDirection = XYZ.BasisZ;   // 上方向为Z轴
                    break;
                case "vertical":
                case "竖直剖面":
                case "竖直":
                    viewDirection = XYZ.BasisZ; // 沿Z轴方向观看(竖直方向)
                    upDirection = XYZ.BasisY;   // 上方向为Y轴
                    break;
                default:
                    // 如果有特定的方向向量，使用它
                    if (location.Direction.IsValid())
                    {
                        viewDirection = new XYZ(location.Direction.X, location.Direction.Y, location.Direction.Z);
                        // 为竖直方向自动确定上方向
                        if (Math.Abs(viewDirection.Z) > 0.8) // 主要是Z方向
                        {
                            upDirection = XYZ.BasisY;
                        }
                        else
                        {
                            upDirection = XYZ.BasisZ;
                        }
                    }
                    else
                    {
                        // 默认竖直方向
                        viewDirection = XYZ.BasisZ;
                        upDirection = XYZ.BasisY;
                    }
                    break;
            }
            
            // 确保视图方向是单位向量
            viewDirection = viewDirection.Normalize();
            upDirection = upDirection.Normalize();
            
            // 计算右方向（叉积），确保右手坐标系
            var rightDirection = viewDirection.CrossProduct(upDirection).Normalize();
            
            // 重新计算上方向确保正交
            upDirection = rightDirection.CrossProduct(viewDirection).Normalize();
            
            // 创建变换矩阵
            var transform = Transform.Identity;
            transform.Origin = center;
            transform.BasisX = rightDirection;
            transform.BasisY = upDirection;
            transform.BasisZ = viewDirection;
            
            // 计算合适的剖面框尺寸 - 根据剖面类型调整
            double width, height, depth;
            
            if (location.Type?.ToLower() == "vertical" || location.Type?.ToLower() == "竖直剖面" || location.Type?.ToLower() == "竖直")
            {
                // 竖直剖面：X-Y平面，深度为Z方向
                width = overallBBox.Max.X - overallBBox.Min.X;
                height = overallBBox.Max.Y - overallBBox.Min.Y;
                depth = overallBBox.Max.Z - overallBBox.Min.Z;
            }
            else
            {
                // 其他剖面：保持原有逻辑
                width = overallBBox.Max.X - overallBBox.Min.X;
                height = overallBBox.Max.Z - overallBBox.Min.Z;
                depth = Math.Max(width, overallBBox.Max.Y - overallBBox.Min.Y);
            }
            
            // 添加一些边距确保完全包含坝体
            var margin = Math.Max(width, height) * 0.2;
            
            // 创建剖面边界框
            var sectionBox = new BoundingBoxXYZ();
            sectionBox.Transform = transform;
            sectionBox.Min = new XYZ(-width/2 - margin, -height/2 - margin, 0);
            sectionBox.Max = new XYZ(width/2 + margin, height/2 + margin, depth + margin);
            
            _logger.LogInformation("创建{SectionType}剖面边界框: 中心({X:F2}, {Y:F2}, {Z:F2}), 尺寸({W:F2} x {H:F2} x {D:F2}), 视图方向({VX:F2}, {VY:F2}, {VZ:F2})",
                location.Type, center.X, center.Y, center.Z, 
                width + 2*margin, height + 2*margin, depth + margin,
                viewDirection.X, viewDirection.Y, viewDirection.Z);
                
            return sectionBox;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建剖面边界框时发生错误");
            return null;
        }
    }
    
    /// <summary>
    /// 创建剖面视图
    /// </summary>
    private async Task<ViewSection> CreateSectionViewAsync(Document doc, BoundingBoxXYZ sectionBox, string sectionName)
    {
        return await Task.Run(() =>
        {
            try
            {
                // 查找剖面视图族类型
                var collector = new FilteredElementCollector(doc);
                collector.OfClass(typeof(ViewFamilyType));
                var viewFamilyType = collector
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(vft => vft.ViewFamily == ViewFamily.Section);
                
                if (viewFamilyType == null)
                {
                    _logger.LogError("未找到剖面视图族类型");
                    return null;
                }
                
                // 在事务中创建剖面视图
                ViewSection sectionView = null;
                using (var trans = new Transaction(doc, $"创建临时剖面视图: {sectionName}"))
                {
                    trans.Start();
                    
                    try
                    {
                        sectionView = ViewSection.CreateSection(doc, viewFamilyType.Id, sectionBox);
                        sectionView.Name = $"TempSection_{DateTime.Now:HHmmss}_{sectionName}";
                        
                        // 设置视图的详细程度为精细，确保获取完整几何
                        sectionView.DetailLevel = ViewDetailLevel.Fine;
                        
                        trans.Commit();
                        _logger.LogInformation("成功创建剖面视图: {ViewName}", sectionView.Name);
                    }
                    catch (Exception ex)
                    {
                        trans.RollBack();
                        _logger.LogError(ex, "创建剖面视图时发生错误");
                        return null;
                    }
                }
                
                return sectionView;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建剖面视图异步任务失败");
                return null;
            }
        });
    }
    
    /// <summary>
    /// 从剖面视图中提取几何轮廓
    /// </summary>
    private List<CurveLoop> ExtractGeometryFromSectionView(List<Element> damElements, ViewSection sectionView)
    {
        var sectionProfiles = new List<CurveLoop>();
        var sectionViewDirection = sectionView.ViewDirection;
        
        try
        {
            // 配置几何提取选项
            var geoOptions = new Options();
            geoOptions.View = sectionView; // 关键：指定从剖面视图提取几何
            geoOptions.DetailLevel = ViewDetailLevel.Fine;
            geoOptions.ComputeReferences = true;
            
            foreach (var element in damElements)
            {
                try
                {
                    // 从指定视图获取几何
                    var geomElement = element.get_Geometry(geoOptions);
                    if (geomElement == null) continue;
                    
                    foreach (var geoObject in geomElement)
                    {
                        if (geoObject is Solid solid && solid.Faces.Size > 0)
                        {
                            // 遍历实体的所有面
                            foreach (Face face in solid.Faces)
                            {
                                if (face is PlanarFace planarFace)
                                {
                                    // 检查面的法线是否与剖面方向平行
                                    var normalDot = Math.Abs(planarFace.FaceNormal.DotProduct(sectionViewDirection));
                                    if (normalDot > 0.99) // 几乎平行（考虑数值误差）
                                    {
                                        // 这个面就是剖切面，获取其边界
                                        var curveLoops = face.GetEdgesAsCurveLoops();
                                        foreach (var curveLoop in curveLoops)
                                        {
                                            if (curveLoop != null && curveLoop.Count() > 0)
                                            {
                                                sectionProfiles.Add(curveLoop);
                                                _logger.LogDebug("提取到剖面轮廓，包含 {CurveCount} 条曲线", curveLoop.Count());
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (geoObject is GeometryInstance geoInstance)
                        {
                            // 处理几何实例（如族实例）
                            var instanceGeometry = geoInstance.GetInstanceGeometry();
                            // 递归处理几何实例...
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "从元素 {ElementId} 提取几何时发生错误", element.Id);
                    // 继续处理下一个元素
                }
            }
            
            _logger.LogInformation("从剖面视图提取到 {ProfileCount} 个轮廓", sectionProfiles.Count);
            return sectionProfiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从剖面视图提取几何时发生错误");
            return sectionProfiles;
        }
    }
    
    /// <summary>
    /// 处理剖面几何，转换为EnhancedProfile2D
    /// </summary>
    private EnhancedProfile2D ProcessSectionGeometry(List<CurveLoop> sectionProfiles, SectionLocation location, ViewSection sectionView)
    {
        try
        {
            var profile = new EnhancedProfile2D
            {
                Name = location.Name ?? "未命名剖面",
                CreatedAt = DateTime.Now,
                Status = Core.Entities.ValidationStatus.Pending
            };
            
            // 设置剖面坐标系信息
            var sectionPlane = Plane.CreateByNormalAndOrigin(sectionView.ViewDirection, sectionView.Origin);
            profile.SectionNormal = new Vector3D(sectionPlane.Normal.X, sectionPlane.Normal.Y, sectionPlane.Normal.Z);
            profile.SectionOrigin = new Point3D(sectionPlane.Origin.X, sectionPlane.Origin.Y, sectionPlane.Origin.Z);
            profile.LocalXAxis = new Vector3D(sectionPlane.XVec.X, sectionPlane.XVec.Y, sectionPlane.XVec.Z);
            profile.LocalYAxis = new Vector3D(sectionPlane.YVec.X, sectionPlane.YVec.Y, sectionPlane.YVec.Z);
            
            if (sectionProfiles.Any())
            {
                // 找到最大的轮廓作为主轮廓（通常是坝体外轮廓）
                var mainCurveLoop = sectionProfiles
                    .OrderByDescending(loop => CalculateCurveLoopArea(loop))
                    .First();
                
                // 转换为Point2D列表
                profile.MainContour = ConvertCurveLoopToPoint2D(mainCurveLoop, sectionPlane);
                
                // 处理内部轮廓（如果有）
                foreach (var innerLoop in sectionProfiles.Skip(1))
                {
                    var innerContour = ConvertCurveLoopToPoint2D(innerLoop, sectionPlane);
                    if (innerContour.Any())
                    {
                        profile.InnerContours.Add(innerContour);
                    }
                }
                
                _logger.LogInformation("处理剖面几何完成，主轮廓包含 {PointCount} 个点", profile.MainContour.Count);
            }
            else
            {
                _logger.LogWarning("未找到有效的剖面轮廓");
                profile.MainContour = new List<Core.Entities.Point2D>();
            }
            
            // 初始化其他属性
            profile.MaterialZones = new List<MaterialZone>();
            profile.BoundaryConditions = new BoundaryConditions();
            profile.FeaturePoints = new Dictionary<string, Core.Entities.Point2D>();
            profile.Features = new Dictionary<string, object>();
            profile.FoundationContour = new List<Core.Entities.Point2D>();
            
            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理剖面几何时发生错误");
            return new EnhancedProfile2D { Name = location.Name ?? "错误剖面" };
        }
    }
    
    /// <summary>
    /// 将CurveLoop转换为Point2D列表
    /// </summary>
    private List<Core.Entities.Point2D> ConvertCurveLoopToPoint2D(CurveLoop curveLoop, Plane sectionPlane)
    {
        var points = new List<Core.Entities.Point2D>();
        
        try
        {
            foreach (var curve in curveLoop)
            {
                // 获取曲线的起点和终点
                var startPoint = curve.GetEndPoint(0);
                var endPoint = curve.GetEndPoint(1);
                
                // 将3D点投影到剖面平面的2D坐标系
                var localStart = ProjectToPlane(startPoint, sectionPlane);
                var localEnd = ProjectToPlane(endPoint, sectionPlane);
                
                // 添加起点（避免重复点）
                if (!points.Any() || !IsPointEqual(points.Last(), localStart))
                {
                    points.Add(localStart);
                }
                
                // 如果是非直线（如圆弧），可能需要添加中间点
                if (curve is Arc arc)
                {
                    // 对于圆弧，添加几个中间点以保持精度
                    var parameterStep = 1.0 / 10.0; // 将圆弧分成10段
                    for (int i = 1; i < 10; i++)
                    {
                        var parameter = i * parameterStep;
                        var pointOnArc = arc.Evaluate(parameter, false);
                        var localPoint = ProjectToPlane(pointOnArc, sectionPlane);
                        points.Add(localPoint);
                    }
                }
            }
            
            // 确保轮廓闭合
            if (points.Count > 2 && !IsPointEqual(points.First(), points.Last()))
            {
                points.Add(points.First());
            }
            
            return points;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换CurveLoop到Point2D时发生错误");
            return new List<Core.Entities.Point2D>();
        }
    }
    
    /// <summary>
    /// 将3D点投影到剖面平面的2D坐标系
    /// </summary>
    private Core.Entities.Point2D ProjectToPlane(XYZ point3D, Plane plane)
    {
        // 计算点相对于平面原点的向量
        var relativeVector = point3D - plane.Origin;
        
        // 投影到平面的X和Y轴
        var x = relativeVector.DotProduct(plane.XVec);
        var y = relativeVector.DotProduct(plane.YVec);
        
        return new Core.Entities.Point2D(x, y);
    }
    
    /// <summary>
    /// 计算CurveLoop的面积（用于排序）
    /// </summary>
    private double CalculateCurveLoopArea(CurveLoop curveLoop)
    {
        try
        {
            // 简单的近似面积计算（基于边界框）
            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minY = double.MaxValue;
            var maxY = double.MinValue;
            
            foreach (var curve in curveLoop)
            {
                var start = curve.GetEndPoint(0);
                var end = curve.GetEndPoint(1);
                
                minX = Math.Min(minX, Math.Min(start.X, end.X));
                maxX = Math.Max(maxX, Math.Max(start.X, end.X));
                minY = Math.Min(minY, Math.Min(start.Y, end.Y));
                maxY = Math.Max(maxY, Math.Max(start.Y, end.Y));
            }
            
            return (maxX - minX) * (maxY - minY);
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>
    /// 判断两个点是否相等（在容差范围内）
    /// </summary>
    private bool IsPointEqual(Core.Entities.Point2D p1, Core.Entities.Point2D p2, double tolerance = 1e-6)
    {
        return Math.Abs(p1.X - p2.X) < tolerance && Math.Abs(p1.Y - p2.Y) < tolerance;
    }
    
    /// <summary>
    /// 清理临时剖面视图
    /// </summary>
    private async Task CleanupTemporarySectionView(Document doc, ViewSection sectionView)
    {
        await Task.Run(() =>
        {
            try
            {
                using (var trans = new Transaction(doc, "删除临时剖面视图"))
                {
                    trans.Start();
                    
                    doc.Delete(sectionView.Id);
                    
                    trans.Commit();
                    _logger.LogInformation("成功删除临时剖面视图: {ViewName}", sectionView.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "删除临时剖面视图时发生错误，视图可能需要手动删除");
            }
        });
    }
} 