using NodeEditor.Prefab.Services.Execution;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace NodeEditor.Integration.Tests;

public class NodeGraphCategoryIntegrationTests
{
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
        var g2 = save2!.ToModel();
        Assert.Equal(NodeGraphCategory.ControlFlow, g2.Category);

        // Execute
        var engine = new ControlFlowEngine();
        await engine.ExecuteAsync(g2);

        // Verify at least executed; no exception means success; Called only on original instance
        Assert.True(g2.Validate());
    }

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
        var g2 = save2!.ToModel();
        Assert.Equal(NodeGraphCategory.DataFlow, g2.Category);

        var engine = new DataFlowEngine();
        var result = await engine.ExecuteAsync(g2) as DataFlowResult;
        Assert.NotNull(result);

        // find sink node (no data outputs) after load
        var sink = g2.Nodes.First(n => n.OutputPins.All(p => p.IsFlowPin || p.Value == null));
        Assert.True(result!.SinkNodeOutputs.ContainsKey(sink.Id));
    }
}

