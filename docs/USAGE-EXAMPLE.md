# Revit 插件收集和部署 - 完整使用示例

## 🎯 总结：Revit Addin 执行关系

### 核心执行流程
1. **Revit启动** → 扫描 `%APPDATA%\Autodesk\Revit\Addins\[版本]\` 目录
2. **读取 .addin 文件** → 解析插件清单，获取Assembly路径和入口类
3. **加载主程序集** → 加载 `GravityDamAnalysis.Revit.dll`
4. **解析依赖** → 自动加载依赖的DLL文件（Core、Calculation、Infrastructure等）
5. **实例化Application** → 创建 `DamAnalysisApplication` 实例
6. **调用OnStartup** → 注册UI面板和命令
7. **插件就绪** → 用户可以使用插件功能

### 文件依赖关系
```
GravityDamAnalysis.addin (清单文件)
├── GravityDamAnalysis.Revit.dll (主程序集)
    ├── GravityDamAnalysis.Core.dll (业务实体)
    ├── GravityDamAnalysis.Calculation.dll (计算引擎)
    ├── GravityDamAnalysis.Infrastructure.dll (Revit集成)
    ├── GravityDamAnalysis.Reports.dll (报告生成)
    └── appsettings.json (配置文件)
```

## 🛠️ 实际使用演示

### 步骤1: 构建项目
```powershell
# 构建整个解决方案
dotnet build --configuration Release

# 或者构建特定项目
dotnet build src/GravityDamAnalysis.Revit/GravityDamAnalysis.Revit.csproj --configuration Release
```

### 步骤2: 收集插件文件（三种方法）

#### 方法1: 使用简化脚本（推荐开发时使用）
```powershell
# 收集Debug版本文件
.\simple-collect.ps1

# 收集Release版本文件
.\simple-collect.ps1 -Config Release

# 直接部署到Revit
.\simple-collect.ps1 -Config Release -Deploy
```

**输出结果：**
```
收集插件文件...
复制DLL文件...
  OK: GravityDamAnalysis.Revit.dll
  OK: GravityDamAnalysis.Core.dll
  OK: GravityDamAnalysis.Calculation.dll
  OK: GravityDamAnalysis.Infrastructure.dll
复制配置文件...
  OK: GravityDamAnalysis.addin
收集完成!
文件位置: bin\collected
已收集 4 个DLL文件
```

#### 方法2: 使用完整脚本（推荐生产部署）
```powershell
# 开发环境收集
.\collect-plugin-files.ps1

# 生产环境打包
.\collect-plugin-files.ps1 -Configuration Release -Target Production -Platform x64

# 直接部署到Revit 2025
.\collect-plugin-files.ps1 -Configuration Release -DeployToRevit -RevitVersion 2025
```

#### 方法3: 手动收集
```powershell
# 创建目标目录
New-Item -ItemType Directory -Path "bin\manual" -Force

# 复制主要DLL文件
$sourceDir = "src\GravityDamAnalysis.Revit\bin\Release\net8.0"
Copy-Item "$sourceDir\*.dll" "bin\manual\" -Force

# 复制配置文件
Copy-Item "GravityDamAnalysis.addin" "bin\manual\" -Force
Copy-Item "src\GravityDamAnalysis.Revit\Resources\appsettings.json" "bin\manual\" -Force
```

### 步骤3: 验证收集结果
```powershell
# 查看收集的文件
Get-ChildItem "bin\collected" | Select-Object Name, Length

# 检查DLL版本信息
Get-ChildItem "bin\collected\*.dll" | ForEach-Object { 
    Write-Host "$($_.Name): $([System.Diagnostics.FileVersionInfo]::GetVersionInfo($_.FullName).FileVersion)"
}
```

### 步骤4: 部署到Revit

#### 自动部署（推荐）
```powershell
# 使用脚本自动部署
.\simple-collect.ps1 -Config Release -Deploy
```

#### 手动部署
```powershell
# 目标路径
$revitAddinPath = "$env:APPDATA\Autodesk\Revit\Addins\2025"
$pluginDir = "$revitAddinPath\GravityDamAnalysis"

# 创建插件目录
New-Item -ItemType Directory -Path $pluginDir -Force

# 复制插件文件
Copy-Item "bin\collected\*" $pluginDir -Force

# 复制addin清单文件到Revit根目录
Copy-Item "bin\collected\GravityDamAnalysis.addin" $revitAddinPath -Force
```

### 步骤5: 在Revit中验证

1. **启动Revit 2025**
2. **检查插件加载**：
   - 查看功能区是否有"重力坝分析"选项卡
   - 检查是否有"快速识别坝体"和"坝体稳定性分析"按钮
3. **测试插件功能**：
   - 打开包含坝体的Revit文档
   - 尝试运行"快速识别坝体"命令
   - 验证插件是否正常工作

## 📂 最终文件结构

### Revit插件目录结构
```
%APPDATA%\Autodesk\Revit\Addins\2025\
├── GravityDamAnalysis.addin                      # 插件清单
└── GravityDamAnalysis\                           # 插件文件夹
    ├── GravityDamAnalysis.Revit.dll             # 主程序集 (36KB)
    ├── GravityDamAnalysis.Core.dll              # 核心业务 (25KB)
    ├── GravityDamAnalysis.Calculation.dll       # 计算引擎 (21KB)
    ├── GravityDamAnalysis.Infrastructure.dll    # 基础设施 (12KB)
    ├── GravityDamAnalysis.Reports.dll           # 报告模块 (可选)
    └── appsettings.json                          # 配置文件
```

### 项目输出目录结构
```
bin/
├── collected/                                    # 简化脚本输出
│   ├── *.dll
│   ├── GravityDamAnalysis.addin
│   └── appsettings.json
├── Development-Debug-x64/                       # 完整脚本输出
│   ├── *.dll
│   ├── 配置文件
│   └── deployment-report.txt
└── packages/                                     # 发布包
    └── GravityDamAnalysis-Production-Release-x64-20241215.zip
```

## 🔍 故障排除

### 常见问题检查清单

1. **构建问题**
   ```powershell
   # 检查构建错误
   dotnet build --verbosity normal
   
   # 清理并重新构建
   dotnet clean && dotnet build
   ```

2. **文件缺失**
   ```powershell
   # 检查源文件是否存在
   Test-Path "src\GravityDamAnalysis.Revit\bin\Release\net8.0\*.dll"
   
   # 列出所有生成的DLL
   Get-ChildItem "src\GravityDamAnalysis.Revit\bin" -Recurse -Filter "*.dll"
   ```

3. **权限问题**
   ```powershell
   # 以管理员身份运行PowerShell
   Start-Process PowerShell -Verb RunAs
   
   # 检查Revit目录权限
   Get-Acl "$env:APPDATA\Autodesk\Revit\Addins\2025"
   ```

4. **版本兼容性**
   ```powershell
   # 检查.NET版本
   dotnet --version
   
   # 检查DLL目标框架
   dotnet-info "bin\collected\GravityDamAnalysis.Revit.dll"
   ```

## 🎯 开发工作流程推荐

### 日常开发
```powershell
# 1. 修改代码后重新构建
dotnet build

# 2. 快速收集和部署测试
.\simple-collect.ps1 -Deploy

# 3. 启动Revit测试功能
```

### 发布准备
```powershell
# 1. 清理和完整构建
dotnet clean
dotnet build --configuration Release

# 2. 创建发布包
.\collect-plugin-files.ps1 -Configuration Release -Target Production

# 3. 测试不同Revit版本兼容性
.\collect-plugin-files.ps1 -RevitVersion 2024 -DeployToRevit
.\collect-plugin-files.ps1 -RevitVersion 2025 -DeployToRevit
```

## 📋 关键要点总结

1. **Revit Addin基本原理**：通过.addin清单文件告诉Revit加载哪个DLL和调用哪个类
2. **文件依赖关系**：主程序集依赖业务逻辑DLL，所有文件必须在同一目录
3. **部署路径**：插件文件放在`%APPDATA%\Autodesk\Revit\Addins\[版本]\`目录
4. **脚本自动化**：使用PowerShell脚本可以自动化收集、打包和部署过程
5. **版本管理**：不同Revit版本需要单独的插件目录

通过这套脚本和流程，您可以高效地管理Revit插件的开发、测试和发布全生命周期。 