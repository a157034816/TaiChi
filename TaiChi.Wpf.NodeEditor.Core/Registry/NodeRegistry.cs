using System.Collections.Concurrent;
using System.Reflection;
using TaiChi.Wpf.NodeEditor.Core.Attributes;
using TaiChi.Wpf.NodeEditor.Core.Enums;
using TaiChi.Wpf.NodeEditor.Core.Models;

namespace TaiChi.Wpf.NodeEditor.Core.Registry;

/// <summary>
/// 分层分类数据结构，用于支持多层级节点分组
/// </summary>
public class HierarchicalCategory
{
    /// <summary>
    /// 分类名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 完整路径
    /// </summary>
    public string FullPath { get; set; } = string.Empty;

    /// <summary>
    /// 层级深度
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// 子分类
    /// </summary>
    public List<HierarchicalCategory> Children { get; set; } = new();

    /// <summary>
    /// 该分类下的节点
    /// </summary>
    public List<NodeMetadata> Nodes { get; set; } = new();

    /// <summary>
    /// 父分类
    /// </summary>
    public HierarchicalCategory? Parent { get; set; }
}

/// <summary>
/// 节点注册表，负责存储所有已知的节点定义
/// </summary>
public static class NodeRegistry
{
    private static readonly ConcurrentDictionary<string, List<NodeMetadata>> _categories = new();
    private static readonly object _lock = new();

    /// <summary>
    /// 存储所有节点元数据，按 Category 分组
    /// </summary>
    public static IReadOnlyDictionary<string, List<NodeMetadata>> Categories
    {
        get
        {
            lock (_lock)
            {
                return _categories.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList());
            }
        }
    }

    /// <summary>
    /// 获取所有已注册的节点元数据
    /// </summary>
    public static IEnumerable<NodeMetadata> AllNodes
    {
        get
        {
            lock (_lock)
            {
                return _categories.Values.SelectMany(list => list).ToList();
            }
        }
    }

    /// <summary>
    /// 获取按路径分层的节点树形结构
    /// </summary>
    public static IEnumerable<HierarchicalCategory> HierarchicalCategories
    {
        get
        {
            lock (_lock)
            {
                return BuildHierarchicalCategories();
            }
        }
    }

    /// <summary>
    /// 扫描程序集以发现节点。
    /// 支持两种模式：基于类的节点（传统模式）和基于方法的节点（节点库模式）
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    public static void ScanAssembly(Assembly assembly)
    {
        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        // 清空现有的节点注册（可选，根据需求决定）
        // Clear(); 

        // 1. 扫描基于方法的节点库
        ScanNodeLibraries(assembly);

        // 2. 扫描传统的基于类的节点（向后兼容）
        ScanClassBasedNodes(assembly);
    }

    /// <summary>
    /// 扫描程序集中的节点库，发现基于方法的节点
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    private static void ScanNodeLibraries(Assembly assembly)
    {
        var libraryTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetCustomAttribute<NodeLibraryAttribute>() != null);

        foreach (var libraryType in libraryTypes)
        {
            var libraryAttribute = libraryType.GetCustomAttribute<NodeLibraryAttribute>()!;
            
            try
            {
                // 创建库实例以调用非静态方法
                var libraryInstance = Activator.CreateInstance(libraryType);

                // 扫描库中的所有公共方法
                var methods = libraryType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                    .Where(m => m.GetCustomAttribute<NodeAttribute>() != null && m.DeclaringType == libraryType);

                foreach (var method in methods)
                {
                    var nodeAttribute = method.GetCustomAttribute<NodeAttribute>()!;
                    
                    // 路径解析逻辑：NodeAttribute.Path 优先级高于 NodeLibraryAttribute.Path
                    string finalPath = nodeAttribute.Path ?? libraryAttribute.Path;

                    var metadata = new NodeMetadata(
                        sourceMethod: method,
                        libraryInstance: method.IsStatic ? null : libraryInstance,
                        name: nodeAttribute.Name,
                        path: finalPath,
                        description: nodeAttribute.Description
                    );

                    // 解析方法的输入引脚（参数）
                    AnalyzeMethodInputs(method, metadata);

                    // 解析方法的输出引脚（返回值）
                    AnalyzeMethodOutputs(method, metadata);

                    // 解析方法的流程入口特性
                    AnalyzeMethodFlowEntries(method, metadata);

                    // 解析方法的流程出口特性
                    AnalyzeMethodFlowExits(method, metadata);

                    RegisterNode(metadata);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续扫描其他库
                Console.WriteLine($"Error scanning node library {libraryType.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 扫描传统的基于类的节点（向后兼容）
    /// </summary>
    /// <param name="assembly">要扫描的程序集</param>
    private static void ScanClassBasedNodes(Assembly assembly)
    {
        var nodeTypes = assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.IsSubclassOf(typeof(Node)))
            .Where(type => type.GetCustomAttribute<NodeAttribute>() != null);

        foreach (var nodeType in nodeTypes)
        {
            var nodeAttribute = nodeType.GetCustomAttribute<NodeAttribute>()!;
            var metadata = CreateMetadataFromType(nodeType, nodeAttribute);
            RegisterNode(metadata);
        }
    }

    /// <summary>
    /// 通过元数据注册节点
    /// </summary>
    /// <param name="metadata">节点元数据</param>
    public static void RegisterNode(NodeMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        if (!metadata.IsValid())
            throw new ArgumentException("Invalid node metadata", nameof(metadata));

        lock (_lock)
        {
            // 使用 Path 的根级别作为分类键，保持向后兼容
            var rootCategory = metadata.Path.Split('/').FirstOrDefault() ?? "Default";
            
            if (!_categories.ContainsKey(rootCategory))
            {
                _categories[rootCategory] = new List<NodeMetadata>();
            }

            // 检查是否已经注册了相同的节点
            NodeMetadata? existingNode = null;
            
            if (metadata.IsMethodBased)
            {
                // 对于基于方法的节点，使用方法信息进行唯一性检查
                existingNode = _categories[rootCategory]
                    .FirstOrDefault(n => n.IsMethodBased && 
                                       n.SourceMethod == metadata.SourceMethod && 
                                       n.Name == metadata.Name);
            }
            else
            {
                // 对于基于类的节点，使用传统的类型和名称检查
                existingNode = _categories[rootCategory]
                    .FirstOrDefault(n => !n.IsMethodBased && 
                                       n.NodeType == metadata.NodeType && 
                                       n.Name == metadata.Name);
            }

            if (existingNode != null)
            {
                // 替换现有的节点元数据
                var index = _categories[rootCategory].IndexOf(existingNode);
                _categories[rootCategory][index] = metadata;
            }
            else
            {
                _categories[rootCategory].Add(metadata);
            }
        }
    }

    /// <summary>
    /// 通过工厂函数手动注册节点
    /// </summary>
    /// <param name="nodeFactory">节点工厂函数</param>
    /// <param name="name">节点名称</param>
    /// <param name="category">节点类别</param>
    /// <param name="description">节点描述</param>
    public static void RegisterNode(Func<Node> nodeFactory, string name, string category = "Default", string description = "")
    {
        if (nodeFactory == null)
            throw new ArgumentNullException(nameof(nodeFactory));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Node name cannot be null or empty", nameof(name));

        // 创建一个临时实例来获取类型信息
        var tempInstance = nodeFactory();
        var nodeType = tempInstance.GetType();

        var metadata = new NodeMetadata(nodeType, name, category, description)
        {
            NodeFactory = nodeFactory
        };

        // 尝试从临时实例获取引脚信息
        foreach (var inputPin in tempInstance.InputPins)
        {
            metadata.InputPins.Add(new PinMetadata(inputPin.Name, inputPin.Direction, inputPin.DataType));
        }

        foreach (var outputPin in tempInstance.OutputPins)
        {
            metadata.OutputPins.Add(new PinMetadata(outputPin.Name, outputPin.Direction, outputPin.DataType));
        }

        RegisterNode(metadata);
    }

    /// <summary>
    /// 根据元数据创建节点实例
    /// </summary>
    /// <param name="metadata">节点元数据</param>
    /// <returns>创建的节点实例</returns>
    public static Node CreateInstance(NodeMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        return metadata.CreateInstance();
    }

    /// <summary>
    /// 根据节点类型和名称查找元数据
    /// </summary>
    /// <param name="nodeType">节点类型</param>
    /// <param name="name">节点名称</param>
    /// <returns>找到的元数据，如果不存在返回null</returns>
    public static NodeMetadata? FindMetadata(Type nodeType, string name)
    {
        lock (_lock)
        {
            return _categories.Values
                .SelectMany(list => list)
                .FirstOrDefault(metadata => metadata.NodeType == nodeType && metadata.Name == name);
        }
    }

    /// <summary>
    /// 根据类别和名称查找元数据
    /// </summary>
    /// <param name="category">节点类别</param>
    /// <param name="name">节点名称</param>
    /// <returns>找到的元数据，如果不存在返回null</returns>
    public static NodeMetadata? FindMetadata(string category, string name)
    {
        lock (_lock)
        {
            if (_categories.TryGetValue(category, out var nodes))
            {
                return nodes.FirstOrDefault(metadata => metadata.Name == name);
            }
            return null;
        }
    }

    /// <summary>
    /// 清空所有已注册的节点
    /// </summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _categories.Clear();
        }
    }

    /// <summary>
    /// 从类型和特性创建节点元数据
    /// </summary>
    private static NodeMetadata CreateMetadataFromType(Type nodeType, NodeAttribute nodeAttribute)
    {
        var metadata = new NodeMetadata(nodeType, nodeAttribute.Name, nodeAttribute.Path ?? "Default", nodeAttribute.Description);

        // 查找Execute方法来分析引脚
        var executeMethod = nodeType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Execute" && m.DeclaringType == nodeType);

        if (executeMethod != null)
        {
            // 分析方法参数作为输入引脚
            var parameters = executeMethod.GetParameters();
            foreach (var parameter in parameters)
            {
                var pinAttribute = parameter.GetCustomAttribute<PinAttribute>();
                var pinName = pinAttribute?.Name ?? parameter.Name ?? "Input";

                metadata.AddInputPin(pinName, parameter.ParameterType, pinAttribute?.Description ?? "");
            }

            // 分析返回值作为输出引脚
            if (executeMethod.ReturnType != typeof(void))
            {
                var returnAttribute = executeMethod.ReturnParameter?.GetCustomAttribute<PinAttribute>();
                var pinName = returnAttribute?.Name ?? "Result";

                metadata.AddOutputPin(pinName, executeMethod.ReturnType, returnAttribute?.Description ?? "");
            }
        }

        // 分析属性上的Pin特性（输入引脚）
        var inputProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<PinAttribute>() != null);

        foreach (var property in inputProperties)
        {
            var pinAttribute = property.GetCustomAttribute<PinAttribute>()!;
            var pinName = pinAttribute.Name ?? property.Name;

            if (property.CanRead && property.CanWrite)
            {
                // 可读写属性作为输入引脚
                metadata.AddInputPin(pinName, property.PropertyType, pinAttribute.Description);
            }
            else if (property.CanRead)
            {
                // 只读属性作为输出引脚
                metadata.AddOutputPin(pinName, property.PropertyType, pinAttribute.Description);
            }
        }

        // 分析属性上的PinReturn特性（输出引脚）
        var outputProperties = nodeType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<PinReturnAttribute>() != null);

        foreach (var property in outputProperties)
        {
            var pinReturnAttribute = property.GetCustomAttribute<PinReturnAttribute>()!;
            var pinName = pinReturnAttribute.Name ?? property.Name;

            metadata.AddOutputPin(pinName, property.PropertyType, pinReturnAttribute.Description);
        }

        // 分析流程入口特性
        var entryAttributes = nodeType.GetCustomAttributes<NodeEntryAttribute>();
        foreach (var entry in entryAttributes)
        {
            var pinMetadata = new PinMetadata
            {
                Name = entry.Name,
                Direction = PinDirection.Input,
                DataType = typeof(object),
                Description = entry.Description,
                IsFlowPin = true
            };
            metadata.InputPins.Add(pinMetadata);
        }

        // 分析流程出口特性
        var exitAttributes = nodeType.GetCustomAttributes<NodeExitAttribute>();
        foreach (var exit in exitAttributes)
        {
            var pinMetadata = new PinMetadata
            {
                Name = exit.Name,
                Direction = PinDirection.Output,
                DataType = typeof(object),
                Description = exit.Description,
                IsFlowPin = true
            };
            metadata.OutputPins.Add(pinMetadata);
        }

        return metadata;
    }

    /// <summary>
    /// 分析方法的输入参数并创建输入引脚元数据
    /// </summary>
    /// <param name="method">要分析的方法</param>
    /// <param name="metadata">要填充的节点元数据</param>
    private static void AnalyzeMethodInputs(MethodInfo method, NodeMetadata metadata)
    {
        var parameters = method.GetParameters();
        foreach (var parameter in parameters)
        {
            var pinAttribute = parameter.GetCustomAttribute<PinAttribute>();
            if (pinAttribute != null)
            {
                // 使用 PinAttribute 指定的名称
                var pinName = pinAttribute.Name;
                var defaultValue = pinAttribute.DefaultValue;
                
                metadata.AddInputPin(pinName, parameter.ParameterType, pinAttribute.Description, defaultValue);
            }
            else
            {
                // 如果没有 PinAttribute，使用参数名作为引脚名
                metadata.AddInputPin(parameter.Name ?? "Input", parameter.ParameterType);
            }
        }
    }

    /// <summary>
    /// 分析方法的返回值并创建输出引脚元数据
    /// </summary>
    /// <param name="method">要分析的方法</param>
    /// <param name="metadata">要填充的节点元数据</param>
    private static void AnalyzeMethodOutputs(MethodInfo method, NodeMetadata metadata)
    {
        // 检查方法是否有返回值
        if (method.ReturnType != typeof(void))
        {
            var returnAttribute = method.ReturnParameter?.GetCustomAttribute<PinAttribute>();
            if (returnAttribute != null)
            {
                // 使用 PinAttribute 指定的名称和描述
                metadata.AddOutputPin(returnAttribute.Name, method.ReturnType, returnAttribute.Description);
            }
            else
            {
                // 默认输出引脚名称
                metadata.AddOutputPin("Result", method.ReturnType);
            }
        }

        // TODO: 未来可以支持多返回值（通过 Tuple 或 ValueTuple）
        // 或者通过 out/ref 参数支持多输出
    }

    /// <summary>
    /// 分析方法的流程入口特性并创建流程入口引脚元数据
    /// </summary>
    /// <param name="method">要分析的方法</param>
    /// <param name="metadata">要填充的节点元数据</param>
    private static void AnalyzeMethodFlowEntries(MethodInfo method, NodeMetadata metadata)
    {
        var entryAttributes = method.GetCustomAttributes<NodeEntryAttribute>();
        foreach (var entry in entryAttributes)
        {
            var pinMetadata = new PinMetadata
            {
                Name = entry.Name,
                Direction = PinDirection.Input,
                DataType = typeof(object),
                Description = entry.Description,
                IsFlowPin = true
            };
            metadata.InputPins.Add(pinMetadata);
        }
    }

    /// <summary>
    /// 分析方法的流程出口特性并创建流程出口引脚元数据
    /// </summary>
    /// <param name="method">要分析的方法</param>
    /// <param name="metadata">要填充的节点元数据</param>
    private static void AnalyzeMethodFlowExits(MethodInfo method, NodeMetadata metadata)
    {
        var exitAttributes = method.GetCustomAttributes<NodeExitAttribute>();
        foreach (var exit in exitAttributes)
        {
            var pinMetadata = new PinMetadata
            {
                Name = exit.Name,
                Direction = PinDirection.Output,
                DataType = typeof(object),
                Description = exit.Description,
                IsFlowPin = true
            };
            metadata.OutputPins.Add(pinMetadata);
        }
    }

    /// <summary>
    /// 构建分层分类结构
    /// </summary>
    /// <returns>分层分类列表</returns>
    private static List<HierarchicalCategory> BuildHierarchicalCategories()
    {
        var rootCategories = new Dictionary<string, HierarchicalCategory>();
        var allCategories = new Dictionary<string, HierarchicalCategory>();

        // 处理所有节点，构建分层结构
        foreach (var node in AllNodes)
        {
            var path = node.Path?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(path))
            {
                // 空Path节点，添加到根级别分类
                AddNodeToRootCategory(node, rootCategories, allCategories);
            }
            else
            {
                // 有Path的节点，按路径层级添加
                AddNodeToHierarchicalCategory(node, path, rootCategories, allCategories);
            }
        }

        return rootCategories.Values.OrderBy(c => c.Name).ToList();
    }

    /// <summary>
    /// 将节点添加到根级别分类
    /// </summary>
    /// <param name="node">节点元数据</param>
    /// <param name="rootCategories">根分类字典</param>
    /// <param name="allCategories">全部分类字典</param>
    private static void AddNodeToRootCategory(NodeMetadata node, Dictionary<string, HierarchicalCategory> rootCategories, Dictionary<string, HierarchicalCategory> allCategories)
    {
        const string rootCategoryName = "根级别";

        if (!rootCategories.TryGetValue(rootCategoryName, out var rootCategory))
        {
            rootCategory = new HierarchicalCategory
            {
                Name = rootCategoryName,
                FullPath = string.Empty,
                Level = 0
            };
            rootCategories[rootCategoryName] = rootCategory;
            allCategories[string.Empty] = rootCategory;
        }

        rootCategory.Nodes.Add(node);
    }

    /// <summary>
    /// 将节点添加到分层分类
    /// </summary>
    /// <param name="node">节点元数据</param>
    /// <param name="path">节点路径</param>
    /// <param name="rootCategories">根分类字典</param>
    /// <param name="allCategories">全部分类字典</param>
    private static void AddNodeToHierarchicalCategory(NodeMetadata node, string path, Dictionary<string, HierarchicalCategory> rootCategories, Dictionary<string, HierarchicalCategory> allCategories)
    {
        var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        HierarchicalCategory? currentCategory = null;
        var currentPath = string.Empty;

        // 构建或获取路径上的每一层分类
        for (int i = 0; i < pathSegments.Length; i++)
        {
            var segment = pathSegments[i];
            currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";

            if (!allCategories.TryGetValue(currentPath, out var category))
            {
                category = new HierarchicalCategory
                {
                    Name = segment,
                    FullPath = currentPath,
                    Level = i
                };

                allCategories[currentPath] = category;

                // 设置父子关系
                if (currentCategory != null)
                {
                    category.Parent = currentCategory;
                    currentCategory.Children.Add(category);
                }
                else
                {
                    // 这是根级别分类
                    rootCategories[segment] = category;
                }
            }

            currentCategory = category;
        }

        // 将节点添加到最终的分类中
        if (currentCategory != null)
        {
            currentCategory.Nodes.Add(node);
        }
    }
}
