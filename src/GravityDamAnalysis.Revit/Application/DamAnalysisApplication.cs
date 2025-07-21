using System;
using System.Reflection;
using Autodesk.Revit.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Infrastructure.Revit;
using GravityDamAnalysis.Calculation.Services;
using GravityDamAnalysis.Revit.SectionAnalysis;
using GravityDamAnalysis.Core.Services;
using Serilog;

namespace GravityDamAnalysis.Revit.Application;

/// <summary>
/// 重力坝分析插件应用程序类
/// 负责插件的初始化、UI创建和服务配置
/// </summary>
public class DamAnalysisApplication : IExternalApplication
{
    private static IServiceProvider? _serviceProvider;
    private static ILogger<DamAnalysisApplication>? _logger;

    /// <summary>
    /// 获取服务提供者
    /// </summary>
    public static IServiceProvider ServiceProvider => _serviceProvider 
        ?? throw new InvalidOperationException("服务提供者尚未初始化");

    /// <summary>
    /// 插件启动时调用
    /// </summary>
    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            // 配置日志
            ConfigureLogging();
            
            // 配置依赖注入
            ConfigureServices();
            
            _logger = ServiceProvider.GetRequiredService<ILogger<DamAnalysisApplication>>();
            _logger.LogInformation("重力坝分析插件正在启动...");

            // 创建功能区面板
            CreateRibbonPanel(application);
            
            _logger.LogInformation("重力坝分析插件启动成功");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "插件启动失败");
            return Result.Failed;
        }
    }

    /// <summary>
    /// 插件关闭时调用
    /// </summary>
    public Result OnShutdown(UIControlledApplication application)
    {
        try
        {
            _logger?.LogInformation("重力坝分析插件正在关闭...");
            
            // 释放服务提供者
            if (_serviceProvider is IDisposable disposableServiceProvider)
            {
                disposableServiceProvider.Dispose();
            }

            Log.CloseAndFlush();
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "插件关闭失败");
            return Result.Failed;
        }
    }

    /// <summary>
    /// 配置日志记录
    /// </summary>
    private static void ConfigureLogging()
    {
        var logPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var logFile = System.IO.Path.Combine(logPath, "GravityDamAnalysis", "logs", "plugin-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(logFile, 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// 配置依赖注入服务
    /// </summary>
    private static void ConfigureServices()
    {
        var services = new ServiceCollection();

        // 添加日志服务
        services.AddLogging(builder =>
        {
            builder.AddSerilog();
        });

        // 注册核心服务
        services.AddScoped<RevitDataExtractor>();
        services.AddScoped<IStabilityAnalysisService, StabilityAnalysisService>();

        // 注册新增的高级分析服务
        services.AddScoped<AdvancedSectionExtractor>();
        services.AddScoped<IntelligentSectionLocator>();
        services.AddTransient<SafeTransactionManager>();
        
        // 注册验证引擎
        services.AddScoped<ProfileValidationEngine>();

        _serviceProvider = services.BuildServiceProvider();
    }

    /// <summary>
    /// 创建Revit功能区面板
    /// </summary>
    private void CreateRibbonPanel(UIControlledApplication application)
    {
        // 创建选项卡（如果不存在）
        const string tabName = "重力坝分析";
        try
        {
            application.CreateRibbonTab(tabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // 选项卡已存在，忽略异常
        }

        // 创建面板
        var panel = application.CreateRibbonPanel(tabName, "稳定性分析");

        // 获取当前程序集路径
        var assemblyPath = Assembly.GetExecutingAssembly().Location;

        // 创建主要分析命令按钮
        var buttonData = new PushButtonData(
            "DamStabilityAnalysis", 
            "坝体稳定性\n分析", 
            assemblyPath,
            "GravityDamAnalysis.Revit.Commands.DamStabilityAnalysisCommand");

        var button = panel.AddItem(buttonData) as PushButton;
        
        if (button != null)
        {
            button.ToolTip = "选择重力坝实体，进行抗滑和抗倾覆稳定性分析";
            button.LongDescription = "此工具可以帮助您从Revit模型中选择重力坝实体，" +
                                   "自动提取几何参数和材料属性，并进行稳定性计算。" +
                                   "计算结果包括抗滑安全系数和抗倾覆安全系数。";

            // 可以在后续添加自定义图标
        }

        // 添加高级分析命令按钮
        var advancedButtonData = new PushButtonData(
            "AdvancedDamAnalysis",
            "高级坝体\n分析",
            assemblyPath,
            "GravityDamAnalysis.Revit.Commands.AdvancedDamAnalysisCommand");

        var advancedButton = panel.AddItem(advancedButtonData) as PushButton;
        
        if (advancedButton != null)
        {
            advancedButton.ToolTip = "使用改进的几何算法进行高级坝体分析";
            advancedButton.LongDescription = "集成了智能剖面定位、改进几何切割和安全事务管理的高级分析功能。" +
                                           "支持批量剖面提取、几何特征识别和详细稳定性分析。" +
                                           "相比标准分析，具有更高的精度和更好的性能表现。";
        }

        // 添加UI集成分析命令按钮
        var uiButtonData = new PushButtonData(
            "GravityDamAnalysis",
            "UI集成\n分析",
            assemblyPath,
            "GravityDamAnalysis.Revit.Commands.GravityDamAnalysisCommand");

        var uiButton = panel.AddItem(uiButtonData) as PushButton;
        
        if (uiButton != null)
        {
            uiButton.ToolTip = "使用WPF UI界面进行重力坝稳定性分析";
            uiButton.LongDescription = "提供现代化的WPF用户界面，支持实时进度反馈、详细结果展示和报告生成。" +
                                     "集成了完整的分析流程，包括坝体识别、剖面提取、稳定性计算和结果管理。";
        }

        _logger?.LogInformation("功能区面板创建成功");
    }
} 