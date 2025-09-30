using NodeEditor.Prefab.Services.Execution;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace NodeEditor.Prefab.Tests.Services;

public class ExecutionEnginesTests
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
        protected override void OnExecute()
        {
            Called++;
        }
    }

    private sealed class SourceNode : Node
    {
        public Pin Dout { get; }
        public SourceNode()
        {
            Dout = (Pin)typeof(Node).GetMethod("AddOutputPin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(this, new object[] { "y", typeof(int) })!;
        }
        protected override void OnExecute()
        {
            Dout.Value = 1;
        }
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
    public async Task ControlFlowEngine_Executes_Sequentially()
    {
        var g = new NodeGraph { Category = NodeGraphCategory.ControlFlow };
        var a = new FlowNode();
        var b = new FlowNode();
        g.AddNode(a); g.AddNode(b);
        g.Connect(a.Out, b.In);

        var engine = new ControlFlowEngine();
        await engine.ExecuteAsync(g);

        Assert.True(a.Called >= 1);
        Assert.True(b.Called >= 1);
    }

    [Fact]
    public async Task DataFlowEngine_Computes_Data_Dependencies()
    {
        var g = new NodeGraph { Category = NodeGraphCategory.DataFlow };
        var src = new SourceNode();
        var p1 = new PlusOneNode();
        g.AddNode(src); g.AddNode(p1);
        g.Connect(src.OutputPins[0], p1.InputPins[0]);

        var engine = new DataFlowEngine();
        var result = await engine.ExecuteAsync(g) as DataFlowResult;
        Assert.NotNull(result);

        // 取终端节点（PlusOne，无数据出边）输出
        var sink = g.Nodes.First(n => n == p1);
        Assert.True(result!.SinkNodeOutputs.ContainsKey(sink.Id));
        var map = result!.SinkNodeOutputs[sink.Id];
        Assert.True(map.ContainsKey("y"));
        Assert.Equal(2, (int)map["y"]!);
    }
}

