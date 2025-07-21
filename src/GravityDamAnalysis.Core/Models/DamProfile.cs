using System.Collections.Generic;

namespace GravityDamAnalysis.Core.Models
{
    /// <summary>
    /// 坝体剖面信息
    /// </summary>
    public class DamProfile
    {
        public string DamId { get; set; }
        public int ProfileIndex { get; set; }
        public string Name { get; set; }
        public List<Point2D> Coordinates { get; set; } = new List<Point2D>();
        public List<Point2D> FoundationLine { get; set; } = new List<Point2D>();
        public double WaterLevel { get; set; }
        public double FoundationElevation { get; set; }
        public double CrestElevation { get; set; }
    }
    
    /// <summary>
    /// 二维点
    /// </summary>
    public class Point2D
    {
        public double X { get; set; }
        public double Y { get; set; }
        
        public Point2D() { }
        
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
} 