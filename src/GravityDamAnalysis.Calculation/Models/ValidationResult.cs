namespace GravityDamAnalysis.Calculation.Models;

/// <summary>
/// 验证结果
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// 验证是否通过
    /// </summary>
    public bool IsValid { get; set; } = true;
    
    /// <summary>
    /// 错误信息列表
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();
    
    /// <summary>
    /// 警告信息列表
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();
    
    /// <summary>
    /// 添加错误信息
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="message">错误消息</param>
    public void AddError(string propertyName, string message)
    {
        Errors.Add(new ValidationError(propertyName, message));
        IsValid = false;
    }
    
    /// <summary>
    /// 添加警告信息
    /// </summary>
    /// <param name="propertyName">属性名</param>
    /// <param name="message">警告消息</param>
    public void AddWarning(string propertyName, string message)
    {
        Warnings.Add(new ValidationWarning(propertyName, message));
    }
    
    /// <summary>
    /// 获取所有错误消息
    /// </summary>
    /// <returns>错误消息字符串</returns>
    public string GetErrorMessages()
    {
        return string.Join("\n", Errors.Select(e => $"{e.PropertyName}: {e.Message}"));
    }
    
    /// <summary>
    /// 获取所有警告消息
    /// </summary>
    /// <returns>警告消息字符串</returns>
    public string GetWarningMessages()
    {
        return string.Join("\n", Warnings.Select(w => $"{w.PropertyName}: {w.Message}"));
    }
}

/// <summary>
/// 验证错误
/// </summary>
public class ValidationError
{
    public ValidationError(string propertyName, string message)
    {
        PropertyName = propertyName;
        Message = message;
    }
    
    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; }
    
    /// <summary>
    /// 错误消息
    /// </summary>
    public string Message { get; set; }
}

/// <summary>
/// 验证警告
/// </summary>
public class ValidationWarning
{
    public ValidationWarning(string propertyName, string message)
    {
        PropertyName = propertyName;
        Message = message;
    }
    
    /// <summary>
    /// 属性名
    /// </summary>
    public string PropertyName { get; set; }
    
    /// <summary>
    /// 警告消息
    /// </summary>
    public string Message { get; set; }
} 