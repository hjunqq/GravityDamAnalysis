namespace GravityDamAnalysis.Calculation.Models;

/// <summary>
/// 稳定性分析参数
/// </summary>
public class AnalysisParameters
{
    /// <summary>
    /// 上游水位 (m)
    /// </summary>
    public double UpstreamWaterLevel { get; set; } = 100.0;
    
    /// <summary>
    /// 下游水位 (m)
    /// </summary>
    public double DownstreamWaterLevel { get; set; } = 10.0;
    
    /// <summary>
    /// 水的重度 (kN/m³)
    /// </summary>
    public double WaterDensity { get; set; } = 9.8;
    
    /// <summary>
    /// 地震系数
    /// </summary>
    public double SeismicCoefficient { get; set; } = 0.0;
    
    /// <summary>
    /// 基础摩擦系数
    /// </summary>
    public double FrictionCoefficient { get; set; } = 0.75;
    
    /// <summary>
    /// 抗滑安全系数标准值
    /// </summary>
    public double RequiredSlidingSafetyFactor { get; set; } = 3.0;
    
    /// <summary>
    /// 抗倾覆安全系数标准值
    /// </summary>
    public double RequiredOverturnSafetyFactor { get; set; } = 1.5;
    
    /// <summary>
    /// 是否考虑扬压力
    /// </summary>
    public bool ConsiderUpliftPressure { get; set; } = true;
    
    /// <summary>
    /// 扬压力折减系数
    /// </summary>
    public double UpliftReductionFactor { get; set; } = 0.8;
    
    /// <summary>
    /// 分析类型
    /// </summary>
    public AnalysisType AnalysisType { get; set; } = AnalysisType.Static;
    
    /// <summary>
    /// 计算精度
    /// </summary>
    public double CalculationPrecision { get; set; } = 0.001;
    
    /// <summary>
    /// 最大迭代次数
    /// </summary>
    public int MaxIterations { get; set; } = 1000;
}

/// <summary>
/// 分析类型枚举
/// </summary>
public enum AnalysisType
{
    /// <summary>
    /// 静力分析
    /// </summary>
    Static,
    
    /// <summary>
    /// 动力分析（地震）
    /// </summary>
    Dynamic,
    
    /// <summary>
    /// 渗流分析
    /// </summary>
    Seepage
} 