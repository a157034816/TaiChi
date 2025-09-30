using NodeEditor.Prefab.Services;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Registry;

namespace NodeEditor.Prefab.Tests.Services;

public class NodeGraphCategoryServiceTests
{
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
