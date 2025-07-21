using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using GravityDamAnalysis.Calculation.Services;
using GravityDamAnalysis.Calculation.Models;
using Xunit;

namespace GravityDamAnalysis.Core.Tests.Services;

public class StabilityAnalysisServiceTests
{
    private readonly Mock<ILogger<StabilityAnalysisService>> _mockLogger;
    private readonly StabilityAnalysisService _service;
    
    public StabilityAnalysisServiceTests()
    {
        _mockLogger = new Mock<ILogger<StabilityAnalysisService>>();
        _service = new StabilityAnalysisService(_mockLogger.Object);
    }
    
    [Fact]
    public async Task AnalyzeStabilityAsync_WithValidInput_ShouldReturnCompletedResult()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        var parameters = CreateTestAnalysisParameters();
        
        // Act
        var result = await _service.AnalyzeStabilityAsync(damEntity, parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(AnalysisStatus.Completed);
        result.DamName.Should().Be(damEntity.Name);
        result.SlidingSafetyFactor.Should().BeGreaterThan(0);
        result.OverturnSafetyFactor.Should().BeGreaterThan(0);
        result.EndTime.Should().BeAfter(result.StartTime);
    }
    
    [Fact]
    public async Task AnalyzeStabilityAsync_WithInvalidParameters_ShouldReturnFailedResult()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        var invalidParameters = new AnalysisParameters
        {
            UpstreamWaterLevel = -10, // 无效的负数水位
            FrictionCoefficient = 2.0 // 无效的摩擦系数
        };
        
        // Act
        var result = await _service.AnalyzeStabilityAsync(damEntity, invalidParameters);
        
        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(AnalysisStatus.Failed);
        result.Errors.Should().NotBeEmpty();
    }
    
    [Fact]
    public async Task AnalyzeStabilityAsync_WithCancellation_ShouldThrowOperationCancelledException()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        var parameters = CreateTestAnalysisParameters();
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel(); // 立即取消
        
        // Act & Assert
        var action = async () => await _service.AnalyzeStabilityAsync(
            damEntity, parameters, cancellationTokenSource.Token);
        await action.Should().ThrowAsync<OperationCanceledException>();
    }
    
    [Theory]
    [InlineData(1000.0, 0.0, 3.0)] // 基本情况
    [InlineData(1500.0, 0.1, 2.5)] // 包含地震力
    [InlineData(500.0, 0.0, 5.0)]  // 较小水压力
    public void CalculateSlidingStability_WithValidInput_ShouldReturnPositiveValue(
        double waterPressure, double seismicCoefficient, double expectedMinimum)
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        
        // Act
        var result = _service.CalculateSlidingStability(damEntity, waterPressure, seismicCoefficient);
        
        // Assert
        result.Should().BeGreaterThan(0);
        result.Should().BeGreaterThan(expectedMinimum);
    }
    
    [Theory]
    [InlineData(1000.0, 0.0, 1.0)] // 基本情况
    [InlineData(1500.0, 0.1, 0.8)] // 包含地震力
    [InlineData(500.0, 0.0, 2.0)]  // 较小水压力
    public void CalculateOverturnStability_WithValidInput_ShouldReturnPositiveValue(
        double waterPressure, double seismicCoefficient, double expectedMinimum)
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        
        // Act
        var result = _service.CalculateOverturnStability(damEntity, waterPressure, seismicCoefficient);
        
        // Assert
        result.Should().BeGreaterThan(0);
        result.Should().BeGreaterThan(expectedMinimum);
    }
    
    [Fact]
    public void ValidateParameters_WithValidParameters_ShouldReturnValidResult()
    {
        // Arrange
        var parameters = CreateTestAnalysisParameters();
        
        // Act
        var result = _service.ValidateParameters(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
    
    [Fact]
    public void ValidateParameters_WithInvalidWaterLevel_ShouldReturnInvalidResult()
    {
        // Arrange
        var parameters = new AnalysisParameters
        {
            UpstreamWaterLevel = -10 // 无效的负数
        };
        
        // Act
        var result = _service.ValidateParameters(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(parameters.UpstreamWaterLevel));
    }
    
    [Theory]
    [InlineData(-0.1)] // 负数
    [InlineData(2.0)]  // 过大
    public void ValidateParameters_WithInvalidFrictionCoefficient_ShouldReturnInvalidResult(
        double invalidFrictionCoefficient)
    {
        // Arrange
        var parameters = CreateTestAnalysisParameters();
        parameters.FrictionCoefficient = invalidFrictionCoefficient;
        
        // Act
        var result = _service.ValidateParameters(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
    
    [Theory]
    [InlineData(-0.1)] // 负数
    [InlineData(0.5)]  // 过大
    public void ValidateParameters_WithInvalidSeismicCoefficient_ShouldReturnInvalidResult(
        double invalidSeismicCoefficient)
    {
        // Arrange
        var parameters = CreateTestAnalysisParameters();
        parameters.SeismicCoefficient = invalidSeismicCoefficient;
        
        // Act
        var result = _service.ValidateParameters(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
    
    [Fact]
    public void ValidateParameters_WithLowSafetyFactorRequirements_ShouldReturnWarnings()
    {
        // Arrange
        var parameters = CreateTestAnalysisParameters();
        parameters.RequiredSlidingSafetyFactor = 0.8; // 过低
        parameters.RequiredOverturnSafetyFactor = 0.9; // 过低
        
        // Act
        var result = _service.ValidateParameters(parameters);
        
        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(); // 警告不影响有效性
        result.Warnings.Should().NotBeEmpty();
    }
    
    private static DamEntity CreateTestDamEntity()
    {
        var geometry = new DamGeometry(
            volume: 1500.0,
            boundingBox: new BoundingBox3D(
                new Point3D(0, 0, 0),
                new Point3D(20, 20, 100)
            )
        );
        
        var materialProperties = new MaterialProperties(
            "C30混凝土",
            density: 2400.0,
            elasticModulus: 30000.0,
            poissonRatio: 0.18,
            compressiveStrength: 30.0,
            tensileStrength: 3.0,
            frictionCoefficient: 0.75
        );
        
        return new DamEntity(
            Guid.NewGuid(),
            "测试重力坝",
            geometry,
            materialProperties
        );
    }
    
    private static AnalysisParameters CreateTestAnalysisParameters()
    {
        return new AnalysisParameters
        {
            UpstreamWaterLevel = 80.0,
            DownstreamWaterLevel = 10.0,
            WaterDensity = 9.8,
            SeismicCoefficient = 0.1,
            FrictionCoefficient = 0.75,
            RequiredSlidingSafetyFactor = 3.0,
            RequiredOverturnSafetyFactor = 1.5,
            ConsiderUpliftPressure = true,
            UpliftReductionFactor = 0.8
        };
    }
} 