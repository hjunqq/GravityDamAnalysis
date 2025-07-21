using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Models;
using GravityDamAnalysis.Core.Services;
using GravityDamAnalysis.Core.ValueObjects;
using GravityDamAnalysis.UI.Interfaces;
using GravityDamAnalysis.UI.Services;
using CoreEntities = GravityDamAnalysis.Core.Entities;
using System.Windows;

namespace GravityDamAnalysis.UI.ViewModels
{
    /// <summary>
    /// 主控制台ViewModel
    /// </summary>
    public class MainDashboardViewModel : ViewModelBase
    {
        private readonly ILogger<MainDashboardViewModel> _logger;
        private readonly ProfileValidationEngine _validationEngine;
        private ProjectInfo _currentProject;
        private string _connectionStatus = "未连接";
        private string _analysisStatus = "就绪";
        private string _statusMessage = "等待操作...";
        private int _progressValue;
        private bool _isProgressVisible;
        private ObservableCollection<DamGeometry> _detectedDams;
        private ObservableCollection<AnalysisResults> _recentAnalysisResults;
        
        public MainDashboardViewModel(IRevitIntegration revitIntegration, ILogger<MainDashboardViewModel> logger) : base(revitIntegration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // 创建验证引擎
            var validationLogger = LoggerFactory.Create(builder => { }).CreateLogger<ProfileValidationEngine>();
            _validationEngine = new ProfileValidationEngine(validationLogger);
            
            // 初始化集合
            _detectedDams = new ObservableCollection<DamGeometry>();
            _recentAnalysisResults = new ObservableCollection<AnalysisResults>();
            
            // 初始化命令
            AutoDetectDamsCommand = new AsyncCommand(AutoDetectDamsAsync);
            ExtractProfilesCommand = new AsyncCommand(ExtractProfilesAsync);
            StabilityAnalysisCommand = new AsyncCommand(StabilityAnalysisAsync);
            GenerateReportCommand = new AsyncCommand(GenerateReportAsync);
            
            // 初始化数据
            _ = InitializeAsync();
        }
        
        #region 属性
        
        public ProjectInfo CurrentProject
        {
            get => _currentProject;
            set => SetProperty(ref _currentProject, value);
        }
        
        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }
        
        public string AnalysisStatus
        {
            get => _analysisStatus;
            set => SetProperty(ref _analysisStatus, value);
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        public int ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }
        
        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set => SetProperty(ref _isProgressVisible, value);
        }
        
        public ObservableCollection<DamGeometry> DetectedDams
        {
            get => _detectedDams;
            set => SetProperty(ref _detectedDams, value);
        }
        
        public ObservableCollection<AnalysisResults> RecentAnalysisResults
        {
            get => _recentAnalysisResults;
            set => SetProperty(ref _recentAnalysisResults, value);
        }
        
        public DateTime LastUpdateTime => DateTime.Now;
        
        #endregion
        
        #region 命令
        
        public ICommand AutoDetectDamsCommand { get; }
        public ICommand ExtractProfilesCommand { get; }
        public ICommand StabilityAnalysisCommand { get; }
        public ICommand GenerateReportCommand { get; }
        
        #endregion
        
        #region 私有方法
        
        private async Task InitializeAsync()
        {
            try
            {
                // 获取项目信息
                CurrentProject = await _revitIntegration.GetProjectInfoAsync();
                
                // 获取最近的分析结果
                var recentResults = await _revitIntegration.GetRecentAnalysisResultsAsync();
                RecentAnalysisResults.Clear();
                foreach (var result in recentResults)
                {
                    RecentAnalysisResults.Add(result);
                }
                
                // 更新连接状态
                ConnectionStatus = _revitIntegration.IsInRevitContext ? "已连接" : "模拟模式";
                StatusMessage = "系统初始化完成";
            }
            catch (Exception ex)
            {
                StatusMessage = $"初始化失败: {ex.Message}";
            }
        }
        
        private async Task AutoDetectDamsAsync()
        {
            await ExecuteAsync(async () =>
            {
                AnalysisStatus = "识别中...";
                StatusMessage = "正在识别坝体...";
                
                var dams = await _revitIntegration.AutoDetectDamsAsync();
                
                DetectedDams.Clear();
                foreach (var dam in dams)
                {
                    DetectedDams.Add(dam);
                }
                
                AnalysisStatus = "识别完成";
                StatusMessage = $"成功识别 {dams.Count} 个坝体";
            }, "坝体识别失败");
        }
        
        private async Task ExtractProfilesAsync()
        {
            await ExecuteAsync(async () =>
            {
                if (DetectedDams.Count == 0)
                {
                    StatusMessage = "请先识别坝体";
                    return;
                }
                
                try
                {
                    // 创建剖面提取配置窗口
                    var loggerFactory = LoggerFactory.Create(builder => { });
                    var configLogger = loggerFactory.CreateLogger<Views.ProfileExtractionConfigWindow>();
                    var configWindow = new Views.ProfileExtractionConfigWindow(_revitIntegration, configLogger);
                    
                    // 显示配置窗口
                    var result = configWindow.ShowDialog();
                    
                    if (result == true && configWindow.ExtractionSuccessful)
                    {
                        AnalysisStatus = "验证中...";
                        StatusMessage = $"成功提取 {configWindow.ExtractedProfiles.Count} 个剖面，开始验证...";
                        
                        // 转换提取的剖面为EnhancedProfile2D格式
                        var enhancedProfiles = new List<CoreEntities.EnhancedProfile2D>();
                        foreach (var damProfile in configWindow.ExtractedProfiles)
                        {
                            var dam = DetectedDams.FirstOrDefault(d => d.Id == damProfile.DamId);
                            if (dam != null)
                            {
                                var enhancedProfile = ConvertToEnhancedProfile2D(damProfile, dam, $"剖面_{damProfile.ProfileIndex}");
                                enhancedProfiles.Add(enhancedProfile);
                            }
                        }
                        
                        // 验证提取的剖面
                        await ValidateExtractedProfiles(enhancedProfiles);
                    }
                    else
                    {
                        StatusMessage = "剖面提取已取消或失败";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "剖面提取过程中发生错误");
                    StatusMessage = $"剖面提取失败: {ex.Message}";
                }
            }, "剖面提取失败");
        }
        
        private CoreEntities.EnhancedProfile2D ConvertToEnhancedProfile2D(
            DamProfile damProfile, 
            DamGeometry damGeometry, 
            string profileName)
        {
            // 创建材料区域
            var materialZones = new List<CoreEntities.MaterialZone>();
            
            // 添加坝体材料区域
            var damMaterialZone = new CoreEntities.MaterialZone
            {
                Name = "坝体混凝土",
                Properties = new CoreEntities.MaterialProperties
                {
                    Name = "C25混凝土",
                    Density = 24.0, // kN/m³
                    ElasticModulus = 30.0, // GPa
                    PoissonRatio = 0.18,
                    CompressiveStrength = 25.0, // MPa
                    TensileStrength = 2.5, // MPa
                    FrictionCoefficient = 0.75
                },
                Boundary = damProfile.Coordinates.Select(p => new CoreEntities.Point2D(p.X, p.Y)).ToList()
            };
            materialZones.Add(damMaterialZone);
            
            // 创建边界条件
            var boundaryConditionDict = new Dictionary<string, CoreEntities.BoundaryCondition>();
            
            // 基础边界条件
            boundaryConditionDict["基础"] = new CoreEntities.BoundaryCondition
            {
                Name = "基础固定约束",
                Type = "固定约束",
                StartPoint = new CoreEntities.Point2D(0, 0),
                EndPoint = new CoreEntities.Point2D(100, 0),
                Parameters = new Dictionary<string, object>
                {
                    ["TranslationX"] = true,
                    ["TranslationY"] = true,
                    ["Rotation"] = true
                }
            };
            
            // 水压力边界条件
            boundaryConditionDict["水压力"] = new CoreEntities.BoundaryCondition
            {
                Name = "上游水压力",
                Type = "压力荷载",
                StartPoint = new CoreEntities.Point2D(0, 0),
                EndPoint = new CoreEntities.Point2D(0, 100),
                Parameters = new Dictionary<string, object>
                {
                    ["Pressure"] = 9.81, // kN/m³
                    ["Direction"] = "Normal"
                }
            };
            
            // 创建增强剖面2D对象
            var enhancedProfile = new CoreEntities.EnhancedProfile2D
            {
                Name = profileName,
                MainContour = damProfile.Coordinates.Select(p => new CoreEntities.Point2D(p.X, p.Y)).ToList(),
                MaterialZones = materialZones,
                BoundaryConditionDict = boundaryConditionDict,
                CreatedAt = DateTime.Now
            };
            
            return enhancedProfile;
        }
        
        private CoreEntities.EnhancedProfile2D CreateSampleProfile()
        {
            // 创建示例剖面坐标（重力坝典型剖面）
            var coordinates = new List<CoreEntities.Point2D>
            {
                new CoreEntities.Point2D(0, 0),      // 基础左端
                new CoreEntities.Point2D(0, 50),     // 坝顶左端
                new CoreEntities.Point2D(10, 50),    // 坝顶右端
                new CoreEntities.Point2D(15, 40),    // 下游面转折点1
                new CoreEntities.Point2D(20, 20),    // 下游面转折点2
                new CoreEntities.Point2D(25, 0)      // 基础右端
            };
            
            // 创建材料区域
            var materialZones = new List<CoreEntities.MaterialZone>();
            
            var damMaterialZone = new CoreEntities.MaterialZone
            {
                Name = "坝体混凝土",
                Properties = new CoreEntities.MaterialProperties
                {
                    Name = "C25混凝土",
                    Density = 24.0,
                    ElasticModulus = 30.0,
                    PoissonRatio = 0.18,
                    CompressiveStrength = 25.0,
                    TensileStrength = 2.5,
                    FrictionCoefficient = 0.75
                },
                Boundary = coordinates
            };
            materialZones.Add(damMaterialZone);
            
            // 创建边界条件
            var boundaryConditionDict = new Dictionary<string, CoreEntities.BoundaryCondition>();
            
            boundaryConditionDict["基础"] = new CoreEntities.BoundaryCondition
            {
                Name = "基础固定约束",
                Type = "固定约束",
                StartPoint = new CoreEntities.Point2D(0, 0),
                EndPoint = new CoreEntities.Point2D(25, 0),
                Parameters = new Dictionary<string, object>
                {
                    ["TranslationX"] = true,
                    ["TranslationY"] = true,
                    ["Rotation"] = true
                }
            };
            
            boundaryConditionDict["水压力"] = new CoreEntities.BoundaryCondition
            {
                Name = "上游水压力",
                Type = "压力荷载",
                StartPoint = new CoreEntities.Point2D(0, 0),
                EndPoint = new CoreEntities.Point2D(0, 50),
                Parameters = new Dictionary<string, object>
                {
                    ["Pressure"] = 9.81,
                    ["Direction"] = "Normal"
                }
            };
            
            // 创建增强剖面2D对象
            var sampleProfile = new CoreEntities.EnhancedProfile2D
            {
                Name = "示例重力坝剖面",
                MainContour = coordinates,
                MaterialZones = materialZones,
                BoundaryConditionDict = boundaryConditionDict,
                CreatedAt = DateTime.Now
            };
            
            return sampleProfile;
        }
        
        private async Task ValidateExtractedProfiles(List<CoreEntities.EnhancedProfile2D> profiles)
        {
            foreach (var profile in profiles)
            {
                try
                {
                    _logger.LogInformation("开始验证剖面: {ProfileName}", profile.Name);
                    
                    // 使用验证引擎验证剖面
                    var validationResult = _validationEngine.ValidateProfile(profile);
                    
                    // 输出验证结果
                    OutputValidationResults(profile, validationResult);
                    
                    // 显示验证对话框
                    var shouldSave = await ShowValidationDialog(profile, validationResult);
                    
                    if (shouldSave)
                    {
                        await SaveValidatedProfile(profile);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "验证剖面 {ProfileName} 时发生错误", profile.Name);
                    StatusMessage = $"验证剖面 {profile.Name} 失败: {ex.Message}";
                }
            }
        }
        
        private void OutputValidationResults(CoreEntities.EnhancedProfile2D profile, CoreEntities.ProfileValidationResult validationResult)
        {
            _logger.LogInformation("=== 剖面验证结果 ===");
            _logger.LogInformation("剖面名称: {ProfileName}", profile.Name);
            _logger.LogInformation("验证状态: {ValidationStatus}", validationResult.OverallStatus == CoreEntities.ValidationStatus.Validated ? "通过" : "失败");
            _logger.LogInformation("坐标点数量: {CoordinateCount}", profile.MainContour.Count);
            _logger.LogInformation("材料区域数量: {MaterialZoneCount}", profile.MaterialZones.Count);
            _logger.LogInformation("边界条件数量: {BoundaryConditionCount}", profile.BoundaryConditionDict.Count);
            
            if (validationResult.GeometryValidation.Issues.Any())
            {
                _logger.LogWarning("发现 {IssueCount} 个几何问题:", validationResult.GeometryValidation.Issues.Count);
                foreach (var issue in validationResult.GeometryValidation.Issues)
                {
                    _logger.LogWarning("- {IssueType}: {IssueMessage}", issue.Type, issue.Description);
                }
            }
            
            if (validationResult.EngineeringValidation.Issues.Any())
            {
                _logger.LogWarning("发现 {IssueCount} 个工程问题:", validationResult.EngineeringValidation.Issues.Count);
                foreach (var issue in validationResult.EngineeringValidation.Issues)
                {
                    _logger.LogWarning("- {IssueType}: {IssueMessage}", issue.Type, issue.Description);
                }
            }
        }
        
        private async Task<bool> ShowValidationDialog(CoreEntities.EnhancedProfile2D profile, CoreEntities.ProfileValidationResult validationResult)
        {
            var message = $"剖面 '{profile.Name}' 验证完成。\n\n";
            message += $"验证状态: {(validationResult.OverallStatus == CoreEntities.ValidationStatus.Validated ? "通过" : "失败")}\n";
            message += $"坐标点数量: {profile.MainContour.Count}\n";
            message += $"发现的问题: {validationResult.GeometryValidation.Issues.Count + validationResult.EngineeringValidation.Issues.Count} 个\n";
            
            var allIssues = validationResult.GeometryValidation.Issues.Concat(validationResult.EngineeringValidation.Issues).ToList();
            if (allIssues.Any())
            {
                message += "\n主要问题:\n";
                foreach (var issue in allIssues.Take(3))
                {
                    message += $"• {issue.Description}\n";
                }
            }
            
            message += "\n是否保存此剖面？";
            
            var result = MessageBox.Show(message, "剖面验证结果", 
                MessageBoxButton.YesNo, 
                validationResult.OverallStatus == CoreEntities.ValidationStatus.Validated ? MessageBoxImage.Information : MessageBoxImage.Warning);
            
            return result == MessageBoxResult.Yes;
        }
        
        private async Task SaveValidatedProfile(CoreEntities.EnhancedProfile2D profile)
        {
            try
            {
                // 这里可以添加保存剖面的逻辑
                _logger.LogInformation("保存验证通过的剖面: {ProfileName}", profile.Name);
                StatusMessage = $"剖面 '{profile.Name}' 已保存";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存剖面 {ProfileName} 时发生错误", profile.Name);
                StatusMessage = $"保存剖面失败: {ex.Message}";
            }
        }
        
        private async Task StabilityAnalysisAsync()
        {
            await ExecuteAsync(async () =>
            {
                AnalysisStatus = "分析中...";
                StatusMessage = "正在进行稳定性分析...";
                
                // 这里添加稳定性分析逻辑
                await Task.Delay(2000); // 模拟分析过程
                
                AnalysisStatus = "分析完成";
                StatusMessage = "稳定性分析完成";
            }, "稳定性分析失败");
        }
        
        private async Task GenerateReportAsync()
        {
            await ExecuteAsync(async () =>
            {
                AnalysisStatus = "生成中...";
                StatusMessage = "正在生成分析报告...";
                
                // 这里添加报告生成逻辑
                await Task.Delay(1000); // 模拟报告生成过程
                
                AnalysisStatus = "报告完成";
                StatusMessage = "分析报告已生成";
            }, "报告生成失败");
        }
        
        protected override void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            ProgressValue = e.ProgressPercentage;
            IsProgressVisible = e.ProgressPercentage > 0 && e.ProgressPercentage < 100;
        }
        
        protected override void OnStatusChanged(object sender, string status)
        {
            StatusMessage = status;
        }
        
        #endregion
    }
} 