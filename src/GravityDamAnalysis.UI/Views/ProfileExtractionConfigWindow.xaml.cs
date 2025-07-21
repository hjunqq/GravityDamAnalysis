using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Models;
using GravityDamAnalysis.UI.Interfaces;
using Microsoft.Win32;

namespace GravityDamAnalysis.UI.Views;

/// <summary>
/// 剖面提取配置窗口
/// </summary>
public partial class ProfileExtractionConfigWindow : Window, INotifyPropertyChanged
{
    private readonly IRevitIntegration _revitIntegration;
    private readonly ILogger<ProfileExtractionConfigWindow> _logger;
    
    // 绑定属性
    private ObservableCollection<SelectableDamGeometry> _availableDams;
    private int _profileCount = 3;
    private string _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    private bool _isExtracting;
    private string _extractionStatus = "就绪";
    private int _extractionProgress;
    
    // 提取结果
    public List<DamProfile> ExtractedProfiles { get; private set; } = new();
    public bool ExtractionSuccessful { get; private set; }

    public ProfileExtractionConfigWindow(IRevitIntegration revitIntegration, ILogger<ProfileExtractionConfigWindow> logger)
    {
        InitializeComponent();
        
        _revitIntegration = revitIntegration ?? throw new ArgumentNullException(nameof(revitIntegration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _availableDams = new ObservableCollection<SelectableDamGeometry>();
        
        DataContext = this;
        Loaded += OnLoaded;
    }

    #region 绑定属性

    public ObservableCollection<SelectableDamGeometry> AvailableDams
    {
        get => _availableDams;
        set
        {
            _availableDams = value;
            OnPropertyChanged();
        }
    }

    public int ProfileCount
    {
        get => _profileCount;
        set
        {
            _profileCount = value;
            OnPropertyChanged();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            _outputPath = value;
            OnPropertyChanged();
        }
    }

    public bool IsExtracting
    {
        get => _isExtracting;
        set
        {
            _isExtracting = value;
            OnPropertyChanged();
            UpdateButtonStates();
        }
    }

    public string ExtractionStatus
    {
        get => _extractionStatus;
        set
        {
            _extractionStatus = value;
            OnPropertyChanged();
        }
    }

    public int ExtractionProgress
    {
        get => _extractionProgress;
        set
        {
            _extractionProgress = value;
            OnPropertyChanged();
        }
    }

    #endregion

    #region 事件处理

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAvailableDams();
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedDams = GetSelectedDams();
            if (!selectedDams.Any())
            {
                MessageBox.Show("请至少选择一个坝体进行预览。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var config = GetExtractionConfiguration();
            
            // 显示预览信息
            var previewMessage = $"将为以下坝体提取剖面：\n\n" +
                                $"选中坝体: {string.Join(", ", selectedDams.Select(d => d.Name))}\n" +
                                $"剖面数量: 每个坝体 {ProfileCount} 个剖面\n" +
                                $"分布方式: {GetDistributionTypeText()}\n" +
                                $"几何精度: {GetAccuracyText()}\n" +
                                $"总计剖面: {selectedDams.Count * ProfileCount} 个\n\n" +
                                $"预计用时: {EstimateExtractionTime(selectedDams.Count * ProfileCount)} 分钟";

            MessageBox.Show(previewMessage, "提取预览", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "预览剖面提取配置时发生错误");
            MessageBox.Show($"预览失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Extract_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selectedDams = GetSelectedDams();
            if (!selectedDams.Any())
            {
                MessageBox.Show("请至少选择一个坝体进行提取。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定要开始提取 {selectedDams.Count * ProfileCount} 个剖面吗？\n\n此操作可能需要较长时间。",
                "确认提取",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await StartExtractionProcess(selectedDams);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "开始剖面提取时发生错误");
            MessageBox.Show($"启动提取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (IsExtracting)
        {
            var result = MessageBox.Show(
                "确定要取消正在进行的提取操作吗？",
                "确认取消",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // 这里可以添加取消逻辑
                IsExtracting = false;
                ExtractionStatus = "已取消";
                DialogResult = false;
            }
        }
        else
        {
            DialogResult = false;
        }
    }

    private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择输出文件夹",
            InitialDirectory = OutputPath
        };

        if (dialog.ShowDialog() == true)
        {
            OutputPath = dialog.FolderName;
        }
    }

    #endregion

    #region 私有方法

    private async Task LoadAvailableDams()
    {
        try
        {
            ExtractionStatus = "正在加载坝体列表...";
            
            var dams = await _revitIntegration.AutoDetectDamsAsync();
            
            AvailableDams.Clear();
            foreach (var dam in dams)
            {
                AvailableDams.Add(new SelectableDamGeometry(dam));
            }
            
            ExtractionStatus = $"已加载 {dams.Count} 个坝体";
            _logger.LogInformation("成功加载 {Count} 个坝体", dams.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载坝体列表时发生错误");
            ExtractionStatus = $"加载失败: {ex.Message}";
            MessageBox.Show($"加载坝体列表失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private List<DamGeometry> GetSelectedDams()
    {
        return AvailableDams
            .Where(d => d.IsSelected)
            .Select(d => d.Dam)
            .ToList();
    }

    private ProfileExtractionConfig GetExtractionConfiguration()
    {
        return new ProfileExtractionConfig
        {
            ProfileCount = ProfileCount,
            DistributionType = GetSelectedDistributionType(),
            ExtractFoundation = ExtractFoundationChk.IsChecked == true,
            ExtractMaterialZones = ExtractMaterialZonesChk.IsChecked == true,
            ExtractDrainage = ExtractDrainageChk.IsChecked == true,
            GeometricAccuracy = GetSelectedAccuracy(),
            MeshDensity = GetSelectedMeshDensity(),
            OutputPath = OutputPath,
            SaveToDatabase = SaveToDatabaseChk.IsChecked == true,
            ExportToDwg = ExportToDwgChk.IsChecked == true,
            GenerateReport = GenerateReportChk.IsChecked == true
        };
    }

    private async Task StartExtractionProcess(List<DamGeometry> selectedDams)
    {
        IsExtracting = true;
        ExtractionProgress = 0;
        ExtractedProfiles.Clear();

        var totalOperations = selectedDams.Count * ProfileCount;
        var completedOperations = 0;

        try
        {
            foreach (var dam in selectedDams)
            {
                ExtractionStatus = $"正在提取 {dam.Name} 的剖面...";
                
                for (int i = 0; i < ProfileCount; i++)
                {
                    ExtractionStatus = $"提取 {dam.Name} - 剖面 {i + 1}/{ProfileCount}";
                    
                    var profile = await _revitIntegration.ExtractProfileAsync(dam, i);
                    if (profile != null)
                    {
                        ExtractedProfiles.Add(profile);
                        _logger.LogInformation("成功提取剖面: {DamName} - 剖面 {Index}", dam.Name, i);
                    }
                    
                    completedOperations++;
                    ExtractionProgress = (int)((double)completedOperations / totalOperations * 100);
                    
                    // 允许UI更新
                    await Task.Delay(50);
                }
            }

            ExtractionSuccessful = true;
            ExtractionStatus = $"提取完成！成功提取 {ExtractedProfiles.Count} 个剖面";
            
            MessageBox.Show(
                $"剖面提取完成！\n\n成功提取: {ExtractedProfiles.Count} 个剖面\n输出路径: {OutputPath}",
                "提取完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            DialogResult = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "剖面提取过程中发生错误");
            ExtractionStatus = $"提取失败: {ex.Message}";
            MessageBox.Show($"剖面提取失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsExtracting = false;
        }
    }

    private void UpdateButtonStates()
    {
        ExtractButton.IsEnabled = !IsExtracting;
        PreviewButton.IsEnabled = !IsExtracting;
    }

    private string GetDistributionTypeText()
    {
        return DistributionTypeCombo.SelectedIndex switch
        {
            0 => "均匀分布",
            1 => "关键位置",
            2 => "自定义位置",
            _ => "未知"
        };
    }

    private string GetAccuracyText()
    {
        return GeometricAccuracyCombo.SelectedIndex switch
        {
            0 => "粗糙 (0.1m)",
            1 => "标准 (0.01m)",
            2 => "精细 (0.001m)",
            _ => "未知"
        };
    }

    private DistributionType GetSelectedDistributionType()
    {
        return DistributionTypeCombo.SelectedIndex switch
        {
            0 => DistributionType.Uniform,
            1 => DistributionType.KeyPositions,
            2 => DistributionType.Custom,
            _ => DistributionType.Uniform
        };
    }

    private double GetSelectedAccuracy()
    {
        return GeometricAccuracyCombo.SelectedIndex switch
        {
            0 => 0.1,
            1 => 0.01,
            2 => 0.001,
            _ => 0.01
        };
    }

    private MeshDensity GetSelectedMeshDensity()
    {
        return MeshDensityCombo.SelectedIndex switch
        {
            0 => MeshDensity.Sparse,
            1 => MeshDensity.Standard,
            2 => MeshDensity.Dense,
            _ => MeshDensity.Standard
        };
    }

    private int EstimateExtractionTime(int profileCount)
    {
        // 估算每个剖面需要30秒
        return Math.Max(1, profileCount * 30 / 60);
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// 可选择的坝体几何信息
/// </summary>
public class SelectableDamGeometry : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public SelectableDamGeometry(DamGeometry dam)
    {
        Dam = dam ?? throw new ArgumentNullException(nameof(dam));
    }

    public DamGeometry Dam { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public string Name => Dam.Name;
    public double Height => Dam.Height;
    public DamType Type => Dam.Type;
    public string Material => Dam.Material;

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// 剖面提取配置
/// </summary>
public class ProfileExtractionConfig
{
    public int ProfileCount { get; set; }
    public DistributionType DistributionType { get; set; }
    public bool ExtractFoundation { get; set; }
    public bool ExtractMaterialZones { get; set; }
    public bool ExtractDrainage { get; set; }
    public double GeometricAccuracy { get; set; }
    public MeshDensity MeshDensity { get; set; }
    public string OutputPath { get; set; }
    public bool SaveToDatabase { get; set; }
    public bool ExportToDwg { get; set; }
    public bool GenerateReport { get; set; }
}

/// <summary>
/// 分布类型枚举
/// </summary>
public enum DistributionType
{
    Uniform,        // 均匀分布
    KeyPositions,   // 关键位置
    Custom          // 自定义位置
}

/// <summary>
/// 网格密度枚举
/// </summary>
public enum MeshDensity
{
    Sparse,     // 稀疏
    Standard,   // 标准
    Dense       // 密集
} 