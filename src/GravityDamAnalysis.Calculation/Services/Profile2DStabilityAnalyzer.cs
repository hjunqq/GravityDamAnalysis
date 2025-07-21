using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Calculation.Models;

namespace GravityDamAnalysis.Calculation.Services;

/// <summary>
/// 基于二维剖面的稳定性分析器
/// 针对从Revit提取的2D剖面进行重力坝稳定性计算
/// </summary>
public class Profile2DStabilityAnalyzer
{
    private readonly ILogger<Profile2DStabilityAnalyzer> _logger;
    
    public Profile2DStabilityAnalyzer(ILogger<Profile2DStabilityAnalyzer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 分析二维剖面的稳定性
    /// </summary>
    /// <param name="profile">二维剖面数据</param>
    /// <param name="parameters">分析参数</param>
    /// <param name="materialProperties">材料属性</param>
    /// <returns>稳定性分析结果</returns>
    public Profile2DAnalysisResult AnalyzeProfile2DStability(
        Profile2D profile, 
        AnalysisParameters parameters,
        MaterialProperties materialProperties)
    {
        try
        {
            _logger.LogInformation("开始分析二维剖面稳定性: {ProfileName}", profile.Name);

            var result = new Profile2DAnalysisResult
            {
                ProfileId = profile.Id,
                ProfileName = profile.Name,
                AnalysisDate = DateTime.Now,
                Parameters = parameters
            };

            // 1. 计算剖面几何属性
            var geometryProps = CalculateProfile2DGeometry(profile);
            result.GeometryProperties = geometryProps;

            _logger.LogInformation("剖面几何属性: 面积={Area:F2} m², 形心=({CentroidX:F2}, {CentroidY:F2})", 
                geometryProps.Area, geometryProps.Centroid.X, geometryProps.Centroid.Y);

            // 2. 计算各项荷载（单位宽度）
            var loads = CalculateLoads2D(geometryProps, parameters, materialProperties);
            result.LoadAnalysis = loads;

            // 3. 计算抗滑稳定安全系数
            result.SlidingSafetyFactor = CalculateSlidingStability2D(loads, materialProperties);

            // 4. 计算抗倾覆稳定安全系数  
            result.OverturnSafetyFactor = CalculateOverturnStability2D(geometryProps, loads);

            // 5. 判断稳定性是否满足要求
            result.IsSlidingStable = result.SlidingSafetyFactor >= parameters.RequiredSlidingSafetyFactor;
            result.IsOverturnStable = result.OverturnSafetyFactor >= parameters.RequiredOverturnSafetyFactor;
            result.IsOverallStable = result.IsSlidingStable && result.IsOverturnStable;

            _logger.LogInformation("稳定性分析完成: 抗滑系数={SlidingFactor:F3}, 抗倾覆系数={OverturnFactor:F3}", 
                result.SlidingSafetyFactor, result.OverturnSafetyFactor);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "二维剖面稳定性分析失败");
            throw;
        }
    }

    /// <summary>
    /// 计算剖面几何属性
    /// </summary>
    private Profile2DGeometryProperties CalculateProfile2DGeometry(Profile2D profile)
    {
        if (!profile.IsValid())
        {
            throw new ArgumentException("无效的剖面数据", nameof(profile));
        }

        var props = new Profile2DGeometryProperties();

        // 基本几何属性
        props.Area = profile.CalculateArea();
        props.Centroid = profile.CalculateCentroid();
        
        var boundingBox = profile.GetBoundingBox();
        props.Width = boundingBox.Width;
        props.Height = boundingBox.Height;
        props.BaseWidth = CalculateBaseWidth(profile.MainContour);
        props.TopWidth = CalculateTopWidth(profile.MainContour);

        // 二阶矩和惯性矩
        var (Ixx, Iyy, Ixy) = CalculateSecondMoments(profile.MainContour);
        props.MomentOfInertiaX = Ixx;
        props.MomentOfInertiaY = Iyy;
        props.ProductOfInertia = Ixy;

        return props;
    }

    /// <summary>
    /// 计算坝底宽度
    /// </summary>
    private double CalculateBaseWidth(List<Point2D> contour)
    {
        if (contour.Count < 3) return 0.0;

        // 找到Y坐标最小的点（坝底）
        var minY = contour.Min(p => p.Y);
        var basePoints = contour.Where(p => Math.Abs(p.Y - minY) < 1e-6).ToList();

        if (basePoints.Count < 2) return 0.0;

        var minX = basePoints.Min(p => p.X);
        var maxX = basePoints.Max(p => p.X);
        
        return maxX - minX;
    }

    /// <summary>
    /// 计算坝顶宽度
    /// </summary>
    private double CalculateTopWidth(List<Point2D> contour)
    {
        if (contour.Count < 3) return 0.0;

        // 找到Y坐标最大的点（坝顶）
        var maxY = contour.Max(p => p.Y);
        var topPoints = contour.Where(p => Math.Abs(p.Y - maxY) < 1e-6).ToList();

        if (topPoints.Count < 2) return 0.0;

        var minX = topPoints.Min(p => p.X);
        var maxX = topPoints.Max(p => p.X);
        
        return maxX - minX;
    }

    /// <summary>
    /// 计算二阶矩
    /// </summary>
    private (double Ixx, double Iyy, double Ixy) CalculateSecondMoments(List<Point2D> polygon)
    {
        if (polygon.Count < 3) return (0, 0, 0);

        double Ixx = 0, Iyy = 0, Ixy = 0;

        for (int i = 0; i < polygon.Count; i++)
        {
            var current = polygon[i];
            var next = polygon[(i + 1) % polygon.Count];

            double xi = current.X, yi = current.Y;
            double xi1 = next.X, yi1 = next.Y;

            double crossProduct = xi * yi1 - xi1 * yi;

            Ixx += (yi * yi + yi * yi1 + yi1 * yi1) * crossProduct;
            Iyy += (xi * xi + xi * xi1 + xi1 * xi1) * crossProduct;
            Ixy += (xi * yi1 + 2 * xi * yi + 2 * xi1 * yi1 + xi1 * yi) * crossProduct;
        }

        Ixx /= 12.0;
        Iyy /= 12.0;
        Ixy /= 24.0;

        return (Math.Abs(Ixx), Math.Abs(Iyy), Math.Abs(Ixy));
    }

    /// <summary>
    /// 计算二维荷载（单位坝轴长度）
    /// </summary>
    private Profile2DLoadAnalysis CalculateLoads2D(
        Profile2DGeometryProperties geometry, 
        AnalysisParameters parameters,
        MaterialProperties materialProperties)
    {
        var loads = new Profile2DLoadAnalysis();

        // 自重（kN/m）
        loads.SelfWeight = geometry.Area * materialProperties.Density;

        // 净水压力（kN/m）
        loads.NetWaterPressure = CalculateNetWaterPressure2D(parameters);

        // 扬压力（kN/m）
        loads.UpliftForce = CalculateUpliftForce2D(geometry, parameters);

        // 地震惯性力（kN/m）
        loads.SeismicForce = parameters.SeismicCoefficient * loads.SelfWeight;

        // 有效法向力
        loads.EffectiveNormalForce = loads.SelfWeight - loads.UpliftForce;

        _logger.LogInformation("荷载计算: 自重={SelfWeight:F1}, 水压={WaterPressure:F1}, 扬压={UpliftForce:F1}, 地震={SeismicForce:F1} kN/m",
            loads.SelfWeight, loads.NetWaterPressure, loads.UpliftForce, loads.SeismicForce);

        return loads;
    }

    /// <summary>
    /// 计算净水压力（二维，单位宽度）
    /// </summary>
    private double CalculateNetWaterPressure2D(AnalysisParameters parameters)
    {
        var upstreamHead = parameters.UpstreamWaterLevel;
        var downstreamHead = parameters.DownstreamWaterLevel;

        // 上游水压力合力 P1 = 0.5 * γw * H1² (kN/m)
        var upstreamPressure = 0.5 * parameters.WaterDensity * upstreamHead * upstreamHead;

        // 下游水压力合力 P2 = 0.5 * γw * H2² (kN/m)
        var downstreamPressure = 0.5 * parameters.WaterDensity * downstreamHead * downstreamHead;

        return Math.Max(0, upstreamPressure - downstreamPressure);
    }

    /// <summary>
    /// 计算扬压力（二维，单位宽度）
    /// </summary>
    private double CalculateUpliftForce2D(Profile2DGeometryProperties geometry, AnalysisParameters parameters)
    {
        // 平均扬压力水头
        var averageHead = (parameters.UpstreamWaterLevel + parameters.DownstreamWaterLevel) / 2.0;
        
        // 扬压力 = 折减系数 × 平均水头 × 底宽 × 水重度 (kN/m)
        return parameters.UpliftReductionFactor * averageHead * geometry.BaseWidth * parameters.WaterDensity;
    }

    /// <summary>
    /// 计算抗滑稳定安全系数（二维）
    /// </summary>
    private double CalculateSlidingStability2D(Profile2DLoadAnalysis loads, MaterialProperties materialProperties)
    {
        // 滑动力：水平水压力 + 地震惯性力 (kN/m)
        var slidingForce = loads.NetWaterPressure + loads.SeismicForce;

        // 抗滑力：摩擦系数 × 有效法向力 (kN/m)
        var resistingForce = materialProperties.FrictionCoefficient * loads.EffectiveNormalForce;

        // 安全系数
        if (slidingForce <= 0) return double.MaxValue;
        if (loads.EffectiveNormalForce <= 0)
        {
            _logger.LogWarning("有效法向力为负值或零，扬压力过大或设计不合理");
            return 0.0;
        }

        return resistingForce / slidingForce;
    }

    /// <summary>
    /// 计算抗倾覆稳定安全系数（二维）
    /// </summary>
    private double CalculateOverturnStability2D(Profile2DGeometryProperties geometry, Profile2DLoadAnalysis loads)
    {
        // 抗倾覆力矩：自重对坝趾的力矩 (kN⋅m/m)
        var resistingMoment = loads.SelfWeight * geometry.Centroid.X;

        // 倾覆力矩：水压力对坝趾的力矩（简化为作用在坝高1/3处）
        var waterPressureHeight = geometry.Height / 3.0; // 水压力作用点高度
        var overturnMoment = loads.NetWaterPressure * waterPressureHeight;

        // 加上地震惯性力的倾覆力矩（作用在形心高度）
        overturnMoment += loads.SeismicForce * geometry.Centroid.Y;

        // 安全系数
        return overturnMoment > 0 ? resistingMoment / overturnMoment : double.MaxValue;
    }
}

/// <summary>
/// 二维剖面几何属性
/// </summary>
public class Profile2DGeometryProperties
{
    /// <summary>
    /// 剖面面积 (m²)
    /// </summary>
    public double Area { get; set; }

    /// <summary>
    /// 形心坐标
    /// </summary>
    public Point2D Centroid { get; set; }

    /// <summary>
    /// 剖面宽度 (m)
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// 剖面高度 (m)
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// 底部宽度 (m)
    /// </summary>
    public double BaseWidth { get; set; }

    /// <summary>
    /// 顶部宽度 (m)
    /// </summary>
    public double TopWidth { get; set; }

    /// <summary>
    /// 对X轴的惯性矩 (m⁴)
    /// </summary>
    public double MomentOfInertiaX { get; set; }

    /// <summary>
    /// 对Y轴的惯性矩 (m⁴)
    /// </summary>
    public double MomentOfInertiaY { get; set; }

    /// <summary>
    /// 惯性积 (m⁴)
    /// </summary>
    public double ProductOfInertia { get; set; }
}

/// <summary>
/// 二维荷载分析结果
/// </summary>
public class Profile2DLoadAnalysis
{
    /// <summary>
    /// 自重 (kN/m)
    /// </summary>
    public double SelfWeight { get; set; }

    /// <summary>
    /// 净水压力 (kN/m)
    /// </summary>
    public double NetWaterPressure { get; set; }

    /// <summary>
    /// 扬压力 (kN/m)
    /// </summary>
    public double UpliftForce { get; set; }

    /// <summary>
    /// 地震惯性力 (kN/m)
    /// </summary>
    public double SeismicForce { get; set; }

    /// <summary>
    /// 有效法向力 (kN/m)
    /// </summary>
    public double EffectiveNormalForce { get; set; }
}

/// <summary>
/// 二维剖面分析结果
/// </summary>
public class Profile2DAnalysisResult
{
    /// <summary>
    /// 剖面ID
    /// </summary>
    public Guid ProfileId { get; set; }

    /// <summary>
    /// 剖面名称
    /// </summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>
    /// 分析日期
    /// </summary>
    public DateTime AnalysisDate { get; set; }

    /// <summary>
    /// 分析参数
    /// </summary>
    public AnalysisParameters Parameters { get; set; } = new();

    /// <summary>
    /// 几何属性
    /// </summary>
    public Profile2DGeometryProperties GeometryProperties { get; set; } = new();

    /// <summary>
    /// 荷载分析
    /// </summary>
    public Profile2DLoadAnalysis LoadAnalysis { get; set; } = new();

    /// <summary>
    /// 抗滑稳定安全系数
    /// </summary>
    public double SlidingSafetyFactor { get; set; }

    /// <summary>
    /// 抗倾覆稳定安全系数
    /// </summary>
    public double OverturnSafetyFactor { get; set; }

    /// <summary>
    /// 抗滑稳定是否满足要求
    /// </summary>
    public bool IsSlidingStable { get; set; }

    /// <summary>
    /// 抗倾覆稳定是否满足要求
    /// </summary>
    public bool IsOverturnStable { get; set; }

    /// <summary>
    /// 整体稳定性是否满足要求
    /// </summary>
    public bool IsOverallStable { get; set; }

    /// <summary>
    /// 生成分析报告
    /// </summary>
    public string GenerateReport()
    {
        var report = $"二维剖面稳定性分析报告\n";
        report += $"========================\n";
        report += $"剖面名称: {ProfileName}\n";
        report += $"分析时间: {AnalysisDate:yyyy-MM-dd HH:mm:ss}\n\n";
        
        report += $"几何属性:\n";
        report += $"- 面积: {GeometryProperties.Area:F2} m²\n";
        report += $"- 高度: {GeometryProperties.Height:F2} m\n";
        report += $"- 底宽: {GeometryProperties.BaseWidth:F2} m\n";
        report += $"- 顶宽: {GeometryProperties.TopWidth:F2} m\n\n";
        
        report += $"荷载分析:\n";
        report += $"- 自重: {LoadAnalysis.SelfWeight:F1} kN/m\n";
        report += $"- 净水压力: {LoadAnalysis.NetWaterPressure:F1} kN/m\n";
        report += $"- 扬压力: {LoadAnalysis.UpliftForce:F1} kN/m\n";
        report += $"- 地震力: {LoadAnalysis.SeismicForce:F1} kN/m\n\n";
        
        report += $"稳定性分析:\n";
        report += $"- 抗滑安全系数: {SlidingSafetyFactor:F3} {(IsSlidingStable ? "✓" : "✗")}\n";
        report += $"- 抗倾覆安全系数: {OverturnSafetyFactor:F3} {(IsOverturnStable ? "✓" : "✗")}\n";
        report += $"- 整体稳定性: {(IsOverallStable ? "满足要求 ✓" : "不满足要求 ✗")}\n";
        
        return report;
    }
} 