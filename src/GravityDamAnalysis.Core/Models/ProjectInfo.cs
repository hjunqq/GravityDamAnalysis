using System;

namespace GravityDamAnalysis.Core.Models
{
    /// <summary>
    /// 项目信息
    /// </summary>
    public class ProjectInfo
    {
        public string Name { get; set; }
        public string Number { get; set; }
        public string Location { get; set; }
        public string Client { get; set; }
        public string Engineer { get; set; }
        public DateTime Date { get; set; }
        public string Description { get; set; }
    }
} 