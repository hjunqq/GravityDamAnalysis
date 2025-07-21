using FluentAssertions;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using Xunit;

namespace GravityDamAnalysis.Core.Tests.Entities;

public class DamSectionTests
{
    [Fact]
    public void DamSection_WhenCreated_ShouldHaveCorrectProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "标准断面1";
        var position = new Point3D(10.0, 0.0, 0.0);
        var sectionType = SectionType.Standard;
        var height = 100.0;
        var topWidth = 5.0;
        var bottomWidth = 20.0;
        
        // Act
        var section = new DamSection(id, name, position, sectionType, height, topWidth, bottomWidth);
        
        // Assert
        section.Id.Should().Be(id);
        section.Name.Should().Be(name);
        section.Position.Should().Be(position);
        section.SectionType.Should().Be(sectionType);
        section.Height.Should().Be(height);
        section.TopWidth.Should().Be(topWidth);
        section.BottomWidth.Should().Be(bottomWidth);
        section.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DamSection_WhenCreatedWithInvalidName_ShouldThrow(string invalidName)
    {
        // Arrange
        var id = Guid.NewGuid();
        var position = new Point3D(0, 0, 0);
        
        // Act & Assert
        var action = () => new DamSection(id, invalidName, position, SectionType.Standard, 100.0, 5.0, 20.0);
        action.Should().Throw<ArgumentException>();
    }
    
    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void DamSection_WhenCreatedWithInvalidHeight_ShouldThrow(double invalidHeight)
    {
        // Arrange
        var id = Guid.NewGuid();
        var position = new Point3D(0, 0, 0);
        
        // Act & Assert
        var action = () => new DamSection(id, "测试断面", position, SectionType.Standard, invalidHeight, 5.0, 20.0);
        action.Should().Throw<ArgumentException>();
    }
    
    [Theory]
    [InlineData(-5.0)]
    public void DamSection_WhenCreatedWithInvalidTopWidth_ShouldThrow(double invalidTopWidth)
    {
        // Arrange
        var id = Guid.NewGuid();
        var position = new Point3D(0, 0, 0);
        
        // Act & Assert
        var action = () => new DamSection(id, "测试断面", position, SectionType.Standard, 100.0, invalidTopWidth, 20.0);
        action.Should().Throw<ArgumentException>();
    }
    
    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    public void DamSection_WhenCreatedWithInvalidBottomWidth_ShouldThrow(double invalidBottomWidth)
    {
        // Arrange
        var id = Guid.NewGuid();
        var position = new Point3D(0, 0, 0);
        
        // Act & Assert
        var action = () => new DamSection(id, "测试断面", position, SectionType.Standard, 100.0, 5.0, invalidBottomWidth);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void DamSection_WhenCreatedWithNullPosition_ShouldThrow()
    {
        // Arrange
        var id = Guid.NewGuid();
        
        // Act & Assert
        var action = () => new DamSection(id, "测试断面", null!, SectionType.Standard, 100.0, 5.0, 20.0);
        action.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void DamSection_CalculateArea_ShouldReturnCorrectValue()
    {
        // Arrange - 梯形断面
        var section = CreateTestSection(height: 100.0, topWidth: 5.0, bottomWidth: 20.0);
        var expectedArea = (5.0 + 20.0) * 100.0 / 2.0; // 1250.0
        
        // Act
        var area = section.Area;
        
        // Assert
        area.Should().Be(expectedArea);
    }
    
    [Fact]
    public void DamSection_CalculateCentroidHeight_ForRectangularSection_ShouldReturnHalfHeight()
    {
        // Arrange - 矩形断面（顶宽 = 底宽）
        var section = CreateTestSection(height: 100.0, topWidth: 20.0, bottomWidth: 20.0);
        var expectedCentroidHeight = 50.0; // height / 2
        
        // Act
        var centroidHeight = section.CentroidHeight;
        
        // Assert
        centroidHeight.Should().Be(expectedCentroidHeight);
    }
    
    [Fact]
    public void DamSection_CalculateCentroidHeight_ForTrapezoidalSection_ShouldReturnCorrectValue()
    {
        // Arrange - 梯形断面
        var height = 100.0;
        var topWidth = 5.0;
        var bottomWidth = 20.0;
        var section = CreateTestSection("测试断面", height, topWidth, bottomWidth);
        
        // 梯形重心高度公式: h * (2*b + t) / (3*(b + t))
        var expectedCentroidHeight = height * (2 * bottomWidth + topWidth) / (3 * (bottomWidth + topWidth));
        
        // Act
        var centroidHeight = section.CentroidHeight;
        
        // Assert
        centroidHeight.Should().BeApproximately(expectedCentroidHeight, 0.001);
    }
    
    [Fact]
    public void DamSection_UpdateName_ShouldUpdateCorrectly()
    {
        // Arrange
        var section = CreateTestSection();
        var newName = "更新后的断面";
        
        // Act
        section.UpdateName(newName);
        
        // Assert
        section.Name.Should().Be(newName);
        section.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DamSection_UpdateName_WithInvalidName_ShouldThrow(string invalidName)
    {
        // Arrange
        var section = CreateTestSection();
        
        // Act & Assert
        var action = () => section.UpdateName(invalidName);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void DamSection_UpdateGeometry_ShouldUpdateCorrectly()
    {
        // Arrange
        var section = CreateTestSection();
        var newHeight = 120.0;
        var newTopWidth = 8.0;
        var newBottomWidth = 25.0;
        
        // Act
        section.UpdateGeometry(newHeight, newTopWidth, newBottomWidth);
        
        // Assert
        section.Height.Should().Be(newHeight);
        section.TopWidth.Should().Be(newTopWidth);
        section.BottomWidth.Should().Be(newBottomWidth);
        section.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    
    [Theory]
    [InlineData(0.0, 5.0, 20.0)]  // 无效高度
    [InlineData(-10.0, 5.0, 20.0)] // 负高度
    [InlineData(100.0, -5.0, 20.0)] // 负顶宽
    [InlineData(100.0, 5.0, 0.0)]   // 零底宽
    [InlineData(100.0, 5.0, -20.0)] // 负底宽
    public void DamSection_UpdateGeometry_WithInvalidParameters_ShouldThrow(
        double height, double topWidth, double bottomWidth)
    {
        // Arrange
        var section = CreateTestSection();
        
        // Act & Assert
        var action = () => section.UpdateGeometry(height, topWidth, bottomWidth);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void DamSection_UpdateSlopes_ShouldUpdateCorrectly()
    {
        // Arrange
        var section = CreateTestSection();
        var newUpstreamSlope = 0.2;
        var newDownstreamSlope = 0.75;
        
        // Act
        section.UpdateSlopes(newUpstreamSlope, newDownstreamSlope);
        
        // Assert
        section.UpstreamSlope.Should().Be(newUpstreamSlope);
        section.DownstreamSlope.Should().Be(newDownstreamSlope);
        section.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
    
    [Theory]
    [InlineData(-0.1, 0.8)]  // 负上游坡度
    [InlineData(0.2, -0.1)]  // 负下游坡度
    public void DamSection_UpdateSlopes_WithInvalidSlopes_ShouldThrow(
        double upstreamSlope, double downstreamSlope)
    {
        // Arrange
        var section = CreateTestSection();
        
        // Act & Assert
        var action = () => section.UpdateSlopes(upstreamSlope, downstreamSlope);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void DamSection_IsValid_WithValidSection_ShouldReturnTrue()
    {
        // Arrange
        var section = CreateTestSection();
        
        // Act
        var isValid = section.IsValid();
        
        // Assert
        isValid.Should().BeTrue();
    }
    
    [Fact]
    public void DamSection_ToString_ShouldReturnDescriptiveString()
    {
        // Arrange
        var section = CreateTestSection("标准断面", 100.0, 5.0, 20.0);
        
        // Act
        var result = section.ToString();
        
        // Assert
        result.Should().Contain("标准断面");
        result.Should().Contain("100.00m");
        result.Should().Contain("5.00m");
        result.Should().Contain("20.00m");
        result.Should().Contain("Standard");
    }
    
    private static DamSection CreateTestSection(
        string name = "测试断面",
        double height = 100.0,
        double topWidth = 5.0,
        double bottomWidth = 20.0)
    {
        return new DamSection(
            Guid.NewGuid(),
            name,
            new Point3D(0, 0, 0),
            SectionType.Standard,
            height,
            topWidth,
            bottomWidth
        );
    }
} 