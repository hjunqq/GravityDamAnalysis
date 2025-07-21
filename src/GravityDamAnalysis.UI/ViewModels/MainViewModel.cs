using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace GravityDamAnalysis.UI.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ILogger<MainViewModel> _logger;
    
    private DamEntity? _selectedDam;
    private bool _isAnalysisRunning;
    private string _analysisResults = string.Empty;
    
    public MainViewModel(ILogger<MainViewModel> logger)
    {
        _logger = logger;
        StartAnalysisCommand = new RelayCommand(ExecuteStartAnalysis, CanExecuteStartAnalysis);
        ClearResultsCommand = new RelayCommand(ExecuteClearResults);
    }
    
    public DamEntity? SelectedDam
    {
        get => _selectedDam;
        set
        {
            if (SetProperty(ref _selectedDam, value))
            {
                OnPropertyChanged(nameof(HasSelectedDam));
                ((RelayCommand)StartAnalysisCommand).RaiseCanExecuteChanged();
            }
        }
    }
    
    public bool HasSelectedDam => SelectedDam != null;
    
    public bool IsAnalysisRunning
    {
        get => _isAnalysisRunning;
        set
        {
            if (SetProperty(ref _isAnalysisRunning, value))
            {
                ((RelayCommand)StartAnalysisCommand).RaiseCanExecuteChanged();
            }
        }
    }
    
    public string AnalysisResults
    {
        get => _analysisResults;
        set => SetProperty(ref _analysisResults, value);
    }
    
    public ICommand StartAnalysisCommand { get; }
    public ICommand ClearResultsCommand { get; }
    
    private async void ExecuteStartAnalysis()
    {
        if (SelectedDam == null) return;
        
        try
        {
            IsAnalysisRunning = true;
            _logger.LogInformation("开始重力坝稳定性分析");
            
            // 这里将集成计算引擎
            await Task.Delay(2000); // 模拟计算过程
            
            AnalysisResults = $"重力坝稳定性分析结果:\n" +
                            $"坝体名称: {SelectedDam.Name}\n" +
                            $"体积: {SelectedDam.Geometry.Volume:F2} m³\n" +
                            $"材料: {SelectedDam.MaterialProperties.Name}\n" +
                            $"分析完成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            
            _logger.LogInformation("重力坝稳定性分析完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分析过程中发生错误");
            AnalysisResults = $"分析失败: {ex.Message}";
        }
        finally
        {
            IsAnalysisRunning = false;
        }
    }
    
    private bool CanExecuteStartAnalysis()
    {
        return HasSelectedDam && !IsAnalysisRunning;
    }
    
    private void ExecuteClearResults()
    {
        AnalysisResults = string.Empty;
        _logger.LogInformation("清除分析结果");
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
    
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

// 简单的RelayCommand实现
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }
    
    public event EventHandler? CanExecuteChanged;
    
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    
    public void Execute(object? parameter) => _execute();
    
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
} 