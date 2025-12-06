using System.Reflection;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Core.Enums;

namespace TaiChi.Wpf.NodeEditor.Core.Registry;

/// <summary>
/// 节点元数据，描述节点类型的数据结构，包含创建节点实例所需的所有信息。
/// 支持基于类和基于方法的两种节点定义模式。
/// </summary>
public class NodeMetadata
{
    /// <summary>
    /// 节点的实现类（用于基于类的节点），或者包含方法的类型（用于基于方法的节点）
    /// </summary>
    public Type? NodeType { get; set; }

    /// <summary>
    /// 源方法的 MethodInfo（仅用于基于方法的节点）
    /// </summary>
    public MethodInfo? SourceMethod { get; internal set; }

    /// <summary>
    /// 持有创建实例的引用，用于非静态方法（仅用于基于方法的节点）
    /// </summary>
    public object? LibraryInstance { get; internal set; }

    /// <summary>
    /// 节点的显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点所属的路径，用于在工具箱中分组和层级显示
    /// </summary>
    public string Path { get; set; } = "Default";

    /// <summary>
    /// 节点所属的类别，用于在工具箱中分组（兼容旧版本，映射到Path的根级别）
    /// </summary>
    [Obsolete("使用 Path 属性代替 Category，此属性仅为向后兼容性保留")]
    public string Category 
    { 
        get => Path.Split('/').FirstOrDefault() ?? "Default";
        set => Path = value;
    }

    /// <summary>
    /// 节点的描述信息
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 输入引脚的定义列表
    /// </summary>
    public List<PinMetadata> InputPins { get; set; } = new();

    /// <summary>
    /// 输出引脚的定义列表
    /// </summary>
    public List<PinMetadata> OutputPins { get; set; } = new();

    /// <summary>
    /// 节点工厂函数，用于创建节点实例（仅用于基于类的节点或自定义工厂）
    /// </summary>
    public Func<Node>? NodeFactory { get; set; }

    /// <summary>
    /// 指示这是否是基于方法的节点
    /// </summary>
    public bool IsMethodBased => SourceMethod != null;

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public NodeMetadata()
    {
    }

    /// <summary>
    /// 构造函数（基于类的节点）
    /// </summary>
    /// <param name="nodeType">节点类型</param>
    /// <param name="name">节点名称</param>
    /// <param name="category">节点类别</param>
    public NodeMetadata(Type nodeType, string name, string category = "Default")
    {
        NodeType = nodeType;
        Name = name;
#pragma warning disable CS0618 // 类型或成员已过时
        Category = category;
#pragma warning restore CS0618 // 类型或成员已过时
    }

    /// <summary>
    /// 构造函数（基于类的节点）
    /// </summary>
    /// <param name="nodeType">节点类型</param>
    /// <param name="name">节点名称</param>
    /// <param name="category">节点类别</param>
    /// <param name="description">节点描述</param>
    public NodeMetadata(Type nodeType, string name, string category, string description)
        : this(nodeType, name, category)
    {
        Description = description;
    }

    /// <summary>
    /// 构造函数（基于方法的节点）
    /// </summary>
    /// <param name="sourceMethod">源方法</param>
    /// <param name="libraryInstance">库实例</param>
    /// <param name="name">节点名称</param>
    /// <param name="path">节点路径</param>
    /// <param name="description">节点描述</param>
    public NodeMetadata(MethodInfo sourceMethod, object? libraryInstance, string name, string path = "Default", string description = "")
    {
        SourceMethod = sourceMethod;
        LibraryInstance = libraryInstance;
        NodeType = sourceMethod.DeclaringType;
        Name = name;
        Path = path;
        Description = description;
    }

    /// <summary>
    /// 添加输入引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="description">描述信息</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>当前NodeMetadata实例，支持链式调用</returns>
    public NodeMetadata AddInputPin(string name, Type dataType, string description = "", object? defaultValue = null)
    {
        InputPins.Add(PinMetadata.CreateInput(name, dataType, description, defaultValue));
        return this;
    }

    /// <summary>
    /// 添加输出引脚
    /// </summary>
    /// <param name="name">引脚名称</param>
    /// <param name="dataType">数据类型</param>
    /// <param name="description">描述信息</param>
    /// <returns>当前NodeMetadata实例，支持链式调用</returns>
    public NodeMetadata AddOutputPin(string name, Type dataType, string description = "")
    {
        OutputPins.Add(PinMetadata.CreateOutput(name, dataType, description));
        return this;
    }

    /// <summary>
    /// 设置节点工厂函数
    /// </summary>
    /// <param name="factory">工厂函数</param>
    /// <returns>当前NodeMetadata实例，支持链式调用</returns>
    public NodeMetadata SetFactory(Func<Node> factory)
    {
        NodeFactory = factory;
        return this;
    }

    /// <summary>
    /// 创建节点实例
    /// </summary>
    /// <returns>创建的节点实例</returns>
    public Node CreateInstance()
    {
        Node node;

        if (IsMethodBased)
        {
            // 基于方法的节点：创建通用的 MethodNode 实例
            var methodNode = new MethodNode(Name);
            
            // 设置执行逻辑
            methodNode.SetExecutionLogic((inputs) => {
                if (SourceMethod == null)
                    throw new InvalidOperationException("SourceMethod is null for method-based node");
                
                var result = SourceMethod.Invoke(LibraryInstance, inputs);
                return SourceMethod.ReturnType == typeof(void) ? new object[0] : new[] { result };
            });
            
            node = methodNode;
        }
        else
        {
            // 基于类的节点：使用传统方式创建
            if (NodeFactory != null)
            {
                node = NodeFactory();
            }
            else if (NodeType != null)
            {
                node = (Node)Activator.CreateInstance(NodeType)!;
            }
            else
            {
                throw new InvalidOperationException("NodeType is null and no factory is provided");
            }
        }

        // 设置基本属性
        node.Name = Name;

        // 只为方法节点创建引脚，基于类的节点需要手动初始化引脚
        if (IsMethodBased)
        {
            // 根据元数据创建引脚
            foreach (var pinMetadata in InputPins)
            {
                var pin = new Pin
                {
                    Name = pinMetadata.Name,
                    Direction = pinMetadata.Direction,
                    DataType = pinMetadata.DataType,
                    Value = pinMetadata.DefaultValue,
                    ParentNode = node,
                    IsFlowPin = pinMetadata.IsFlowPin
                };
                node.InputPins.Add(pin);
            }

            foreach (var pinMetadata in OutputPins)
            {
                var pin = new Pin
                {
                    Name = pinMetadata.Name,
                    Direction = pinMetadata.Direction,
                    DataType = pinMetadata.DataType,
                    ParentNode = node,
                    IsFlowPin = pinMetadata.IsFlowPin
                };
                node.OutputPins.Add(pin);
            }
        }
        else
        {
            // 基于类的节点：手动初始化引脚
            if (node is NodeBase nodeBase)
            {
                nodeBase.InitializePinsFromMetadata(this);
            }
        }

        return node;
    }

    /// <summary>
    /// 验证节点元数据是否有效
    /// </summary>
    /// <returns>如果有效返回true，否则返回false</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return false;

        if (IsMethodBased)
        {
            // 基于方法的节点验证
            return SourceMethod != null && NodeType != null;
        }
        else
        {
            // 基于类的节点验证
            return NodeType != null && (NodeType.IsSubclassOf(typeof(Node)) || NodeFactory != null);
        }
    }

    public override string ToString()
    {
        var typeName = NodeType?.Name ?? "Unknown";
        if (IsMethodBased)
        {
            return $"{Path}/{Name} (Method: {SourceMethod?.Name})";
        }
        else
        {
            return $"{Path}/{Name} (Class: {typeName})";
        }
    }
}
