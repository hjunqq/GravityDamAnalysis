using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GravityDamAnalysis.Core.Models;
using GravityDamAnalysis.UI.Interfaces;

namespace GravityDamAnalysis.UI.Services
{
    /// <summary>
    /// Mock Revit集成服务，用于独立测试UI功能
    /// </summary>
    public class MockRevitIntegration : IRevitIntegration
    {
        public object RevitDocument => null;
        public object RevitApplication => null;
        public bool IsInRevitContext => false;
        
        public event EventHandler<ProgressEventArgs> ProgressChanged;
        public event EventHandler<string> StatusChanged;
        
        public async Task<List<DamGeometry>> AutoDetectDamsAsync()
        {
            OnStatusChanged("正在模拟识别坝体...");
            OnProgressChanged(0, "开始识别", true);
            
            await Task.Delay(2000); // 模拟处理时间
            
            OnProgressChanged(50, "识别中...", false);
            await Task.Delay(1000);
            
            OnProgressChanged(100, "识别完成", false);
            OnStatusChanged("坝体识别完成");
            
            // 返回模拟数据
            return new List<DamGeometry>
            {
                new DamGeometry
                {
                    Id = "Dam_001",
                    Name = "主坝体",
                    Type = DamType.Gravity,
                    Height = 120.0,
                    Length = 500.0,
                    Volume = 150000.0,
                    Material = "C25混凝土"
                },
                new DamGeometry
                {
                    Id = "Dam_002", 
                    Name = "副坝体",
                    Type = DamType.Gravity,
                    Height = 80.0,
                    Length = 200.0,
                    Volume = 80000.0,
                    Material = "C20混凝土"
                }
            };
        }
        
        public async Task<DamProfile> ExtractProfileAsync(DamGeometry dam, int profileIndex)
        {
            OnStatusChanged($"正在提取 {dam.Name} 的剖面...");
            OnProgressChanged(0, "开始提取", true);
            
            await Task.Delay(1500);
            
            OnProgressChanged(100, "提取完成", false);
            OnStatusChanged("剖面提取完成");
            
            return new DamProfile
            {
                DamId = dam.Id,
                ProfileIndex = profileIndex,
                Name = $"{dam.Name}_剖面_{profileIndex}",
                Coordinates = GenerateMockCoordinates(),
                FoundationLine = new List<Point2D>
                {
                    new Point2D(0, 0),
                    new Point2D(100, 0)
                },
                WaterLevel = 100.0
            };
        }
        
        public async Task<AnalysisResults> PerformStabilityAnalysisAsync(DamProfile profile, CalculationParameters parameters)
        {
            OnStatusChanged("正在执行稳定性分析...");
            OnProgressChanged(0, "开始分析", true);
            
            await Task.Delay(1000);
            OnProgressChanged(25, "计算荷载...", false);
            
            await Task.Delay(1000);
            OnProgressChanged(50, "计算安全系数...", false);
            
            await Task.Delay(1000);
            OnProgressChanged(75, "计算应力分布...", false);
            
            await Task.Delay(1000);
            OnProgressChanged(100, "分析完成", false);
            
            OnStatusChanged("稳定性分析完成");
            
            return new AnalysisResults
            {
                Id = Guid.NewGuid().ToString(),
                DamId = profile.DamId,
                ProfileId = profile.Name,
                AnalysisDateTime = DateTime.Now,
                SlidingSafetyFactor = 1.85,
                OverturningSafetyFactor = 2.1,
                CompressionSafetyFactor = 4.2,
                SlidingStatus = "安全",
                OverturningStatus = "安全", 
                CompressionStatus = "安全",
                SelfWeight = 1500000.0,
                WaterPressure = 800000.0,
                UpliftPressure = 200000.0,
                SeismicForce = 50000.0,
                MaxCompressiveStress = 8.5,
                MaxTensileStress = 0.2,
                PrincipalStress = 12.3
            };
        }
        
        public async Task WriteResultsToRevitAsync(AnalysisResults results)
        {
            OnStatusChanged("正在将结果写回Revit模型...");
            await Task.Delay(1000);
            OnStatusChanged("结果已成功写回Revit模型");
        }
        
        public async Task<ProjectInfo> GetProjectInfoAsync()
        {
            await Task.Delay(100);
            return new ProjectInfo
            {
                Name = "重力坝稳定性分析项目",
                Number = "GD-2024-001",
                Location = "长江流域",
                Client = "水利部",
                Engineer = "张工程师",
                Date = DateTime.Now
            };
        }
        
        public async Task<List<AnalysisResults>> GetRecentAnalysisResultsAsync()
        {
            await Task.Delay(100);
            return new List<AnalysisResults>
            {
                new AnalysisResults
                {
                    Id = "Result_001",
                    DamId = "Dam_001",
                    ProfileId = "主坝体_剖面_1",
                    AnalysisDateTime = DateTime.Now.AddDays(-1),
                    SlidingSafetyFactor = 1.75,
                    SlidingStatus = "安全"
                },
                new AnalysisResults
                {
                    Id = "Result_002",
                    DamId = "Dam_002", 
                    ProfileId = "副坝体_剖面_1",
                    AnalysisDateTime = DateTime.Now.AddDays(-2),
                    SlidingSafetyFactor = 1.95,
                    SlidingStatus = "安全"
                }
            };
        }
        
        public async Task SaveAnalysisResultsAsync(AnalysisResults results)
        {
            OnStatusChanged("正在保存分析结果...");
            await Task.Delay(500);
            OnStatusChanged("分析结果已保存");
        }
        
        public async Task<string> GenerateReportAsync(AnalysisResults results)
        {
            OnStatusChanged("正在生成分析报告...");
            await Task.Delay(2000);
            OnStatusChanged("分析报告已生成");
            
            return $"重力坝稳定性分析报告\n\n" +
                   $"坝体ID: {results.DamId}\n" +
                   $"剖面ID: {results.ProfileId}\n" +
                   $"分析时间: {results.AnalysisDateTime}\n\n" +
                   $"安全系数:\n" +
                   $"  抗滑稳定: {results.SlidingSafetyFactor:F2}\n" +
                   $"  抗倾覆: {results.OverturningSafetyFactor:F2}\n" +
                   $"  抗压强度: {results.CompressionSafetyFactor:F2}";
        }
        
        public async Task ExportToExcelAsync(AnalysisResults results, string filePath)
        {
            OnStatusChanged("正在导出到Excel...");
            await Task.Delay(1500);
            OnStatusChanged($"结果已导出到: {filePath}");
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
        
        private List<Point2D> GenerateMockCoordinates()
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
    }
} 