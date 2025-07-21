using System;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GravityDamAnalysis.Revit.Services;
using GravityDamAnalysis.UI.Interfaces;
using GravityDamAnalysis.UI.Views;
using GravityDamAnalysis.UI.ViewModels;

namespace GravityDamAnalysis.Revit.Commands
{
    /// <summary>
    /// 重力坝稳定性分析主命令
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class GravityDamAnalysisCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApplication = commandData.Application;
                var document = uiApplication.ActiveUIDocument?.Document;
                
                if (document == null)
                {
                    TaskDialog.Show("错误", "没有活动的Revit文档。请先打开一个Revit项目。");
                    return Result.Failed;
                }
                
                // 创建Revit集成服务
                IRevitIntegration revitIntegration = new RevitIntegration(uiApplication);
                
                // 创建主控制台窗口
                var dashboardWindow = new MainDashboard();
                
                // 设置Revit集成服务
                dashboardWindow.SetRevitIntegration(revitIntegration);
                
                // 显示窗口
                dashboardWindow.Show();
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"执行命令时出错: {ex.Message}";
                TaskDialog.Show("错误", message);
                return Result.Failed;
            }
        }
    }
} 