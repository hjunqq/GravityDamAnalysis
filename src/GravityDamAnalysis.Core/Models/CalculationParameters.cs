namespace GravityDamAnalysis.Core.Models
{
    /// <summary>
    /// 计算参数
    /// </summary>
    public class CalculationParameters
    {
        // 材料参数
        public string MaterialType { get; set; } = "混凝土";
        public double UnitWeight { get; set; } = 24.0; // kN/m³
        public double CompressiveStrength { get; set; } = 20.0; // MPa
        public double FrictionCoefficient { get; set; } = 0.7;
        public double Cohesion { get; set; } = 0.0; // kPa
        
        // 水位参数
        public double UpstreamWaterLevel { get; set; } = 100.0; // m
        public double DownstreamWaterLevel { get; set; } = 0.0; // m
        public double UpliftCoefficient { get; set; } = 0.3;
        public double DrainageEffectiveness { get; set; } = 0.8;
        
        // 地震参数
        public string SeismicIntensity { get; set; } = "7度";
        public double SeismicCoefficient { get; set; } = 0.1;
        
        // 安全系数要求
        public double SlidingSafetyFactor { get; set; } = 1.3;
        public double OverturningSafetyFactor { get; set; } = 1.5;
        public double AllowableCompressiveStress { get; set; } = 15.0; // MPa
        public double AllowableTensileStress { get; set; } = 1.5; // MPa
        public double SpecialConditionSafetyFactor { get; set; } = 1.1;
        
        // 计算选项
        public bool ConsiderSeismicLoad { get; set; } = true;
        public bool ConsiderThermalStress { get; set; } = false;
        public bool ConsiderConstructionLoad { get; set; } = false;
        public bool ConsiderIcePressure { get; set; } = false;
        public bool ConsiderWavePressure { get; set; } = false;
        public bool ConsiderSedimentPressure { get; set; } = false;
        
        // 输出选项
        public string CalculationPrecision { get; set; } = "标准";
        public string OutputFormat { get; set; } = "详细";
    }
} 