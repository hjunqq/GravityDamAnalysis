using System;
using System.Windows;
using GravityDamAnalysis.UI.Views;

namespace GravityDamAnalysis.UI
{
    public class TestUI
    {
        [STAThread]
        public static void Main()
        {
            try
            {
                var app = new Application();
                
                // 测试主控制台（包含完整业务逻辑）
                var dashboardWindow = new MainDashboard();
                dashboardWindow.Show();
                
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"UI测试失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 