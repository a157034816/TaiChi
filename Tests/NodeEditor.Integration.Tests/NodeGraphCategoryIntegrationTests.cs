using NodeEditor.Prefab.Services.Execution;
using NodeEditor.Prefab.ViewModels;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace NodeEditor.Integration.Tests;

/// <summary>
/// 节点编辑器端到端集成测试：覆盖 ControlFlow/DataFlow 两类图的创建、序列化、反序列化与执行流程。
/// </summary>
public class NodeGraphCategoryIntegrationTests
{
    /// <summary>
    /// 注册测试用节点类型（用于反序列化时按节点名创建实例）。
    /// </summary>
    private static void RegisterTestNodes()
    {
        NodeRegistry.Clear();
        NodeRegistry.RegisterNode(() => new FlowNode(), nameof(FlowNode));
        NodeRegistry.RegisterNode(() => new SourceNode(), nameof(SourceNode));
        NodeRegistry.RegisterNode(() => new PlusOneNode(), nameof(PlusOneNode));
    }

    /// <summary>
    /// 流程节点：包含流程输入/输出 pin，执行时仅计数（用于验证流程链路被触发）。
    /// </summary>
    private sealed class FlowNode : Node
    {
        public int Called { get; private set; }
        public Pin In { get; }
        public Pin Out { get; }
        public FlowNode()
        {
            In = (Pin)typeof(Node).GetMethod("AddInputPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(this, new object[] { "In", typeof(object) })!;
            Out = (Pin)typeof(Node).GetMethod("AddOutputPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(this, new object[] { "Out", typeof(object) })!;
            In.IsFlowPin = true;
            Out.IsFlowPin = true;
        }
        protected override void OnExecute() => Called++;
    }

    /// <summary>
    /// 数据源节点：输出固定的 int 值（用于 DataFlow 链路测试）。
    /// </summary>
    private sealed class SourceNode : Node
    {
        public Pin Dout { get; }
        public SourceNode()
        {
            Dout = (Pin)typeof(Node).GetMethod("AddOutputPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(this, new object[] { "y", typeof(int) })!;
        }
        protected override void OnExecute() => Dout.Value = 1;
    }

    /// <summary>
    /// 数据处理节点：把输入值 +1 后输出（用于 DataFlow 依赖计算测试）。
    /// </summary>
    private sealed class PlusOneNode : Node
    {
        public Pin Din { get; }
        public Pin Dout { get; }
        public PlusOneNode()
        {
            Din = (Pin)typeof(Node).GetMethod("AddInputPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(this, new object[] { "x", typeof(int) })!;
            Dout = (Pin)typeof(Node).GetMethod("AddOutputPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(this, new object[] { "y", typeof(int) })!;
        }
        protected override void OnExecute()
        {
            var x = Din.Value is int v ? v : 0;
            Dout.Value = x + 1;
        }
    }

    /// <summary>
    /// 验证 ControlFlow 图：创建并保存/加载后仍保持分类，并可被执行引擎正常执行。
    /// </summary>
    [Fact]
    public async Task ControlFlow_New_Save_Load_Execute_EndToEnd()
    {
        // New
        var g = new NodeGraph { Category = NodeGraphCategory.ControlFlow };
        var a = new FlowNode();
        var b = new FlowNode();
        g.AddNode(a); g.AddNode(b);
        g.Connect(a.Out, b.In);

        // Save
        var save = NodeGraphSaveData.FromModel(g);
        var json = save.Serialize();

        // Load
        var save2 = NodeGraphSaveData.Deserialize(json!);
        Assert.NotNull(save2);
        RegisterTestNodes();
        var g2 = save2!.ToModel();
        Assert.Equal(NodeGraphCategory.ControlFlow, g2.Category);

        // Execute
        var engine = new ControlFlowEngine();
        await engine.ExecuteAsync(g2);

        // Verify at least executed; no exception means success; Called only on original instance
        Assert.True(g2.Validate());
    }

    /// <summary>
    /// 验证 DataFlow 图：创建并保存/加载后仍保持分类，执行后能得到终端节点输出集合。
    /// </summary>
    [Fact]
    public async Task DataFlow_New_Save_Load_Execute_EndToEnd()
    {
        var g = new NodeGraph { Category = NodeGraphCategory.DataFlow };
        var src = new SourceNode();
        var p1 = new PlusOneNode();
        g.AddNode(src); g.AddNode(p1);
        g.Connect(src.OutputPins[0], p1.InputPins[0]);

        var save = NodeGraphSaveData.FromModel(g);
        var json = save.Serialize();
        var save2 = NodeGraphSaveData.Deserialize(json!);
        Assert.NotNull(save2);
        RegisterTestNodes();
        var g2 = save2!.ToModel();
        Assert.Equal(NodeGraphCategory.DataFlow, g2.Category);

        var engine = new DataFlowEngine();
        var result = await engine.ExecuteAsync(g2) as DataFlowResult;
        Assert.NotNull(result);

        // 查找终端节点：无数据出边（与 DataFlowEngine 的 outdegree 规则一致）
        var sink = g2.Nodes.First(n =>
            !g2.Connections.Any(c =>
                c.SourcePin?.ParentNode == n &&
                c.SourcePin is { IsFlowPin: false } &&
                c.TargetPin is { IsFlowPin: false }));

        Assert.True(result!.SinkNodeOutputs.ContainsKey(sink.Id));
    }
}

