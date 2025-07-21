namespace GravityDamAnalysis.Calculation.Models;

/// <summary>
/// 稳定性分析结果
/// </summary>
public class StabilityAnalysisResult
{
    /// <summary>
    /// 分析ID
    /// </summary>
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// 分析开始时间
    /// </summary>
    public DateTime StartTime { get; set; }
    
    /// <summary>
    /// 分析结束时间
    /// </summary>
    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// 分析持续时间
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
    
    /// <summary>
    /// 坝体名称
    /// </summary>
    public string DamName { get; set; } = string.Empty;
    
    /// <summary>
    /// 分析参数
    /// </summary>
    public AnalysisParameters Parameters { get; set; } = new();
    
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
    public bool IsSlidingStable => SlidingSafetyFactor >= Parameters.RequiredSlidingSafetyFactor;
    
    /// <summary>
    /// 抗倾覆稳定是否满足要求
    /// </summary>
    public bool IsOverturnStable => OverturnSafetyFactor >= Parameters.RequiredOverturnSafetyFactor;
    
    /// <summary>
    /// 整体稳定性是否满足要求
    /// </summary>
    public bool IsOverallStable => IsSlidingStable && IsOverturnStable;
    
    /// <summary>
    /// 力学计算详细结果
    /// </summary>
    public ForceAnalysisResult ForceAnalysis { get; set; } = new();
    
    /// <summary>
    /// 警告信息
    /// </summary>
    public List<string> Warnings { get; set; } = new();
    
    /// <summary>
    /// 错误信息
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// 分析状态
    /// </summary>
    public AnalysisStatus Status { get; set; } = AnalysisStatus.NotStarted;
    
    /// <summary>
    /// 生成分析报告摘要
    /// </summary>
    /// <returns>报告摘要字符串</returns>
    public string GenerateSummary()
    {
        var summary = $"重力坝稳定性分析报告\n";
        summary += $"============================\n";
        summary += $"坝体名称: {DamName}\n";
        summary += $"分析时间: {StartTime:yyyy-MM-dd HH:mm:ss}\n";
        summary += $"分析用时: {Duration.TotalSeconds:F2} 秒\n";
        summary += $"分析状态: {GetStatusDescription()}\n\n";
        
        summary += $"分析参数:\n";
        summary += $"- 上游水位: {Parameters.UpstreamWaterLevel:F2} m\n";
        summary += $"- 下游水位: {Parameters.DownstreamWaterLevel:F2} m\n";
        summary += $"- 地震系数: {Parameters.SeismicCoefficient:F3}\n";
        summary += $"- 摩擦系数: {Parameters.FrictionCoefficient:F3}\n\n";
        
        summary += $"分析结果:\n";
        summary += $"- 抗滑安全系数: {SlidingSafetyFactor:F3} (要求: ≥{Parameters.RequiredSlidingSafetyFactor:F1}) {(IsSlidingStable ? "✓" : "✗")}\n";
        summary += $"- 抗倾覆安全系数: {OverturnSafetyFactor:F3} (要求: ≥{Parameters.RequiredOverturnSafetyFactor:F1}) {(IsOverturnStable ? "✓" : "✗")}\n";
        summary += $"- 整体稳定性: {(IsOverallStable ? "满足要求 ✓" : "不满足要求 ✗")}\n\n";
        
        if (Warnings.Count > 0)
        {
            summary += $"警告信息:\n";
            foreach (var warning in Warnings)
            {
                summary += $"- {warning}\n";
            }
            summary += "\n";
        }
        
        if (Errors.Count > 0)
        {
            summary += $"错误信息:\n";
            foreach (var error in Errors)
            {
                summary += $"- {error}\n";
            }
        }
        
        return summary;
    }
    
    private string GetStatusDescription()
    {
        return Status switch
        {
            AnalysisStatus.NotStarted => "未开始",
            AnalysisStatus.Running => "运行中",
            AnalysisStatus.Completed => "已完成",
            AnalysisStatus.Failed => "失败",
            AnalysisStatus.Cancelled => "已取消",
            _ => "未知"
        };
    }
}

/// <summary>
/// 力学分析结果
/// </summary>
public class ForceAnalysisResult
{
    /// <summary>
    /// 自重 (kN)
    /// </summary>
    public double SelfWeight { get; set; }
    
    /// <summary>
    /// 水平力 (kN)
    /// </summary>
    public double HorizontalForce { get; set; }
    
    /// <summary>
    /// 垂直力 (kN)
    /// </summary>
    public double VerticalForce { get; set; }
    
    /// <summary>
    /// 扬压力 (kN)
    /// </summary>
    public double UpliftForce { get; set; }
    
    /// <summary>
    /// 倾覆力矩 (kN·m)
    /// </summary>
    public double OverturnMoment { get; set; }
    
    /// <summary>
    /// 抗倾覆力矩 (kN·m)
    /// </summary>
    public double ResistingMoment { get; set; }
    
    /// <summary>
    /// 合力水平分量 (kN)
    /// </summary>
    public double ResultantHorizontal { get; set; }
    
    /// <summary>
    /// 合力垂直分量 (kN)
    /// </summary>
    public double ResultantVertical { get; set; }
}

/// <summary>
/// 分析状态枚举
/// </summary>
public enum AnalysisStatus
{
    /// <summary>
    /// 未开始
    /// </summary>
    NotStarted,
    
    /// <summary>
    /// 运行中
    /// </summary>
    Running,
    
    /// <summary>
    /// 已完成
    /// </summary>
    Completed,
    
    /// <summary>
    /// 失败
    /// </summary>
    Failed,
    
    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled
} 