using System.Windows;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.UI.Services;
using GravityDamAnalysis.UI.ViewModels;
using GravityDamAnalysis.UI.Interfaces;

namespace GravityDamAnalysis.UI.Views
{
    /// <summary>
    /// MainDashboard.xaml 的交互逻辑
    /// </summary>
    public partial class MainDashboard : Window
    {
        private MainDashboardViewModel _viewModel;
        
        public MainDashboard()
        {
            InitializeComponent();
            
            // 默认使用Mock Revit集成服务（用于独立测试）
            var revitIntegration = new MockRevitIntegration();
            
            // 创建Logger (空配置)
            var loggerFactory = LoggerFactory.Create(builder => { });
            var logger = loggerFactory.CreateLogger<MainDashboardViewModel>();
            
            // 创建ViewModel
            _viewModel = new MainDashboardViewModel(revitIntegration, logger);
            
            // 设置DataContext
            DataContext = _viewModel;
        }
        
        /// <summary>
        /// 设置Revit集成服务（用于在Revit环境中运行）
        /// </summary>
        public void SetRevitIntegration(IRevitIntegration revitIntegration)
        {
            // 创建Logger
            var loggerFactory = LoggerFactory.Create(builder => { });
            var logger = loggerFactory.CreateLogger<MainDashboardViewModel>();
            
            // 重新创建ViewModel以使用真实的Revit集成
            _viewModel = new MainDashboardViewModel(revitIntegration, logger);
            DataContext = _viewModel;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
} 