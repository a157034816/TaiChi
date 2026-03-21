using NodeEditor.Prefab.Services.Execution;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace NodeEditor.Prefab.Tests.Services;

/// <summary>
/// 执行引擎单元测试：验证 ControlFlowEngine/DataFlowEngine 的基本执行语义。
/// </summary>
public class ExecutionEnginesTests
{
    /// <summary>
    /// 流程节点：包含流程输入/输出 pin，执行时仅计数。
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
        protected override void OnExecute()
        {
            Called++;
        }
    }

    /// <summary>
    /// 数据源节点：输出固定的 int 值。
    /// </summary>
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

    /// <summary>
    /// 数据处理节点：把输入值 +1 后输出。
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
    /// 验证 ControlFlowEngine：按流程连接顺序执行节点，确保下游节点也会被触发。
    /// </summary>
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

    /// <summary>
    /// 验证 DataFlowEngine：能按数据依赖计算，并在终端节点输出中包含预期的计算结果。
    /// </summary>
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

