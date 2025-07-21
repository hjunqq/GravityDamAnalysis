using Xunit;
using GravityDamAnalysis.Core.Entities;

namespace GravityDamAnalysis.Core.Tests.Entities;

public class Vector3DTests
{
    [Fact]
    public void IsZero_WithDefaultVector_ReturnsTrue()
    {
        // Arrange
        var vector = new Vector3D(); // 默认构造，所有分量为0
        
        // Act & Assert
        Assert.True(vector.IsZero());
    }
    
    [Fact]
    public void IsZero_WithSmallValues_ReturnsTrue()
    {
        // Arrange
        var vector = new Vector3D(1e-7, 1e-8, 1e-9); // 非常小的值
        
        // Act & Assert
        Assert.True(vector.IsZero()); // 使用默认容差1e-6
    }
    
    [Fact]
    public void IsZero_WithSignificantValues_ReturnsFalse()
    {
        // Arrange
        var vector = new Vector3D(0.1, 0.0, 0.0);
        
        // Act & Assert
        Assert.False(vector.IsZero());
    }
    
    [Fact]
    public void IsValid_WithDefaultVector_ReturnsFalse()
    {
        // Arrange
        var vector = new Vector3D(); // 默认构造，所有分量为0
        
        // Act & Assert
        Assert.False(vector.IsValid());
    }
    
    [Fact]
    public void IsValid_WithSignificantValues_ReturnsTrue()
    {
        // Arrange
        var vector = new Vector3D(1.0, 0.0, 0.0);
        
        // Act & Assert
        Assert.True(vector.IsValid());
    }
    
    [Fact]
    public void IsValid_WithCustomTolerance_WorksCorrectly()
    {
        // Arrange
        var vector = new Vector3D(0.005, 0.0, 0.0);
        
        // Act & Assert
        Assert.False(vector.IsValid(0.01)); // 长度小于容差
        Assert.True(vector.IsValid(0.001)); // 长度大于容差
    }
    
    [Theory]
    [InlineData(0.0, 0.0, 0.0, true)]
    [InlineData(1e-7, 1e-7, 1e-7, true)]
    [InlineData(0.1, 0.0, 0.0, false)]
    [InlineData(0.0, 0.1, 0.0, false)]
    [InlineData(0.0, 0.0, 0.1, false)]
    public void IsZero_WithVariousInputs_BehavesCorrectly(double x, double y, double z, bool expected)
    {
        // Arrange
        var vector = new Vector3D(x, y, z);
        
        // Act & Assert
        Assert.Equal(expected, vector.IsZero());
    }
    
    [Fact]
    public void ComparisonWithNull_DoesNotCompile()
    {
        // 这个测试用于文档化目的 - Vector3D不能与null比较
        // 以下代码如果取消注释会导致编译错误CS0019：
        
        // var vector = new Vector3D();
        // var result = vector != null; // CS0019编译错误
        
        // 正确的做法是：
        var vector = new Vector3D();
        var isValid = vector.IsValid(); // ✅ 正确的检查方式
        
        Assert.False(isValid); // 默认构造的向量无效
    }
} 