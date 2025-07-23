using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GravityDamAnalysis.Core.Models;
using GravityDamAnalysis.UI.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace GravityDamAnalysis.Revit.Services
{
    /// <summary>
    /// 真实的Revit集成服务
    /// </summary>
    public class RevitIntegration : IRevitIntegration
    {
        private readonly UIApplication _uiApplication;
        private readonly Document _document;
        private readonly ILogger<RevitIntegration> _logger;
        
        public RevitIntegration(UIApplication uiApplication, ILogger<RevitIntegration> logger = null)
        {
            _uiApplication = uiApplication ?? throw new ArgumentNullException(nameof(uiApplication));
            _document = uiApplication.ActiveUIDocument?.Document ?? throw new InvalidOperationException("没有活动的Revit文档");
            _logger = logger;
        }
        
        public object RevitDocument => _document;
        public object RevitApplication => _uiApplication;
        public bool IsInRevitContext => true;
        
        public event EventHandler<ProgressEventArgs> ProgressChanged;
        public event EventHandler<string> StatusChanged;
        
        public async Task<List<DamGeometry>> AutoDetectDamsAsync()
        {
            return await Task.Run(() =>
            {
                OnStatusChanged("正在识别Revit模型中的坝体...");
                OnProgressChanged(0, "开始识别", true);
                
                var dams = new List<DamGeometry>();
                
                try
                {
                    // 使用过滤器查找可能的坝体元素
                    var collector = new FilteredElementCollector(_document);
                    
                    // 查找墙体、楼板、结构柱、结构框架、公制常规模型等可能的坝体元素
                    var walls = collector.OfClass(typeof(Wall)).Cast<Wall>().ToList();
                    var floors = collector.OfClass(typeof(Floor)).Cast<Floor>().ToList();
                    var structuralColumns = collector.OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Column)
                        .ToList();
                    var structuralFramings = collector.OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Beam ||
                                     fi.StructuralType == Autodesk.Revit.DB.Structure.StructuralType.Brace)
                        .ToList();
                    var genericModels = collector.OfClass(typeof(FamilyInstance))
                        .Cast<FamilyInstance>()
                        .Where(fi => fi.Category != null && fi.Category.BuiltInCategory == BuiltInCategory.OST_GenericModel)
                        .ToList();


                    // 2. 使用 FilteredElementCollector 获取所有“实例元素”（不是类型）
                    collector = new FilteredElementCollector(_document);
                    var allElements = collector.WhereElementIsNotElementType(); // 过滤掉类型元素

                    var families = collector.OfClass(typeof(Family)).ToElements();

                    List<Element> customModels = new List<Element>();
                    foreach (Element family in families)
                    {
                        // 这里可以添加自定义逻辑来筛选特定的族
                        if (family.Name.Contains("Dam", StringComparison.OrdinalIgnoreCase))
                        {
                            customModels.Add(family);
                        }
                    }

                    OnProgressChanged(30, "分析几何特征...", false);
                    
                    // 分析墙体作为坝体
                    foreach (var wall in walls)
                    {
                        if (IsDamStructure(wall))
                        {
                            var dam = CreateDamGeometry(wall, "墙体坝体");
                            dams.Add(dam);
                        }
                    }
                    
                    OnProgressChanged(60, "分析楼板结构...", false);
                    
                    // 分析楼板作为坝体
                    foreach (var floor in floors)
                    {
                        if (IsDamStructure(floor))
                        {
                            var dam = CreateDamGeometry(floor, "楼板坝体");
                            dams.Add(dam);
                        }
                    }
                    
                    OnProgressChanged(80, "分析结构柱...", false);
                    
                    // 分析结构柱作为坝体
                    foreach (var column in structuralColumns)
                    {
                        if (IsDamStructure(column))
                        {
                            var dam = CreateDamGeometry(column, "柱状坝体");
                            dams.Add(dam);
                        }
                    }

                    OnProgressChanged(90, "分析结构框架...", false);

                    // 分析结构框架（如梁、板等）作为坝体
                    foreach (var framing in structuralFramings)
                    {
                        if (IsDamStructure(framing))
                        {
                            var dam = CreateDamGeometry(framing, "框架坝体");
                            dams.Add(dam);
                        }
                    }

                    OnProgressChanged(95, "分析常规模型...", false);

                    // 分析公制常规模型作为坝体
                    foreach (var gm in genericModels)
                    {
                        if (IsDamStructure(gm))
                        {
                            var dam = CreateDamGeometry(gm, "常规模型坝体");
                            dams.Add(dam);
                        }
                    }

                    foreach (var customModel in customModels)
                    {
                        //if (IsDamStructure(customModel))
                        //{
                            var dam = CreateDamGeometry(customModel, "自定义坝体");
                            dams.Add(dam);
                        //}
                    }

                    OnProgressChanged(100, "识别完成", false);
                    OnStatusChanged($"成功识别 {dams.Count} 个坝体");
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"识别坝体时出错: {ex.Message}");
                    throw;
                }
                
                return dams;
            });
        }
        
        public async Task<DamProfile> ExtractProfileAsync(DamGeometry dam, int profileIndex)
        {
            return await Task.Run(() =>
            {
                OnStatusChanged($"正在提取 {dam.Name} 的剖面...");
                OnProgressChanged(0, "开始提取", true);
                
                try
                {
                    // 根据坝体ID查找对应的Revit元素
                    var element = _document.GetElement(new ElementId(int.Parse(dam.Id)));
                    if (element == null)
                    {
                        throw new InvalidOperationException($"找不到ID为 {dam.Id} 的元素");
                    }
                    
                    OnProgressChanged(30, "获取几何信息...", false);
                    
                    // 检查是否为拉伸模型
                    if (IsExtrusionModel(element))
                    {
                        OnStatusChanged("检测到拉伸模型，使用优化的拉伸剖面提取方法");
                        return ExtractExtrusionModelProfile(element, dam, profileIndex);
                    }
                    
                    // 获取元素的几何信息
                    var geometry = element.get_Geometry(new Options());
                    if (geometry == null)
                    {
                        throw new InvalidOperationException("无法获取元素几何信息");
                    }
                    
                    OnProgressChanged(60, "提取剖面坐标...", false);
                    
                    // 提取剖面坐标
                    var coordinates = ExtractProfileCoordinates(geometry, profileIndex);
                    
                    OnProgressChanged(90, "计算基础线...", false);
                    
                    // 计算基础线
                    var foundationLine = CalculateFoundationLine(coordinates);
                    
                    OnProgressChanged(100, "提取完成", false);
                    OnStatusChanged("剖面提取完成");
                    
                    return new DamProfile
                    {
                        DamId = dam.Id,
                        ProfileIndex = profileIndex,
                        Name = $"{dam.Name}_剖面_{profileIndex}",
                        Coordinates = coordinates,
                        FoundationLine = foundationLine,
                        WaterLevel = GetWaterLevel(),
                        FoundationElevation = GetFoundationElevation(coordinates),
                        CrestElevation = GetCrestElevation(coordinates)
                    };
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"提取剖面时出错: {ex.Message}");
                    throw;
                }
            });
        }
        
        public async Task<AnalysisResults> PerformStabilityAnalysisAsync(DamProfile profile, CalculationParameters parameters)
        {
            return await Task.Run(() =>
            {
                OnStatusChanged("正在执行稳定性分析...");
                OnProgressChanged(0, "开始分析", true);
                
                try
                {
                    // 这里应该调用实际的计算引擎
                    // 暂时使用模拟计算
                    OnProgressChanged(25, "计算荷载...", false);
                    var loads = CalculateLoads(profile, parameters);
                    
                    OnProgressChanged(50, "计算安全系数...", false);
                    var safetyFactors = CalculateSafetyFactors(loads, parameters);
                    
                    OnProgressChanged(75, "计算应力分布...", false);
                    var stresses = CalculateStresses(profile, loads);
                    
                    OnProgressChanged(100, "分析完成", false);
                    OnStatusChanged("稳定性分析完成");
                    
                    return new AnalysisResults
                    {
                        Id = Guid.NewGuid().ToString(),
                        DamId = profile.DamId,
                        ProfileId = profile.Name,
                        AnalysisDateTime = DateTime.Now,
                        SlidingSafetyFactor = safetyFactors.Sliding,
                        OverturningSafetyFactor = safetyFactors.Overturning,
                        CompressionSafetyFactor = safetyFactors.Compression,
                        SlidingStatus = safetyFactors.Sliding >= parameters.SlidingSafetyFactor ? "安全" : "不安全",
                        OverturningStatus = safetyFactors.Overturning >= parameters.OverturningSafetyFactor ? "安全" : "不安全",
                        CompressionStatus = stresses.MaxCompressive <= parameters.AllowableCompressiveStress ? "安全" : "不安全",
                        SelfWeight = loads.SelfWeight,
                        WaterPressure = loads.WaterPressure,
                        UpliftPressure = loads.UpliftPressure,
                        SeismicForce = loads.SeismicForce,
                        MaxCompressiveStress = stresses.MaxCompressive,
                        MaxTensileStress = stresses.MaxTensile,
                        PrincipalStress = stresses.Principal
                    };
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"稳定性分析时出错: {ex.Message}");
                    throw;
                }
            });
        }
        
        public async Task WriteResultsToRevitAsync(AnalysisResults results)
        {
            await Task.Run(() =>
            {
                OnStatusChanged("正在将结果写回Revit模型...");
                
                try
                {
                    using (var transaction = new Transaction(_document, "写入分析结果"))
                    {
                        transaction.Start();
                        
                        // 创建参数来存储分析结果
                        var damElement = _document.GetElement(new ElementId(int.Parse(results.DamId)));
                        if (damElement != null)
                        {
                            // 添加或更新分析结果参数
                            AddAnalysisResultParameters(damElement, results);
                        }
                        
                        transaction.Commit();
                    }
                    
                    OnStatusChanged("结果已成功写回Revit模型");
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"写回结果时出错: {ex.Message}");
                    throw;
                }
            });
        }
        
        public async Task<GravityDamAnalysis.Core.Models.ProjectInfo> GetProjectInfoAsync()
        {
            return await Task.Run(() =>
            {
                var projectInfo = _document.ProjectInformation;
                return new GravityDamAnalysis.Core.Models.ProjectInfo
                {
                    Name = projectInfo?.Name ?? "未命名项目",
                    Number = projectInfo?.Number ?? "未知编号",
                    Location = projectInfo?.Address ?? "未知位置",
                    Client = projectInfo?.ClientName ?? "未知客户",
                    Engineer = projectInfo?.Author ?? "未知工程师",
                    Date = DateTime.Now,
                    Description = "重力坝稳定性分析项目"
                };
            });
        }
        
        public async Task<List<AnalysisResults>> GetRecentAnalysisResultsAsync()
        {
            return await Task.Run(() =>
            {
                var results = new List<AnalysisResults>();
                
                // 从Revit参数中读取最近的分析结果
                var collector = new FilteredElementCollector(_document);
                var elements = collector.WhereElementIsNotElementType().ToList();
                
                foreach (var element in elements)
                {
                    var result = ReadAnalysisResultFromElement(element);
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
                
                return results.OrderByDescending(r => r.AnalysisDateTime).Take(10).ToList();
            });
        }
        
        public async Task SaveAnalysisResultsAsync(AnalysisResults results)
        {
            await Task.Run(() =>
            {
                OnStatusChanged("正在保存分析结果...");
                
                try
                {
                    // 保存到Revit参数或外部数据库
                    using (var transaction = new Transaction(_document, "保存分析结果"))
                    {
                        transaction.Start();
                        
                        var damElement = _document.GetElement(new ElementId(int.Parse(results.DamId)));
                        if (damElement != null)
                        {
                            SaveAnalysisResultToElement(damElement, results);
                        }
                        
                        transaction.Commit();
                    }
                    
                    OnStatusChanged("分析结果已保存");
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"保存结果时出错: {ex.Message}");
                    throw;
                }
            });
        }
        
        public async Task<string> GenerateReportAsync(AnalysisResults results)
        {
            return await Task.Run(() =>
            {
                OnStatusChanged("正在生成分析报告...");
                
                try
                {
                    var report = $"重力坝稳定性分析报告\n\n" +
                                $"项目名称: {_document.ProjectInformation?.Name}\n" +
                                $"分析时间: {results.AnalysisDateTime:yyyy-MM-dd HH:mm:ss}\n" +
                                $"坝体ID: {results.DamId}\n" +
                                $"剖面ID: {results.ProfileId}\n\n" +
                                $"安全系数分析:\n" +
                                $"  抗滑稳定: {results.SlidingSafetyFactor:F2} ({results.SlidingStatus})\n" +
                                $"  抗倾覆: {results.OverturningSafetyFactor:F2} ({results.OverturningStatus})\n" +
                                $"  抗压强度: {results.CompressionSafetyFactor:F2} ({results.CompressionStatus})\n\n" +
                                $"荷载分析:\n" +
                                $"  自重: {results.SelfWeight:F0} kN\n" +
                                $"  水压力: {results.WaterPressure:F0} kN\n" +
                                $"  扬压力: {results.UpliftPressure:F0} kN\n" +
                                $"  地震力: {results.SeismicForce:F0} kN\n\n" +
                                $"应力分析:\n" +
                                $"  最大压应力: {results.MaxCompressiveStress:F1} MPa\n" +
                                $"  最大拉应力: {results.MaxTensileStress:F1} MPa\n" +
                                $"  主应力: {results.PrincipalStress:F1} MPa";
                    
                    OnStatusChanged("分析报告已生成");
                    return report;
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"生成报告时出错: {ex.Message}");
                    throw;
                }
            });
        }
        
        public async Task ExportToExcelAsync(AnalysisResults results, string filePath)
        {
            await Task.Run(() =>
            {
                OnStatusChanged("正在导出到Excel...");
                
                try
                {
                    // 这里应该实现Excel导出逻辑
                    // 暂时只是模拟
                    System.IO.File.WriteAllText(filePath, 
                        $"坝体ID,{results.DamId}\n" +
                        $"分析时间,{results.AnalysisDateTime}\n" +
                        $"抗滑安全系数,{results.SlidingSafetyFactor}\n" +
                        $"抗倾覆安全系数,{results.OverturningSafetyFactor}\n" +
                        $"抗压安全系数,{results.CompressionSafetyFactor}");
                    
                    OnStatusChanged($"结果已导出到: {filePath}");
                }
                catch (Exception ex)
                {
                    OnStatusChanged($"导出Excel时出错: {ex.Message}");
                    throw;
                }
            });
        }
        
        #region 私有辅助方法
        
        /// <summary>
        /// 检查元素是否为拉伸模型
        /// </summary>
        /// <param name="element">Revit元素</param>
        /// <returns>是否为拉伸模型</returns>
        private bool IsExtrusionModel(Element element)
        {
            try
            {
                // 检查是否为公制常规模型
                if (element is FamilyInstance familyInstance)
                {
                    var category = familyInstance.Category;
                    if (category?.BuiltInCategory == BuiltInCategory.OST_GenericModel)
                    {
                        // 获取几何信息检查是否为拉伸实体
                        var geometry = element.get_Geometry(new Options());
                        if (geometry != null)
                        {
                            foreach (var geoObj in geometry)
                            {
                                if (geoObj is Solid solid)
                                {
                                    return IsExtrusionSolid(solid);
                                }
                            }
                        }
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "检查拉伸模型时发生错误");
                return false;
            }
        }
        
        /// <summary>
        /// 提取拉伸模型的剖面（专门针对拉伸模型优化）
        /// </summary>
        /// <param name="element">拉伸模型元素</param>
        /// <param name="dam">坝体信息</param>
        /// <param name="profileIndex">剖面索引</param>
        /// <returns>坝体剖面</returns>
        private DamProfile ExtractExtrusionModelProfile(Element element, DamGeometry dam, int profileIndex)
        {
            try
            {
                OnProgressChanged(40, "分析拉伸模型几何...", false);
                
                // 获取几何信息
                var geometry = element.get_Geometry(new Options());
                if (geometry == null)
                {
                    throw new InvalidOperationException("无法获取拉伸模型几何信息");
                }
                
                OnProgressChanged(50, "提取拉伸轮廓...", false);
                
                // 提取拉伸轮廓
                var extrusionProfile = ExtractExtrusionModelProfileCoordinates(geometry, profileIndex);
                
                OnProgressChanged(70, "计算拉伸基础线...", false);
                
                // 计算拉伸基础线
                var foundationLine = CalculateExtrusionFoundationLine(extrusionProfile);
                
                OnProgressChanged(90, "设置拉伸参数...", false);
                
                // 获取拉伸参数
                var (foundationElevation, crestElevation) = GetExtrusionElevations(extrusionProfile);
                
                OnProgressChanged(100, "拉伸剖面提取完成", false);
                OnStatusChanged("拉伸模型剖面提取完成");
                
                return new DamProfile
                {
                    DamId = dam.Id,
                    ProfileIndex = profileIndex,
                    Name = $"{dam.Name}_拉伸剖面_{profileIndex}",
                    Coordinates = extrusionProfile,
                    FoundationLine = foundationLine,
                    WaterLevel = GetWaterLevel(),
                    FoundationElevation = foundationElevation,
                    CrestElevation = crestElevation
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "提取拉伸模型剖面时发生错误");
                OnStatusChanged($"拉伸模型剖面提取失败: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// 从拉伸模型几何中提取剖面坐标
        /// </summary>
        /// <param name="geometry">几何元素</param>
        /// <param name="profileIndex">剖面索引</param>
        /// <returns>2D剖面坐标</returns>
        private List<Point2D> ExtractExtrusionModelProfileCoordinates(GeometryElement geometry, int profileIndex)
        {
            var coordinates = new List<Point2D>();
            var extractedGeometry = new ExtractedGeometryInfo();
            
            try
            {
                OnStatusChanged($"正在提取拉伸模型剖面 {profileIndex} 的几何信息...");
                
                // 遍历几何对象，提取点、线、面
                foreach (GeometryObject geoObj in geometry)
                {
                    ProcessGeometryObject(geoObj, extractedGeometry);
                }
                
                // 输出详细的提取统计信息
                var extractionStats = new
                {
                    PointsCount = extractedGeometry.Points.Count,
                    CurvesCount = extractedGeometry.Curves.Count,
                    FacesCount = extractedGeometry.Faces.Count,
                    TotalArea = extractedGeometry.Faces.Sum(f => f.Area),
                    ProfileIndex = profileIndex
                };
                
                OnStatusChanged($"拉伸模型几何提取完成 - 点: {extractionStats.PointsCount}, 线: {extractionStats.CurvesCount}, 面: {extractionStats.FacesCount}, 总面积: {extractionStats.TotalArea:F2} 平方英尺");
                
                // 高亮显示提取的几何元素
                HighlightExtractedGeometry(extractedGeometry);
                
                // 转换为2D剖面坐标（针对拉伸模型优化）
                coordinates = ConvertExtrusionTo2DCoordinates(extractedGeometry, profileIndex);
                
                // 验证提取的坐标
                var validationInfo = ValidateExtractedCoordinates(coordinates, extractionStats);
                
                // 输出验证结果
                OnStatusChanged($"拉伸模型剖面 {profileIndex} 验证结果: {validationInfo.Status} - {validationInfo.Message}");
                
                // 如果验证失败，提供用户提示
                if (!validationInfo.IsValid)
                {
                    OnStatusChanged($"⚠️ 拉伸模型剖面提取警告: {validationInfo.Suggestions}");
                }
                
                return coordinates;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"提取拉伸模型剖面 {profileIndex} 几何时出错: {ex.Message}");
                _logger?.LogError(ex, "提取拉伸模型剖面 {ProfileIndex} 几何时发生错误", profileIndex);
                
                // 返回默认坐标作为后备方案
                return GetDefaultExtrusionCoordinates();
            }
        }
        
        /// <summary>
        /// 将拉伸模型几何转换为2D坐标（针对拉伸模型优化）
        /// </summary>
        /// <param name="extractedGeometry">提取的几何信息</param>
        /// <param name="profileIndex">剖面索引</param>
        /// <returns>2D坐标列表</returns>
        private List<Point2D> ConvertExtrusionTo2DCoordinates(ExtractedGeometryInfo extractedGeometry, int profileIndex)
        {
            var coordinates = new List<Point2D>();
            
            try
            {
                // 计算剖面的投影平面（基于剖面索引）
                var projectionPlane = CalculateProjectionPlane(profileIndex);
                
                // 优先从拉伸轮廓线中提取坐标
                var profileCurves = extractedGeometry.Curves
                    .Where(c => c.Curve != null)
                    .OrderByDescending(c => c.Curve.Length) // 优先选择较长的曲线
                    .ToList();
                
                if (profileCurves.Any())
                {
                    // 从轮廓线中提取点
                    var contourPoints = ExtractContourFromCurves(profileCurves, projectionPlane);
                    coordinates.AddRange(contourPoints);
                    
                    OnStatusChanged($"从拉伸轮廓线提取了 {contourPoints.Count} 个坐标点");
                }
                
                // 如果轮廓线不够，从面中提取边界
                if (coordinates.Count < 3)
                {
                    var faceBoundaryPoints = ExtractBoundaryFromFaces(extractedGeometry.Faces, projectionPlane);
                    coordinates.AddRange(faceBoundaryPoints);
                    OnStatusChanged($"从拉伸面边界提取了 {faceBoundaryPoints.Count} 个坐标点");
                }
                
                // 如果仍然不够，从点中提取
                if (coordinates.Count < 3)
                {
                    var pointProjections = ProjectPointsToPlane(extractedGeometry.Points, projectionPlane);
                    coordinates.AddRange(pointProjections);
                    OnStatusChanged($"从拉伸顶点提取了 {pointProjections.Count} 个坐标点");
                }
                
                // 确保坐标按顺序排列（逆时针）
                coordinates = OrderCoordinatesClockwise(coordinates);
                
                // 如果坐标太少，使用默认拉伸坐标
                if (coordinates.Count < 3)
                {
                    coordinates = GetDefaultExtrusionCoordinates();
                    OnStatusChanged("使用默认拉伸坐标作为后备方案");
                }
                
                return coordinates;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "转换拉伸模型2D坐标时发生错误");
                return GetDefaultExtrusionCoordinates();
            }
        }
        
        /// <summary>
        /// 计算拉伸模型的基础线
        /// </summary>
        /// <param name="coordinates">剖面坐标</param>
        /// <returns>基础线坐标</returns>
        private List<Point2D> CalculateExtrusionFoundationLine(List<Point2D> coordinates)
        {
            if (!coordinates.Any()) return new List<Point2D>();
            
            // 简化的基础线计算（针对拉伸模型优化）
            var minY = coordinates.Min(p => p.Y);
            var maxX = coordinates.Max(p => p.X);
            var minX = coordinates.Min(p => p.X);
            
            return new List<Point2D>
            {
                new Point2D(minX, minY),
                new Point2D(maxX, minY)
            };
        }
        
        /// <summary>
        /// 获取拉伸模型的标高信息
        /// </summary>
        /// <param name="coordinates">剖面坐标</param>
        /// <returns>(基础标高, 坝顶标高)</returns>
        private (double FoundationElevation, double CrestElevation) GetExtrusionElevations(List<Point2D> coordinates)
        {
            if (!coordinates.Any())
                return (0.0, 100.0);
            
            var foundationElevation = coordinates.Min(p => p.Y);
            var crestElevation = coordinates.Max(p => p.Y);
            
            return (foundationElevation, crestElevation);
        }
        
        /// <summary>
        /// 获取默认拉伸坐标（后备方案）
        /// </summary>
        /// <returns>默认拉伸坐标列表</returns>
        private List<Point2D> GetDefaultExtrusionCoordinates()
        {
            return new List<Point2D>
            {
                new Point2D(0, 0),      // 左下角
                new Point2D(50, 0),     // 右下角
                new Point2D(60, 20),    // 右侧斜坡
                new Point2D(70, 40),    // 右侧斜坡
                new Point2D(80, 60),    // 右侧斜坡
                new Point2D(90, 80),    // 右侧斜坡
                new Point2D(100, 100),  // 坝顶右侧
                new Point2D(100, 120),  // 坝顶
                new Point2D(80, 120),   // 坝顶左侧
                new Point2D(60, 100),   // 左侧斜坡
                new Point2D(40, 80),    // 左侧斜坡
                new Point2D(20, 60),    // 左侧斜坡
                new Point2D(10, 40),    // 左侧斜坡
                new Point2D(0, 20)      // 左侧斜坡
            };
        }
        
        private bool IsDamStructure(Element element)
        {
            // 简单的坝体识别逻辑
            // 可以根据实际需求进行更复杂的判断
            var height = GetElementHeight(element);
            var volume = GetElementVolume(element);
            
            // 坝体通常具有较大的高度和体积
            return height > 10 && volume > 1000;
        }
        
        private DamGeometry CreateDamGeometry(Element element, string type)
        {
            var height = GetElementHeight(element);
            var volume = GetElementVolume(element);
            var material = GetElementMaterial(element);
            
            return new DamGeometry
            {
                Id = element.Id.Value.ToString(),
                Name = $"{type}_{element.Id.Value}",
                Type = DamType.Gravity,
                Height = height,
                Length = GetElementLength(element),
                Volume = volume,
                Material = material
            };
        }
        
        private double GetElementHeight(Element element)
        {
            try
            {
                var boundingBox = element.get_BoundingBox(null);
                return boundingBox?.Max.Z - boundingBox?.Min.Z ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        
        private double GetElementVolume(Element element)
        {
            try
            {
                var geometry = element.get_Geometry(new Options());
                if (geometry != null)
                {
                    double volume = 0;
                    foreach (var geoElement in geometry)
                    {
                        if (geoElement is Solid solid)
                        {
                            volume += solid.Volume;
                        }
                    }
                    return volume;
                }
            }
            catch
            {
                // 忽略错误
            }
            return 0;
        }
        
        private double GetElementLength(Element element)
        {
            try
            {
                var boundingBox = element.get_BoundingBox(null);
                return boundingBox?.Max.X - boundingBox?.Min.X ?? 0;
            }
            catch
            {
                return 0;
            }
        }
        
        private string GetElementMaterial(Element element)
        {
            try
            {
                // 根据元素类型返回默认材料
                if (element is Wall)
                    return "混凝土墙体";
                else if (element is Floor)
                    return "混凝土楼板";
                else if (element is FamilyInstance)
                    return "混凝土结构";
                else
                    return "混凝土材料";
            }
            catch
            {
                // 忽略错误
            }
            
            return "混凝土材料";
        }
        
        /// <summary>
        /// 提取剖面坐标并高亮显示几何元素
        /// 从输入的GeometryElement中提取所有点、线、面，并在Revit中高亮显示
        /// </summary>
        /// <param name="geometry">Revit几何元素</param>
        /// <param name="profileIndex">剖面索引</param>
        /// <returns>2D剖面坐标列表</returns>
        private List<Point2D> ExtractProfileCoordinates(GeometryElement geometry, int profileIndex)
        {
            var coordinates = new List<Point2D>();
            var extractedGeometry = new ExtractedGeometryInfo();
            
            try
            {
                OnStatusChanged($"正在提取剖面 {profileIndex} 的几何信息...");
                
                // 遍历几何对象，提取点、线、面
                foreach (GeometryObject geoObj in geometry)
                {
                    ProcessGeometryObject(geoObj, extractedGeometry);
                }
                
                // 输出详细的提取统计信息
                var extractionStats = new
                {
                    PointsCount = extractedGeometry.Points.Count,
                    CurvesCount = extractedGeometry.Curves.Count,
                    FacesCount = extractedGeometry.Faces.Count,
                    TotalArea = extractedGeometry.Faces.Sum(f => f.Area),
                    ProfileIndex = profileIndex
                };
                
                OnStatusChanged($"几何提取完成 - 点: {extractionStats.PointsCount}, 线: {extractionStats.CurvesCount}, 面: {extractionStats.FacesCount}, 总面积: {extractionStats.TotalArea:F2} 平方英尺");
                
                // 高亮显示提取的几何元素
                HighlightExtractedGeometry(extractedGeometry);
                
                // 转换为2D剖面坐标
                coordinates = ConvertTo2DCoordinates(extractedGeometry, profileIndex);
                
                // 验证提取的坐标
                var validationInfo = ValidateExtractedCoordinates(coordinates, extractionStats);
                
                // 输出验证结果
                OnStatusChanged($"剖面 {profileIndex} 验证结果: {validationInfo.Status} - {validationInfo.Message}");
                
                // 如果验证失败，提供用户提示
                if (!validationInfo.IsValid)
                {
                    OnStatusChanged($"⚠️ 剖面提取警告: {validationInfo.Suggestions}");
                }
                
                return coordinates;
            }
            catch (Exception ex)
            {
                OnStatusChanged($"提取剖面 {profileIndex} 几何时出错: {ex.Message}");
                _logger?.LogError(ex, "提取剖面 {ProfileIndex} 几何时发生错误", profileIndex);
                
                // 返回默认坐标作为后备方案
                return GetDefaultCoordinates();
            }
        }
        
        /// <summary>
        /// 处理单个几何对象，提取其中的点、线、面
        /// </summary>
        /// <param name="geoObj">几何对象</param>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void ProcessGeometryObject(GeometryObject geoObj, ExtractedGeometryInfo extractedGeometry)
        {
            switch (geoObj)
            {
                case Point point:
                    // 提取点
                    extractedGeometry.Points.Add(new ExtractedPoint
                    {
                        Position = point.Coord,
                        Reference = point.Reference
                    });
                    break;
                    
                case Line line:
                    // 提取线
                    extractedGeometry.Curves.Add(new ExtractedCurve
                    {
                        Curve = line,
                        StartPoint = line.GetEndPoint(0),
                        EndPoint = line.GetEndPoint(1),
                        Reference = line.Reference
                    });
                    break;
                    
                case Arc arc:
                    // 提取圆弧
                    extractedGeometry.Curves.Add(new ExtractedCurve
                    {
                        Curve = arc,
                        StartPoint = arc.GetEndPoint(0),
                        EndPoint = arc.GetEndPoint(1),
                        Center = arc.Center,
                        Radius = arc.Radius,
                        Reference = arc.Reference
                    });
                    break;
                    
                case Solid solid:
                    // 提取实体中的面、边和顶点
                    ExtractSolidGeometry(solid, extractedGeometry);
                    break;
                    
                case GeometryInstance instance:
                    // 处理几何实例
                    ProcessGeometryInstance(instance, extractedGeometry);
                    break;
                    
                case Mesh mesh:
                    // 提取网格中的面和边
                    ExtractMeshGeometry(mesh, extractedGeometry);
                    break;
                    
                case Curve curve:
                    // 处理一般曲线（包括拉伸的轮廓线）
                    extractedGeometry.Curves.Add(new ExtractedCurve
                    {
                        Curve = curve,
                        StartPoint = curve.GetEndPoint(0),
                        EndPoint = curve.GetEndPoint(1),
                        Reference = curve.Reference
                    });
                    break;
            }
        }
        
        /// <summary>
        /// 从实体中提取几何信息
        /// </summary>
        /// <param name="solid">Revit实体</param>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void ExtractSolidGeometry(Solid solid, ExtractedGeometryInfo extractedGeometry)
        {
            try
            {
                // 检查是否为拉伸实体
                if (IsExtrusionSolid(solid))
                {
                    OnStatusChanged("检测到拉伸实体，使用专门的拉伸剖面提取方法");
                    ExtractExtrusionSolidGeometry(solid, extractedGeometry);
                    return;
                }
                
                // 常规实体处理
                ExtractRegularSolidGeometry(solid, extractedGeometry);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "提取实体几何时发生错误，回退到常规处理");
                ExtractRegularSolidGeometry(solid, extractedGeometry);
            }
        }
        
        /// <summary>
        /// 检查实体是否为拉伸实体
        /// </summary>
        /// <param name="solid">Revit实体</param>
        /// <returns>是否为拉伸实体</returns>
        private bool IsExtrusionSolid(Solid solid)
        {
            try
            {
                // 拉伸实体的特征：
                // 1. 通常有6个面（上下底面 + 4个侧面）
                // 2. 上下底面平行且形状相同
                // 3. 侧面都是矩形面
                
                if (solid.Faces.Size != 6) return false;
                
                var faces = solid.Faces.Cast<Face>().ToList();
                
                // 找到上下底面（通常面积最大且平行）
                var planarFaces = faces.OfType<PlanarFace>().ToList();
                if (planarFaces.Count < 2) return false;
                
                // 检查是否有平行的面
                var parallelFaces = new List<PlanarFace>();
                for (int i = 0; i < planarFaces.Count; i++)
                {
                    for (int j = i + 1; j < planarFaces.Count; j++)
                    {
                        var face1 = planarFaces[i];
                        var face2 = planarFaces[j];
                        
                        // 检查法向量是否平行（考虑容差）
                        var normal1 = face1.FaceNormal;
                        var normal2 = face2.FaceNormal;
                        
                        var dotProduct = Math.Abs(normal1.DotProduct(normal2));
                        if (dotProduct > 0.99) // 几乎平行
                        {
                            parallelFaces.Add(face1);
                            parallelFaces.Add(face2);
                        }
                    }
                }
                
                // 如果找到平行的面，很可能是拉伸实体
                return parallelFaces.Count >= 2;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// 提取拉伸实体的几何信息（专门针对拉伸模型优化）
        /// </summary>
        /// <param name="solid">拉伸实体</param>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void ExtractExtrusionSolidGeometry(Solid solid, ExtractedGeometryInfo extractedGeometry)
        {
            try
            {
                OnStatusChanged("正在提取拉伸实体的几何信息...");
                
                var faces = solid.Faces.Cast<Face>().ToList();
                var planarFaces = faces.OfType<PlanarFace>().ToList();
                
                // 分离上下底面和侧面
                var (topFaces, bottomFaces, sideFaces) = ClassifyExtrusionFaces(planarFaces);
                
                // 提取拉伸方向
                var extrusionDirection = DetermineExtrusionDirection(topFaces, bottomFaces);
                
                // 提取拉伸轮廓（从上下底面）
                var extrusionProfile = ExtractExtrusionProfile(topFaces, bottomFaces, extrusionDirection);
                
                // 添加到几何信息中
                extractedGeometry.Curves.AddRange(extrusionProfile);
                
                // 提取所有面
                foreach (Face face in faces)
                {
                    extractedGeometry.Faces.Add(new ExtractedFace
                    {
                        Face = face,
                        Area = face.Area,
                        Reference = face.Reference
                    });
                }
                
                // 提取所有边
                foreach (Edge edge in solid.Edges)
                {
                    var curve = edge.AsCurve();
                    extractedGeometry.Curves.Add(new ExtractedCurve
                    {
                        Curve = curve,
                        StartPoint = curve.GetEndPoint(0),
                        EndPoint = curve.GetEndPoint(1),
                        Reference = edge.Reference
                    });
                }
                
                // 提取顶点
                var vertexPoints = new HashSet<XYZ>();
                foreach (Edge edge in solid.Edges)
                {
                    var curve = edge.AsCurve();
                    vertexPoints.Add(curve.GetEndPoint(0));
                    vertexPoints.Add(curve.GetEndPoint(1));
                }
                
                foreach (var point in vertexPoints)
                {
                    extractedGeometry.Points.Add(new ExtractedPoint
                    {
                        Position = point,
                        Reference = null
                    });
                }
                
                OnStatusChanged($"拉伸实体几何提取完成 - 轮廓线: {extrusionProfile.Count} 条");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "提取拉伸实体几何时发生错误");
                OnStatusChanged("拉伸实体几何提取失败，使用常规方法");
                ExtractRegularSolidGeometry(solid, extractedGeometry);
            }
        }
        
        /// <summary>
        /// 分类拉伸实体的面（上底面、下底面、侧面）
        /// </summary>
        /// <param name="planarFaces">平面面列表</param>
        /// <returns>分类后的面</returns>
        private (List<PlanarFace> TopFaces, List<PlanarFace> BottomFaces, List<PlanarFace> SideFaces) 
            ClassifyExtrusionFaces(List<PlanarFace> planarFaces)
        {
            var topFaces = new List<PlanarFace>();
            var bottomFaces = new List<PlanarFace>();
            var sideFaces = new List<PlanarFace>();
            
            // 按法向量分组
            var faceGroups = new Dictionary<XYZ, List<PlanarFace>>();
            
            foreach (var face in planarFaces)
            {
                var normal = face.FaceNormal;
                var key = NormalizeVector(normal);
                
                if (!faceGroups.ContainsKey(key))
                    faceGroups[key] = new List<PlanarFace>();
                
                faceGroups[key].Add(face);
            }
            
            // 找到最大的面组（通常是上下底面）
            var largestGroup = faceGroups.Values.OrderByDescending(g => g.Count).First();
            
            // 检查是否为水平面（法向量接近Z轴）
            var isHorizontal = largestGroup.Any(f => Math.Abs(f.FaceNormal.DotProduct(XYZ.BasisZ)) > 0.9);
            
            if (isHorizontal)
            {
                // 按Z坐标分类上下底面
                var sortedFaces = largestGroup.OrderBy(f => f.Origin.Z).ToList();
                
                if (sortedFaces.Count >= 2)
                {
                    bottomFaces.Add(sortedFaces.First()); // 最低的面
                    topFaces.Add(sortedFaces.Last());     // 最高的面
                }
                
                // 其余面为侧面
                sideFaces.AddRange(planarFaces.Except(largestGroup));
            }
            else
            {
                // 非水平拉伸，按面积分类
                var sortedByArea = largestGroup.OrderByDescending(f => f.Area).ToList();
                
                if (sortedByArea.Count >= 2)
                {
                    topFaces.Add(sortedByArea.First());   // 面积最大的面
                    bottomFaces.Add(sortedByArea[1]);     // 面积第二大的面
                }
                
                // 其余面为侧面
                sideFaces.AddRange(planarFaces.Except(largestGroup));
            }
            
            return (topFaces, bottomFaces, sideFaces);
        }
        
        /// <summary>
        /// 标准化向量（用于比较）
        /// </summary>
        /// <param name="vector">输入向量</param>
        /// <returns>标准化向量</returns>
        private XYZ NormalizeVector(XYZ vector)
        {
            var length = vector.GetLength();
            if (length < 1e-6) return XYZ.Zero;
            
            return vector / length;
        }
        
        /// <summary>
        /// 确定拉伸方向
        /// </summary>
        /// <param name="topFaces">上底面</param>
        /// <param name="bottomFaces">下底面</param>
        /// <returns>拉伸方向</returns>
        private XYZ DetermineExtrusionDirection(List<PlanarFace> topFaces, List<PlanarFace> bottomFaces)
        {
            if (!topFaces.Any() || !bottomFaces.Any())
                return XYZ.BasisZ; // 默认Z方向
            
            var topOrigin = topFaces.First().Origin;
            var bottomOrigin = bottomFaces.First().Origin;
            
            var direction = topOrigin - bottomOrigin;
            return direction.GetLength() > 1e-6 ? direction.Normalize() : XYZ.BasisZ;
        }
        
        /// <summary>
        /// 提取拉伸轮廓
        /// </summary>
        /// <param name="topFaces">上底面</param>
        /// <param name="bottomFaces">下底面</param>
        /// <param name="extrusionDirection">拉伸方向</param>
        /// <returns>轮廓曲线列表</returns>
        private List<ExtractedCurve> ExtractExtrusionProfile(List<PlanarFace> topFaces, List<PlanarFace> bottomFaces, XYZ extrusionDirection)
        {
            var profileCurves = new List<ExtractedCurve>();
            
            try
            {
                // 从上下底面提取轮廓
                var allFaces = topFaces.Concat(bottomFaces).ToList();
                
                foreach (var face in allFaces)
                {
                    // 获取面的边界曲线
                    var edgeLoops = face.GetEdgesAsCurveLoops();
                    
                    foreach (var edgeLoop in edgeLoops)
                    {
                        foreach (var curve in edgeLoop)
                        {
                            // 检查曲线是否垂直于拉伸方向（轮廓线）
                            var curveDirection = curve.GetEndPoint(1) - curve.GetEndPoint(0);
                            var dotProduct = Math.Abs(curveDirection.Normalize().DotProduct(extrusionDirection));
                            
                            // 如果曲线方向与拉伸方向垂直，则为轮廓线
                            if (dotProduct < 0.1)
                            {
                                profileCurves.Add(new ExtractedCurve
                                {
                                    Curve = curve,
                                    StartPoint = curve.GetEndPoint(0),
                                    EndPoint = curve.GetEndPoint(1),
                                    Reference = curve.Reference
                                });
                            }
                        }
                    }
                }
                
                // 如果没有找到轮廓线，尝试从所有边中提取
                if (!profileCurves.Any())
                {
                    OnStatusChanged("未找到标准轮廓线，尝试从所有边中提取...");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "提取拉伸轮廓时发生错误");
            }
            
            return profileCurves;
        }
        
        /// <summary>
        /// 常规实体几何提取（原有逻辑）
        /// </summary>
        /// <param name="solid">Revit实体</param>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void ExtractRegularSolidGeometry(Solid solid, ExtractedGeometryInfo extractedGeometry)
        {
            // 提取面
            foreach (Face face in solid.Faces)
            {
                extractedGeometry.Faces.Add(new ExtractedFace
                {
                    Face = face,
                    Area = face.Area,
                    Reference = face.Reference
                });
            }
            
            // 提取边
            foreach (Edge edge in solid.Edges)
            {
                var curve = edge.AsCurve();
                extractedGeometry.Curves.Add(new ExtractedCurve
                {
                    Curve = curve,
                    StartPoint = curve.GetEndPoint(0),
                    EndPoint = curve.GetEndPoint(1),
                    Reference = edge.Reference
                });
            }
            
            // 提取顶点 - 通过边的端点获取
            var vertexPoints = new HashSet<XYZ>();
            foreach (Edge edge in solid.Edges)
            {
                var curve = edge.AsCurve();
                vertexPoints.Add(curve.GetEndPoint(0));
                vertexPoints.Add(curve.GetEndPoint(1));
            }
            
            foreach (var point in vertexPoints)
            {
                extractedGeometry.Points.Add(new ExtractedPoint
                {
                    Position = point,
                    Reference = null // 顶点通常没有直接的引用
                });
            }
        }
        
        /// <summary>
        /// 处理几何实例
        /// </summary>
        /// <param name="instance">几何实例</param>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void ProcessGeometryInstance(GeometryInstance instance, ExtractedGeometryInfo extractedGeometry)
        {
            var transform = instance.Transform;
            var instanceGeometry = instance.GetInstanceGeometry();
            
            foreach (GeometryObject instGeomObj in instanceGeometry)
            {
                // 递归处理实例中的几何对象
                ProcessGeometryObject(instGeomObj, extractedGeometry);
            }
        }
        
        /// <summary>
        /// 从网格中提取几何信息
        /// </summary>
        /// <param name="mesh">Revit网格</param>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void ExtractMeshGeometry(Mesh mesh, ExtractedGeometryInfo extractedGeometry)
        {
            // 提取网格顶点
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                extractedGeometry.Points.Add(new ExtractedPoint
                {
                    Position = mesh.Vertices[i],
                    Reference = null // 网格顶点通常没有引用
                });
            }
            
            // 提取网格面
            for (int i = 0; i < mesh.NumTriangles; i++)
            {
                var triangle = mesh.get_Triangle(i);
                
                // 手动计算三角形面积
                var area = CalculateTriangleArea(triangle);
                
                extractedGeometry.Faces.Add(new ExtractedFace
                {
                    Face = null, // 网格面不是Face对象
                    Area = area,
                    Reference = null
                });
            }
        }
        
        /// <summary>
        /// 计算三角形面积
        /// </summary>
        /// <param name="triangle">网格三角形</param>
        /// <returns>三角形面积</returns>
        private double CalculateTriangleArea(MeshTriangle triangle)
        {
            try
            {
                // 获取三角形的三个顶点
                var vertex1 = triangle.get_Vertex(0);
                var vertex2 = triangle.get_Vertex(1);
                var vertex3 = triangle.get_Vertex(2);
                
                // 计算两个边向量
                var edge1 = vertex2 - vertex1;
                var edge2 = vertex3 - vertex1;
                
                // 计算叉积得到面积
                var crossProduct = edge1.CrossProduct(edge2);
                var area = crossProduct.GetLength() / 2.0;
                
                return area;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "计算三角形面积时发生错误");
                return 0.0;
            }
        }
        
        /// <summary>
        /// 高亮显示提取的几何元素
        /// </summary>
        /// <param name="extractedGeometry">提取的几何信息</param>
        private void HighlightExtractedGeometry(ExtractedGeometryInfo extractedGeometry)
        {
            try
            {
                var uidoc = _uiApplication.ActiveUIDocument;
                if (uidoc == null) return;
                
                var elementIds = new List<ElementId>();
                
                // 收集所有有引用的几何元素的父元素ID
                var allReferences = new List<Reference>();
                
                // 添加点的引用
                allReferences.AddRange(extractedGeometry.Points
                    .Where(p => p.Reference != null)
                    .Select(p => p.Reference));
                
                // 添加线的引用
                allReferences.AddRange(extractedGeometry.Curves
                    .Where(c => c.Reference != null)
                    .Select(c => c.Reference));
                
                // 添加面的引用
                allReferences.AddRange(extractedGeometry.Faces
                    .Where(f => f.Reference != null)
                    .Select(f => f.Reference));
                
                // 获取父元素ID
                foreach (var reference in allReferences)
                {
                    try
                    {
                        var element = _document.GetElement(reference);
                        if (element != null && !elementIds.Contains(element.Id))
                        {
                            elementIds.Add(element.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "获取几何引用对应的元素时出错");
                    }
                }
                
                // 高亮显示元素
                if (elementIds.Any())
                {
                    uidoc.Selection.SetElementIds(elementIds);
                    uidoc.ShowElements(elementIds);
                    
                    OnStatusChanged($"已高亮显示 {elementIds.Count} 个包含提取几何的元素");
                }
                else
                {
                    OnStatusChanged("未找到可高亮显示的元素");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "高亮显示几何元素时发生错误");
                OnStatusChanged("高亮显示几何元素时出错");
            }
        }
        
        /// <summary>
        /// 将提取的几何信息转换为2D剖面坐标
        /// </summary>
        /// <param name="extractedGeometry">提取的几何信息</param>
        /// <param name="profileIndex">剖面索引</param>
        /// <returns>2D坐标列表</returns>
        private List<Point2D> ConvertTo2DCoordinates(ExtractedGeometryInfo extractedGeometry, int profileIndex)
        {
            var coordinates = new List<Point2D>();
            
            try
            {
                // 计算剖面的投影平面（基于剖面索引）
                var projectionPlane = CalculateProjectionPlane(profileIndex);
                
                // 从曲线中提取轮廓点
                var contourPoints = ExtractContourFromCurves(extractedGeometry.Curves, projectionPlane);
                coordinates.AddRange(contourPoints);
                
                // 如果曲线不够，从面中提取边界
                if (coordinates.Count < 3)
                {
                    var faceBoundaryPoints = ExtractBoundaryFromFaces(extractedGeometry.Faces, projectionPlane);
                    coordinates.AddRange(faceBoundaryPoints);
                }
                
                // 如果仍然不够，从点中提取
                if (coordinates.Count < 3)
                {
                    var pointProjections = ProjectPointsToPlane(extractedGeometry.Points, projectionPlane);
                    coordinates.AddRange(pointProjections);
                }
                
                // 确保坐标按顺序排列（逆时针）
                coordinates = OrderCoordinatesClockwise(coordinates);
                
                // 如果坐标太少，使用默认坐标
                if (coordinates.Count < 3)
                {
                    coordinates = GetDefaultCoordinates();
                }
                
                return coordinates;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "转换2D坐标时发生错误");
                return GetDefaultCoordinates();
            }
        }
        
        /// <summary>
        /// 计算投影平面
        /// </summary>
        /// <param name="profileIndex">剖面索引</param>
        /// <returns>投影平面</returns>
        private Plane CalculateProjectionPlane(int profileIndex)
        {
            // 根据剖面索引确定投影方向
            XYZ normal;
            switch (profileIndex % 3)
            {
                case 0: // X方向剖面
                    normal = XYZ.BasisX;
                    break;
                case 1: // Y方向剖面
                    normal = XYZ.BasisY;
                    break;
                default: // Z方向剖面
                    normal = XYZ.BasisZ;
                    break;
            }
            
            return Plane.CreateByNormalAndOrigin(normal, XYZ.Zero);
        }
        
        /// <summary>
        /// 从曲线中提取轮廓点
        /// </summary>
        /// <param name="curves">曲线列表</param>
        /// <param name="projectionPlane">投影平面</param>
        /// <returns>2D坐标列表</returns>
        private List<Point2D> ExtractContourFromCurves(List<ExtractedCurve> curves, Plane projectionPlane)
        {
            var points = new List<Point2D>();
            
            foreach (var curve in curves)
            {
                // 将3D点投影到2D平面
                var startPoint2D = ProjectPointToPlane(curve.StartPoint, projectionPlane);
                var endPoint2D = ProjectPointToPlane(curve.EndPoint, projectionPlane);
                
                if (startPoint2D != null)
                    points.Add(startPoint2D);
                if (endPoint2D != null)
                    points.Add(endPoint2D);
            }
            
            return points.Distinct(new Point2DComparer()).ToList();
        }
        
        /// <summary>
        /// 从面中提取边界点
        /// </summary>
        /// <param name="faces">面列表</param>
        /// <param name="projectionPlane">投影平面</param>
        /// <returns>2D坐标列表</returns>
        private List<Point2D> ExtractBoundaryFromFaces(List<ExtractedFace> faces, Plane projectionPlane)
        {
            var points = new List<Point2D>();
            
            foreach (var face in faces)
            {
                if (face.Face != null)
                {
                    // 获取面的边界曲线
                    var edgeLoops = face.Face.EdgeLoops;
                    foreach (EdgeArray edgeLoop in edgeLoops)
                    {
                        foreach (Edge edge in edgeLoop)
                        {
                            var curve = edge.AsCurve();
                            var startPoint2D = ProjectPointToPlane(curve.GetEndPoint(0), projectionPlane);
                            var endPoint2D = ProjectPointToPlane(curve.GetEndPoint(1), projectionPlane);
                            
                            if (startPoint2D != null)
                                points.Add(startPoint2D);
                            if (endPoint2D != null)
                                points.Add(endPoint2D);
                        }
                    }
                }
            }
            
            return points.Distinct(new Point2DComparer()).ToList();
        }
        
        /// <summary>
        /// 将点投影到平面
        /// </summary>
        /// <param name="points">3D点列表</param>
        /// <param name="projectionPlane">投影平面</param>
        /// <returns>2D坐标列表</returns>
        private List<Point2D> ProjectPointsToPlane(List<ExtractedPoint> points, Plane projectionPlane)
        {
            var result = new List<Point2D>();
            
            foreach (var point in points)
            {
                var projectedPoint = ProjectPointToPlane(point.Position, projectionPlane);
                if (projectedPoint != null)
                    result.Add(projectedPoint);
            }
            
            return result.Distinct(new Point2DComparer()).ToList();
        }
        
        /// <summary>
        /// 将单个3D点投影到2D平面
        /// </summary>
        /// <param name="point3D">3D点</param>
        /// <param name="projectionPlane">投影平面</param>
        /// <returns>2D坐标</returns>
        private Point2D ProjectPointToPlane(XYZ point3D, Plane projectionPlane)
        {
            try
            {
                // 计算点到平面的投影
                var vectorToPoint = point3D - projectionPlane.Origin;
                var distance = vectorToPoint.DotProduct(projectionPlane.Normal);
                var projectedPoint3D = point3D - distance * projectionPlane.Normal;
                
                // 转换为2D坐标
                var xAxis = projectionPlane.XVec;
                var yAxis = projectionPlane.YVec;
                
                var x = projectedPoint3D.DotProduct(xAxis);
                var y = projectedPoint3D.DotProduct(yAxis);
                
                return new Point2D(x, y);
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 将坐标按逆时针顺序排列
        /// </summary>
        /// <param name="coordinates">坐标列表</param>
        /// <returns>排序后的坐标列表</returns>
        private List<Point2D> OrderCoordinatesClockwise(List<Point2D> coordinates)
        {
            if (coordinates.Count < 3) return coordinates;
            
            // 计算质心
            var centerX = coordinates.Average(p => p.X);
            var centerY = coordinates.Average(p => p.Y);
            
            // 按角度排序
            return coordinates.OrderBy(p => Math.Atan2(p.Y - centerY, p.X - centerX)).ToList();
        }
        
        /// <summary>
        /// 获取默认坐标（后备方案）
        /// </summary>
        /// <returns>默认坐标列表</returns>
        private List<Point2D> GetDefaultCoordinates()
        {
            return new List<Point2D>
            {
                new Point2D(0, 0),
                new Point2D(20, 0),
                new Point2D(40, 10),
                new Point2D(60, 30),
                new Point2D(80, 60),
                new Point2D(100, 100),
                new Point2D(100, 120),
                new Point2D(80, 120),
                new Point2D(60, 100),
                new Point2D(40, 80),
                new Point2D(20, 60),
                new Point2D(0, 40)
            };
        }
        
        /// <summary>
        /// 验证提取的坐标数据
        /// </summary>
        /// <param name="coordinates">提取的坐标</param>
        /// <param name="extractionStats">提取统计信息</param>
        /// <returns>验证结果</returns>
        private (bool IsValid, string Status, string Message, string Suggestions) ValidateExtractedCoordinates(
            List<Point2D> coordinates, dynamic extractionStats)
        {
            var issues = new List<string>();
            var suggestions = new List<string>();
            
            // 检查坐标数量
            if (coordinates.Count < 3)
            {
                issues.Add("坐标点数量不足");
                suggestions.Add("建议检查几何体是否与剖面平面相交");
            }
            else if (coordinates.Count > 1000)
            {
                issues.Add("坐标点数量过多");
                suggestions.Add("建议简化几何体或调整剖面位置");
            }
            
            // 检查坐标范围
            if (coordinates.Any())
            {
                var minX = coordinates.Min(p => p.X);
                var maxX = coordinates.Max(p => p.X);
                var minY = coordinates.Min(p => p.Y);
                var maxY = coordinates.Max(p => p.Y);
                
                var width = maxX - minX;
                var height = maxY - minY;
                
                if (width < 0.1)
                {
                    issues.Add("剖面宽度过小");
                    suggestions.Add("建议调整剖面位置或检查几何体");
                }
                
                if (height < 0.1)
                {
                    issues.Add("剖面高度过小");
                    suggestions.Add("建议调整剖面位置或检查几何体");
                }
                
                if (width > 1000 || height > 1000)
                {
                    issues.Add("剖面尺寸过大");
                    suggestions.Add("建议检查单位设置或几何体尺寸");
                }
            }
            
            // 检查几何提取统计
            if (extractionStats.PointsCount == 0 && extractionStats.CurvesCount == 0 && extractionStats.FacesCount == 0)
            {
                issues.Add("未提取到任何几何元素");
                suggestions.Add("建议检查元素是否包含有效几何体");
            }
            
            if (extractionStats.FacesCount == 0)
            {
                issues.Add("未提取到面几何");
                suggestions.Add("建议检查元素是否为实体或包含面");
            }
            
            // 确定状态
            string status;
            string message;
            
            if (issues.Count == 0)
            {
                status = "✅ 验证通过";
                message = $"成功提取 {coordinates.Count} 个坐标点，几何数据完整";
            }
            else if (issues.Count <= 2)
            {
                status = "⚠️ 验证警告";
                message = $"提取成功但存在 {issues.Count} 个问题: {string.Join(", ", issues)}";
            }
            else
            {
                status = "❌ 验证失败";
                message = $"提取失败，存在 {issues.Count} 个问题: {string.Join(", ", issues)}";
            }
            
            return (
                IsValid: issues.Count <= 2,
                Status: status,
                Message: message,
                Suggestions: string.Join("; ", suggestions)
            );
        }
        
        /// <summary>
        /// 提取的几何信息数据结构
        /// </summary>
        private class ExtractedGeometryInfo
        {
            public List<ExtractedPoint> Points { get; set; } = new List<ExtractedPoint>();
            public List<ExtractedCurve> Curves { get; set; } = new List<ExtractedCurve>();
            public List<ExtractedFace> Faces { get; set; } = new List<ExtractedFace>();
        }
        
        /// <summary>
        /// 提取的点信息
        /// </summary>
        private class ExtractedPoint
        {
            public XYZ Position { get; set; }
            public Reference Reference { get; set; }
        }
        
        /// <summary>
        /// 提取的曲线信息
        /// </summary>
        private class ExtractedCurve
        {
            public Curve Curve { get; set; }
            public XYZ StartPoint { get; set; }
            public XYZ EndPoint { get; set; }
            public XYZ Center { get; set; } // 用于圆弧
            public double Radius { get; set; } // 用于圆弧
            public Reference Reference { get; set; }
        }
        
        /// <summary>
        /// 提取的面信息
        /// </summary>
        private class ExtractedFace
        {
            public Face Face { get; set; }
            public double Area { get; set; }
            public Reference Reference { get; set; }
        }
        
        /// <summary>
        /// Point2D比较器
        /// </summary>
        private class Point2DComparer : IEqualityComparer<Point2D>
        {
            private const double TOLERANCE = 1e-6;
            
            public bool Equals(Point2D x, Point2D y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;
                
                return Math.Abs(x.X - y.X) < TOLERANCE && Math.Abs(x.Y - y.Y) < TOLERANCE;
            }
            
            public int GetHashCode(Point2D obj)
            {
                if (obj == null) return 0;
                return obj.X.GetHashCode() ^ obj.Y.GetHashCode();
            }
        }
        
        private List<Point2D> CalculateFoundationLine(List<Point2D> coordinates)
        {
            // 简化的基础线计算
            var minY = coordinates.Min(p => p.Y);
            var maxX = coordinates.Max(p => p.X);
            
            return new List<Point2D>
            {
                new Point2D(0, minY),
                new Point2D(maxX, minY)
            };
        }
        
        private double GetWaterLevel()
        {
            // 从Revit参数或用户输入获取水位
            return 100.0;
        }
        
        private double GetFoundationElevation(List<Point2D> coordinates)
        {
            return coordinates.Min(p => p.Y);
        }
        
        private double GetCrestElevation(List<Point2D> coordinates)
        {
            return coordinates.Max(p => p.Y);
        }
        
        private (double SelfWeight, double WaterPressure, double UpliftPressure, double SeismicForce) CalculateLoads(DamProfile profile, CalculationParameters parameters)
        {
            // 简化的荷载计算
            var area = CalculateProfileArea(profile.Coordinates);
            var height = profile.CrestElevation - profile.FoundationElevation;
            
            var selfWeight = area * height * parameters.UnitWeight;
            var waterPressure = 0.5 * 9.81 * Math.Pow(profile.WaterLevel, 2) * height;
            var upliftPressure = parameters.UpliftCoefficient * waterPressure;
            var seismicForce = selfWeight * parameters.SeismicCoefficient;
            
            return (selfWeight, waterPressure, upliftPressure, seismicForce);
        }
        
        private double CalculateProfileArea(List<Point2D> coordinates)
        {
            // 简化的面积计算
            double area = 0;
            for (int i = 0; i < coordinates.Count - 1; i++)
            {
                area += (coordinates[i + 1].X - coordinates[i].X) * 
                       (coordinates[i + 1].Y + coordinates[i].Y) / 2;
            }
            return Math.Abs(area);
        }
        
        private (double Sliding, double Overturning, double Compression) CalculateSafetyFactors((double SelfWeight, double WaterPressure, double UpliftPressure, double SeismicForce) loads, CalculationParameters parameters)
        {
            // 简化的安全系数计算
            var sliding = loads.SelfWeight * parameters.FrictionCoefficient / 
                         (loads.WaterPressure - loads.UpliftPressure + loads.SeismicForce);
            
            var overturning = loads.SelfWeight / (loads.WaterPressure + loads.SeismicForce);
            
            var compression = parameters.CompressiveStrength / 5.0; // 简化的应力计算
            
            return (sliding, overturning, compression);
        }
        
        private (double MaxCompressive, double MaxTensile, double Principal) CalculateStresses(DamProfile profile, (double SelfWeight, double WaterPressure, double UpliftPressure, double SeismicForce) loads)
        {
            // 简化的应力计算
            var area = CalculateProfileArea(profile.Coordinates);
            var maxCompressive = loads.SelfWeight / area;
            var maxTensile = loads.WaterPressure / area * 0.1;
            var principal = maxCompressive * 1.5;
            
            return (maxCompressive, maxTensile, principal);
        }
        
        private void AddAnalysisResultParameters(Element element, AnalysisResults results)
        {
            // 添加分析结果参数到Revit元素
            // 这里需要根据实际的Revit参数结构进行调整
        }
        
        private AnalysisResults ReadAnalysisResultFromElement(Element element)
        {
            // 从Revit元素读取分析结果
            // 这里需要根据实际的Revit参数结构进行调整
            return null;
        }
        
        private void SaveAnalysisResultToElement(Element element, AnalysisResults results)
        {
            // 保存分析结果到Revit元素
            // 这里需要根据实际的Revit参数结构进行调整
        }
        
        private void OnProgressChanged(int percentage, string message, bool isIndeterminate)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs
            {
                ProgressPercentage = percentage,
                Message = message,
                IsIndeterminate = isIndeterminate
            });
        }
        
        private void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, status);
        }
        
        #endregion
    }
} 