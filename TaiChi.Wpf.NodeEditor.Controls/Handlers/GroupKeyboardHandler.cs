using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TaiChi.Wpf.NodeEditor.Core.Models;
using TaiChi.Wpf.NodeEditor.Controls.Managers;

namespace TaiChi.Wpf.NodeEditor.Controls.Handlers;

/// <summary>
/// 分组键盘处理器：
/// - Ctrl+G：将当前选中的节点创建为一个分组并选中该分组
/// - F2：重命名当前选中的分组（默认生成唯一名称）
/// 使用方式：
/// 在 NodeCanvas 上设置附加属性：
///   handlers:GroupKeyboardHandler.IsEnabled="True"
///   handlers:GroupKeyboardHandler.GroupManager="{Binding YourNodeGroupManager}"
/// 注意：建议将 NodeCanvas.GroupsSource 绑定到 GroupManager.Groups 以实现可视化。
/// </summary>
public static class GroupKeyboardHandler
{
    #region 附加属性

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(GroupKeyboardHandler),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static readonly DependencyProperty GroupManagerProperty =
        DependencyProperty.RegisterAttached(
            "GroupManager",
            typeof(NodeGroupManager),
            typeof(GroupKeyboardHandler),
            new PropertyMetadata(null));

    public static void SetGroupManager(DependencyObject element, NodeGroupManager? value) => element.SetValue(GroupManagerProperty, value);
    public static NodeGroupManager? GetGroupManager(DependencyObject element) => (NodeGroupManager?)element.GetValue(GroupManagerProperty);

    #endregion

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NodeCanvas canvas) return;

        var enable = (bool)e.NewValue;
        if (enable)
        {
            canvas.PreviewKeyDown += OnCanvasPreviewKeyDown;
        }
        else
        {
            canvas.PreviewKeyDown -= OnCanvasPreviewKeyDown;
        }
    }

    private static void OnCanvasPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not NodeCanvas canvas) return;
        var manager = GetGroupManager(canvas);
        if (manager == null) return;

        // Ctrl+G 创建分组
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.G)
        {
            TryCreateGroupFromSelection(canvas, manager);
            e.Handled = true;
            return;
        }

        // F2 重命名已选分组
        if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
        {
            TryRenameSelectedGroup(manager);
            e.Handled = true;
            return;
        }
    }

    private static void TryCreateGroupFromSelection(NodeCanvas canvas, NodeGroupManager manager)
    {
        var selectedNodes = FindVisualChildren<NodeControl>(canvas)
            .Where(n => n.IsSelected && n.NodeData != null)
            .Select(n => n.NodeData!)
            .Distinct()
            .ToList();

        if (selectedNodes.Count == 0)
            return;

        // 生成唯一分组名
        var name = GenerateUniqueGroupName(manager.Groups, "分组");

        var group = manager.CreateGroupFromNodes(name, selectedNodes, padding: 16);

        // 选中新分组，并清除其它分组选中
        foreach (var g in FlattenGroups(manager.Groups))
        {
            g.IsSelected = ReferenceEquals(g, group);
        }
    }

    private static void TryRenameSelectedGroup(NodeGroupManager manager)
    {
        var selected = FlattenGroups(manager.Groups).Where(g => g.IsSelected).ToList();
        if (selected.Count != 1) return;

        var target = selected[0];
        var newName = GenerateUniqueGroupName(manager.Groups, baseName: string.IsNullOrWhiteSpace(target.Name) ? "分组" : target.Name);
        target.Name = newName;
    }

    private static IEnumerable<NodeGroup> FlattenGroups(IEnumerable<NodeGroup> roots)
    {
        foreach (var g in roots)
        {
            yield return g;
            foreach (var c in FlattenGroups(g.Children))
                yield return c;
        }
    }

    private static string GenerateUniqueGroupName(IEnumerable<NodeGroup> roots, string baseName)
    {
        var existing = new HashSet<string>(FlattenGroups(roots).Select(g => g.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        if (!existing.Contains(baseName)) return baseName;

        int i = 1;
        while (true)
        {
            var candidate = $"{baseName} {i}";
            if (!existing.Contains(candidate)) return candidate;
            i++;
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
    {
        if (depObj == null) yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);
            if (child is T t)
                yield return t;

            foreach (var childOfChild in FindVisualChildren<T>(child))
                yield return childOfChild;
        }
    }
}

