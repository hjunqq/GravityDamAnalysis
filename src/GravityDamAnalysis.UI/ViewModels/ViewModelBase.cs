using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using GravityDamAnalysis.UI.Interfaces;

namespace GravityDamAnalysis.UI.ViewModels
{
    /// <summary>
    /// ViewModel基类
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        protected readonly IRevitIntegration _revitIntegration;
        
        public ViewModelBase(IRevitIntegration revitIntegration)
        {
            _revitIntegration = revitIntegration;
            
            // 订阅事件
            _revitIntegration.ProgressChanged += OnProgressChanged;
            _revitIntegration.StatusChanged += OnStatusChanged;
        }
        
        // 属性变更通知
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        
        // 进度和状态处理
        protected virtual void OnProgressChanged(object sender, ProgressEventArgs e)
        {
            // 子类可以重写此方法
        }
        
        protected virtual void OnStatusChanged(object sender, string status)
        {
            // 子类可以重写此方法
        }
        
        // 异步命令执行
        protected async Task ExecuteAsync(Func<Task> action, string errorMessage = "操作失败")
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                // 这里可以添加日志记录
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
                throw new Exception(errorMessage, ex);
            }
        }
    }
    
    /// <summary>
    /// 异步命令基类
    /// </summary>
    public class AsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;
        
        public AsyncCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }
        
        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    CommandManager.InvalidateRequerySuggested();
                    await _execute();
                }
                finally
                {
                    _isExecuting = false;
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
} 