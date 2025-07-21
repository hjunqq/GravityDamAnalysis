namespace GravityDamAnalysis.Reports.Models;

/// <summary>
/// 报告生成选项
/// </summary>
public class ReportOptions
{
    /// <summary>
    /// 报告标题
    /// </summary>
    public string Title { get; set; } = "重力坝稳定性分析报告";
    
    /// <summary>
    /// 项目名称
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;
    
    /// <summary>
    /// 工程师姓名
    /// </summary>
    public string EngineerName { get; set; } = string.Empty;
    
    /// <summary>
    /// 公司名称
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;
    
    /// <summary>
    /// 是否包含详细计算过程
    /// </summary>
    public bool IncludeDetailedCalculations { get; set; } = true;
    
    /// <summary>
    /// 是否包含图表
    /// </summary>
    public bool IncludeCharts { get; set; } = true;
    
    /// <summary>
    /// 是否包含参数表
    /// </summary>
    public bool IncludeParameterTables { get; set; } = true;
    
    /// <summary>
    /// 是否包含结论和建议
    /// </summary>
    public bool IncludeConclusionsAndRecommendations { get; set; } = true;
    
    /// <summary>
    /// 是否包含附录
    /// </summary>
    public bool IncludeAppendix { get; set; } = false;
    
    /// <summary>
    /// 报告格式
    /// </summary>
    public ReportFormat Format { get; set; } = ReportFormat.PDF;
    
    /// <summary>
    /// 页面方向
    /// </summary>
    public PageOrientation PageOrientation { get; set; } = PageOrientation.Portrait;
    
    /// <summary>
    /// 页面大小
    /// </summary>
    public PageSize PageSize { get; set; } = PageSize.A4;
    
    /// <summary>
    /// 字体大小
    /// </summary>
    public int FontSize { get; set; } = 12;
    
    /// <summary>
    /// 自定义CSS样式（仅用于HTML报告）
    /// </summary>
    public string? CustomCssStyle { get; set; }
    
    /// <summary>
    /// 报告语言
    /// </summary>
    public ReportLanguage Language { get; set; } = ReportLanguage.Chinese;
    
    /// <summary>
    /// 输出质量
    /// </summary>
    public OutputQuality Quality { get; set; } = OutputQuality.High;
}

/// <summary>
/// 报告格式枚举
/// </summary>
public enum ReportFormat
{
    /// <summary>
    /// PDF格式
    /// </summary>
    PDF,
    
    /// <summary>
    /// Word文档
    /// </summary>
    Word,
    
    /// <summary>
    /// HTML网页
    /// </summary>
    HTML,
    
    /// <summary>
    /// 纯文本
    /// </summary>
    Text
}

/// <summary>
/// 页面方向枚举
/// </summary>
public enum PageOrientation
{
    /// <summary>
    /// 纵向
    /// </summary>
    Portrait,
    
    /// <summary>
    /// 横向
    /// </summary>
    Landscape
}

/// <summary>
/// 页面大小枚举
/// </summary>
public enum PageSize
{
    /// <summary>
    /// A4纸张
    /// </summary>
    A4,
    
    /// <summary>
    /// A3纸张
    /// </summary>
    A3,
    
    /// <summary>
    /// Letter
    /// </summary>
    Letter,
    
    /// <summary>
    /// Legal
    /// </summary>
    Legal
}

/// <summary>
/// 报告语言枚举
/// </summary>
public enum ReportLanguage
{
    /// <summary>
    /// 中文
    /// </summary>
    Chinese,
    
    /// <summary>
    /// 英文
    /// </summary>
    English
}

/// <summary>
/// 输出质量枚举
/// </summary>
public enum OutputQuality
{
    /// <summary>
    /// 低质量
    /// </summary>
    Low,
    
    /// <summary>
    /// 标准质量
    /// </summary>
    Standard,
    
    /// <summary>
    /// 高质量
    /// </summary>
    High
} 