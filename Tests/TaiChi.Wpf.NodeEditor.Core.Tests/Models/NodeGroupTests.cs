using System.Collections.ObjectModel;
using System.ComponentModel;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Core.Tests.Models;

public class NodeGroupTests
{
    private sealed class TestNode : Node { }

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

