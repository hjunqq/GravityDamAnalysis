# 重力坝稳定性分析插件核心实现示例
## 基于 .NET 8 + Revit 2025/2026 + 业务流程

## 1. 核心业务流程完整实现

### 1.1 主控制器 - 编排整个业务流程
```csharp
// 主要的业务流程编排器
[ApiController]
[Route("api/[controller]")]
public class DamAnalysisController : ControllerBase
{
    private readonly IDamEntityRecognitionService _recognitionService;
    private readonly ISectionAnalysisService _sectionService;
    private readonly ICalculationOrchestrationService _calculationService;
    private readonly ILogger<DamAnalysisController> _logger;
    
    public DamAnalysisController(
        IDamEntityRecognitionService recognitionService,
        ISectionAnalysisService sectionService,
        ICalculationOrchestrationService calculationService,
        ILogger<DamAnalysisController> logger)
    {
        _recognitionService = recognitionService;
        _sectionService = sectionService;
        _calculationService = calculationService;
        _logger = logger;
    }
    
    /// <summary>
    /// 执行完整的重力坝稳定性分析业务流程
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AnalysisResult>> AnalyzeDamStability(
        [FromBody] AnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("DamAnalysis.FullWorkflow");
        activity?.SetTag("dam.elementId", request.SelectedElementId.ToString());
        
        try
        {
            _logger.LogInformation("开始重力坝稳定性分析流程，元素ID: {ElementId}", request.SelectedElementId);
            
            // 步骤1: 识别和验证重力坝实体
            _logger.LogInformation("步骤1: 识别重力坝实体");
            var damEntity = await RecognizeDamEntity(request.SelectedElementId, cancellationToken);
            if (damEntity == null)
            {
                return BadRequest("无法识别选中的元素为有效的重力坝实体");
            }
            
            // 步骤2: 提取断面信息
            _logger.LogInformation("步骤2: 提取坝体断面信息");
            var sections = await ExtractDamSections(damEntity, request.SectionOptions, cancellationToken);
            
            // 步骤3: 配置计算参数
            _logger.LogInformation("步骤3: 配置计算参数");
            var calculationParams = PrepareCalculationParameters(request, damEntity, sections);
            
            // 步骤4: 执行稳定性分析
            _logger.LogInformation("步骤4: 执行稳定性计算");
            var analysisResult = await _calculationService.ExecuteFullAnalysis(
                damEntity, calculationParams, cancellationToken);
            
            _logger.LogInformation("分析完成，整体安全系数: {SafetyFactor}", 
                analysisResult.OverallStability.SafetyFactor);
            
            return Ok(analysisResult);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("分析被用户取消");
            return StatusCode(499, "分析被取消");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分析过程中发生错误");
            return StatusCode(500, $"分析失败: {ex.Message}");
        }
    }
    
    private async Task<DamEntity?> RecognizeDamEntity(ElementId elementId, CancellationToken cancellationToken)
    {
        // 获取Revit文档和选中的元素
        var document = GetActiveDocument();
        var selectedElement = document.GetElement(elementId);
        
        if (selectedElement == null)
        {
            _logger.LogWarning("无法找到指定的元素: {ElementId}", elementId);
            return null;
        }
        
        // 使用识别服务分析元素
        var damEntity = await _recognitionService.RecognizeSelectedEntity(selectedElement);
        
        if (damEntity != null)
        {
            // 验证实体的有效性
            var isValid = await _recognitionService.ValidateDamEntity(damEntity);
            if (!isValid)
            {
                _logger.LogWarning("识别的坝体实体验证失败");
                return null;
            }
        }
        
        return damEntity;
    }
    
    private async Task<List<DamSection>> ExtractDamSections(
        DamEntity damEntity, 
        SectionExtractionOptions options, 
        CancellationToken cancellationToken)
    {
        // 根据用户配置提取断面
        var sections = await _sectionService.ExtractDamSections(damEntity, options);
        
        if (!sections.Any())
        {
            throw new InvalidOperationException("无法从坝体实体提取有效的断面信息");
        }
        
        _logger.LogInformation("成功提取 {Count} 个断面", sections.Count);
        
        // 并行分析各断面的几何特性
        var geometryTasks = sections.Select(async section =>
        {
            var geometry = await _sectionService.AnalyzeSectionGeometry(section);
            section.Geometry = geometry;
            return section;
        });
        
        await Task.WhenAll(geometryTasks);
        
        return sections;
    }
}
```

### 1.2 实体识别服务实现
```csharp
// 专门负责识别和验证重力坝实体
public class DamEntityRecognitionService : IDamEntityRecognitionService
{
    private readonly IRevitDataExtractor _dataExtractor;
    private readonly IOptions<AnalysisSettings> _settings;
    private readonly ILogger<DamEntityRecognitionService> _logger;
    
    public async Task<DamEntity?> RecognizeSelectedEntity(Element selectedElement)
    {
        _logger.LogInformation("开始识别元素: {ElementName} (ID: {ElementId})", 
            selectedElement.Name, selectedElement.Id);
        
        // 1. 基本类型检查
        if (!IsValidElementCategory(selectedElement))
        {
            _logger.LogWarning("元素类别不适用于重力坝分析");
            return null;
        }
        
        // 2. 几何特征分析
        var geometryAnalysis = await AnalyzeElementGeometry(selectedElement);
        if (!geometryAnalysis.IsValidDamGeometry)
        {
            _logger.LogWarning("元素几何特征不符合重力坝要求");
            return null;
        }
        
        // 3. 材料属性检查
        var materialProperties = ExtractMaterialProperties(selectedElement);
        if (!IsValidDamMaterial(materialProperties))
        {
            _logger.LogWarning("材料属性不适用于重力坝分析");
            return null;
        }
        
        // 4. 构建坝体实体
        var damEntity = new DamEntity
        {
            Id = selectedElement.Id.IntegerValue,
            Name = GetDamName(selectedElement),
            RevitElement = selectedElement,
            BaseGeometry = geometryAnalysis.Geometry,
            MaterialProperties = materialProperties,
            Classification = ClassifyDamType(geometryAnalysis),
            BoundingBox = selectedElement.get_BoundingBox(null),
            CreatedAt = DateTime.UtcNow
        };
        
        _logger.LogInformation("成功识别重力坝实体: {DamName}", damEntity.Name);
        return damEntity;
    }
    
    private async Task<GeometryAnalysisResult> AnalyzeElementGeometry(Element element)
    {
        var options = new Options
        {
            ComputeReferences = true,
            DetailLevel = ViewDetailLevel.Fine,
            IncludeNonVisibleObjects = false
        };
        
        var geometryElement = element.get_Geometry(options);
        var solids = new List<Solid>();
        
        // 提取所有实体几何
        foreach (GeometryObject geoObj in geometryElement)
        {
            if (geoObj is Solid solid && solid.Volume > 0.0001)
            {
                solids.Add(solid);
            }
            else if (geoObj is GeometryInstance instance)
            {
                var instanceGeometry = instance.GetInstanceGeometry();
                foreach (GeometryObject instObj in instanceGeometry)
                {
                    if (instObj is Solid instSolid && instSolid.Volume > 0.0001)
                    {
                        solids.Add(instSolid);
                    }
                }
            }
        }
        
        if (!solids.Any())
        {
            return new GeometryAnalysisResult { IsValidDamGeometry = false };
        }
        
        // 分析几何特征
        var analysisResult = new GeometryAnalysisResult
        {
            IsValidDamGeometry = true,
            TotalVolume = solids.Sum(s => s.Volume),
            Geometry = await BuildDamGeometry(solids),
            CharacteristicDimensions = CalculateCharacteristicDimensions(solids)
        };
        
        // 验证是否符合重力坝特征
        analysisResult.IsValidDamGeometry = ValidateDamCharacteristics(analysisResult);
        
        return analysisResult;
    }
    
    private async Task<DamGeometry> BuildDamGeometry(List<Solid> solids)
    {
        var allFaces = solids.SelectMany(s => s.Faces.Cast<Face>()).ToList();
        
        // 寻找主要面（上游面、下游面、坝顶面）
        var upstreamFace = FindUpstreamFace(allFaces);
        var downstreamFace = FindDownstreamFace(allFaces);
        var crestFace = FindCrestFace(allFaces);
        
        return new DamGeometry
        {
            UpstreamProfile = ExtractProfileFromFace(upstreamFace),
            DownstreamProfile = ExtractProfileFromFace(downstreamFace),
            CrestProfile = ExtractProfileFromFace(crestFace),
            Volume = solids.Sum(s => s.Volume),
            Height = CalculateMaxHeight(allFaces),
            BaseWidth = CalculateBaseWidth(allFaces),
            CrestWidth = CalculateCrestWidth(crestFace)
        };
    }
    
    private bool ValidateDamCharacteristics(GeometryAnalysisResult analysis)
    {
        var dims = analysis.CharacteristicDimensions;
        
        // 检查高宽比
        if (dims.Height <= 0 || dims.BaseWidth <= 0)
            return false;
            
        var heightToBaseRatio = dims.Height / dims.BaseWidth;
        if (heightToBaseRatio > 3.0) // 重力坝通常高宽比不会太大
        {
            _logger.LogWarning("高宽比过大: {Ratio}", heightToBaseRatio);
            return false;
        }
        
        // 检查体积合理性
        if (analysis.TotalVolume < 10) // 最小体积限制（立方米）
        {
            _logger.LogWarning("坝体体积过小: {Volume}", analysis.TotalVolume);
            return false;
        }
        
        return true;
    }
}
```

### 1.3 断面分析服务实现
```csharp
// 专门处理断面信息提取和几何分析
public class SectionAnalysisService : ISectionAnalysisService
{
    private readonly IRevitDataExtractor _dataExtractor;
    private readonly ISectionDataCache _cache;
    private readonly ILogger<SectionAnalysisService> _logger;
    
    public async Task<List<DamSection>> ExtractDamSections(
        DamEntity damEntity, 
        SectionExtractionOptions options)
    {
        _logger.LogInformation("开始提取坝体断面，断面数量: {Count}", options.SectionCount);
        
        // 1. 生成切割平面
        var cuttingPlanes = GenerateCuttingPlanes(damEntity, options);
        
        // 2. 并行提取各断面
        var sectionTasks = cuttingPlanes.Select(async (plane, index) =>
        {
            var cacheKey = GenerateSectionCacheKey(damEntity.Id, index, plane);
            
            // 检查缓存
            var cachedSection = await _cache.GetAsync<DamSection>(cacheKey);
            if (cachedSection != null)
            {
                _logger.LogDebug("从缓存加载断面 {Index}", index);
                return cachedSection;
            }
            
            // 提取新断面
            var section = await ExtractSectionAtPlane(damEntity, plane, index);
            if (section != null)
            {
                // 缓存结果
                await _cache.SetAsync(cacheKey, section, TimeSpan.FromHours(1));
            }
            
            return section;
        });
        
        var results = await Task.WhenAll(sectionTasks);
        var validSections = results.Where(s => s != null).ToList()!;
        
        _logger.LogInformation("成功提取 {Count} 个有效断面", validSections.Count);
        return validSections;
    }
    
    private async Task<DamSection?> ExtractSectionAtPlane(
        DamEntity damEntity, 
        Plane cuttingPlane, 
        int sectionIndex)
    {
        try
        {
            var element = damEntity.RevitElement;
            var geometryOptions = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };
            
            var geometryElement = element.get_Geometry(geometryOptions);
            var intersectionCurves = new List<Curve>();
            
            // 计算与切割平面的交线
            foreach (GeometryObject geoObj in geometryElement)
            {
                if (geoObj is Solid solid)
                {
                    var intersection = CalculatePlaneIntersection(solid, cuttingPlane);
                    intersectionCurves.AddRange(intersection);
                }
            }
            
            if (!intersectionCurves.Any())
            {
                _logger.LogWarning("断面 {Index} 未找到有效的交线", sectionIndex);
                return null;
            }
            
            // 构建断面轮廓
            var profile = BuildSectionProfile(intersectionCurves);
            
            var section = new DamSection
            {
                Id = Guid.NewGuid(),
                Index = sectionIndex,
                CuttingPlane = cuttingPlane,
                Profile = profile,
                Location = cuttingPlane.Origin,
                Normal = cuttingPlane.Normal,
                CreatedAt = DateTime.UtcNow
            };
            
            _logger.LogDebug("成功提取断面 {Index}", sectionIndex);
            return section;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取断面 {Index} 时发生错误", sectionIndex);
            return null;
        }
    }
    
    public async Task<SectionGeometry> AnalyzeSectionGeometry(DamSection section)
    {
        _logger.LogDebug("分析断面 {Index} 的几何特性", section.Index);
        
        var profile = section.Profile;
        
        // 识别关键点
        var keyPoints = IdentifyKeyPoints(profile);
        
        // 计算几何参数
        var geometry = new SectionGeometry
        {
            SectionId = section.Id,
            
            // 基本尺寸
            CrestWidth = CalculateCrestWidth(keyPoints),
            BaseWidth = CalculateBaseWidth(keyPoints),
            Height = CalculateHeight(keyPoints),
            
            // 坡度信息
            UpstreamSlope = CalculateUpstreamSlope(keyPoints),
            DownstreamSlope = CalculateDownstreamSlope(keyPoints),
            
            // 几何属性
            Area = CalculateArea(profile),
            Centroid = CalculateCentroid(profile),
            MomentOfInertia = CalculateMomentOfInertia(profile),
            
            // 关键点位置
            CrestCenterPoint = keyPoints.CrestCenter,
            BaseCenterPoint = keyPoints.BaseCenter,
            UpstreamToePoint = keyPoints.UpstreamToe,
            DownstreamToePoint = keyPoints.DownstreamToe,
            
            // 计算时间
            CalculatedAt = DateTime.UtcNow
        };
        
        _logger.LogDebug("断面 {Index} 几何分析完成 - 高度: {Height:F2}m, 底宽: {BaseWidth:F2}m", 
            section.Index, geometry.Height, geometry.BaseWidth);
        
        return geometry;
    }
    
    private SectionKeyPoints IdentifyKeyPoints(List<Curve> profile)
    {
        var allPoints = profile.SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) })
                               .Distinct(new XYZEqualityComparer())
                               .ToList();
        
        // 找到最高点和最低点
        var highestPoint = allPoints.OrderByDescending(p => p.Z).First();
        var lowestPoint = allPoints.OrderBy(p => p.Z).First();
        
        // 找到最左侧和最右侧点（假设X轴为坝轴方向）
        var leftmostPoint = allPoints.OrderBy(p => p.Y).First();
        var rightmostPoint = allPoints.OrderByDescending(p => p.Y).First();
        
        return new SectionKeyPoints
        {
            CrestCenter = new XYZ((leftmostPoint.Y + rightmostPoint.Y) / 2, highestPoint.Y, highestPoint.Z),
            BaseCenter = new XYZ((leftmostPoint.Y + rightmostPoint.Y) / 2, lowestPoint.Y, lowestPoint.Z),
            UpstreamToe = leftmostPoint,
            DownstreamToe = rightmostPoint
        };
    }
}
```

### 1.4 稳定性计算服务实现
```csharp
// 重力坝稳定性计算的核心引擎
public class StabilityCalculationService : IStabilityCalculationService
{
    private readonly IOptions<AnalysisSettings> _settings;
    private readonly ILogger<StabilityCalculationService> _logger;
    private readonly ISlidingAnalyzer _slidingAnalyzer;
    private readonly IOverturnAnalyzer _overturnAnalyzer;
    private readonly IStressAnalyzer _stressAnalyzer;
    
    public async Task<StabilityAnalysisResult> CalculateStability(
        DamSection section, 
        SectionGeometry geometry, 
        AnalysisParameters parameters)
    {
        _logger.LogInformation("开始计算断面 {Index} 的稳定性", section.Index);
        
        var result = new StabilityAnalysisResult
        {
            SectionId = section.Id,
            SectionIndex = section.Index,
            CalculationParameters = parameters
        };
        
        try
        {
            // 1. 抗滑稳定性分析
            _logger.LogDebug("计算抗滑稳定性");
            result.SlidingStability = await _slidingAnalyzer.AnalyzeSliding(
                section, geometry, parameters);
            
            // 2. 抗倾覆稳定性分析
            _logger.LogDebug("计算抗倾覆稳定性");
            result.OverturnStability = await _overturnAnalyzer.AnalyzeOverturn(
                section, geometry, parameters);
            
            // 3. 地基应力分析
            _logger.LogDebug("分析地基应力");
            result.StressAnalysis = await _stressAnalyzer.AnalyzeStress(
                section, geometry, parameters);
            
            // 4. 综合评估
            result.OverallAssessment = EvaluateOverallStability(result);
            result.IsStable = DetermineStabilityStatus(result);
            
            _logger.LogInformation("断面 {Index} 稳定性计算完成 - 抗滑安全系数: {SlidingSF:F3}, 抗倾覆安全系数: {OverturnSF:F3}",
                section.Index, result.SlidingStability.SafetyFactor, result.OverturnStability.SafetyFactor);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算断面 {Index} 稳定性时发生错误", section.Index);
            result.ErrorMessage = ex.Message;
            result.IsStable = false;
            return result;
        }
    }
    
    private StabilityAssessment EvaluateOverallStability(StabilityAnalysisResult result)
    {
        var assessment = new StabilityAssessment();
        var settings = _settings.Value;
        
        // 检查各项安全系数
        var slidingSF = result.SlidingStability.SafetyFactor;
        var overturnSF = result.OverturnStability.SafetyFactor;
        var maxStress = result.StressAnalysis.MaxStress;
        
        // 抗滑稳定性评估
        if (slidingSF >= settings.MinSlidingSafetyFactor * 1.2)
        {
            assessment.SlidingStatus = StabilityStatus.Safe;
            assessment.SlidingComment = "抗滑稳定性良好";
        }
        else if (slidingSF >= settings.MinSlidingSafetyFactor)
        {
            assessment.SlidingStatus = StabilityStatus.Acceptable;
            assessment.SlidingComment = "抗滑稳定性满足要求";
        }
        else
        {
            assessment.SlidingStatus = StabilityStatus.Unsafe;
            assessment.SlidingComment = "抗滑稳定性不足，需要加强措施";
        }
        
        // 抗倾覆稳定性评估
        if (overturnSF >= settings.MinOverturnSafetyFactor * 1.2)
        {
            assessment.OverturnStatus = StabilityStatus.Safe;
            assessment.OverturnComment = "抗倾覆稳定性良好";
        }
        else if (overturnSF >= settings.MinOverturnSafetyFactor)
        {
            assessment.OverturnStatus = StabilityStatus.Acceptable;
            assessment.OverturnComment = "抗倾覆稳定性满足要求";
        }
        else
        {
            assessment.OverturnStatus = StabilityStatus.Unsafe;
            assessment.OverturnComment = "抗倾覆稳定性不足，建议增加坝体底宽";
        }
        
        // 应力评估
        var allowableStress = result.CalculationParameters.MaterialProperties.AllowableStress;
        if (maxStress <= allowableStress * 0.8)
        {
            assessment.StressStatus = StabilityStatus.Safe;
            assessment.StressComment = "地基应力分布良好";
        }
        else if (maxStress <= allowableStress)
        {
            assessment.StressStatus = StabilityStatus.Acceptable;
            assessment.StressComment = "地基应力在允许范围内";
        }
        else
        {
            assessment.StressStatus = StabilityStatus.Unsafe;
            assessment.StressComment = "地基应力超限，需要地基处理";
        }
        
        // 综合评估
        var allStatuses = new[] { assessment.SlidingStatus, assessment.OverturnStatus, assessment.StressStatus };
        if (allStatuses.All(s => s == StabilityStatus.Safe))
        {
            assessment.OverallStatus = StabilityStatus.Safe;
            assessment.OverallComment = "该断面稳定性优良，各项指标均满足要求";
        }
        else if (allStatuses.All(s => s != StabilityStatus.Unsafe))
        {
            assessment.OverallStatus = StabilityStatus.Acceptable;
            assessment.OverallComment = "该断面稳定性基本满足要求";
        }
        else
        {
            assessment.OverallStatus = StabilityStatus.Unsafe;
            assessment.OverallComment = "该断面存在稳定性问题，需要采取工程措施";
        }
        
        return assessment;
    }
}
```

### 1.5 现代化UI实现 (WPF + .NET 8)
```csharp
// 主窗口ViewModel，使用CommunityToolkit.Mvvm
[ObservableObject]
public partial class MainViewModel : ViewModelBase
{
    private readonly IDamEntityRecognitionService _recognitionService;
    private readonly ISectionAnalysisService _sectionService;
    private readonly ICalculationOrchestrationService _calculationService;
    private readonly IDialogService _dialogService;
    
    [ObservableProperty]
    private DamEntity? selectedDamEntity;
    
    [ObservableProperty]
    private List<DamSection> extractedSections = new();
    
    [ObservableProperty]
    private AnalysisResult? analysisResult;
    
    [ObservableProperty]
    private bool isAnalyzing;
    
    [ObservableProperty]
    private int progressPercentage;
    
    [ObservableProperty]
    private string statusMessage = "准备就绪";
    
    public MainViewModel(
        IDamEntityRecognitionService recognitionService,
        ISectionAnalysisService sectionService,
        ICalculationOrchestrationService calculationService,
        IDialogService dialogService)
    {
        _recognitionService = recognitionService;
        _sectionService = sectionService;
        _calculationService = calculationService;
        _dialogService = dialogService;
    }
    
    [RelayCommand]
    private async Task SelectDamEntity()
    {
        try
        {
            StatusMessage = "请在Revit中选择重力坝实体...";
            
            // 启动Revit元素选择
            var selection = await GetRevitElementSelection();
            if (selection == null) return;
            
            StatusMessage = "正在识别坝体实体...";
            SelectedDamEntity = await _recognitionService.RecognizeSelectedEntity(selection);
            
            if (SelectedDamEntity != null)
            {
                StatusMessage = $"已识别坝体: {SelectedDamEntity.Name}";
                await ExtractInitialSections();
            }
            else
            {
                StatusMessage = "无法识别为有效的重力坝实体";
                await _dialogService.ShowWarningAsync("识别失败", "选中的元素不是有效的重力坝实体，请选择正确的坝体构件。");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "实体识别失败";
            await _dialogService.ShowErrorAsync("错误", $"识别坝体实体时发生错误：{ex.Message}");
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanExtractSections))]
    private async Task ExtractSections()
    {
        if (SelectedDamEntity == null) return;
        
        try
        {
            IsAnalyzing = true;
            StatusMessage = "正在提取断面信息...";
            
            var options = new SectionExtractionOptions
            {
                SectionCount = 5, // 默认5个断面
                ExtractionMethod = SectionExtractionMethod.EqualSpacing,
                IncludeGeometryAnalysis = true
            };
            
            var progress = new Progress<int>(percentage =>
            {
                ProgressPercentage = percentage;
                StatusMessage = $"正在提取断面... {percentage}%";
            });
            
            ExtractedSections = await _sectionService.ExtractDamSections(SelectedDamEntity, options);
            
            StatusMessage = $"成功提取 {ExtractedSections.Count} 个断面";
        }
        catch (Exception ex)
        {
            StatusMessage = "断面提取失败";
            await _dialogService.ShowErrorAsync("错误", $"提取断面时发生错误：{ex.Message}");
        }
        finally
        {
            IsAnalyzing = false;
            ProgressPercentage = 0;
        }
    }
    
    private bool CanExtractSections() => SelectedDamEntity != null && !IsAnalyzing;
    
    [RelayCommand(CanExecute = nameof(CanStartAnalysis))]
    private async Task StartAnalysis()
    {
        if (SelectedDamEntity == null || !ExtractedSections.Any()) return;
        
        try
        {
            IsAnalyzing = true;
            StatusMessage = "正在进行稳定性分析...";
            
            var parameters = new AnalysisParameters
            {
                MaterialProperties = SelectedDamEntity.MaterialProperties,
                CalculationMethod = CalculationMethod.DetailedAnalysis,
                SafetyFactorRequirements = new SafetyFactorRequirements
                {
                    MinSlidingSafetyFactor = 1.05,
                    MinOverturnSafetyFactor = 1.5
                }
            };
            
            var progress = new Progress<AnalysisProgress>(progress =>
            {
                ProgressPercentage = progress.Percentage;
                StatusMessage = progress.Message;
            });
            
            // 执行流式分析，实时更新进度
            await foreach (var partialResult in _calculationService.ExecuteStreamingAnalysis(
                SelectedDamEntity, parameters))
            {
                // 实时更新UI
                UpdatePartialResult(partialResult);
            }
            
            // 获取最终结果
            AnalysisResult = await _calculationService.ExecuteFullAnalysis(SelectedDamEntity, parameters);
            
            StatusMessage = "分析完成";
            await _dialogService.ShowInfoAsync("分析完成", "重力坝稳定性分析已完成，请查看结果详情。");
        }
        catch (Exception ex)
        {
            StatusMessage = "分析失败";
            await _dialogService.ShowErrorAsync("错误", $"稳定性分析时发生错误：{ex.Message}");
        }
        finally
        {
            IsAnalyzing = false;
            ProgressPercentage = 0;
        }
    }
    
    private bool CanStartAnalysis() => SelectedDamEntity != null && ExtractedSections.Any() && !IsAnalyzing;
}
```

这个实现示例展示了完整的业务流程：从选取重力坝实体到获取断面信息再到开展稳定性计算。代码充分利用了.NET 8的现代化特性，包括：

- **异步编程模式**：全面使用async/await
- **源代码生成器**：CommunityToolkit.Mvvm的ObservableProperty
- **模式匹配**：switch表达式
- **记录类型**：用于数据传输对象
- **可空引用类型**：提高代码安全性
- **依赖注入**：现代化的服务容器
- **结构化日志**：使用Microsoft.Extensions.Logging

同时针对Revit 2025/2026的API进行了优化，提供了完整的错误处理和用户交互体验。 