using System.Collections.ObjectModel;
using System.ComponentModel;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Core.Tests.Models;

/// <summary>
/// <see cref="NodeGroup"/> 的单元测试：验证属性变更通知、节点/子分组增删与递归查询能力。
/// </summary>
public class NodeGroupTests
{
    /// <summary>
    /// 测试用节点类型。
    /// </summary>
    private sealed class TestNode : Node { }

    /// <summary>
    /// 验证 Name 属性变更会触发 <see cref="INotifyPropertyChanged.PropertyChanged"/>。
    /// </summary>
    [Fact]
    public void Name_PropertyChanged_Raised()
    {
        var group = new NodeGroup();
        string? changed = null;
        group.PropertyChanged += (_, e) => changed = e.PropertyName;

        group.Name = "Group A";

        Assert.Equal("Name", changed);
        Assert.Equal("Group A", group.Name);
    }

    /// <summary>
    /// 验证 Bounds 属性变更会触发 <see cref="INotifyPropertyChanged.PropertyChanged"/>。
    /// </summary>
    [Fact]
    public void Bounds_PropertyChanged_Raised()
    {
        var group = new NodeGroup();
        string? changed = null;
        group.PropertyChanged += (_, e) => changed = e.PropertyName;

        group.Bounds = new NodeEditorRect(10, 20, 300, 200);

        Assert.Equal("Bounds", changed);
        Assert.Equal(10, group.Bounds.X);
        Assert.Equal(20, group.Bounds.Y);
        Assert.Equal(300, group.Bounds.Width);
        Assert.Equal(200, group.Bounds.Height);
    }

    /// <summary>
    /// 验证节点增删：AddNode/RemoveNode 应正确维护集合内容。
    /// </summary>
    [Fact]
    public void AddRemove_Node_Works()
    {
        var group = new NodeGroup { Name = "G" };
        var node = new TestNode { Name = "N" };

        group.AddNode(node);
        Assert.Contains(node, group.Nodes);

        var removed = group.RemoveNode(node);
        Assert.True(removed);
        Assert.DoesNotContain(node, group.Nodes);
    }

    /// <summary>
    /// 验证子分组增删：AddChild/RemoveChild 应正确维护 Parent 关系。
    /// </summary>
    [Fact]
    public void AddRemove_ChildGroup_SetsParent()
    {
        var parent = new NodeGroup { Name = "P" };
        var child = new NodeGroup { Name = "C" };

        parent.AddChild(child);
        Assert.Contains(child, parent.Children);
        Assert.Same(parent, child.Parent);

        var removed = parent.RemoveChild(child);
        Assert.True(removed);
        Assert.DoesNotContain(child, parent.Children);
        Assert.Null(child.Parent);
    }

    /// <summary>
    /// 验证递归获取节点：应包含子分组中的节点。
    /// </summary>
    [Fact]
    public void GetAllNodesRecursive_Returns_Nodes_From_Children()
    {
        var root = new NodeGroup { Name = "root" };
        var child = new NodeGroup { Name = "child" };
        root.AddChild(child);

        var n1 = new TestNode { Name = "n1" };
        var n2 = new TestNode { Name = "n2" };
        root.AddNode(n1);
        child.AddNode(n2);

        var list = root.GetAllNodesRecursive().ToList();
        Assert.Equal(2, list.Count);
        Assert.Contains(n1, list);
        Assert.Contains(n2, list);
    }

    /// <summary>
    /// 验证递归包含判断：父分组应能检测到子分组的节点；子分组不应误判父分组节点。
    /// </summary>
    [Fact]
    public void ContainsNodeRecursive_Works()
    {
        var root = new NodeGroup { Name = "root" };
        var child = new NodeGroup { Name = "child" };
        root.AddChild(child);

        var n1 = new TestNode { Name = "n1" };
        var n2 = new TestNode { Name = "n2" };
        root.AddNode(n1);
        child.AddNode(n2);

        Assert.True(root.ContainsNodeRecursive(n1));
        Assert.True(root.ContainsNodeRecursive(n2));
        Assert.True(child.ContainsNodeRecursive(n2));
        Assert.False(child.ContainsNodeRecursive(n1));
    }
}

