using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Core.Tests.Models;

public class NodeGraphTests
{
    private sealed class FlowNode : Node
    {
        public Pin FlowIn { get; }
        public Pin FlowOut { get; }

        public FlowNode()
        {
            FlowIn = AddInputPin("In", typeof(object));
            FlowIn.IsFlowPin = true;
            FlowOut = AddOutputPin("Out", typeof(object));
            FlowOut.IsFlowPin = true;
        }
    }

    private sealed class DataNode : Node
    {
        public Pin Din { get; }
        public Pin Dout { get; }

        public DataNode()
        {
            Din = AddInputPin("x", typeof(int));
            Dout = AddOutputPin("y", typeof(int));
        }
    }

    [Fact]
    public void Category_And_ExecutionKey_Work()
    {
        var g = new NodeGraph();
        Assert.Equal(NodeGraphCategory.ControlFlow, g.Category);
        Assert.Equal("ControlFlow", g.GetExecutionEngineKey());

        g.Category = NodeGraphCategory.DataFlow;
        Assert.Equal("DataFlow", g.GetExecutionEngineKey());
    }

    [Fact]
    public void Add_Remove_Node_And_Connect_Work()
    {
        var g = new NodeGraph { Category = NodeGraphCategory.ControlFlow };
        var a = new FlowNode { Name = "A" };
        var b = new FlowNode { Name = "B" };

        g.AddNode(a);
        g.AddNode(b);

        var conn = g.Connect(a.FlowOut, b.FlowIn);
        Assert.NotNull(conn);
        Assert.Single(g.Connections);

        var removed = g.RemoveNode(a);
        Assert.True(removed);
        Assert.DoesNotContain(a, g.Nodes);
    }

    [Fact]
    public void MoveNodeToGroup_Sets_Both_Sides()
    {
        var g = new NodeGraph();
        var a = new FlowNode { Name = "A" };
        g.AddNode(a);

        var grp = new NodeGroup { Name = "G1" };
        g.AddGroup(grp);

        g.MoveNodeToGroup(a, grp);
        Assert.Same(grp, a.Group);
        Assert.True(a.GroupId.HasValue);
        Assert.Contains(a, grp.Nodes);
    }

    [Fact]
    public void OnDeserialized_Rebuilds_Pins_Groups_And_Connections()
    {
        var g = new NodeGraph();
        var n1 = new DataNode { Name = "N1" };
        var n2 = new DataNode { Name = "N2" };
        g.AddNode(n1);
        g.AddNode(n2);

        // groups
        var gr = new NodeGroup { Name = "GR", Bounds = new NodeEditorRect(10, 10, 100, 80) };
        g.AddGroup(gr);
        n2.GroupId = gr.Id; // 模拟序列化时仅保存 Id

        // set up connection with ids only
        var c = new Connection();
        c.SourcePinId = n1.Dout.Id;
        c.TargetPinId = n2.Din.Id;
        g.AddConnection(c);

        // clear runtime refs to simulate deserialized state
        c.SourcePin = null;
        c.TargetPin = null;

        g.OnDeserialized();

        Assert.NotNull(c.SourcePin);
        Assert.NotNull(c.TargetPin);
        Assert.Same(n1, c.SourcePin!.ParentNode);
        Assert.Same(n2, c.TargetPin!.ParentNode);
        Assert.Same(gr, n2.Group);
    }

    [Fact]
    public void Validate_Returns_False_For_Flow_To_Data_Mismatch()
    {
        var g = new NodeGraph();
        var flow = new FlowNode();
        var data = new DataNode();
        g.AddNode(flow);
        g.AddNode(data);

        // 手工构造不兼容的连接（流程->数据）
        var invalid = new Connection(flow.FlowOut, data.Din);
        g.AddConnection(invalid);

        Assert.False(g.Validate());
    }
}

public class NodeGraphCategoryTests
{
    [Fact]
    public void Enum_Defines_ControlFlow_And_DataFlow()
    {
        Assert.True(Enum.IsDefined(typeof(NodeGraphCategory), NodeGraphCategory.ControlFlow));
        Assert.True(Enum.IsDefined(typeof(NodeGraphCategory), NodeGraphCategory.DataFlow));
    }
}
