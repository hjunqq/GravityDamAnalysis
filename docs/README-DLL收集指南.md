# 重力坝分析插件DLL收集完整指南

## 概述
本指南详细说明如何正确收集重力坝分析插件的所有必需文件，包括项目生成的DLL和第三方依赖DLL。

## 问题背景
在Revit插件开发中，一个常见的问题是插件在运行时出现"找不到程序集"或"依赖项缺失"的错误。这通常是因为：

1. **项目DLL收集不完整** - 缺少某些项目模块的DLL
2. **第三方依赖缺失** - NuGet包的依赖DLL没有被正确复制
3. **运行时依赖问题** - .NET Core/.NET 8的依赖解析机制与传统.NET Framework不同

## 解决方案架构

### 脚本组织
```
项目根目录/
├── collect-plugin-files-enhanced.ps1     # 🔥 新的增强版收集脚本
├── collect-plugin-files-fixed.ps1        # 之前的修复版本
├── collect-plugin-files.ps1              # 原始版本
├── copy-nuget-dependencies.ps1           # 专门的依赖收集脚本
└── bin/
    ├── collected/                         # 预收集的依赖DLL
    └── Development-Release-x64/           # 最终输出目录
```

### 依赖收集策略
1. **项目DLL** - 从各个项目的构建输出目录收集
2. **第三方依赖** - 从预收集目录 (`bin/collected`) 获取
3. **备用查找** - 如果依赖缺失，自动从NuGet缓存重新收集
4. **智能过滤** - 排除不必要的系统DLL，只保留必需依赖

## 使用方法

### 1. 基本使用（推荐）
```powershell
# 收集所有文件到默认输出目录
.\collect-plugin-files-enhanced.ps1

# 同时刷新依赖并收集
.\collect-plugin-files-enhanced.ps1 -RefreshDependencies
```

### 2. 完整部署流程
```powershell
# Step 1: 确保项目已编译
dotnet build --configuration Release --platform x64

# Step 2: 收集并直接部署到Revit
.\collect-plugin-files-enhanced.ps1 -RefreshDependencies -DeployToRevit -RevitVersion "2025"

# Step 3: 检查部署报告
cat .\bin\Development-Release-x64\deployment-report.txt
```

### 3. 高级选项
```powershell
# 指定不同的配置和平台
.\collect-plugin-files-enhanced.ps1 -Configuration Debug -Platform x64

# 为生产环境收集
.\collect-plugin-files-enhanced.ps1 -Target Production -Configuration Release

# 为特定Revit版本部署
.\collect-plugin-files-enhanced.ps1 -DeployToRevit -RevitVersion "2024"
```

## 脚本特性

### 🆕 增强功能
- **分离式依赖管理** - 项目DLL和第三方依赖分开收集
- **智能依赖检测** - 自动发现和收集缺失的依赖
- **详细进度报告** - 清楚显示每个收集步骤的结果
- **文件清单汇总** - 显示所有收集文件的详细信息
- **增强错误处理** - 更好的错误提示和故障排除建议

### 📋 完整依赖列表
脚本会自动收集以下依赖：

#### 项目DLL (5个)
- `GravityDamAnalysis.Revit.dll`
- `GravityDamAnalysis.Core.dll`
- `GravityDamAnalysis.Calculation.dll`
- `GravityDamAnalysis.Infrastructure.dll`
- `GravityDamAnalysis.Reports.dll`

#### Microsoft Extensions (9个)
- `Microsoft.Extensions.DependencyInjection.dll`
- `Microsoft.Extensions.DependencyInjection.Abstractions.dll`
- `Microsoft.Extensions.Logging.dll`
- `Microsoft.Extensions.Logging.Abstractions.dll`
- `Microsoft.Extensions.Configuration.dll`
- `Microsoft.Extensions.Configuration.Abstractions.dll`
- `Microsoft.Extensions.Configuration.Json.dll`
- `Microsoft.Extensions.Options.dll`
- `Microsoft.Extensions.Primitives.dll`

#### 日志框架 (3个)
- `Serilog.dll`
- `Serilog.Extensions.Logging.dll`
- `Serilog.Sinks.File.dll`

#### 系统依赖 (4个)
- `System.Text.Json.dll`
- `System.Text.Encodings.Web.dll`
- `System.Diagnostics.DiagnosticSource.dll`
- 其他必要的系统组件

## 故障排除

### 常见问题

#### 1. "找不到构建输出路径"
**原因**: 项目未编译或编译配置不匹配
**解决方案**:
```powershell
# 先编译项目
dotnet build --configuration Release --platform x64

# 或使用Debug配置
.\collect-plugin-files-enhanced.ps1 -Configuration Debug
```

#### 2. "缺少依赖DLL文件"
**原因**: NuGet依赖未正确下载或收集
**解决方案**:
```powershell
# 刷新NuGet包
dotnet restore

# 重新收集依赖
.\collect-plugin-files-enhanced.ps1 -RefreshDependencies
```

#### 3. "插件在Revit中无法加载"
**检查项**:
- 确认所有DLL文件都在插件目录中
- 检查.addin文件路径是否正确
- 查看Windows事件查看器中的错误详情
- 确认Revit版本匹配

### 手动验证收集结果
```powershell
# 查看收集的文件
Get-ChildItem .\bin\Development-Release-x64\ -Recurse

# 检查DLL数量（应该有20+个DLL文件）
(Get-ChildItem .\bin\Development-Release-x64\ -Filter "*.dll").Count

# 查看详细报告
Get-Content .\bin\Development-Release-x64\deployment-report.txt
```

## 部署验证

### 自动部署后验证
```powershell
# 检查Revit插件目录
$revitPath = "$env:APPDATA\Autodesk\Revit\Addins\2025\GravityDamAnalysis"
Get-ChildItem $revitPath

# 确认addin文件存在
Test-Path "$env:APPDATA\Autodesk\Revit\Addins\2025\GravityDamAnalysis.addin"
```

### 手动部署步骤
如果自动部署失败，可以手动执行：

1. **复制插件文件**:
   ```
   源目录: .\bin\Development-Release-x64\*
   目标目录: %APPDATA%\Autodesk\Revit\Addins\2025\GravityDamAnalysis\
   ```

2. **复制addin清单**:
   ```
   源文件: .\bin\Development-Release-x64\GravityDamAnalysis.addin
   目标位置: %APPDATA%\Autodesk\Revit\Addins\2025\
   ```

## 脚本参数详解

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `-Configuration` | String | "Release" | 构建配置 (Debug/Release) |
| `-Platform` | String | "x64" | 目标平台 (AnyCPU/x64) |
| `-Target` | String | "Development" | 部署目标 (Development/Testing/Production) |
| `-OutputDir` | String | "bin" | 输出根目录 |
| `-DeployToRevit` | Switch | False | 是否自动部署到Revit |
| `-RevitVersion` | String | "2025" | 目标Revit版本 |
| `-RefreshDependencies` | Switch | False | 是否刷新NuGet依赖 |

## 最佳实践

### 开发阶段
1. 使用 `-RefreshDependencies` 确保依赖最新
2. 使用 `-Configuration Debug` 进行调试
3. 频繁验证收集结果的完整性

### 生产部署
1. 使用 `-Configuration Release` 
2. 使用 `-Target Production` 创建生产包
3. 创建ZIP包进行分发

### CI/CD集成
```yaml
# Azure DevOps Pipeline 示例
- task: PowerShell@2
  displayName: 'Collect Plugin Files'
  inputs:
    filePath: 'collect-plugin-files-enhanced.ps1'
    arguments: '-Configuration Release -Target Production -RefreshDependencies'
```

## 维护说明

### 添加新依赖
如果项目添加了新的NuGet包，需要：

1. 更新 `copy-nuget-dependencies.ps1` 中的依赖列表
2. 更新 `collect-plugin-files-enhanced.ps1` 中的 `$RequiredDependencies` 数组
3. 测试收集脚本确保新依赖被正确收集

### 支持新Revit版本
在 `$RevitPaths` 哈希表中添加新版本的路径映射。

## 技术支持

如果遇到问题：
1. 查看脚本输出的详细日志
2. 检查 `deployment-report.txt` 文件
3. 验证项目编译状态
4. 确认NuGet包已正确恢复

---

**注意**: 此增强版脚本向后兼容，但建议逐步迁移到新版本以获得更好的依赖管理和错误处理能力。 