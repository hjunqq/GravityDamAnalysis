# Revit 2026 重力坝分析插件 - 部署和加载指南

## 🎯 插件概述

本插件为 Revit 2026 用户提供重力坝稳定性分析功能，包括抗滑和抗倾覆安全系数计算。

## 📋 系统要求

- Autodesk Revit 2025/2026
- .NET 8.0 Runtime
- Windows 10/11 x64

## 🔧 编译插件

### 1. 首先编译项目
```powershell
# 在项目根目录下执行
dotnet build src/GravityDamAnalysis.Revit/GravityDamAnalysis.Revit.csproj --configuration Release
```

编译成功后，插件DLL文件位于：
```
src\GravityDamAnalysis.Revit\bin\Release\net8.0\GravityDamAnalysis.Revit.dll
```

## 📁 插件文件部署

### 2. 复制插件文件到Revit目录

**方法一：自动部署（推荐）**
```powershell
# 创建部署脚本
$revitVersion = "2026"
$addinPath = "$env:APPDATA\Autodesk\Revit\Addins\$revitVersion"
$pluginPath = "src\GravityDamAnalysis.Revit\bin\Release\net8.0"

# 创建目录
New-Item -Path $addinPath -ItemType Directory -Force

# 复制.addin文件
Copy-Item "GravityDamAnalysis.addin" -Destination $addinPath

# 复制所有插件文件
Copy-Item "$pluginPath\*" -Destination $addinPath -Recurse -Force
```

**方法二：手动部署**
1. 导航到Revit插件目录：
   ```
   C:\Users\[用户名]\AppData\Roaming\Autodesk\Revit\Addins\2026\
   ```

2. 复制以下文件到该目录：
   - `GravityDamAnalysis.addin` （插件清单文件）
   - `GravityDamAnalysis.Revit.dll` （主插件程序集）
   - `GravityDamAnalysis.Core.dll` （核心业务逻辑）
   - `GravityDamAnalysis.Infrastructure.dll` （基础设施层）
   - `GravityDamAnalysis.Calculation.dll` （计算引擎）
   - 以及所有依赖的NuGet包DLL文件

## 🚀 在Revit中加载插件

### 3. 启动Revit并验证插件加载

1. **启动Revit 2026**

2. **检查插件是否加载成功**：
   - 在Revit功能区查找"重力坝分析"选项卡
   - 应该能看到"稳定性分析"面板和"坝体稳定性分析"按钮

3. **如果插件未出现**：
   - 检查Windows事件查看器中的错误信息
   - 查看Revit日志文件（通常在用户文档下的临时文件夹）

## 📊 使用插件

### 4. 执行重力坝分析

1. **打开包含重力坝模型的Revit文档**

2. **启动分析工具**：
   - 点击"重力坝分析"选项卡
   - 点击"坝体稳定性分析"按钮

3. **选择坝体元素**：
   - 根据提示选择重力坝实体（支持体量、结构构件、墙体等）

4. **确认参数**：
   - 查看提取的几何和材料参数
   - 确认信息正确后继续

5. **查看结果**：
   - 插件将显示抗滑和抗倾覆安全系数
   - 给出稳定性评估结论

## 🛠️ 故障排除

### 常见问题及解决方案

**问题1：插件不出现在Revit中**
- 检查.addin文件路径是否正确
- 确认所有DLL文件都在同一目录
- 检查Revit版本与插件目标版本是否匹配

**问题2：插件加载失败**
- 查看Windows事件查看器中的.NET运行时错误
- 确认.NET 8.0运行时已安装
- 检查DLL文件是否被Windows安全软件阻止

**问题3：选择元素时没有响应**
- 确保选择的是有效的三维实体
- 检查元素是否具有几何信息
- 尝试选择不同类型的元素（体量、结构构件等）

**问题4：计算结果异常**
- 检查元素的材料属性是否已设置
- 验证几何尺寸是否合理
- 查看插件日志文件（在用户文档/GravityDamAnalysis/logs目录）

## 📝 日志和调试

插件会在以下位置生成日志文件：
```
C:\Users\[用户名]\Documents\GravityDamAnalysis\logs\plugin-[日期].log
```

日志包含：
- 插件启动和关闭信息
- 元素选择和数据提取过程
- 计算过程详细信息
- 错误和异常信息

## 🔄 更新插件

要更新插件：
1. 关闭Revit
2. 重新编译项目
3. 替换Revit插件目录中的DLL文件
4. 重新启动Revit

## ⚠️ 注意事项

1. **模型要求**：
   - 坝体必须建模为实体几何
   - 建议使用体量或结构构件族
   - 确保材料属性已正确设置

2. **计算限制**：
   - 当前版本使用简化的计算方法
   - 适用于初步设计阶段的快速评估
   - 详细设计需要专业计算软件验证

3. **性能考虑**：
   - 复杂几何可能影响计算性能
   - 建议对大型模型进行几何简化

## 📞 技术支持

如果遇到问题，请提供：
- Revit版本信息
- 插件日志文件
- 具体的错误信息截图
- 问题重现步骤

---

**版本信息**：v1.0.0 - 适用于Revit 2025/2026
**最后更新**：2024年12月 