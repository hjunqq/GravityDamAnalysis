using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Calculation.Models;

namespace GravityDamAnalysis.Calculation.Services;

/// <summary>
/// 稳定性分析服务接口
/// </summary>
public interface IStabilityAnalysisService
{
    /// <summary>
    /// 执行完整的稳定性分析
    /// </summary>
    /// <param name="damEntity">重力坝实体</param>
    /// <param name="analysisParameters">分析参数</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分析结果</returns>
    Task<StabilityAnalysisResult> AnalyzeStabilityAsync(
        DamEntity damEntity, 
        AnalysisParameters analysisParameters,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 计算抗滑稳定安全系数
    /// </summary>
    /// <param name="damEntity">重力坝实体</param>
    /// <param name="waterPressure">水压力</param>
    /// <param name="seismicCoefficient">地震系数</param>
    /// <returns>抗滑安全系数</returns>
    double CalculateSlidingStability(DamEntity damEntity, double waterPressure, double seismicCoefficient = 0.0);
    
    /// <summary>
    /// 计算抗倾覆稳定安全系数
    /// </summary>
    /// <param name="damEntity">重力坝实体</param>
    /// <param name="waterPressure">水压力</param>
    /// <param name="seismicCoefficient">地震系数</param>
    /// <returns>抗倾覆安全系数</returns>
    double CalculateOverturnStability(DamEntity damEntity, double waterPressure, double seismicCoefficient = 0.0);
    
    /// <summary>
    /// 验证分析参数
    /// </summary>
    /// <param name="parameters">分析参数</param>
    /// <returns>验证结果</returns>
    ValidationResult ValidateParameters(AnalysisParameters parameters);
} 