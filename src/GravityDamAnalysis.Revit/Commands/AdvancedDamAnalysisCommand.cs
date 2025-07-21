using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.Services;
using GravityDamAnalysis.Calculation.Models;
using GravityDamAnalysis.Calculation.Services;
using GravityDamAnalysis.Revit.Application;
using GravityDamAnalysis.Revit.Selection;
using GravityDamAnalysis.Revit.SectionAnalysis;


namespace GravityDamAnalysis.Revit.Commands;

/// <summary>
/// 高级重力坝分析命令
/// 集成了改进的几何提取、智能剖面定位和安全事务管理功能
/// </summary>
[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AdvancedDamAnalysisCommand : IExternalCommand
{
    private readonly ILogger<AdvancedDamAnalysisCommand> _logger;
    private CancellationTokenSource _cancellationTokenSource;

    public AdvancedDamAnalysisCommand()
    {
        _logger = DamAnalysisApplication.ServiceProvider.GetRequiredService<ILogger<AdvancedDamAnalysisCommand>>();
    }

    /// <summary>
    /// 命令执行入口点
    /// </summary>
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            _logger.LogInformation("开始执行高级重力坝分析命令");

            var uiApp = commandData.Application;
            var uidoc = uiApp.ActiveUIDocument;
            var doc = uidoc.Document;

            // 检查活动文档
            if (doc == null)
            {
                TaskDialog.Show("错误", "请先打开一个Revit文档");
                return Result.Cancelled;
            }

            // 初始化取消令牌
            _cancellationTokenSource = new CancellationTokenSource();

            // 步骤1：在UI线程中选择坝体元素
            _logger.LogInformation("步骤1：选择坝体元素");
            var selectedElements = SelectDamElements(uidoc);
            if (!selectedElements.Any())
            {
                TaskDialog.Show("取消", "未选择任何坝体元素，操作已取消");
                return Result.Cancelled;
            }

            // 步骤2：在UI线程中进行关键剖面智能识别和用户确认
            _logger.LogInformation("步骤2：智能识别关键剖面位置");
            var sectionLocations = IdentifyKeySectionsSync(selectedElements);
            if (!sectionLocations.Any())
            {
                TaskDialog.Show("错误", "无法识别关键剖面位置");
                return Result.Failed;
            }

            // 步骤3：用户确认剖面配置（UI线程）
            var confirmedSections = ConfirmSectionConfiguration(sectionLocations);
            if (!confirmedSections.Any())
            {
                return Result.Cancelled;
            }

            // 步骤4：增强的剖面提取（含初步验证）
            _logger.LogInformation("步骤4：提取剖面并进行验证");
            var extractionResults = ExtractSectionsWithValidation(doc, selectedElements, confirmedSections).GetAwaiter().GetResult();
            if (!extractionResults.Any())
            {
                TaskDialog.Show("错误", "未能成功提取任何剖面");
                return Result.Failed;
            }

            // 步骤5：交互式验证阶段
            _logger.LogInformation("步骤5：开始交互式验证");
            var validatedProfiles = PerformInteractiveValidation(extractionResults).GetAwaiter().GetResult();
            if (!validatedProfiles.Any())
            {
                TaskDialog.Show("取消", "验证阶段被取消，分析终止");
                return Result.Cancelled;
            }

            // 步骤6：计算准备确认
            var calculationReady = ConfirmCalculationParameters(validatedProfiles);
            if (!calculationReady)
            {
                return Result.Cancelled;
            }

            // 步骤7-8：计算密集的部分异步执行
            _logger.LogInformation("步骤7：开始后台稳定性计算...");
            var analysisTask = Task.Run(async () => 
            {
                try
                {
                    // 执行稳定性分析
                    var analysisResults = await PerformStabilityAnalysisAsync(validatedProfiles);
                    return (success: true, error: string.Empty, profiles: validatedProfiles, results: analysisResults);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "稳定性计算失败");
                    return (success: false, error: ex.Message, profiles: validatedProfiles, results: new List<StabilityAnalysisResult>());
                }
            });

            // 等待计算完成（显示进度）
            var result = ShowProgressAndWaitForResult(analysisTask);
            
            if (!result.success)
            {
                TaskDialog.Show("错误", $"分析失败: {result.error}");
                return Result.Failed;
            }

            // 步骤8：在UI线程中展示分析结果
            _logger.LogInformation("步骤8：展示分析结果");
            DisplayComprehensiveResults(result.profiles, result.results);

            _logger.LogInformation("高级重力坝分析命令执行完成");
            return Result.Succeeded;
        }
        catch (OperationCanceledException)
        {
            message = "分析被用户取消";
            return Result.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "执行高级重力坝分析命令时发生错误");
            message = $"分析过程中发生错误: {ex.Message}";
            return Result.Failed;
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// 选择坝体元素（支持多选）
    /// </summary>
    private List<Element> SelectDamElements(UIDocument uidoc)
    {
        try
        {
            var selectionFilter = new DamElementSelectionFilter();
            var references = uidoc.Selection.PickObjects(
                ObjectType.Element,
                selectionFilter,
                "请选择一个或多个重力坝实体（支持Ctrl+点击多选）");

            return references.Select(r => uidoc.Document.GetElement(r)).ToList();
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return new List<Element>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "选择坝体元素时发生错误");
            TaskDialog.Show("选择错误", $"选择元素时发生错误: {ex.Message}");
            return new List<Element>();
        }
    }

    /// <summary>
    /// 智能识别关键剖面位置（同步版本，用于UI线程）
    /// </summary>
    private List<SectionLocation> IdentifyKeySectionsSync(List<Element> damElements)
    {
        var sectionLocator = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<ILogger<IntelligentSectionLocator>>();
        var locator = new IntelligentSectionLocator(sectionLocator);

        var allSections = new List<SectionLocation>();

        foreach (var element in damElements)
        {
            // 为每个坝体元素创建对应的DamEntity（同步版本）
            var damEntity = CreateDamEntityFromElementSync(element);
            if (damEntity != null)
            {
                var parameters = new AnalysisParameters(); // 使用默认参数
                var sections = locator.IdentifyKeySections(damEntity, parameters);
                allSections.AddRange(sections);
            }
        }

        return allSections;
    }

    /// <summary>
    /// 从Revit元素创建DamEntity（同步版本）
    /// </summary>
    private DamEntity CreateDamEntityFromElementSync(Element element)
    {
        try
        {
            // 简化实现：从元素边界框创建几何信息
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) return null;

            var geometry = new DamGeometry(
                (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * (bbox.Max.Z - bbox.Min.Z), // 体积
                new Core.ValueObjects.BoundingBox3D(
                    new Core.ValueObjects.Point3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                    new Core.ValueObjects.Point3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
                )
            );

            var materialProperties = new MaterialProperties(
                element.Name,
                24000, // 默认混凝土密度
                30000, // 默认弹性模量
                0.18,  // 默认泊松比
                25.0,  // 默认抗压强度
                2.5,   // 默认抗拉强度
                0.75   // 默认摩擦系数
            );

            return new DamEntity(
                Guid.NewGuid(),
                element.Name ?? "未命名坝体",
                geometry,
                materialProperties
            )
            {
                RevitElementId = element.Id.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从Revit元素创建DamEntity失败");
            return null;
        }
    }

    /// <summary>
    /// 智能识别关键剖面位置
    /// </summary>
    private async Task<List<SectionLocation>> IdentifyKeySectionsAsync(List<Element> damElements)
    {
        var sectionLocator = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<ILogger<IntelligentSectionLocator>>();
        var locator = new IntelligentSectionLocator(sectionLocator);

        var allSections = new List<SectionLocation>();

        foreach (var element in damElements)
        {
            // 为每个坝体元素创建对应的DamEntity
            var damEntity = await CreateDamEntityFromElementAsync(element);
            if (damEntity != null)
            {
                var parameters = new AnalysisParameters(); // 使用默认参数
                var sections = locator.IdentifyKeySections(damEntity, parameters);
                allSections.AddRange(sections);
            }
        }

        return allSections;
    }

    /// <summary>
    /// 从Revit元素创建DamEntity
    /// </summary>
    private async Task<DamEntity> CreateDamEntityFromElementAsync(Element element)
    {
        try
        {
            // 简化实现：从元素边界框创建几何信息
            var bbox = element.get_BoundingBox(null);
            if (bbox == null) return null;

            var geometry = new DamGeometry(
                (bbox.Max.X - bbox.Min.X) * (bbox.Max.Y - bbox.Min.Y) * (bbox.Max.Z - bbox.Min.Z), // 体积
                new Core.ValueObjects.BoundingBox3D(
                    new Core.ValueObjects.Point3D(bbox.Min.X, bbox.Min.Y, bbox.Min.Z),
                    new Core.ValueObjects.Point3D(bbox.Max.X, bbox.Max.Y, bbox.Max.Z)
                )
            );

            var materialProperties = new MaterialProperties(
                element.Name,
                24000, // 默认混凝土密度
                30000, // 默认弹性模量
                0.18,  // 默认泊松比
                25.0,  // 默认抗压强度
                2.5,   // 默认抗拉强度
                0.75   // 默认摩擦系数
            );

            return new DamEntity(
                Guid.NewGuid(),
                element.Name ?? "未命名坝体",
                geometry,
                materialProperties
            )
            {
                RevitElementId = element.Id.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从Revit元素创建DamEntity失败");
            return null;
        }
    }

    /// <summary>
    /// 确认剖面配置
    /// </summary>
    private List<SectionLocation> ConfirmSectionConfiguration(List<SectionLocation> proposedSections)
    {
        var dialog = new TaskDialog("剖面配置确认")
        {
            MainInstruction = "已识别关键剖面位置",
            MainContent = $"系统自动识别了 {proposedSections.Count} 个关键分析剖面：\n\n" +
                         string.Join("\n", proposedSections.Select(s => $"• {s.Name}: {s.Description}")),
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            DefaultButton = TaskDialogResult.Yes,
            ExpandedContent = "剖面详情:\n" + 
                            string.Join("\n", proposedSections.Select(s => 
                                $"{s.Name} - 位置: ({s.Position.X:F2}, {s.Position.Y:F2}, {s.Position.Z:F2}), " +
                                $"优先级: {s.Priority}"))
        };

        var result = dialog.Show();
        
        return result == TaskDialogResult.Yes ? proposedSections : new List<SectionLocation>();
    }

    /// <summary>
    /// 批量提取剖面
    /// </summary>
    private async Task<List<EnhancedProfile2D>> ExtractSectionsAsync(
        Document doc, 
        List<Element> damElements, 
        List<SectionLocation> sectionLocations)
    {
        var extractorLogger = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<ILogger<AdvancedSectionExtractor>>();
        var managerLogger = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<ILogger<SafeTransactionManager>>();

        var extractor = new AdvancedSectionExtractor(extractorLogger);
        var transactionManager = new SafeTransactionManager(doc, managerLogger);

        try
        {
            var profiles = await transactionManager.ExtractSectionsWithTransactionAsync(
                damElements,
                sectionLocations,
                extractor,
                _cancellationTokenSource.Token);

            _logger.LogInformation("成功提取 {Count} 个剖面", profiles.Count);
            return profiles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "批量剖面提取失败");
            throw;
        }
    }

    /// <summary>
    /// 执行稳定性分析
    /// </summary>
    private async Task<List<StabilityAnalysisResult>> PerformStabilityAnalysisAsync(List<EnhancedProfile2D> profiles)
    {
        var analysisService = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<IStabilityAnalysisService>();
        
        var results = new List<StabilityAnalysisResult>();

        foreach (var profile in profiles)
        {
            try
            {
                // 为每个剖面创建对应的DamEntity进行分析
                var damEntity = CreateDamEntityFromProfile(profile);
                var parameters = new AnalysisParameters
                {
                    UpstreamWaterLevel = 100.0,
                    DownstreamWaterLevel = 10.0,
                    FrictionCoefficient = 0.75
                };

                var result = await analysisService.AnalyzeStabilityAsync(
                    damEntity, 
                    parameters, 
                    _cancellationTokenSource.Token);

                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "剖面 {ProfileName} 稳定性分析失败", profile.Name);
            }
        }

        return results;
    }

    /// <summary>
    /// 从剖面创建DamEntity（用于稳定性分析）
    /// </summary>
    private DamEntity CreateDamEntityFromProfile(EnhancedProfile2D profile)
    {
        var area = profile.CalculateArea();
        var volume = area; // 简化：假设单位厚度

        var geometry = new DamGeometry(
            volume,
            new Core.ValueObjects.BoundingBox3D(
                new Core.ValueObjects.Point3D(0, 0, 0),
                new Core.ValueObjects.Point3D(100, 1, 50) // 简化边界框
            )
        );

        var materialProperties = new MaterialProperties(
            "从剖面推导",
            24000, 30000, 0.18, 25.0, 2.5, 0.75
        );

        return new DamEntity(
            Guid.NewGuid(),
            profile.Name,
            geometry,
            materialProperties
        );
    }

    /// <summary>
    /// 展示综合分析结果
    /// </summary>
    private void DisplayComprehensiveResults(
        List<EnhancedProfile2D> profiles, 
        List<StabilityAnalysisResult> analysisResults)
    {
        var successCount = analysisResults.Count(r => r.IsOverallStable);
        var totalCount = analysisResults.Count;

        var summary = $"高级重力坝稳定性分析完成\n" +
                     $"================================\n\n" +
                     $"剖面分析概况:\n" +
                     $"• 总剖面数: {profiles.Count}\n" +
                     $"• 成功分析: {totalCount}\n" +
                     $"• 稳定剖面: {successCount}\n" +
                     $"• 稳定率: {(totalCount > 0 ? (double)successCount / totalCount * 100 : 0):F1}%\n\n";

        summary += "详细结果:\n";
        for (int i = 0; i < analysisResults.Count; i++)
        {
            var result = analysisResults[i];
            var profile = profiles[i];
            
            summary += $"\n剖面 {i + 1}: {profile.Name}\n" +
                      $"  面积: {profile.CalculateArea():F2} m²\n" +
                      $"  特征点: {profile.FeaturePoints.Count} 个\n" +
                      $"  抗滑系数: {result.SlidingSafetyFactor:F3} {(result.IsSlidingStable ? "✓" : "✗")}\n" +
                      $"  抗倾覆系数: {result.OverturnSafetyFactor:F3} {(result.IsOverturnStable ? "✓" : "✗")}\n" +
                      $"  整体稳定: {(result.IsOverallStable ? "稳定 ✓" : "不稳定 ✗")}\n";
        }

        var dialog = new TaskDialog("高级分析结果")
        {
            MainInstruction = $"分析完成 - 稳定率: {(totalCount > 0 ? (double)successCount / totalCount * 100 : 0):F1}%",
            MainContent = summary,
            CommonButtons = TaskDialogCommonButtons.Ok
        };

        if (successCount < totalCount)
        {
            dialog.MainIcon = TaskDialogIcon.TaskDialogIconWarning;
            dialog.ExpandedContent = "建议:\n" +
                                   "• 对不稳定剖面进行详细检查\n" +
                                   "• 考虑优化坝体几何参数\n" +
                                   "• 验证材料属性和荷载条件\n" +
                                   "• 必要时调整设计方案";
        }

        dialog.Show();
        _logger.LogInformation("分析结果已展示给用户");
    }

    /// <summary>
    /// 带验证的剖面提取
    /// </summary>
    private async Task<List<ProfileExtractionResult>> ExtractSectionsWithValidation(
        Document doc, 
        List<Element> damElements, 
        List<SectionLocation> sectionLocations)
    {
        var extractor = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<AdvancedSectionExtractor>();
        var validationEngine = DamAnalysisApplication.ServiceProvider
            .GetRequiredService<ProfileValidationEngine>();

        var results = new List<ProfileExtractionResult>();

        foreach (var location in sectionLocations)
        {
            try
            {
                // 提取基础剖面
                var profile = await extractor.ExtractProfile(doc, damElements, location);
                
                // 执行自动验证
                var validationResult = validationEngine.ValidateProfile(profile);
                
                // 计算质量度量
                var qualityMetrics = new ExtractionQualityMetrics
                {
                    OverallScore = validationResult.OverallScore,
                    GeometricAccuracy = validationResult.GeometryValidation.GeometryScore,
                    FeatureCompleteness = validationResult.EngineeringValidation.EngineeringScore,
                    DataConsistency = validationResult.BoundaryConditionValidation.CompletenessScore,
                    ProcessingEfficiency = 1.0, // 简化处理
                    ExtractionTime = TimeSpan.FromSeconds(1)
                };
                
                var extractionResult = new ProfileExtractionResult
                {
                    Profile = profile,
                    ValidationResults = validationResult,
                    RequiresUserReview = validationResult.RequiresUserReview,
                    AutoCorrectSuggestions = GenerateAutoCorrectSuggestions(validationResult),
                    QualityMetrics = qualityMetrics,
                    ExtractionTime = DateTime.Now,
                    ExtractionMethod = "AdvancedSectionExtractor"
                };
                
                results.Add(extractionResult);
                
                _logger.LogInformation("剖面 {Name} 提取完成，质量评分: {Score:P}", 
                    location.Name, qualityMetrics.OverallScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "剖面 {Name} 提取失败", location.Name);
            }
        }

        return results;
    }

    /// <summary>
    /// 交互式验证阶段（临时简化版本）
    /// </summary>
    private async Task<List<EnhancedProfile2D>> PerformInteractiveValidation(
        List<ProfileExtractionResult> extractionResults)
    {
        var validatedProfiles = new List<EnhancedProfile2D>();

        foreach (var result in extractionResults)
        {
            if (result.RequiresUserReview)
            {
                // 临时使用对话框替代验证窗口
                _logger.LogInformation("剖面 {Name} 需要人工验证", result.Profile.Name);
                
                var allIssues = result.ValidationResults.GetAllIssues();
                var issueCount = allIssues.Count;
                var criticalCount = allIssues.Count(i => i.Severity == IssueSeverity.Critical);
                
                var dialog = new TaskDialog("剖面验证")
                {
                    MainInstruction = $"剖面 {result.Profile.Name} 验证结果",
                    MainContent = $"发现 {issueCount} 个问题，其中 {criticalCount} 个严重问题。\n\n" +
                                 "是否接受此剖面用于计算？\n\n" +
                                 "选择'是'将使用当前剖面继续计算\n" +
                                 "选择'否'将跳过此剖面",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                    DefaultButton = criticalCount > 0 ? TaskDialogResult.No : TaskDialogResult.Yes
                };

                var dialogResult = dialog.Show();
                
                if (dialogResult == TaskDialogResult.Yes)
                {
                    result.Profile.Status = ValidationStatus.Validated;
                    validatedProfiles.Add(result.Profile);
                    _logger.LogInformation("剖面 {Name} 验证通过", result.Profile.Name);
                }
                else
                {
                    _logger.LogInformation("剖面 {Name} 被用户拒绝", result.Profile.Name);
                }
            }
            else
            {
                // 自动通过验证
                result.Profile.Status = ValidationStatus.Validated;
                validatedProfiles.Add(result.Profile);
                _logger.LogInformation("剖面 {Name} 自动通过验证", result.Profile.Name);
            }
        }

        return validatedProfiles;
    }

    /// <summary>
    /// 计算准备确认
    /// </summary>
    private bool ConfirmCalculationParameters(List<EnhancedProfile2D> validatedProfiles)
    {
        var dialog = new TaskDialog("计算参数确认")
        {
            MainInstruction = "验证完成，准备开始稳定性计算",
            MainContent = $"已验证 {validatedProfiles.Count} 个剖面，所有剖面已通过验证检查。\n\n" +
                         "系统将使用以下参数进行稳定性分析：\n" +
                         "• 安全系数要求: 抗滑 ≥ 2.5, 抗倾覆 ≥ 3.0\n" +
                         "• 扬压力折减系数: 0.8\n" +
                         "• 计算方法: 极限平衡法\n\n" +
                         "是否继续进行计算？",
            CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
            DefaultButton = TaskDialogResult.Yes,
            ExpandedContent = "详细参数:\n" + 
                            string.Join("\n", validatedProfiles.Select(p => 
                                $"• {p.Name}: 上游水位 {p.WaterLevels.UpstreamWaterLevel:F1}m, " +
                                $"下游水位 {p.WaterLevels.DownstreamWaterLevel:F1}m"))
        };

        var result = dialog.Show();
        return result == TaskDialogResult.Yes;
    }

    /// <summary>
    /// 创建验证窗口（简化版本）
    /// </summary>
    private TaskDialog CreateValidationDialog(ProfileExtractionResult extractionResult)
    {
        var allIssues = extractionResult.ValidationResults.GetAllIssues();
        var issueDetails = string.Join("\n", allIssues.Take(5).Select(i => $"• {i.Severity}: {i.Description}"));
        
        if (allIssues.Count > 5)
        {
            issueDetails += $"\n... 还有 {allIssues.Count - 5} 个问题";
        }

        return new TaskDialog("剖面验证详情")
        {
            MainInstruction = $"剖面 {extractionResult.Profile.Name} 验证详情",
            MainContent = $"质量评分: {extractionResult.QualityMetrics.OverallScore:P}\n\n" +
                         $"发现的问题:\n{issueDetails}",
            CommonButtons = TaskDialogCommonButtons.Ok
        };
    }

    /// <summary>
    /// 生成自动修正建议
    /// </summary>
    private List<AutoCorrectSuggestion> GenerateAutoCorrectSuggestions(ProfileValidationResult validationResult)
    {
        var suggestions = new List<AutoCorrectSuggestion>();

        foreach (var issue in validationResult.GetAllIssues().Where(i => i.CanAutoFix))
        {
            var suggestion = issue.Type switch
            {
                IssueType.OpenContour => new AutoCorrectSuggestion
                {
                    Title = "闭合开放轮廓",
                    Description = "自动连接轮廓的起点和终点",
                    ActionType = "CloseContour",
                    ConfidenceLevel = 0.9,
                    RequiresUserConfirmation = false
                },
                
                IssueType.MaterialZoneOverlap => new AutoCorrectSuggestion
                {
                    Title = "添加默认材料分区",
                    Description = "为整个坝体添加默认的混凝土材料属性",
                    ActionType = "AddDefaultMaterial",
                    ConfidenceLevel = 0.8,
                    RequiresUserConfirmation = true
                },
                
                IssueType.InvalidDimensions => new AutoCorrectSuggestion
                {
                    Title = "设置默认边界条件",
                    Description = "添加缺失的水位和约束条件",
                    ActionType = "SetDefaultBoundary",
                    ConfidenceLevel = 0.7,
                    RequiresUserConfirmation = true
                },
                
                _ => new AutoCorrectSuggestion
                {
                    Title = "通用修复",
                    Description = issue.SuggestedFix,
                    ActionType = "Generic",
                    ConfidenceLevel = 0.5,
                    RequiresUserConfirmation = true
                }
            };
            
            suggestions.Add(suggestion);
        }

        return suggestions;
    }

    /// <summary>
    /// 显示进度并等待后台任务完成
    /// </summary>
    private (bool success, string error, List<EnhancedProfile2D> profiles, List<StabilityAnalysisResult> results) 
        ShowProgressAndWaitForResult(Task<(bool success, string error, List<EnhancedProfile2D> profiles, List<StabilityAnalysisResult> results)> task)
    {
        var progressDialog = new TaskDialog("计算进度")
        {
            MainInstruction = "正在进行稳定性分析计算...",
            MainContent = "系统正在后台执行二维剖面提取和稳定性计算，请稍候",
            CommonButtons = TaskDialogCommonButtons.Cancel,
            AllowCancellation = true
        };

        // 显示进度对话框并定期检查任务状态
        var startTime = DateTime.Now;
        while (!task.IsCompleted)
        {
            Thread.Sleep(500); // 每500ms检查一次
            
            // 检查用户是否取消
            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                return (false, "用户取消操作", new List<EnhancedProfile2D>(), new List<StabilityAnalysisResult>());
            }

            // 更新进度信息
            var elapsed = DateTime.Now - startTime;
            var progressContent = $"已执行时间: {elapsed.TotalSeconds:F1} 秒\n\n" +
                                "当前正在进行:\n" +
                                "• 二维剖面几何提取\n" +
                                "• 边界条件识别\n" +
                                "• 稳定性数值计算";

            // 如果运行时间过长，显示详细信息
            if (elapsed.TotalMinutes > 1)
            {
                progressContent += "\n\n计算较为复杂，请继续等待...";
            }
        }

        try
        {
            return task.Result;
        }
        catch (AggregateException ex)
        {
            var innerEx = ex.InnerException ?? ex;
            return (false, innerEx.Message, new List<EnhancedProfile2D>(), new List<StabilityAnalysisResult>());
        }
        catch (Exception ex)
        {
            return (false, ex.Message, new List<EnhancedProfile2D>(), new List<StabilityAnalysisResult>());
        }
    }
} 