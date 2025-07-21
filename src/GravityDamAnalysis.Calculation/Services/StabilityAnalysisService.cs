using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Calculation.Models;

namespace GravityDamAnalysis.Calculation.Services;

/// <summary>
/// 稳定性分析服务实现
/// </summary>
public class StabilityAnalysisService : IStabilityAnalysisService
{
    private readonly ILogger<StabilityAnalysisService> _logger;
    
    public StabilityAnalysisService(ILogger<StabilityAnalysisService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 执行完整的稳定性分析
    /// </summary>
    public async Task<StabilityAnalysisResult> AnalyzeStabilityAsync(
        DamEntity damEntity, 
        AnalysisParameters analysisParameters,
        CancellationToken cancellationToken = default)
    {
        var result = new StabilityAnalysisResult
        {
            StartTime = DateTime.Now,
            DamName = damEntity.Name,
            Parameters = analysisParameters,
            Status = AnalysisStatus.Running
        };
        
        try
        {
            _logger.LogInformation("开始稳定性分析，坝体: {DamName}", damEntity.Name);
            
            // 验证输入参数
            var validation = ValidateParameters(analysisParameters);
            if (!validation.IsValid)
            {
                result.Status = AnalysisStatus.Failed;
                result.Errors.AddRange(validation.Errors.Select(e => e.Message));
                result.EndTime = DateTime.Now;
                return result;
            }
            
            // 添加警告信息
            result.Warnings.AddRange(validation.Warnings.Select(w => w.Message));
            
            // 模拟分析过程
            await Task.Delay(500, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 计算水压力
            double waterPressure = CalculateWaterPressure(analysisParameters);
            
            await Task.Delay(300, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 计算安全系数 - 传递完整的分析参数
            result.SlidingSafetyFactor = CalculateSlidingStabilityWithParameters(
                damEntity, waterPressure, analysisParameters);
                
            result.OverturnSafetyFactor = CalculateOverturnStability(
                damEntity, waterPressure, analysisParameters.SeismicCoefficient);
            
            await Task.Delay(200, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            
            // 计算详细力学分析
            result.ForceAnalysis = CalculateForceAnalysis(damEntity, analysisParameters, waterPressure);
            
            result.Status = AnalysisStatus.Completed;
            result.EndTime = DateTime.Now;
            
            _logger.LogInformation("稳定性分析完成，坝体: {DamName}, 抗滑系数: {SlidingFactor:F3}, 抗倾覆系数: {OverturnFactor:F3}", 
                damEntity.Name, result.SlidingSafetyFactor, result.OverturnSafetyFactor);
                
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Status = AnalysisStatus.Cancelled;
            result.EndTime = DateTime.Now;
            _logger.LogWarning("稳定性分析被取消，坝体: {DamName}", damEntity.Name);
            throw;
        }
        catch (Exception ex)
        {
            result.Status = AnalysisStatus.Failed;
            result.EndTime = DateTime.Now;
            result.Errors.Add($"分析过程中发生错误: {ex.Message}");
            _logger.LogError(ex, "稳定性分析失败，坝体: {DamName}", damEntity.Name);
            return result;
        }
    }
    
    /// <summary>
    /// 计算抗滑稳定安全系数（内部版本，接收完整参数）
    /// 根据《重力坝设计规范》GB 50287-2006
    /// Ks = ΣR / ΣS = f(W - U) / (P + E)
    /// </summary>
    private double CalculateSlidingStabilityWithParameters(DamEntity damEntity, double waterPressure, AnalysisParameters parameters)
    {
        try
        {
            // 坝体自重 (kN)
            double selfWeight = damEntity.Geometry.Volume * damEntity.MaterialProperties.Density; // 已经是kN
            
            // 计算扬压力 (kN)
            double upliftForce = CalculateUpliftForce(damEntity, 
                parameters.UpstreamWaterLevel,
                parameters.DownstreamWaterLevel,
                parameters.UpliftReductionFactor,
                parameters.WaterDensity);
            
            // 有效法向力：自重减去扬压力 (kN)
            double effectiveNormalForce = selfWeight - upliftForce;
            
            // 地震惯性力（水平分量）(kN)
            double seismicForceHorizontal = parameters.SeismicCoefficient * selfWeight;
            
            // 抗滑力：摩擦系数 × 有效法向力 (kN)
            double resistingForce = damEntity.MaterialProperties.FrictionCoefficient * effectiveNormalForce;
            
            // 滑动力：水平水压力 + 地震惯性力 (kN)
            double slidingForce = waterPressure + seismicForceHorizontal;
            
            // 抗滑稳定安全系数
            double safetyFactor = slidingForce > 0 ? resistingForce / slidingForce : double.MaxValue;
            
            return Math.Max(0, safetyFactor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算抗滑稳定安全系数失败");
            return 0.0;
        }
    }
    
    /// <summary>
    /// 计算抗滑稳定安全系数（公开接口版本）
    /// 根据《重力坝设计规范》GB 50287-2006
    /// Ks = ΣR / ΣS = f(W - U) / (P + E)
    /// </summary>
    public double CalculateSlidingStability(DamEntity damEntity, double waterPressure, double seismicCoefficient = 0.0)
    {
        try
        {
            // 坝体自重 (kN)
            double selfWeight = damEntity.Geometry.Volume * damEntity.MaterialProperties.Density; // 已经是kN
            
            // 简化计算：使用默认扬压力参数
            double upliftForce = CalculateUpliftForce(damEntity, 100.0, 10.0, 0.8, 9.8);
            
            // 有效法向力：自重减去扬压力 (kN)
            double effectiveNormalForce = selfWeight - upliftForce;
            
            // 地震惯性力（水平分量）(kN)
            double seismicForceHorizontal = seismicCoefficient * selfWeight;
            
            // 抗滑力：摩擦系数 × 有效法向力 (kN)
            double resistingForce = damEntity.MaterialProperties.FrictionCoefficient * effectiveNormalForce;
            
            // 滑动力：水平水压力 + 地震惯性力 (kN)
            double slidingForce = waterPressure + seismicForceHorizontal;
            
            // 抗滑稳定安全系数
            double safetyFactor = slidingForce > 0 ? resistingForce / slidingForce : double.MaxValue;
            
            return Math.Max(0, safetyFactor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算抗滑稳定安全系数失败");
            return 0.0;
        }
    }
    
    /// <summary>
    /// 计算抗倾覆稳定安全系数
    /// </summary>
    public double CalculateOverturnStability(DamEntity damEntity, double waterPressure, double seismicCoefficient = 0.0)
    {
        try
        {
            // 坝体自重
            double selfWeight = damEntity.Geometry.Volume * damEntity.MaterialProperties.Density * 9.81; // N
            
            // 简化计算：假设重心在几何中心
            double damWidth = damEntity.Geometry.BoundingBox.Width;
            double damHeight = damEntity.Geometry.BoundingBox.Height;
            
            // 抗倾覆力矩（自重对底部边缘的力矩）
            double resistingMoment = selfWeight * (damWidth / 2);
            
            // 倾覆力矩（水压力对底部边缘的力矩，简化为作用在坝高中点）
            double overturnMoment = waterPressure * (damHeight / 2);
            
            // 地震惯性力矩（作用在重心高度）
            if (seismicCoefficient > 0)
            {
                double seismicForce = seismicCoefficient * selfWeight;
                overturnMoment += seismicForce * (damHeight / 2);
            }
            
            // 安全系数
            double safetyFactor = overturnMoment > 0 ? resistingMoment / overturnMoment : double.MaxValue;
            
            return Math.Max(0, safetyFactor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算抗倾覆稳定安全系数失败");
            return 0.0;
        }
    }
    
    /// <summary>
    /// 验证分析参数
    /// </summary>
    public ValidationResult ValidateParameters(AnalysisParameters parameters)
    {
        var result = new ValidationResult();
        
        // 检查水位
        if (parameters.UpstreamWaterLevel <= 0)
        {
            result.AddError(nameof(parameters.UpstreamWaterLevel), "上游水位必须大于0");
        }
        
        if (parameters.DownstreamWaterLevel < 0)
        {
            result.AddError(nameof(parameters.DownstreamWaterLevel), "下游水位不能小于0");
        }
        
        if (parameters.UpstreamWaterLevel <= parameters.DownstreamWaterLevel)
        {
            result.AddWarning(nameof(parameters.UpstreamWaterLevel), "上游水位应大于下游水位");
        }
        
        // 检查物理参数
        if (parameters.WaterDensity <= 0)
        {
            result.AddError(nameof(parameters.WaterDensity), "水的重度必须大于0");
        }
        
        if (parameters.FrictionCoefficient <= 0 || parameters.FrictionCoefficient > 1.5)
        {
            result.AddError(nameof(parameters.FrictionCoefficient), "摩擦系数应在0-1.5之间");
        }
        
        if (parameters.SeismicCoefficient < 0 || parameters.SeismicCoefficient > 0.4)
        {
            result.AddError(nameof(parameters.SeismicCoefficient), "地震系数应在0-0.4之间");
        }
        
        // 检查安全系数要求
        if (parameters.RequiredSlidingSafetyFactor <= 1.0)
        {
            result.AddWarning(nameof(parameters.RequiredSlidingSafetyFactor), "抗滑安全系数要求建议大于1.0");
        }
        
        if (parameters.RequiredOverturnSafetyFactor <= 1.0)
        {
            result.AddWarning(nameof(parameters.RequiredOverturnSafetyFactor), "抗倾覆安全系数要求建议大于1.0");
        }
        
        return result;
    }
    
    /// <summary>
    /// 计算水平水压力合力
    /// 根据静水压力分布计算三角形分布荷载的合力
    /// </summary>
    private double CalculateWaterPressure(AnalysisParameters parameters)
    {
        // 上游水头 (m)
        double upstreamHead = parameters.UpstreamWaterLevel;
        // 下游水头 (m) 
        double downstreamHead = parameters.DownstreamWaterLevel;
        
        // 上游水压力合力 P1 = 0.5 * γw * H1² (kN/m)
        double upstreamPressure = 0.5 * parameters.WaterDensity * upstreamHead * upstreamHead;
        
        // 下游水压力合力 P2 = 0.5 * γw * H2² (kN/m)  
        double downstreamPressure = 0.5 * parameters.WaterDensity * downstreamHead * downstreamHead;
        
        // 净水压力 (kN/m)
        double netPressure = upstreamPressure - downstreamPressure;
        
        return Math.Max(0, netPressure);
    }
    
    /// <summary>
    /// 计算扬压力
    /// 根据《重力坝设计规范》GB 50287-2006
    /// </summary>
    private double CalculateUpliftForce(DamEntity damEntity, double upstreamWaterLevel, 
        double downstreamWaterLevel, double upliftReductionFactor, double waterDensity)
    {
        
        // 坝底面积 (m²)
        double baseArea = damEntity.Geometry.BaseWidth * damEntity.Geometry.Length;
        
        // 平均扬压力水头 (考虑上下游水位差的线性分布)
        double averageUpliftHead = (upstreamWaterLevel + downstreamWaterLevel) / 2;
        
        // 扬压力 = 折减系数 × 平均水头 × 底面积 × 水重度 (kN)
        double upliftForce = upliftReductionFactor * averageUpliftHead * baseArea * waterDensity;
        
        return upliftForce;
    }
    
    /// <summary>
    /// 计算详细力学分析
    /// </summary>
    private ForceAnalysisResult CalculateForceAnalysis(DamEntity damEntity, AnalysisParameters parameters, double waterPressure)
    {
        var result = new ForceAnalysisResult();
        
        // 自重
        result.SelfWeight = damEntity.Geometry.Volume * damEntity.MaterialProperties.Density * 9.81;
        
        // 水平力
        result.HorizontalForce = waterPressure;
        
        // 垂直力
        result.VerticalForce = result.SelfWeight;
        
        // 扬压力（简化计算）
        if (parameters.ConsiderUpliftPressure)
        {
            double baseArea = damEntity.Geometry.BoundingBox.Width * damEntity.Geometry.BoundingBox.Depth;
            result.UpliftForce = parameters.UpliftReductionFactor * parameters.WaterDensity * 
                               parameters.UpstreamWaterLevel * baseArea * 1000;
        }
        
        // 合力
        result.ResultantHorizontal = result.HorizontalForce;
        result.ResultantVertical = result.VerticalForce - result.UpliftForce;
        
        // 力矩计算（简化）
        double damHeight = damEntity.Geometry.BoundingBox.Height;
        double damWidth = damEntity.Geometry.BoundingBox.Width;
        
        result.OverturnMoment = result.HorizontalForce * (damHeight / 2);
        result.ResistingMoment = result.ResultantVertical * (damWidth / 2);
        
        return result;
    }
} 