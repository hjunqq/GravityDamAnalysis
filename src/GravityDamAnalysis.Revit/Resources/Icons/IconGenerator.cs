using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

namespace GravityDamAnalysis.Revit.Resources.Icons;

/// <summary>
/// 图标生成器 - 为重力坝分析插件生成图标
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// 生成坝体稳定性分析图标
    /// </summary>
    public static Bitmap GenerateStabilityAnalysisIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // 绘制坝体轮廓
            using (var pen = new Pen(Color.DarkBlue, 2))
            using (var brush = new SolidBrush(Color.LightBlue))
            {
                var damPath = new GraphicsPath();
                damPath.AddPolygon(new Point[]
                {
                    new Point(6, 26),   // 底部左
                    new Point(8, 20),   // 坝体左
                    new Point(12, 16),  // 坝体左
                    new Point(20, 12),  // 坝体顶部
                    new Point(28, 16),  // 坝体右
                    new Point(32, 20),  // 坝体右
                    new Point(34, 26)   // 底部右
                });

                graphics.FillPath(brush, damPath);
                graphics.DrawPath(pen, damPath);
            }

            // 绘制安全系数指示器
            using (var pen = new Pen(Color.Green, 1))
            {
                graphics.DrawEllipse(pen, 14, 8, 4, 4);
                graphics.DrawLine(pen, 16, 10, 16, 14);
                graphics.DrawLine(pen, 14, 12, 18, 12);
            }
        }
        return bitmap;
    }

    /// <summary>
    /// 生成高级坝体分析图标
    /// </summary>
    public static Bitmap GenerateAdvancedAnalysisIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // 绘制复杂的坝体结构
            using (var pen = new Pen(Color.DarkGreen, 2))
            using (var brush = new SolidBrush(Color.LightGreen))
            {
                // 主坝体
                var mainDamPath = new GraphicsPath();
                mainDamPath.AddPolygon(new Point[]
                {
                    new Point(4, 28),   // 底部左
                    new Point(6, 22),   // 坝体左
                    new Point(10, 18),  // 坝体左
                    new Point(16, 14),  // 坝体顶部
                    new Point(22, 18),  // 坝体右
                    new Point(26, 22),  // 坝体右
                    new Point(28, 28)   // 底部右
                });

                graphics.FillPath(brush, mainDamPath);
                graphics.DrawPath(pen, mainDamPath);

                // 绘制分析网格
                using (var gridPen = new Pen(Color.Orange, 1))
                {
                    for (int i = 8; i <= 24; i += 4)
                    {
                        graphics.DrawLine(gridPen, i, 16, i, 26);
                    }
                    for (int i = 16; i <= 26; i += 2)
                    {
                        graphics.DrawLine(gridPen, 8, i, 24, i);
                    }
                }
            }
        }
        return bitmap;
    }

    /// <summary>
    /// 生成UI集成分析图标
    /// </summary>
    public static Bitmap GenerateUIIntegrationIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // 绘制窗口框架
            using (var pen = new Pen(Color.DarkGray, 2))
            using (var brush = new SolidBrush(Color.White))
            {
                var windowRect = new Rectangle(4, 6, 24, 20);
                graphics.FillRectangle(brush, windowRect);
                graphics.DrawRectangle(pen, windowRect);

                // 绘制标题栏
                using (var titleBrush = new SolidBrush(Color.LightBlue))
                {
                    graphics.FillRectangle(titleBrush, 4, 6, 24, 4);
                }

                // 绘制内容区域
                using (var contentBrush = new SolidBrush(Color.LightYellow))
                {
                    graphics.FillRectangle(contentBrush, 6, 12, 20, 12);
                }

                // 绘制按钮
                using (var buttonBrush = new SolidBrush(Color.LightGreen))
                {
                    graphics.FillEllipse(buttonBrush, 8, 14, 4, 4);
                    graphics.FillEllipse(buttonBrush, 14, 14, 4, 4);
                    graphics.FillEllipse(buttonBrush, 20, 14, 4, 4);
                }
            }
        }
        return bitmap;
    }

    /// <summary>
    /// 生成拉伸Sketch提取图标
    /// </summary>
    public static Bitmap GenerateExtrusionSketchIcon()
    {
        var bitmap = new Bitmap(32, 32);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);

            // 绘制拉伸体轮廓
            using (var pen = new Pen(Color.Purple, 2))
            using (var brush = new SolidBrush(Color.LightPink))
            {
                // 底部轮廓（Sketch）
                var bottomPath = new GraphicsPath();
                bottomPath.AddPolygon(new Point[]
                {
                    new Point(8, 24),   // 底部左
                    new Point(12, 20),  // 底部
                    new Point(20, 20),  // 底部
                    new Point(24, 24)   // 底部右
                });

                graphics.FillPath(brush, bottomPath);
                graphics.DrawPath(pen, bottomPath);

                // 绘制拉伸方向箭头
                using (var arrowPen = new Pen(Color.Red, 2))
                {
                    graphics.DrawLine(arrowPen, 16, 20, 16, 12);
                    
                    // 箭头头部
                    var arrowPath = new GraphicsPath();
                    arrowPath.AddPolygon(new Point[]
                    {
                        new Point(16, 12),
                        new Point(14, 14),
                        new Point(18, 14)
                    });
                    graphics.FillPath(new SolidBrush(Color.Red), arrowPath);
                }

                // 绘制提取指示器
                using (var extractPen = new Pen(Color.Green, 1))
                {
                    graphics.DrawEllipse(extractPen, 14, 22, 4, 4);
                    graphics.DrawLine(extractPen, 16, 22, 16, 26);
                }
            }
        }
        return bitmap;
    }

    /// <summary>
    /// 保存图标到文件
    /// </summary>
    public static void SaveIconsToFiles()
    {
        var iconDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons");
        Directory.CreateDirectory(iconDir);

        // 保存所有图标
        GenerateStabilityAnalysisIcon().Save(Path.Combine(iconDir, "StabilityAnalysis.png"));
        GenerateAdvancedAnalysisIcon().Save(Path.Combine(iconDir, "AdvancedAnalysis.png"));
        GenerateUIIntegrationIcon().Save(Path.Combine(iconDir, "UIIntegration.png"));
        GenerateExtrusionSketchIcon().Save(Path.Combine(iconDir, "ExtrusionSketch.png"));
    }
} 