using System;

namespace GravityDamAnalysis.Core.Models
{
    /// <summary>
    /// 分析结果
    /// </summary>
    public class AnalysisResults
    {
        public string Id { get; set; }
        public string DamId { get; set; }
        public string ProfileId { get; set; }
        public DateTime AnalysisDateTime { get; set; }
        
        // 安全系数
        public double SlidingSafetyFactor { get; set; }
        public double OverturningSafetyFactor { get; set; }
        public double CompressionSafetyFactor { get; set; }
        
        // 状态
        public string SlidingStatus { get; set; }
        public string OverturningStatus { get; set; }
        public string CompressionStatus { get; set; }
        
        // 荷载分析
        public double SelfWeight { get; set; }
        public double WaterPressure { get; set; }
        public double UpliftPressure { get; set; }
        public double SeismicForce { get; set; }
        
        // 应力分析
        public double MaxCompressiveStress { get; set; }
        public double MaxTensileStress { get; set; }
        public double PrincipalStress { get; set; }
        
        // 分析类型
        public string AnalysisType { get; set; } = "稳定性分析";
    }
} 