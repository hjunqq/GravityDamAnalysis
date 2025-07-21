using System;

namespace GravityDamAnalysis.Core.Models
{
    /// <summary>
    /// 坝体几何信息
    /// </summary>
    public class DamGeometry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DamType Type { get; set; }
        public double Height { get; set; }
        public double Length { get; set; }
        public double Volume { get; set; }
        public string Material { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
    
    /// <summary>
    /// 坝体类型
    /// </summary>
    public enum DamType
    {
        Gravity,        // 重力坝
        Arch,           // 拱坝
        Embankment,     // 土石坝
        Buttress        // 支墩坝
    }
} 