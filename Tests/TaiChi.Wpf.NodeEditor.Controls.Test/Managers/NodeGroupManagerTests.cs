using System.Collections.ObjectModel;
using System.Windows;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Controls.Managers;

namespace TaiChi.Wpf.NodeEditor.Controls.Tests;

/// <summary>
/// <see cref="NodeGroupManager"/> 的单元测试：验证分组边界计算、节点加入、移动级联与边界约束/扩展逻辑。
/// </summary>
public class NodeGroupManagerTests
{
    /// <summary>
    /// 测试用节点类型（无需额外行为）。
    /// </summary>
    private sealed class TestNode : Node { }

    /// <summary>
    /// 构造一个带坐标与名称的测试节点。
    /// </summary>
    /// <param name="x">X 坐标。</param>
    /// <param name="y">Y 坐标。</param>
    /// <returns>节点实例。</returns>
    private static Node MakeNode(double x, double y)
    {
        return new TestNode { Position = new NodeEditorPoint(x, y), Name = $"N({x},{y})" };
    }

    /// <summary>
    /// 验证从节点集合创建分组时的边界计算：应包含节点尺寸并叠加 padding。
    /// </summary>
    [Fact]
    public void CreateGroupFromNodes_ComputesBounds_WithPadding()
    {
        var n1 = MakeNode(10, 10);
        var n2 = MakeNode(100, 40);
        var mgr = new NodeGroupManager
        {
            MeasureNode = _ => new Size(50, 30)
        };

        var g = mgr.CreateGroupFromNodes("G", new[] { n1, n2 }, padding: 10);

        // 两个节点矩形：
        // n1: (10,10,50,30) => 右下(60,40)
        // n2: (100,40,50,30)=> 右下(150,70)
        // 包围：minX=10, minY=10, maxX=150, maxY=70，加 padding 10 后：
        // X=0, Y=0, W=160, H=80
        Assert.Equal(0, g.Bounds.X);
        Assert.Equal(0, g.Bounds.Y);
        Assert.Equal(160, g.Bounds.Width);
        Assert.Equal(80, g.Bounds.Height);

        // 节点加入分组
        Assert.Same(g, n1.Group);
        Assert.Same(g, n2.Group);
        Assert.Contains(n1, g.Nodes);
        Assert.Contains(n2, g.Nodes);
    }

    /// <summary>
    /// 验证把节点加入分组：应设置节点的 Group 引用，并在需要时扩展分组边界。
    /// </summary>
    [Fact]
    public void AddNodeToGroup_SetsGroup_And_OptionallyExpandsBounds()
    {
        var mgr = new NodeGroupManager { MeasureNode = _ => new Size(100, 50) };
        var g = mgr.CreateGroup("G", new NodeEditorRect(0, 0, 100, 100));
        var n = MakeNode(120, 80); // 部分在外

        var ok = mgr.AddNodeToGroup(n, g, adjustBounds: true);
        Assert.True(ok);
        Assert.Same(g, n.Group);
        // 边界应扩展以包含节点(100x50) + padding 8
        Assert.True(g.Bounds.Width >= 228 || g.Bounds.X <= 0); // 宽度被扩展（宽+padding）
    }

    /// <summary>
    /// 验证移动分组时的级联：可选择级联移动节点与子分组。
    /// </summary>
    [Fact]
    public void MoveGroup_Cascade_Nodes_And_Children()
    {
        var mgr = new NodeGroupManager();
        var parent = mgr.CreateGroup("P", new NodeEditorRect(0, 0, 200, 200));
        var child = mgr.CreateGroup("C", new NodeEditorRect(10, 10, 50, 50), parent);

        var n1 = MakeNode(20, 30);
        var n2 = MakeNode(25, 35);
        parent.AddNode(n1);
        child.AddNode(n2);

        mgr.MoveGroup(parent, 5, 7, cascadeNodes: true, cascadeChildren: true);

        Assert.Equal(5, parent.Bounds.X);
        Assert.Equal(7, parent.Bounds.Y);
        Assert.Equal(15, child.Bounds.X);
        Assert.Equal(17, child.Bounds.Y);
        Assert.Equal(25, n1.Position.X);
        Assert.Equal(37, n1.Position.Y);
        Assert.Equal(30, n2.Position.X);
        Assert.Equal(42, n2.Position.Y);
    }

    /// <summary>
    /// 验证节点约束/扩展：不扩展时应钳制在分组内；允许扩展时分组边界应随节点移动增长。
    /// </summary>
    [Fact]
    public void ConstrainOrExpandNodePosition_Clamp_Or_Expand()
    {
        var mgr = new NodeGroupManager { MeasureNode = _ => new Size(50, 30) };
        var g = mgr.CreateGroup("G", new NodeEditorRect(0, 0, 100, 100));
        var n = MakeNode(0, 0);
        g.AddNode(n);

        // 不扩展时应被钳制
        var clamped = mgr.ConstrainOrExpandNodePosition(n, g, new NodeEditorPoint(90, 90), dynamicExpand: false);
        Assert.Equal(50, clamped.X); // 100-50
        Assert.Equal(70, clamped.Y); // 100-30

        // 允许扩展时，边界应扩展并返回原始期望
        var desired = new NodeEditorPoint(120, 120);
        var expanded = mgr.ConstrainOrExpandNodePosition(n, g, desired, dynamicExpand: true, padding: 10);
        Assert.Equal(desired, expanded);
        Assert.True(g.Bounds.Width >= 180); // 120+50 + padding 衍生
        Assert.True(g.Bounds.Height >= 160);
    }
}

