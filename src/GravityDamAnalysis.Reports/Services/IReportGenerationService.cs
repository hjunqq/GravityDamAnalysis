using GravityDamAnalysis.Calculation.Models;
using GravityDamAnalysis.Reports.Models;

namespace GravityDamAnalysis.Reports.Services;

/// <summary>
/// 报告生成服务接口
/// </summary>
public interface IReportGenerationService
{
    /// <summary>
    /// 生成PDF报告
    /// </summary>
    /// <param name="analysisResult">分析结果</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="options">报告选项</param>
    /// <returns>生成的报告文件路径</returns>
    Task<string> GeneratePdfReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath,
        ReportOptions? options = null);
    
    /// <summary>
    /// 生成Word报告
    /// </summary>
    /// <param name="analysisResult">分析结果</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="options">报告选项</param>
    /// <returns>生成的报告文件路径</returns>
    Task<string> GenerateWordReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath,
        ReportOptions? options = null);
    
    /// <summary>
    /// 生成HTML报告
    /// </summary>
    /// <param name="analysisResult">分析结果</param>
    /// <param name="outputPath">输出路径</param>
    /// <param name="options">报告选项</param>
    /// <returns>生成的报告文件路径</returns>
    Task<string> GenerateHtmlReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath,
        ReportOptions? options = null);
    
    /// <summary>
    /// 生成简单文本报告
    /// </summary>
    /// <param name="analysisResult">分析结果</param>
    /// <param name="outputPath">输出路径</param>
    /// <returns>生成的报告文件路径</returns>
    Task<string> GenerateTextReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath);
    
    /// <summary>
    /// 验证输出路径
    /// </summary>
    /// <param name="outputPath">输出路径</param>
    /// <returns>是否有效</returns>
    bool ValidateOutputPath(string outputPath);
} 