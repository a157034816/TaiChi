using NodeEditor.Prefab.Services;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace NodeEditor.Prefab.Tests.Services;

/// <summary>
/// <see cref="NodeGraphCategoryService"/> 的单元测试：验证节点元数据到图分类的推导与兼容性判断。
/// </summary>
public class NodeGraphCategoryServiceTests
{
    /// <summary>
    /// 验证通过元数据推导类别：流程 pin 与数据 pin 的组合应映射到不同的图分类。
    /// </summary>
    [Fact]
    public void GetNodeCategory_ByMetadata_FlowVsData()
    {
        var flowMeta = new NodeMetadata(typeof(object), "FlowNode", "Test");
        var inPin = PinMetadata.CreateInput("In", typeof(object));
        inPin.IsFlowPin = true;
        flowMeta.InputPins.Add(inPin);
        var outPin = PinMetadata.CreateOutput("Out", typeof(object));
        outPin.IsFlowPin = true;
        flowMeta.OutputPins.Add(outPin);

        var dataMeta = new NodeMetadata(typeof(object), "DataNode", "Test");
        dataMeta.InputPins.Add(PinMetadata.CreateInput("x", typeof(int)));
        dataMeta.OutputPins.Add(PinMetadata.CreateOutput("y", typeof(int)));

        Assert.Equal(NodeGraphCategory.ControlFlow, NodeGraphCategoryService.GetNodeCategory(flowMeta));
        Assert.Equal(NodeGraphCategory.DataFlow, NodeGraphCategoryService.GetNodeCategory(dataMeta));
    }

    /// <summary>
    /// 验证兼容性判断：不同节点类型在 ControlFlow/DataFlow 分类下的可用性规则应符合预期。
    /// </summary>
    [Fact]
    public void IsCompatible_Works()
    {
        var flowMeta = new NodeMetadata(typeof(object), "FlowNode", "Test");
        var flowIn = PinMetadata.CreateInput("In", typeof(object));
        flowIn.IsFlowPin = true;
        flowMeta.InputPins.Add(flowIn);

        var dataMeta = new NodeMetadata(typeof(object), "DataNode", "Test");
        dataMeta.InputPins.Add(PinMetadata.CreateInput("x", typeof(int)));

        Assert.True(NodeGraphCategoryService.IsCompatible(flowMeta, NodeGraphCategory.ControlFlow));
        Assert.True(NodeGraphCategoryService.IsCompatible(dataMeta, NodeGraphCategory.ControlFlow));
        Assert.True(NodeGraphCategoryService.IsCompatible(dataMeta, NodeGraphCategory.DataFlow));
        Assert.False(NodeGraphCategoryService.IsCompatible(flowMeta, NodeGraphCategory.DataFlow));
    }
}
