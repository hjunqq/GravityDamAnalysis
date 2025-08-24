using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace GravityDamAnalysis.Revit.Resources;

/// <summary>
/// 图标资源管理器 - 管理嵌入的图标资源
/// </summary>
public static class IconResourceManager
{
    /// <summary>
    /// 从嵌入资源加载图标
    /// </summary>
    public static Bitmap? LoadIcon(string iconName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"GravityDamAnalysis.Revit.Resources.Icons.{iconName}.png";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    return new Bitmap(stream);
                }
            }
            
            // 如果嵌入资源不存在，尝试生成图标
            return GenerateIcon(iconName);
        }
        catch
        {
            // 如果加载失败，返回生成的图标
            return GenerateIcon(iconName);
        }
    }

    /// <summary>
    /// 生成图标
    /// </summary>
    private static Bitmap? GenerateIcon(string iconName)
    {
        try
        {
            return iconName switch
            {
                "StabilityAnalysis" => Icons.IconGenerator.GenerateStabilityAnalysisIcon(),
                "AdvancedAnalysis" => Icons.IconGenerator.GenerateAdvancedAnalysisIcon(),
                "UIIntegration" => Icons.IconGenerator.GenerateUIIntegrationIcon(),
                "ExtrusionSketch" => Icons.IconGenerator.GenerateExtrusionSketchIcon(),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取图标的文件路径
    /// </summary>
    public static string GetIconPath(string iconName)
    {
        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var assemblyDir = Path.GetDirectoryName(assemblyPath);
        return Path.Combine(assemblyDir ?? "", "Resources", "Icons", $"{iconName}.png");
    }

    /// <summary>
    /// 确保图标文件存在
    /// </summary>
    public static void EnsureIconsExist()
    {
        try
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var assemblyDir = Path.GetDirectoryName(assemblyPath);
            var iconDir = Path.Combine(assemblyDir ?? "", "Resources", "Icons");
            
            if (!Directory.Exists(iconDir))
            {
                Directory.CreateDirectory(iconDir);
            }

            // 检查并生成缺失的图标
            var iconNames = new[] { "StabilityAnalysis", "AdvancedAnalysis", "UIIntegration", "ExtrusionSketch" };
            
            foreach (var iconName in iconNames)
            {
                var iconPath = Path.Combine(iconDir, $"{iconName}.png");
                if (!File.Exists(iconPath))
                {
                    var icon = GenerateIcon(iconName);
                    if (icon != null)
                    {
                        icon.Save(iconPath);
                    }
                }
            }
        }
        catch
        {
            // 忽略错误，使用生成的图标
        }
    }
} 