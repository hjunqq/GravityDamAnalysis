using FluentAssertions;
using GravityDamAnalysis.Core.Entities;
using GravityDamAnalysis.Core.ValueObjects;
using Xunit;

namespace GravityDamAnalysis.Core.Tests.Entities;

public class DamEntityTests
{
    [Fact]
    public void DamEntity_WhenCreated_ShouldHaveCorrectProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var name = "测试重力坝";
        var geometry = CreateTestGeometry();
        var materialProperties = CreateTestMaterialProperties();
        
        // Act
        var damEntity = new DamEntity(id, name, geometry, materialProperties);
        
        // Assert
        damEntity.Id.Should().Be(id);
        damEntity.Name.Should().Be(name);
        damEntity.Geometry.Should().Be(geometry);
        damEntity.MaterialProperties.Should().Be(materialProperties);
        damEntity.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public void DamEntity_WhenCreatedWithEmptyName_ShouldThrow()
    {
        // Arrange
        var id = Guid.NewGuid();
        var geometry = CreateTestGeometry();
        var materialProperties = CreateTestMaterialProperties();
        
        // Act & Assert
        var action = () => new DamEntity(id, string.Empty, geometry, materialProperties);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void DamEntity_WhenUpdatedName_ShouldUpdateCorrectly()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        var newName = "更新后的坝体名称";
        
        // Act
        damEntity.UpdateName(newName);
        
        // Assert
        damEntity.Name.Should().Be(newName);
        damEntity.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }
    
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void DamEntity_WhenUpdatedWithInvalidName_ShouldThrow(string invalidName)
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        
        // Act & Assert
        var action = () => damEntity.UpdateName(invalidName);
        action.Should().Throw<ArgumentException>();
    }
    
    [Fact]
    public void DamEntity_WhenGeometryUpdated_ShouldUpdateCorrectly()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        var newGeometry = new DamGeometry(
            volume: 2000.0,
            boundingBox: new BoundingBox3D(
                new Point3D(0, 0, 0),
                new Point3D(25, 25, 120)
            )
        );
        
        // Act
        damEntity.UpdateGeometry(newGeometry);
        
        // Assert
        damEntity.Geometry.Should().Be(newGeometry);
        damEntity.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public void DamEntity_WhenMaterialPropertiesUpdated_ShouldUpdateCorrectly()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        var newMaterialProperties = new MaterialProperties(
            "新混凝土",
            density: 2500.0,
            elasticModulus: 35000.0,
            poissonRatio: 0.20,
            compressiveStrength: 35.0,
            tensileStrength: 3.5,
            frictionCoefficient: 0.80
        );
        
        // Act
        damEntity.UpdateMaterialProperties(newMaterialProperties);
        
        // Assert
        damEntity.MaterialProperties.Should().Be(newMaterialProperties);
        damEntity.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public void DamEntity_WhenUpdatedWithNullGeometry_ShouldThrow()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        
        // Act & Assert
        var action = () => damEntity.UpdateGeometry(null!);
        action.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void DamEntity_WhenUpdatedWithNullMaterialProperties_ShouldThrow()
    {
        // Arrange
        var damEntity = CreateTestDamEntity();
        
        // Act & Assert
        var action = () => damEntity.UpdateMaterialProperties(null!);
        action.Should().Throw<ArgumentNullException>();
    }
    
    private static DamEntity CreateTestDamEntity()
    {
        return new DamEntity(
            Guid.NewGuid(),
            "测试重力坝",
            CreateTestGeometry(),
            CreateTestMaterialProperties()
        );
    }
    
    private static DamGeometry CreateTestGeometry()
    {
        return new DamGeometry(
            volume: 1500.0,
            boundingBox: new BoundingBox3D(
                new Point3D(0, 0, 0),
                new Point3D(20, 20, 100)
            )
        );
    }
    
    private static MaterialProperties CreateTestMaterialProperties()
    {
        return new MaterialProperties(
            "C30混凝土",
            density: 2400.0,
            elasticModulus: 30000.0,
            poissonRatio: 0.18,
            compressiveStrength: 30.0,
            tensileStrength: 3.0,
            frictionCoefficient: 0.75
        );
    }
} 