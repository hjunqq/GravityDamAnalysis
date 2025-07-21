using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.Services;
using GravityDamAnalysis.UI.Interfaces;

namespace GravityDamAnalysis.UI.ViewModels;

/// <summary>
/// 剖面验证窗口的ViewModel
/// 处理二维剖面的验证、预览和用户交互
/// </summary>
public class ProfileValidationViewModel : ViewModelBase
{
    private readonly ILogger<ProfileValidationViewModel> _logger;
    private readonly ProfileValidationEngine _validationEngine;
    
    private EnhancedProfile2D _profile;
    private string _profileName = string.Empty;
    private string _profileDescription = string.Empty;
    private string _extractionStatus = "未知";
    private DateTime _extractionTime = DateTime.Now;
    private bool _isValidationRunning;
    private string _validationStatus = "待验证";
    private double _zoomLevel = 1.0;
    private bool _showDimensions = true;
    private bool _showGrid = false;

    #region 公共属性

    /// <summary>
    /// 当前剖面数据
    /// </summary>
    public EnhancedProfile2D Profile
    {
        get => _profile;
        set
        {
            if (SetProperty(ref _profile, value))
            {
                LoadProfileData();
            }
        }
    }

    /// <summary>
    /// 剖面名称
    /// </summary>
    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    /// <summary>
    /// 剖面描述
    /// </summary>
    public string ProfileDescription
    {
        get => _profileDescription;
        set => SetProperty(ref _profileDescription, value);
    }

    /// <summary>
    /// 提取状态
    /// </summary>
    public string ExtractionStatus
    {
        get => _extractionStatus;
        set => SetProperty(ref _extractionStatus, value);
    }

    /// <summary>
    /// 提取时间
    /// </summary>
    public DateTime ExtractionTime
    {
        get => _extractionTime;
        set => SetProperty(ref _extractionTime, value);
    }

    /// <summary>
    /// 验证状态
    /// </summary>
    public string ValidationStatus
    {
        get => _validationStatus;
        set => SetProperty(ref _validationStatus, value);
    }

    /// <summary>
    /// 是否正在验证
    /// </summary>
    public bool IsValidationRunning
    {
        get => _isValidationRunning;
        set => SetProperty(ref _isValidationRunning, value);
    }

    /// <summary>
    /// 缩放级别
    /// </summary>
    public double ZoomLevel
    {
        get => _zoomLevel;
        set => SetProperty(ref _zoomLevel, value);
    }

    /// <summary>
    /// 显示尺寸标注
    /// </summary>
    public bool ShowDimensions
    {
        get => _showDimensions;
        set => SetProperty(ref _showDimensions, value);
    }

    /// <summary>
    /// 显示网格
    /// </summary>
    public bool ShowGrid
    {
        get => _showGrid;
        set => SetProperty(ref _showGrid, value);
    }

    #endregion

    #region 命令

    public ICommand RunValidationCommand { get; }
    public ICommand ConfirmValidationCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand FitToViewCommand { get; }

    #endregion

    #region 事件

    /// <summary>
    /// 验证确认事件
    /// </summary>
    public event EventHandler<ProfileValidationEventArgs> ValidationConfirmed;

    /// <summary>
    /// 取消事件
    /// </summary>
    public event EventHandler ValidationCancelled;

    /// <summary>
    /// 剖面更新事件（用于通知UI重绘）
    /// </summary>
    public event EventHandler ProfileUpdated;

    #endregion

    #region 构造函数

    public ProfileValidationViewModel(
        ILogger<ProfileValidationViewModel> logger,
        ProfileValidationEngine validationEngine,
        IRevitIntegration revitIntegration) : base(revitIntegration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validationEngine = validationEngine ?? throw new ArgumentNullException(nameof(validationEngine));

        // 初始化命令
        RunValidationCommand = new RelayCommand(RunValidation, CanRunValidation);
        ConfirmValidationCommand = new RelayCommand(ConfirmValidation, CanConfirmValidation);
        CancelCommand = new RelayCommand(CancelValidation);
        ZoomInCommand = new RelayCommand(ZoomIn);
        ZoomOutCommand = new RelayCommand(ZoomOut);
        FitToViewCommand = new RelayCommand(FitToView);
    }

    #endregion

    #region 私有方法

    /// <summary>
    /// 加载剖面数据
    /// </summary>
    private void LoadProfileData()
    {
        if (Profile == null) return;

        try
        {
            ProfileName = Profile.Name ?? "未命名剖面";
            ProfileDescription = "二维剖面验证"; // 简化处理
            ExtractionTime = Profile.CreatedAt;
            
            // 简化状态设置
            if (Profile.Status == GravityDamAnalysis.Core.Entities.ValidationStatus.Pending)
                ExtractionStatus = "提取完成，待验证";
            else if (Profile.Status == GravityDamAnalysis.Core.Entities.ValidationStatus.Validated)
                ExtractionStatus = "已验证";
            else
                ExtractionStatus = "需要验证";

            _logger.LogInformation("已加载剖面数据: {ProfileName}", ProfileName);
            
            // 通知UI更新
            OnPropertyChanged();
            ProfileUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载剖面数据时发生错误");
        }
    }

    #endregion

    #region 命令实现

    /// <summary>
    /// 运行验证
    /// </summary>
    private void RunValidation()
    {
        if (Profile == null) return;

        try
        {
            IsValidationRunning = true;
            ValidationStatus = "验证中...";

            // 简化验证逻辑
            var result = _validationEngine.ValidateProfile(Profile);
            
            if (result.OverallStatus == GravityDamAnalysis.Core.Entities.ValidationStatus.Validated)
            {
                ValidationStatus = "验证通过";
            }
            else
            {
                ValidationStatus = "发现问题";
            }

            _logger.LogInformation("剖面验证完成，状态: {Status}", ValidationStatus);
            ProfileUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "验证过程中发生错误");
            ValidationStatus = "验证失败";
        }
        finally
        {
            IsValidationRunning = false;
        }
    }

    /// <summary>
    /// 确认验证
    /// </summary>
    private void ConfirmValidation()
    {
        if (Profile == null) return;

        try
        {
            Profile.Status = GravityDamAnalysis.Core.Entities.ValidationStatus.Validated;
            
            var args = new ProfileValidationEventArgs
            {
                Profile = Profile,
                Action = ValidationAction.Confirmed
            };

            ValidationConfirmed?.Invoke(this, args);
            _logger.LogInformation("用户确认验证通过: {ProfileName}", ProfileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "确认验证时发生错误");
        }
    }

    /// <summary>
    /// 取消验证
    /// </summary>
    private void CancelValidation()
    {
        try
        {
            ValidationCancelled?.Invoke(this, EventArgs.Empty);
            _logger.LogInformation("用户取消验证");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "取消验证时发生错误");
        }
    }

    /// <summary>
    /// 放大视图
    /// </summary>
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel * 1.25, 5.0);
        ProfileUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 缩小视图
    /// </summary>
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel / 1.25, 0.1);
        ProfileUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 适应窗口
    /// </summary>
    private void FitToView()
    {
        ZoomLevel = 1.0;
        ProfileUpdated?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region 命令条件判断

    private bool CanRunValidation() => Profile != null && !IsValidationRunning;

    private bool CanConfirmValidation() => Profile != null && ValidationStatus == "验证通过";

    #endregion
}

/// <summary>
/// 剖面验证事件参数
/// </summary>
public class ProfileValidationEventArgs : EventArgs
{
    public EnhancedProfile2D Profile { get; set; }
    public ValidationAction Action { get; set; }
}

/// <summary>
/// 验证动作枚举
/// </summary>
public enum ValidationAction
{
    Confirmed,
    ReExtract,
    Cancelled
}

 