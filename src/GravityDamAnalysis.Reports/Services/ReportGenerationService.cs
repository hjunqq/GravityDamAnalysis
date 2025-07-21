using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Calculation.Models;
using GravityDamAnalysis.Reports.Models;
using System.Text;

namespace GravityDamAnalysis.Reports.Services;

/// <summary>
/// 报告生成服务实现
/// </summary>
public class ReportGenerationService : IReportGenerationService
{
    private readonly ILogger<ReportGenerationService> _logger;
    
    public ReportGenerationService(ILogger<ReportGenerationService> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// 生成PDF报告
    /// </summary>
    public async Task<string> GeneratePdfReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath,
        ReportOptions? options = null)
    {
        try
        {
            options ??= new ReportOptions();
            _logger.LogInformation("开始生成PDF报告: {OutputPath}", outputPath);
            
            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 这里应该使用iTextSharp生成PDF
            // 为了简化，目前先生成HTML再转换为文本格式模拟
            var htmlContent = await GenerateHtmlContentAsync(analysisResult, options);
            
            // 模拟PDF生成过程
            await Task.Delay(1000);
            
            // 临时实现：保存为文本文件，实际应该是PDF格式
            var finalPath = Path.ChangeExtension(outputPath, ".pdf");
            await File.WriteAllTextAsync(finalPath, htmlContent, Encoding.UTF8);
            
            _logger.LogInformation("PDF报告生成完成: {FinalPath}", finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成PDF报告失败: {OutputPath}", outputPath);
            throw;
        }
    }
    
    /// <summary>
    /// 生成Word报告
    /// </summary>
    public async Task<string> GenerateWordReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath,
        ReportOptions? options = null)
    {
        try
        {
            options ??= new ReportOptions();
            _logger.LogInformation("开始生成Word报告: {OutputPath}", outputPath);
            
            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 这里应该使用DocumentFormat.OpenXml生成Word文档
            // 为了简化，目前先生成文本内容
            var content = GenerateTextContent(analysisResult, options);
            
            // 模拟Word生成过程
            await Task.Delay(800);
            
            // 临时实现：保存为文本文件，实际应该是Word格式
            var finalPath = Path.ChangeExtension(outputPath, ".docx");
            await File.WriteAllTextAsync(finalPath, content, Encoding.UTF8);
            
            _logger.LogInformation("Word报告生成完成: {FinalPath}", finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成Word报告失败: {OutputPath}", outputPath);
            throw;
        }
    }
    
    /// <summary>
    /// 生成HTML报告
    /// </summary>
    public async Task<string> GenerateHtmlReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath,
        ReportOptions? options = null)
    {
        try
        {
            options ??= new ReportOptions();
            _logger.LogInformation("开始生成HTML报告: {OutputPath}", outputPath);
            
            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var htmlContent = await GenerateHtmlContentAsync(analysisResult, options);
            
            var finalPath = Path.ChangeExtension(outputPath, ".html");
            await File.WriteAllTextAsync(finalPath, htmlContent, Encoding.UTF8);
            
            _logger.LogInformation("HTML报告生成完成: {FinalPath}", finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成HTML报告失败: {OutputPath}", outputPath);
            throw;
        }
    }
    
    /// <summary>
    /// 生成简单文本报告
    /// </summary>
    public async Task<string> GenerateTextReportAsync(
        StabilityAnalysisResult analysisResult, 
        string outputPath)
    {
        try
        {
            _logger.LogInformation("开始生成文本报告: {OutputPath}", outputPath);
            
            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var content = GenerateTextContent(analysisResult, new ReportOptions());
            
            var finalPath = Path.ChangeExtension(outputPath, ".txt");
            await File.WriteAllTextAsync(finalPath, content, Encoding.UTF8);
            
            _logger.LogInformation("文本报告生成完成: {FinalPath}", finalPath);
            return finalPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成文本报告失败: {OutputPath}", outputPath);
            throw;
        }
    }
    
    /// <summary>
    /// 验证输出路径
    /// </summary>
    public bool ValidateOutputPath(string outputPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                return false;
                
            var directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(directory))
                return false;
                
            // 检查路径是否包含无效字符
            var invalidChars = Path.GetInvalidPathChars().Concat(Path.GetInvalidFileNameChars()).ToArray();
            if (outputPath.IndexOfAny(invalidChars) >= 0)
                return false;
                
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// 生成HTML内容
    /// </summary>
    private async Task<string> GenerateHtmlContentAsync(StabilityAnalysisResult analysisResult, ReportOptions options)
    {
        await Task.Delay(100); // 模拟异步处理
        
        var html = new StringBuilder();
        
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang='zh-CN'>");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset='UTF-8'>");
        html.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        html.AppendLine($"    <title>{options.Title}</title>");
        html.AppendLine("    <style>");
        html.AppendLine(GetDefaultCssStyle());
        if (!string.IsNullOrEmpty(options.CustomCssStyle))
        {
            html.AppendLine(options.CustomCssStyle);
        }
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // 标题部分
        html.AppendLine($"    <h1>{options.Title}</h1>");
        
        if (!string.IsNullOrEmpty(options.ProjectName))
        {
            html.AppendLine($"    <h2>项目名称: {options.ProjectName}</h2>");
        }
        
        // 基本信息
        html.AppendLine("    <div class='section'>");
        html.AppendLine("        <h3>基本信息</h3>");
        html.AppendLine("        <table>");
        html.AppendLine($"            <tr><td>坝体名称</td><td>{analysisResult.DamName}</td></tr>");
        html.AppendLine($"            <tr><td>分析时间</td><td>{analysisResult.StartTime:yyyy-MM-dd HH:mm:ss}</td></tr>");
        html.AppendLine($"            <tr><td>分析用时</td><td>{analysisResult.Duration.TotalSeconds:F2} 秒</td></tr>");
        html.AppendLine($"            <tr><td>分析状态</td><td>{GetStatusText(analysisResult.Status)}</td></tr>");
        html.AppendLine("        </table>");
        html.AppendLine("    </div>");
        
        // 分析参数
        if (options.IncludeParameterTables)
        {
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h3>分析参数</h3>");
            html.AppendLine("        <table>");
            html.AppendLine($"            <tr><td>上游水位</td><td>{analysisResult.Parameters.UpstreamWaterLevel:F2} m</td></tr>");
            html.AppendLine($"            <tr><td>下游水位</td><td>{analysisResult.Parameters.DownstreamWaterLevel:F2} m</td></tr>");
            html.AppendLine($"            <tr><td>地震系数</td><td>{analysisResult.Parameters.SeismicCoefficient:F3}</td></tr>");
            html.AppendLine($"            <tr><td>摩擦系数</td><td>{analysisResult.Parameters.FrictionCoefficient:F3}</td></tr>");
            html.AppendLine($"            <tr><td>是否考虑扬压力</td><td>{(analysisResult.Parameters.ConsiderUpliftPressure ? "是" : "否")}</td></tr>");
            html.AppendLine("        </table>");
            html.AppendLine("    </div>");
        }
        
        // 分析结果
        html.AppendLine("    <div class='section'>");
        html.AppendLine("        <h3>分析结果</h3>");
        html.AppendLine("        <table>");
        html.AppendLine($"            <tr><td>抗滑安全系数</td><td class='{(analysisResult.IsSlidingStable ? "success" : "error")}'>{analysisResult.SlidingSafetyFactor:F3}</td></tr>");
        html.AppendLine($"            <tr><td>抗倾覆安全系数</td><td class='{(analysisResult.IsOverturnStable ? "success" : "error")}'>{analysisResult.OverturnSafetyFactor:F3}</td></tr>");
        html.AppendLine($"            <tr><td>整体稳定性</td><td class='{(analysisResult.IsOverallStable ? "success" : "error")}'>{(analysisResult.IsOverallStable ? "满足要求" : "不满足要求")}</td></tr>");
        html.AppendLine("        </table>");
        html.AppendLine("    </div>");
        
        // 详细计算（如果需要）
        if (options.IncludeDetailedCalculations)
        {
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h3>详细计算结果</h3>");
            html.AppendLine("        <table>");
            html.AppendLine($"            <tr><td>自重</td><td>{analysisResult.ForceAnalysis.SelfWeight:F2} kN</td></tr>");
            html.AppendLine($"            <tr><td>水平力</td><td>{analysisResult.ForceAnalysis.HorizontalForce:F2} kN</td></tr>");
            html.AppendLine($"            <tr><td>垂直力</td><td>{analysisResult.ForceAnalysis.VerticalForce:F2} kN</td></tr>");
            html.AppendLine($"            <tr><td>扬压力</td><td>{analysisResult.ForceAnalysis.UpliftForce:F2} kN</td></tr>");
            html.AppendLine($"            <tr><td>倾覆力矩</td><td>{analysisResult.ForceAnalysis.OverturnMoment:F2} kN·m</td></tr>");
            html.AppendLine($"            <tr><td>抗倾覆力矩</td><td>{analysisResult.ForceAnalysis.ResistingMoment:F2} kN·m</td></tr>");
            html.AppendLine("        </table>");
            html.AppendLine("    </div>");
        }
        
        // 结论和建议
        if (options.IncludeConclusionsAndRecommendations)
        {
            html.AppendLine("    <div class='section'>");
            html.AppendLine("        <h3>结论和建议</h3>");
            html.AppendLine("        <div class='conclusions'>");
            
            if (analysisResult.IsOverallStable)
            {
                html.AppendLine("            <p class='success'>✓ 重力坝整体稳定性满足设计要求。</p>");
            }
            else
            {
                html.AppendLine("            <p class='error'>✗ 重力坝整体稳定性不满足设计要求，需要采取相应措施。</p>");
                
                if (!analysisResult.IsSlidingStable)
                {
                    html.AppendLine("            <p class='warning'>• 抗滑稳定安全系数不足，建议增加坝底摩擦系数或调整坝体结构。</p>");
                }
                
                if (!analysisResult.IsOverturnStable)
                {
                    html.AppendLine("            <p class='warning'>• 抗倾覆稳定安全系数不足，建议增加坝体宽度或优化断面形状。</p>");
                }
            }
            
            html.AppendLine("        </div>");
            html.AppendLine("    </div>");
        }
        
        // 页脚
        html.AppendLine("    <div class='footer'>");
        if (!string.IsNullOrEmpty(options.EngineerName))
        {
            html.AppendLine($"        <p>分析工程师: {options.EngineerName}</p>");
        }
        if (!string.IsNullOrEmpty(options.CompanyName))
        {
            html.AppendLine($"        <p>单位: {options.CompanyName}</p>");
        }
        html.AppendLine($"        <p>报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>");
        html.AppendLine("    </div>");
        
        html.AppendLine("</body>");
        html.AppendLine("</html>");
        
        return html.ToString();
    }
    
    /// <summary>
    /// 生成文本内容
    /// </summary>
    private string GenerateTextContent(StabilityAnalysisResult analysisResult, ReportOptions options)
    {
        var content = new StringBuilder();
        
        content.AppendLine(options.Title);
        content.AppendLine(new string('=', options.Title.Length));
        content.AppendLine();
        
        if (!string.IsNullOrEmpty(options.ProjectName))
        {
            content.AppendLine($"项目名称: {options.ProjectName}");
            content.AppendLine();
        }
        
        content.AppendLine("基本信息:");
        content.AppendLine($"  坝体名称: {analysisResult.DamName}");
        content.AppendLine($"  分析时间: {analysisResult.StartTime:yyyy-MM-dd HH:mm:ss}");
        content.AppendLine($"  分析用时: {analysisResult.Duration.TotalSeconds:F2} 秒");
        content.AppendLine($"  分析状态: {GetStatusText(analysisResult.Status)}");
        content.AppendLine();
        
        content.AppendLine("分析参数:");
        content.AppendLine($"  上游水位: {analysisResult.Parameters.UpstreamWaterLevel:F2} m");
        content.AppendLine($"  下游水位: {analysisResult.Parameters.DownstreamWaterLevel:F2} m");
        content.AppendLine($"  地震系数: {analysisResult.Parameters.SeismicCoefficient:F3}");
        content.AppendLine($"  摩擦系数: {analysisResult.Parameters.FrictionCoefficient:F3}");
        content.AppendLine();
        
        content.AppendLine("分析结果:");
        content.AppendLine($"  抗滑安全系数: {analysisResult.SlidingSafetyFactor:F3} {(analysisResult.IsSlidingStable ? "✓" : "✗")}");
        content.AppendLine($"  抗倾覆安全系数: {analysisResult.OverturnSafetyFactor:F3} {(analysisResult.IsOverturnStable ? "✓" : "✗")}");
        content.AppendLine($"  整体稳定性: {(analysisResult.IsOverallStable ? "满足要求 ✓" : "不满足要求 ✗")}");
        content.AppendLine();
        
        if (analysisResult.Warnings.Count > 0)
        {
            content.AppendLine("警告信息:");
            foreach (var warning in analysisResult.Warnings)
            {
                content.AppendLine($"  - {warning}");
            }
            content.AppendLine();
        }
        
        if (analysisResult.Errors.Count > 0)
        {
            content.AppendLine("错误信息:");
            foreach (var error in analysisResult.Errors)
            {
                content.AppendLine($"  - {error}");
            }
            content.AppendLine();
        }
        
        content.AppendLine($"报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        
        return content.ToString();
    }
    
    /// <summary>
    /// 获取默认CSS样式
    /// </summary>
    private string GetDefaultCssStyle()
    {
        return @"
            body { 
                font-family: 'Microsoft YaHei', Arial, sans-serif; 
                line-height: 1.6; 
                margin: 20px; 
                color: #333; 
            }
            h1 { 
                color: #2c3e50; 
                text-align: center; 
                border-bottom: 3px solid #3498db; 
                padding-bottom: 10px; 
            }
            h2 { 
                color: #34495e; 
                text-align: center; 
                margin-bottom: 30px; 
            }
            h3 { 
                color: #2c3e50; 
                border-left: 4px solid #3498db; 
                padding-left: 10px; 
            }
            .section { 
                margin-bottom: 30px; 
                padding: 15px; 
                border: 1px solid #ecf0f1; 
                border-radius: 5px; 
            }
            table { 
                width: 100%; 
                border-collapse: collapse; 
                margin: 10px 0; 
            }
            th, td { 
                border: 1px solid #bdc3c7; 
                padding: 8px; 
                text-align: left; 
            }
            th { 
                background-color: #ecf0f1; 
                font-weight: bold; 
            }
            .success { 
                color: #27ae60; 
                font-weight: bold; 
            }
            .error { 
                color: #e74c3c; 
                font-weight: bold; 
            }
            .warning { 
                color: #f39c12; 
            }
            .conclusions { 
                background-color: #f8f9fa; 
                padding: 15px; 
                border-radius: 5px; 
            }
            .footer { 
                margin-top: 40px; 
                padding-top: 20px; 
                border-top: 1px solid #ecf0f1; 
                text-align: center; 
                color: #7f8c8d; 
            }
        ";
    }
    
    /// <summary>
    /// 获取状态文本
    /// </summary>
    private string GetStatusText(AnalysisStatus status)
    {
        return status switch
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