using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GravityDamAnalysis.Core.Models;

namespace GravityDamAnalysis.UI.Interfaces
{
    /// <summary>
    /// Revit集成接口，定义UI与Revit插件之间的交互契约
    /// </summary>
    public interface IRevitIntegration
    {
        /// <summary>
        /// 获取当前Revit文档
        /// </summary>
        object RevitDocument { get; }
        
        /// <summary>
        /// 获取当前Revit应用程序
        /// </summary>
        object RevitApplication { get; }
        
        /// <summary>
        /// 检查是否在Revit环境中运行
        /// </summary>
        bool IsInRevitContext { get; }
        
        /// <summary>
        /// 自动识别坝体
        /// </summary>
        Task<List<DamGeometry>> AutoDetectDamsAsync();
        
        /// <summary>
        /// 提取指定坝体的剖面
        /// </summary>
        Task<DamProfile> ExtractProfileAsync(DamGeometry dam, int profileIndex);
        
        /// <summary>
        /// 执行稳定性分析
        /// </summary>
        Task<AnalysisResults> PerformStabilityAnalysisAsync(DamProfile profile, CalculationParameters parameters);
        
        /// <summary>
        /// 将分析结果写回Revit模型
        /// </summary>
        Task WriteResultsToRevitAsync(AnalysisResults results);
        
        /// <summary>
        /// 获取项目信息
        /// </summary>
        Task<ProjectInfo> GetProjectInfoAsync();
        
        /// <summary>
        /// 获取最近的分析结果
        /// </summary>
        Task<List<AnalysisResults>> GetRecentAnalysisResultsAsync();
        
        /// <summary>
        /// 保存分析结果
        /// </summary>
        Task SaveAnalysisResultsAsync(AnalysisResults results);
        
        /// <summary>
        /// 生成分析报告
        /// </summary>
        Task<string> GenerateReportAsync(AnalysisResults results);
        
        /// <summary>
        /// 导出结果到Excel
        /// </summary>
        Task ExportToExcelAsync(AnalysisResults results, string filePath);
        
        /// <summary>
        /// 显示进度信息
        /// </summary>
        event EventHandler<ProgressEventArgs> ProgressChanged;
        
        /// <summary>
        /// 显示状态信息
        /// </summary>
        event EventHandler<string> StatusChanged;
    }
    
    /// <summary>
    /// 进度事件参数
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        public int ProgressPercentage { get; set; }
        public string Message { get; set; }
        public bool IsIndeterminate { get; set; }
    }
} 