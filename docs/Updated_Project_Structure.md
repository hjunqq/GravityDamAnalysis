# 重力坝稳定性分析 Revit 插件项目结构 v2.0
## 基于 .NET 8 + Revit 2025/2026 + 业务流程导向

## 推荐的现代化项目组织结构

```
GravityDamAnalysis/
├── src/
│   ├── GravityDamAnalysis.Core/                     # 核心业务逻辑 (.NET 8)
│   │   ├── Entities/                                # 领域实体
│   │   │   ├── DamEntity.cs
│   │   │   ├── DamSection.cs
│   │   │   ├── SectionGeometry.cs
│   │   │   └── AnalysisResult.cs
│   │   ├── Services/                                # 业务服务
│   │   │   ├── Recognition/
│   │   │   │   ├── IDamEntityRecognitionService.cs
│   │   │   │   └── DamEntityRecognitionService.cs
│   │   │   ├── SectionAnalysis/
│   │   │   │   ├── ISectionAnalysisService.cs
│   │   │   │   ├── SectionAnalysisService.cs
│   │   │   │   └── SectionGeometryCalculator.cs
│   │   │   ├── Calculation/
│   │   │   │   ├── IStabilityCalculationService.cs
│   │   │   │   ├── StabilityCalculationService.cs
│   │   │   │   ├── SlidingStabilityCalculator.cs
│   │   │   │   └── OverturnStabilityCalculator.cs
│   │   │   └── Orchestration/
│   │   │       ├── ICalculationOrchestrationService.cs
│   │   │       └── CalculationOrchestrationService.cs
│   │   ├── Models/                                  # 数据传输对象
│   │   │   ├── AnalysisParameters.cs
│   │   │   ├── SectionExtractionOptions.cs
│   │   │   ├── StabilityResult.cs
│   │   │   └── MaterialProperties.cs
│   │   ├── ValueObjects/                            # 值对象
│   │   │   ├── Point3D.cs
│   │   │   ├── SafetyFactor.cs
│   │   │   └── DamClassification.cs
│   │   └── Interfaces/                              # 核心接口
│   │       ├── IRevitDataExtractor.cs
│   │       ├── ISectionDataCache.cs
│   │       └── IResultProcessor.cs
│   │
│   ├── GravityDamAnalysis.Infrastructure/           # 基础设施层 (.NET 8)
│   │   ├── Revit/                                   # Revit 2025/2026 集成
│   │   │   ├── DataExtraction/
│   │   │   │   ├── ModernRevitDataExtractor.cs
│   │   │   │   ├── ElementSelectionHandler.cs
│   │   │   │   └── GeometryAnalyzer.cs
│   │   │   ├── Transactions/
│   │   │   │   ├── TransactionManager.cs
│   │   │   │   └── BatchTransactionHandler.cs
│   │   │   └── Utilities/
│   │   │       ├── RevitGeometryHelper.cs
│   │   │       └── CoordinateTransformer.cs
│   │   ├── Caching/                                 # 高性能缓存
│   │   │   ├── HighPerformanceSectionCache.cs
│   │   │   ├── RedisSectionDataCache.cs
│   │   │   └── CacheKeyGenerator.cs
│   │   ├── Configuration/                           # 配置管理
│   │   │   ├── AnalysisSettings.cs
│   │   │   ├── RevitSettings.cs
│   │   │   └── ConfigurationExtensions.cs
│   │   ├── Logging/                                 # 结构化日志
│   │   │   ├── LoggingExtensions.cs
│   │   │   └── TelemetryCollector.cs
│   │   └── Serialization/                           # 序列化
│   │       ├── JsonConverters/
│   │       └── SerializationSettings.cs
│   │
│   ├── GravityDamAnalysis.UI/                       # 用户界面 (WPF + .NET 8)
│   │   ├── ViewModels/                              # MVVM 视图模型
│   │   │   ├── Base/
│   │   │   │   ├── ViewModelBase.cs
│   │   │   │   └── AsyncRelayCommand.cs
│   │   │   ├── MainViewModel.cs
│   │   │   ├── EntitySelectionViewModel.cs
│   │   │   ├── SectionConfigurationViewModel.cs
│   │   │   ├── CalculationParametersViewModel.cs
│   │   │   └── ResultsViewModel.cs
│   │   ├── Views/                                   # WPF 视图
│   │   │   ├── MainWindow.xaml
│   │   │   ├── EntitySelectionView.xaml
│   │   │   ├── SectionConfigurationView.xaml
│   │   │   ├── CalculationParametersView.xaml
│   │   │   └── ResultsView.xaml
│   │   ├── Controls/                                # 自定义控件
│   │   │   ├── DamVisualizationControl.xaml
│   │   │   ├── SectionProfileViewer.xaml
│   │   │   ├── StabilityResultChart.xaml
│   │   │   └── ProgressIndicator.xaml
│   │   ├── Converters/                              # 值转换器
│   │   │   ├── SafetyFactorToColorConverter.cs
│   │   │   ├── BoolToVisibilityConverter.cs
│   │   │   └── DoubleToFormattedStringConverter.cs
│   │   ├── Services/                                # UI 服务
│   │   │   ├── IDialogService.cs
│   │   │   ├── DialogService.cs
│   │   │   ├── IResultVisualizationService.cs
│   │   │   └── ResultVisualizationService.cs
│   │   ├── Behaviors/                               # WPF 行为
│   │   │   ├── DragDropBehavior.cs
│   │   │   └── ValidationBehavior.cs
│   │   └── Resources/                               # 资源文件
│   │       ├── Styles/
│   │       ├── Templates/
│   │       └── Images/
│   │
│   ├── GravityDamAnalysis.Revit/                    # Revit 插件入口
│   │   ├── Commands/                                # Revit 命令
│   │   │   ├── StartAnalysisCommand.cs
│   │   │   ├── SelectDamEntityCommand.cs
│   │   │   ├── ConfigureSectionsCommand.cs
│   │   │   └── ViewResultsCommand.cs
│   │   ├── Application/                             # 插件应用程序
│   │   │   ├── DamAnalysisApplication.cs
│   │   │   ├── DamAnalysisHost.cs
│   │   │   └── PluginUpdater.cs
│   │   ├── Ribbon/                                  # Revit 界面
│   │   │   ├── RibbonManager.cs
│   │   │   └── ContextMenuHandler.cs
│   │   ├── Selection/                               # 选择处理
│   │   │   ├── ElementSelectionFilter.cs
│   │   │   └── SelectionEventHandler.cs
│   │   └── Resources/                               # 插件资源
│   │       ├── Icons/
│   │       ├── manifest.addin
│   │       └── PluginInfo.cs
│   │
│   ├── GravityDamAnalysis.Calculation/              # 计算引擎 (.NET 8)
│   │   ├── Engines/                                 # 计算引擎
│   │   │   ├── StabilityAnalysisEngine.cs
│   │   │   ├── FiniteElementEngine.cs
│   │   │   └── NumericalMethodsEngine.cs
│   │   ├── Algorithms/                              # 算法实现
│   │   │   ├── SlidingStability/
│   │   │   │   ├── ISlidingAnalyzer.cs
│   │   │   │   ├── ClassicalSlidingAnalyzer.cs
│   │   │   │   └── AdvancedSlidingAnalyzer.cs
│   │   │   ├── OverturnStability/
│   │   │   │   ├── IOverturnAnalyzer.cs
│   │   │   │   └── OverturnAnalyzer.cs
│   │   │   └── StressAnalysis/
│   │   │       ├── IStressAnalyzer.cs
│   │   │       ├── LinearStressAnalyzer.cs
│   │   │       └── NonlinearStressAnalyzer.cs
│   │   ├── Solvers/                                 # 数值求解器
│   │   │   ├── LinearSolver.cs
│   │   │   ├── NonlinearSolver.cs
│   │   │   └── IterativeSolver.cs
│   │   └── Validation/                              # 计算验证
│   │       ├── ResultValidator.cs
│   │       └── ConvergenceChecker.cs
│   │
│   └── GravityDamAnalysis.Reports/                  # 报告生成 (.NET 8)
│       ├── Generators/                              # 报告生成器
│       │   ├── IReportGenerator.cs
│       │   ├── PdfReportGenerator.cs
│       │   ├── WordReportGenerator.cs
│       │   └── ExcelReportGenerator.cs
│       ├── Templates/                               # 报告模板
│       │   ├── StabilityAnalysisReport.html
│       │   ├── SectionAnalysisTemplate.docx
│       │   └── CalculationSummary.xlsx
│       ├── Charts/                                  # 图表生成
│       │   ├── IChartGenerator.cs
│       │   ├── StabilityChartGenerator.cs
│       │   └── SectionProfileChartGenerator.cs
│       └── Exporters/                               # 数据导出
│           ├── IDataExporter.cs
│           ├── CsvExporter.cs
│           └── JsonExporter.cs
│
├── tests/                                           # 测试项目
│   ├── GravityDamAnalysis.Core.Tests/
│   │   ├── Services/
│   │   ├── Entities/
│   │   └── TestData/
│   ├── GravityDamAnalysis.Infrastructure.Tests/
│   │   ├── Revit/
│   │   ├── Caching/
│   │   └── Integration/
│   ├── GravityDamAnalysis.UI.Tests/
│   │   ├── ViewModels/
│   │   └── Controls/
│   └── GravityDamAnalysis.Performance.Tests/        # 性能测试
│       ├── BenchmarkTests/
│       └── LoadTests/
│
├── docs/                                            # 文档
│   ├── Architecture/
│   │   ├── BusinessFlow.md
│   │   ├── TechnicalDesign.md
│   │   └── APIReference.md
│   ├── UserGuide/
│   │   ├── QuickStart.md
│   │   ├── SectionConfiguration.md
│   │   └── ResultInterpretation.md
│   └── Development/
│       ├── SetupGuide.md
│       ├── CodingStandards.md
│       └── TestingStrategy.md
│
├── config/                                          # 配置文件
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── appsettings.Production.json
│   ├── analysis-settings.json
│   └── revit-integration.json
│
├── tools/                                           # 开发工具
│   ├── build/
│   │   ├── build.ps1
│   │   └── package.ps1
│   ├── deployment/
│   │   ├── deploy-revit-plugin.ps1
│   │   └── install-dependencies.ps1
│   └── analysis/
│       ├── code-coverage.ps1
│       └── static-analysis.ps1
│
└── samples/                                         # 示例文件
    ├── test-models/
    │   ├── simple-gravity-dam.rvt
    │   └── complex-gravity-dam.rvt
    ├── configuration-examples/
    │   ├── typical-analysis.json
    │   └── advanced-analysis.json
    └── results-examples/
        ├── sample-report.pdf
        └── calculation-summary.xlsx
```

## 主要项目文件

### 1. 解决方案文件 (GravityDamAnalysis.sln)
```xml
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.8.0
MinimumVisualStudioVersion = 10.0.40219.1

Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GravityDamAnalysis.Core", "src\GravityDamAnalysis.Core\GravityDamAnalysis.Core.csproj", "{GUID-CORE}"
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GravityDamAnalysis.Infrastructure", "src\GravityDamAnalysis.Infrastructure\GravityDamAnalysis.Infrastructure.csproj", "{GUID-INFRA}"
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GravityDamAnalysis.UI", "src\GravityDamAnalysis.UI\GravityDamAnalysis.UI.csproj", "{GUID-UI}"
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GravityDamAnalysis.Revit", "src\GravityDamAnalysis.Revit\GravityDamAnalysis.Revit.csproj", "{GUID-REVIT}"
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GravityDamAnalysis.Calculation", "src\GravityDamAnalysis.Calculation\GravityDamAnalysis.Calculation.csproj", "{GUID-CALC}"
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "GravityDamAnalysis.Reports", "src\GravityDamAnalysis.Reports\GravityDamAnalysis.Reports.csproj", "{GUID-REPORTS}"

Global
    GlobalSection(SolutionConfigurationPlatforms) = preSolution
        Debug|Any CPU = Debug|Any CPU
        Release|Any CPU = Release|Any CPU
    EndGlobalSection
EndGlobal
```

### 2. 核心项目文件示例 (GravityDamAnalysis.Core.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <LangVersion>12</LangVersion>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest</AnalysisLevel>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <PackageReference Include="MathNet.Numerics" Version="5.0.0" />
    <PackageReference Include="CommunityToolkit.Diagnostics" Version="8.2.2" />
    <PackageReference Include="FluentValidation" Version="11.8.0" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="GravityDamAnalysis.Core.Tests" />
  </ItemGroup>
</Project>
```

### 3. Revit 插件项目文件 (GravityDamAnalysis.Revit.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>false</UseWindowsForms>
    <OutputType>Library</OutputType>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Autodesk.Revit.SDK" Version="2025.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\GravityDamAnalysis.Core\GravityDamAnalysis.Core.csproj" />
    <ProjectReference Include="..\GravityDamAnalysis.Infrastructure\GravityDamAnalysis.Infrastructure.csproj" />
    <ProjectReference Include="..\GravityDamAnalysis.UI\GravityDamAnalysis.UI.csproj" />
  </ItemGroup>

  <ItemGroup>
    <Content Include="Resources\manifest.addin">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>

  <Target Name="CopyToRevitAddins" AfterTargets="Build">
    <PropertyGroup>
      <RevitAddinsPath>$(APPDATA)\Autodesk\Revit\Addins\2025</RevitAddinsPath>
    </PropertyGroup>
    <ItemGroup>
      <PluginFiles Include="$(OutputPath)**\*.*" />
    </ItemGroup>
    <Copy SourceFiles="@(PluginFiles)" DestinationFolder="$(RevitAddinsPath)\GravityDamAnalysis\%(RecursiveDir)" />
  </Target>
</Project>
```

## 配置文件示例

### appsettings.json
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "GravityDamAnalysis": "Debug"
    },
    "File": {
      "Path": "logs/dam-analysis-.log",
      "RollingInterval": "Day",
      "RetainedFileCountLimit": 30
    }
  },
  "AnalysisSettings": {
    "DefaultSectionCount": 5,
    "MinSlidingSafetyFactor": 1.05,
    "MinOverturnSafetyFactor": 1.5,
    "CalculationPrecision": 0.001,
    "MaxIterations": 1000,
    "EnableParallelProcessing": true,
    "CacheExpirationMinutes": 60
  },
  "RevitSettings": {
    "SupportedVersions": ["2025", "2026"],
    "Units": "Metric",
    "GeometryPrecision": 0.001,
    "TransactionTimeout": 30,
    "EnableGeometryValidation": true
  },
  "PerformanceSettings": {
    "MaxCacheSize": "500MB",
    "EnableRedisCache": false,
    "ConnectionString": "localhost:6379",
    "BatchSize": 1000,
    "ParallelDegree": 4
  },
  "UISettings": {
    "Theme": "Modern",
    "EnableAnimations": true,
    "RefreshInterval": 500,
    "MaxVisualizationPoints": 10000
  }
}
```

### Revit 插件清单文件 (manifest.addin)
```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>重力坝稳定性分析</Name>
    <Assembly>GravityDamAnalysis.Revit.dll</Assembly>
    <FullClassName>GravityDamAnalysis.Revit.Application.DamAnalysisApplication</FullClassName>
    <ClientId>12345678-1234-1234-1234-123456789ABC</ClientId>
    <VendorId>ADSK</VendorId>
    <VendorDescription>重力坝分析插件开发团队</VendorDescription>
    <VisibilityMode>NotVisibleWhenNoActiveDocument</VisibilityMode>
  </AddIn>
</RevitAddIns>
```

这个项目结构完全基于您的业务流程设计，充分利用了.NET 8的现代化特性，并针对Revit 2025/2026进行了优化。每个项目都有明确的职责边界，便于团队协作和维护。 