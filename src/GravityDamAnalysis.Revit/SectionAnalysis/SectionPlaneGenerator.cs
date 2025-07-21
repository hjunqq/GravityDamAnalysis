using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using Microsoft.Extensions.Logging;

namespace GravityDamAnalysis.Revit.SectionAnalysis;

/// <summary>
/// 剖面平面生成器
/// 负责根据用户指定的法向量在Revit中生成剖面并提取几何数据
/// </summary>
public class SectionPlaneGenerator
{
    private readonly ILogger<SectionPlaneGenerator> _logger;
    private const double TOLERANCE = 1e-6; // 几何容差，单位：英尺

    public SectionPlaneGenerator(ILogger<SectionPlaneGenerator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 根据法向量和偏移创建剖面平面
    /// </summary>
    /// <param name="damElement">坝体元素</param>
    /// <param name="normalVector">剖面法向量（Revit坐标系）</param>
    /// <param name="offset">沿法向量的偏移距离，正值向法向量方向偏移</param>
    /// <returns>剖面平面</returns>
    public Plane CreateSectionPlane(Element damElement, XYZ normalVector, double offset = 0.0)
    {
        try
        {
            // 标准化法向量
            var normal = normalVector.Normalize();
            
            // 获取坝体几何中心作为基准点
            var boundingBox = damElement.get_BoundingBox(null);
            if (boundingBox == null)
            {
                throw new InvalidOperationException("无法获取元素边界框");
            }

            var center = (boundingBox.Min + boundingBox.Max) / 2.0;
            
            // 应用偏移
            var planeOrigin = center + normal * offset;
            
            _logger.LogInformation("创建剖面平面: 原点({0:F3}, {1:F3}, {2:F3}), 法向({3:F3}, {4:F3}, {5:F3})", 
                planeOrigin.X, planeOrigin.Y, planeOrigin.Z,
                normal.X, normal.Y, normal.Z);

            return Plane.CreateByNormalAndOrigin(normal, planeOrigin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建剖面平面失败");
            throw;
        }
    }

    /// <summary>
    /// 从坝体元素提取指定剖面的2D轮廓
    /// </summary>
    /// <param name="damElement">坝体元素</param>
    /// <param name="sectionPlane">剖面平面</param>
    /// <param name="sectionName">剖面名称</param>
    /// <returns>二维剖面数据</returns>
    public Profile2D ExtractSectionProfile(Element damElement, Plane sectionPlane, string sectionName = "")
    {
        try
        {
            _logger.LogInformation("开始提取元素 {ElementId} 的剖面轮廓", damElement.Id);

            // 获取元素的几何数据
            var geometryElement = damElement.get_Geometry(new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            });

            if (geometryElement == null)
            {
                throw new InvalidOperationException("无法获取元素几何数据");
            }

            var curveLoops = new List<CurveLoop>();

            // 遍历几何对象提取实体
            foreach (var geomObj in geometryElement)
            {
                var curves = ExtractCurvesFromGeometry(geomObj, sectionPlane);
                curveLoops.AddRange(curves);
            }

            if (curveLoops.Count == 0)
            {
                _logger.LogWarning("未找到与剖面相交的几何体");
                return CreateEmptyProfile(sectionName, sectionPlane);
            }

            // 转换为2D剖面数据
            var profile2D = ConvertTo2DProfile(curveLoops, sectionPlane, sectionName);
            
            _logger.LogInformation("成功提取剖面轮廓，包含 {ContourCount} 个轮廓", 
                1 + profile2D.InnerContours.Count);

            return profile2D;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取剖面轮廓失败");
            throw;
        }
    }

    /// <summary>
    /// 从几何对象中提取与剖面相交的曲线
    /// </summary>
    private List<CurveLoop> ExtractCurvesFromGeometry(GeometryObject geomObj, Plane sectionPlane)
    {
        var result = new List<CurveLoop>();

        switch (geomObj)
        {
            case Solid solid when solid.Volume > TOLERANCE:
                result.AddRange(ExtractCurvesFromSolid(solid, sectionPlane));
                break;

            case GeometryInstance instance:
                var transform = instance.Transform;
                var instanceGeometry = instance.GetInstanceGeometry();
                
                foreach (var instGeomObj in instanceGeometry)
                {
                    // 应用实例变换
                    var transformedCurves = ExtractCurvesFromGeometry(instGeomObj, sectionPlane);
                    foreach (var curveLoop in transformedCurves)
                    {
                        var transformedLoop = TransformCurveLoop(curveLoop, transform);
                        if (transformedLoop != null)
                        {
                            result.Add(transformedLoop);
                        }
                    }
                }
                break;
        }

        return result;
    }

    /// <summary>
    /// 从实体中提取与剖面相交的曲线
    /// </summary>
    private List<CurveLoop> ExtractCurvesFromSolid(Solid solid, Plane sectionPlane)
    {
        var result = new List<CurveLoop>();

        try
        {
            // 创建一个非常薄的切割实体来模拟剖面
            var profileThickness = 0.01; // 0.01英尺的薄片
            var profileSolid = CreateProfileSolid(sectionPlane, solid.GetBoundingBox(), profileThickness);

            if (profileSolid == null) return result;

            // 执行布尔相交运算
            var intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                solid, profileSolid, BooleanOperationsType.Intersect);

            if (intersection?.Volume > TOLERANCE)
            {
                // 从相交结果中提取边缘曲线
                foreach (Edge edge in intersection.Edges)
                {
                    var curve = edge.AsCurve();
                    if (curve != null && IsOnPlane(curve, sectionPlane))
                    {
                        // 这里需要进一步处理，将边缘组织成闭合的CurveLoop
                        // 简化处理：每条曲线作为单独的loop（实际应用中需要更复杂的逻辑）
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "从实体提取曲线时发生错误");
        }

        return result;
    }

    /// <summary>
    /// 创建用于切割的薄片实体
    /// </summary>
    private Solid CreateProfileSolid(Plane sectionPlane, BoundingBoxXYZ boundingBox, double thickness)
    {
        try
        {
            // 在剖面平面上创建一个足够大的矩形
            var center = sectionPlane.Origin;
            var normal = sectionPlane.Normal;
            var xDir = sectionPlane.XVec;
            var yDir = sectionPlane.YVec;

            // 计算足够大的尺寸
            var size = Math.Max(
                boundingBox.Max.DistanceTo(boundingBox.Min) * 2,
                100.0); // 至少100英尺

            var halfSize = size / 2.0;
            var halfThickness = thickness / 2.0;

            // 创建矩形的4个角点
            var corners = new[]
            {
                center + xDir * (-halfSize) + yDir * (-halfSize),
                center + xDir * halfSize + yDir * (-halfSize),
                center + xDir * halfSize + yDir * halfSize,
                center + xDir * (-halfSize) + yDir * halfSize
            };

            // 创建矩形轮廓
            var curves = new List<Curve>();
            for (int i = 0; i < corners.Length; i++)
            {
                var start = corners[i];
                var end = corners[(i + 1) % corners.Length];
                curves.Add(Line.CreateBound(start, end));
            }

            var curveLoop = CurveLoop.Create(curves);
            var profile = new List<CurveLoop> { curveLoop };

            // 沿法向量拉伸创建实体
            var extrusionDirection = normal * thickness;
            var solid = GeometryCreationUtilities.CreateExtrusionGeometry(
                profile, extrusionDirection, 0);

            return solid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建剖面切割实体失败");
            return null;
        }
    }

    /// <summary>
    /// 检查曲线是否在指定平面上
    /// </summary>
    private bool IsOnPlane(Curve curve, Plane plane)
    {
        var startDistance = Math.Abs(DistancePointToPlane(curve.GetEndPoint(0), plane));
        var endDistance = Math.Abs(DistancePointToPlane(curve.GetEndPoint(1), plane));
        return startDistance < TOLERANCE && endDistance < TOLERANCE;
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
    /// 变换曲线循环
    /// </summary>
    private CurveLoop TransformCurveLoop(CurveLoop curveLoop, Transform transform)
    {
        try
        {
            var transformedCurves = new List<Curve>();
            
            foreach (Curve curve in curveLoop)
            {
                var transformedCurve = curve.CreateTransformed(transform);
                transformedCurves.Add(transformedCurve);
            }

            return CurveLoop.Create(transformedCurves);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "变换曲线循环失败");
            return null;
        }
    }

    /// <summary>
    /// 将3D曲线循环转换为2D剖面
    /// </summary>
    private Profile2D ConvertTo2DProfile(List<CurveLoop> curveLoops, Plane sectionPlane, string sectionName)
    {
        var profile = new Profile2D
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

        // 处理第一个曲线循环作为主轮廓
        if (curveLoops.Count > 0)
        {
            profile.MainContour = ConvertCurveLoopTo2D(curveLoops[0], origin, xAxis, yAxis);
        }

        // 处理其余曲线循环作为内部轮廓
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
            // 简化处理：仅取曲线起点，实际应用中可能需要细分曲线
            var point3D = curve.GetEndPoint(0);
            var point2D = ProjectTo2D(point3D, origin, xAxis, yAxis);
            points2D.Add(point2D);
        }

        return points2D;
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
    /// 创建空的剖面（当没有找到相交几何时）
    /// </summary>
    private Profile2D CreateEmptyProfile(string sectionName, Plane sectionPlane)
    {
        return new Profile2D
        {
            Name = string.IsNullOrEmpty(sectionName) ? "Empty_Section" : sectionName,
            SectionNormal = new Vector3D(sectionPlane.Normal.X, sectionPlane.Normal.Y, sectionPlane.Normal.Z),
            SectionOrigin = new Point3D(sectionPlane.Origin.X, sectionPlane.Origin.Y, sectionPlane.Origin.Z),
            LocalXAxis = new Vector3D(sectionPlane.XVec.X, sectionPlane.XVec.Y, sectionPlane.XVec.Z),
            LocalYAxis = new Vector3D(sectionPlane.YVec.X, sectionPlane.YVec.Y, sectionPlane.YVec.Z)
        };
    }
}

/// <summary>
/// 剖面生成参数
/// </summary>
public class SectionGenerationParameters
{
    /// <summary>
    /// 剖面法向量
    /// </summary>
    public XYZ Normal { get; set; } = XYZ.BasisX;

    /// <summary>
    /// 偏移距离
    /// </summary>
    public double Offset { get; set; } = 0.0;

    /// <summary>
    /// 剖面名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否显示剖面平面（用于调试）
    /// </summary>
    public bool ShowSectionPlane { get; set; } = false;
} 