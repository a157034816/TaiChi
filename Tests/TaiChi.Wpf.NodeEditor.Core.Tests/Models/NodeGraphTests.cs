using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Core.Tests.Models;

/// <summary>
/// <see cref="NodeGraph"/> 的单元测试：验证分类/执行引擎键、节点增删与连接、分组迁移、反序列化重建与合法性校验。
/// </summary>
public class NodeGraphTests
{
    /// <summary>
    /// 流程节点：包含流程输入/输出 pin，用于构造 ControlFlow 图。
    /// </summary>
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

    /// <summary>
    /// 数据节点：包含 int 输入/输出 pin，用于构造 DataFlow 图。
    /// </summary>
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

    /// <summary>
    /// 验证分类与执行引擎键：Category 切换后，ExecutionEngineKey 应同步更新。
    /// </summary>
    [Fact]
    public void Category_And_ExecutionKey_Work()
    {
        var g = new NodeGraph();
        Assert.Equal(NodeGraphCategory.ControlFlow, g.Category);
        Assert.Equal("ControlFlow", g.GetExecutionEngineKey());

        g.Category = NodeGraphCategory.DataFlow;
        Assert.Equal("DataFlow", g.GetExecutionEngineKey());
    }

    /// <summary>
    /// 验证节点增删与连接：添加节点后可连接，移除节点后应从集合中移除。
    /// </summary>
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

    /// <summary>
    /// 验证移动节点到分组：应同时设置节点的 Group 引用与 GroupId，并把节点加入分组集合。
    /// </summary>
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

    /// <summary>
    /// 验证反序列化重建：仅保留 Id 的连接与分组关系在 OnDeserialized 后应恢复为可用的运行时引用。
    /// </summary>
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

    /// <summary>
    /// 验证合法性校验：流程 pin 与数据 pin 之间的错误连接应导致 Validate 返回 false。
    /// </summary>
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

/// <summary>
/// <see cref="NodeGraphCategory"/> 枚举定义测试：确保包含 ControlFlow/DataFlow 两个核心分类。
/// </summary>
public class NodeGraphCategoryTests
{
    /// <summary>
    /// 验证枚举包含两种分类值。
    /// </summary>
    [Fact]
    public void Enum_Defines_ControlFlow_And_DataFlow()
    {
        Assert.True(Enum.IsDefined(typeof(NodeGraphCategory), NodeGraphCategory.ControlFlow));
        Assert.True(Enum.IsDefined(typeof(NodeGraphCategory), NodeGraphCategory.DataFlow));
    }
}
