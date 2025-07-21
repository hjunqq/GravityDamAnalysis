using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Microsoft.Extensions.Logging;
using GravityDamAnalysis.Core.Entities;

namespace GravityDamAnalysis.Revit.SectionAnalysis;

/// <summary>
/// 安全事务管理器
/// 提供批量操作的事务管理和错误恢复机制
/// </summary>
public class SafeTransactionManager
{
    private readonly ILogger<SafeTransactionManager> _logger;
    private readonly Document _document;

    public SafeTransactionManager(Document document, ILogger<SafeTransactionManager> logger)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 安全执行批量剖面提取
    /// </summary>
    /// <param name="damElements">坝体元素列表</param>
    /// <param name="sectionLocations">剖面位置列表</param>
    /// <param name="extractor">剖面提取器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提取的剖面列表</returns>
    public async Task<List<EnhancedProfile2D>> ExtractSectionsWithTransactionAsync(
        List<Element> damElements,
        List<SectionLocation> sectionLocations,
        AdvancedSectionExtractor extractor,
        CancellationToken cancellationToken = default)
    {
        var results = new List<EnhancedProfile2D>();
        var progressReporter = new ProgressReporter(_logger);

        _logger.LogInformation("开始批量剖面提取：{ElementCount} 个元素，{SectionCount} 个剖面",
            damElements.Count, sectionLocations.Count);

        using var transactionGroup = new TransactionGroup(_document, "批量剖面提取");
        
        try
        {
            transactionGroup.Start();
            progressReporter.Report(0, "初始化事务组");

            var totalOperations = damElements.Count * sectionLocations.Count;
            var completedOperations = 0;

            foreach (var damElement in damElements)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var location in sectionLocations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var operationName = $"提取剖面: {damElement.Name}-{location.Name}";
                    progressReporter.Report(
                        (double)completedOperations / totalOperations * 100,
                        operationName);

                    using var transaction = new Transaction(_document, operationName);
                    
                    try
                    {
                        transaction.Start();
                        
                        // 创建剖面平面
                        var sectionPlane = CreateSectionPlane(location);
                        
                        // 执行剖面提取
                        var profile = await Task.Run(() => 
                            extractor.ExtractSectionProfileAdvanced(damElement, sectionPlane, location.Name),
                            cancellationToken);

                        if (profile?.IsValid() == true)
                        {
                            results.Add(profile);
                            transaction.Commit();
                            _logger.LogDebug("成功提取剖面: {Name}", location.Name);
                        }
                        else
                        {
                            transaction.RollBack();
                            _logger.LogWarning("剖面 {Name} 提取失败，已回滚", location.Name);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        transaction.RollBack();
                        _logger.LogInformation("剖面提取被用户取消");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        transaction.RollBack();
                        _logger.LogError(ex, "剖面 {Name} 提取出错，事务已回滚", location.Name);
                        
                        // 继续处理其他剖面，不因单个失败而中断
                        continue;
                    }

                    completedOperations++;
                }
            }

            transactionGroup.Assimilate();
            progressReporter.Report(100, "批量提取完成");
            
            _logger.LogInformation("批量剖面提取完成：成功 {SuccessCount}/{TotalCount}",
                results.Count, totalOperations);

            return results;
        }
        catch (OperationCanceledException)
        {
            transactionGroup.RollBack();
            _logger.LogInformation("批量剖面提取被取消，所有操作已回滚");
            throw;
        }
        catch (Exception ex)
        {
            transactionGroup.RollBack();
            _logger.LogError(ex, "批量剖面提取失败，所有操作已回滚");
            throw;
        }
    }

    /// <summary>
    /// 安全执行单个剖面提取
    /// </summary>
    /// <param name="damElement">坝体元素</param>
    /// <param name="location">剖面位置</param>
    /// <param name="extractor">剖面提取器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>提取的剖面</returns>
    public async Task<EnhancedProfile2D> ExtractSingleSectionAsync(
        Element damElement,
        SectionLocation location,
        AdvancedSectionExtractor extractor,
        CancellationToken cancellationToken = default)
    {
        var operationName = $"提取剖面: {damElement.Name}-{location.Name}";
        _logger.LogInformation("开始 {OperationName}", operationName);

        using var transaction = new Transaction(_document, operationName);
        
        try
        {
            transaction.Start();
            cancellationToken.ThrowIfCancellationRequested();

            // 创建剖面平面
            var sectionPlane = CreateSectionPlane(location);
            
            // 执行剖面提取
            var profile = await Task.Run(() => 
                extractor.ExtractSectionProfileAdvanced(damElement, sectionPlane, location.Name),
                cancellationToken);

            if (profile?.IsValid() == true)
            {
                transaction.Commit();
                _logger.LogInformation("成功完成 {OperationName}", operationName);
                return profile;
            }
            else
            {
                transaction.RollBack();
                _logger.LogWarning("{OperationName} 失败：无效的剖面数据", operationName);
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            transaction.RollBack();
            _logger.LogInformation("{OperationName} 被取消", operationName);
            throw;
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            _logger.LogError(ex, "{OperationName} 发生错误，事务已回滚", operationName);
            throw;
        }
    }

    /// <summary>
    /// 创建剖面平面
    /// </summary>
    /// <param name="location">剖面位置</param>
    /// <returns>Revit平面对象</returns>
    private Plane CreateSectionPlane(SectionLocation location)
    {
        try
        {
            // 转换坐标系
            var origin = new XYZ(location.Position.X, location.Position.Y, location.Position.Z);
            var normal = new XYZ(location.Normal.X, location.Normal.Y, location.Normal.Z).Normalize();
            
            return Plane.CreateByNormalAndOrigin(normal, origin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建剖面平面失败");
            throw;
        }
    }

    /// <summary>
    /// 验证事务状态
    /// </summary>
    /// <param name="transaction">事务对象</param>
    /// <returns>是否有效</returns>
    private bool ValidateTransactionState(Transaction transaction)
    {
        try
        {
            return transaction.GetStatus() == TransactionStatus.Started;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "验证事务状态失败");
            return false;
        }
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        _logger.LogInformation("SafeTransactionManager 资源已清理");
    }
}

/// <summary>
/// 进度报告器
/// </summary>
public class ProgressReporter
{
    private readonly ILogger _logger;
    private DateTime _lastReportTime = DateTime.Now;
    private const double REPORT_INTERVAL_SECONDS = 1.0; // 每秒最多报告一次

    public ProgressReporter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 报告进度
    /// </summary>
    /// <param name="percentage">完成百分比 (0-100)</param>
    /// <param name="message">状态消息</param>
    public void Report(double percentage, string message)
    {
        var now = DateTime.Now;
        
        // 限制报告频率
        if ((now - _lastReportTime).TotalSeconds < REPORT_INTERVAL_SECONDS && percentage < 100)
        {
            return;
        }

        _lastReportTime = now;
        _logger.LogInformation("进度: {Percentage:F1}% - {Message}", percentage, message);
    }
}

/// <summary>
/// 批量操作结果
/// </summary>
public class BatchOperationResult<T>
{
    /// <summary>
    /// 成功结果列表
    /// </summary>
    public List<T> SuccessResults { get; set; } = new();

    /// <summary>
    /// 失败操作列表
    /// </summary>
    public List<FailedOperation> FailedOperations { get; set; } = new();

    /// <summary>
    /// 总操作数
    /// </summary>
    public int TotalOperations { get; set; }

    /// <summary>
    /// 成功率
    /// </summary>
    public double SuccessRate => TotalOperations > 0 ? (double)SuccessResults.Count / TotalOperations : 0;

    /// <summary>
    /// 是否全部成功
    /// </summary>
    public bool AllSucceeded => FailedOperations.Count == 0;
}

/// <summary>
/// 失败操作记录
/// </summary>
public class FailedOperation
{
    /// <summary>
    /// 操作名称
    /// </summary>
    public string OperationName { get; set; } = string.Empty;

    /// <summary>
    /// 错误消息
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// 异常详情
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// 失败时间
    /// </summary>
    public DateTime FailureTime { get; set; } = DateTime.Now;
}

/// <summary>
/// 事务超时管理器
/// </summary>
public class TransactionTimeoutManager
{
    private readonly ILogger<TransactionTimeoutManager> _logger;
    private readonly TimeSpan _defaultTimeout;

    public TransactionTimeoutManager(ILogger<TransactionTimeoutManager> logger, TimeSpan? timeout = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaultTimeout = timeout ?? TimeSpan.FromMinutes(5); // 默认5分钟超时
    }

    /// <summary>
    /// 执行带超时的事务操作
    /// </summary>
    /// <typeparam name="T">返回类型</typeparam>
    /// <param name="document">文档对象</param>
    /// <param name="operationName">操作名称</param>
    /// <param name="operation">操作函数</param>
    /// <param name="timeout">超时时间</param>
    /// <returns>操作结果</returns>
    public async Task<T> ExecuteWithTimeoutAsync<T>(
        Document document,
        string operationName,
        Func<Transaction, Task<T>> operation,
        TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? _defaultTimeout;
        _logger.LogInformation("开始执行带超时的事务操作: {OperationName}, 超时: {Timeout}",
            operationName, actualTimeout);

        using var cts = new CancellationTokenSource(actualTimeout);
        using var transaction = new Transaction(document, operationName);

        try
        {
            transaction.Start();

            var result = await operation(transaction).WaitAsync(cts.Token);

            transaction.Commit();
            _logger.LogInformation("事务操作成功完成: {OperationName}", operationName);
            
            return result;
        }
        catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
        {
            transaction.RollBack();
            _logger.LogWarning("事务操作超时: {OperationName}, 超时时间: {Timeout}", operationName, actualTimeout);
            throw new TimeoutException($"事务操作 '{operationName}' 超时");
        }
        catch (Exception ex)
        {
            transaction.RollBack();
            _logger.LogError(ex, "事务操作失败: {OperationName}", operationName);
            throw;
        }
    }
} 