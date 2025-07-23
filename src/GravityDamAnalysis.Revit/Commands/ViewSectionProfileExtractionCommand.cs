using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GravityDamAnalysis.Core.Models;
using GravityDamAnalysis.Revit.Services;
using GravityDamAnalysis.Revit.SectionAnalysis;
using Microsoft.Extensions.Logging;

namespace GravityDamAnalysis.Revit.Commands
{
    /// <summary>
    /// 剖面提取命令 - 支持拉伸模型
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ViewSectionProfileExtractionCommand : IExternalCommand
    {
        private readonly ILogger<ViewSectionProfileExtractionCommand> _logger;

        public ViewSectionProfileExtractionCommand(ILogger<ViewSectionProfileExtractionCommand> logger)
        {
            _logger = logger;
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var doc = uiApp.ActiveUIDocument.Document;
                var uidoc = uiApp.ActiveUIDocument;

                _logger?.LogInformation("开始执行剖面提取命令");

                // 获取选中的元素
                var selectedElements = uidoc.Selection.GetElementIds()
                    .Select(id => doc.GetElement(id))
                    .Where(e => e != null)
                    .ToList();

                if (!selectedElements.Any())
                {
                    TaskDialog.Show("剖面提取", "请先选择要提取剖面的坝体元素。");
                    return Result.Failed;
                }

                // 检查是否包含拉伸模型
                var extrusionModels = selectedElements.Where(e => IsExtrusionModel(e)).ToList();
                var regularModels = selectedElements.Except(extrusionModels).ToList();

                if (extrusionModels.Any())
                {
                    _logger?.LogInformation($"检测到 {extrusionModels.Count} 个拉伸模型");
                    TaskDialog.Show("拉伸模型检测", 
                        $"检测到 {extrusionModels.Count} 个拉伸模型，将使用优化的拉伸剖面提取方法。");
                }

                // 创建Revit集成服务
                var revitIntegration = new RevitIntegration(uiApp, null);

                // 执行剖面提取
                var result = ExecuteProfileExtraction(revitIntegration, selectedElements, extrusionModels);

                if (result)
                {
                    TaskDialog.Show("剖面提取完成", "剖面提取已成功完成！");
                    return Result.Succeeded;
                }
                else
                {
                    message = "剖面提取过程中出现错误，请查看日志获取详细信息。";
                    return Result.Failed;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "剖面提取命令执行失败");
                message = $"剖面提取失败: {ex.Message}";
                return Result.Failed;
            }
        }

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
        /// 执行剖面提取
        /// </summary>
        /// <param name="revitIntegration">Revit集成服务</param>
        /// <param name="selectedElements">选中的元素</param>
        /// <param name="extrusionModels">拉伸模型</param>
        /// <returns>是否成功</returns>
        private bool ExecuteProfileExtraction(RevitIntegration revitIntegration, List<Element> selectedElements, List<Element> extrusionModels)
        {
            try
            {
                // 为每个元素创建坝体几何信息
                var damGeometries = new List<DamGeometry>();
                
                foreach (var element in selectedElements)
                {
                    var isExtrusion = extrusionModels.Contains(element);
                    var damGeometry = new DamGeometry
                    {
                        Id = element.Id.Value.ToString(),
                        Name = isExtrusion ? $"拉伸模型_{element.Id.Value}" : $"常规模型_{element.Id.Value}",
                        Type = DamType.Gravity,
                        Height = GetElementHeight(element),
                        Length = GetElementLength(element),
                        Volume = GetElementVolume(element),
                        Material = GetElementMaterial(element)
                    };
                    
                    damGeometries.Add(damGeometry);
                }

                // 提取剖面（支持多个剖面索引）
                var profileIndexes = new[] { 0, 1, 2 }; // X, Y, Z方向剖面
                var extractedProfiles = new List<DamProfile>();

                foreach (var damGeometry in damGeometries)
                {
                    foreach (var profileIndex in profileIndexes)
                    {
                        try
                        {
                            var profile = revitIntegration.ExtractProfileAsync(damGeometry, profileIndex).Result;
                            if (profile != null && profile.Coordinates.Any())
                            {
                                extractedProfiles.Add(profile);
                                _logger?.LogInformation($"成功提取 {damGeometry.Name} 的剖面 {profileIndex}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, $"提取 {damGeometry.Name} 的剖面 {profileIndex} 时出错");
                        }
                    }
                }

                if (extractedProfiles.Any())
                {
                    _logger?.LogInformation($"成功提取 {extractedProfiles.Count} 个剖面");
                    
                    // 这里可以添加剖面验证和保存逻辑
                    // 例如：保存到数据库、生成报告等
                    
                    return true;
                }
                else
                {
                    _logger?.LogWarning("未成功提取任何剖面");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "执行剖面提取时发生错误");
                return false;
            }
        }

        #region 辅助方法

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

        private string GetElementMaterial(Element element)
        {
            try
            {
                if (element is FamilyInstance)
                    return "拉伸混凝土";
                else
                    return "混凝土材料";
            }
            catch
            {
                return "混凝土材料";
            }
        }

        #endregion
    }
} 