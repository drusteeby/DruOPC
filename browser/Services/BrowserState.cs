namespace DruOpc.Services;

using Opc.Ua;

/// <summary>
/// Per-circuit UI state shared between components.
/// </summary>
public sealed class BrowserState
{
    public UaTreeNode? SelectedNode { get; private set; }

    public event Action? SelectionChanged;

    public event Action<string, bool>? Notified;

    /// <summary>Raised when a component asks the tree to expand to and show a node.</summary>
    public event Action<NodeId>? RevealRequested;

    /// <summary>Raised when a component asks to open the value inspector for a node.</summary>
    public event Action<NodeId, string>? InspectRequested;

    /// <summary>Raised when a component asks to open the history dialog for a node.</summary>
    public event Action<NodeId, string>? HistoryRequested;

    public void Select(UaTreeNode? node)
    {
        SelectedNode = node;
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Ask the address tree to expand down to the given node and select it.
    /// </summary>
    public void RequestReveal(NodeId nodeId)
        => RevealRequested?.Invoke(nodeId);

    /// <summary>
    /// Ask to open the full value inspector for a node.
    /// </summary>
    public void RequestInspect(NodeId nodeId, string displayName)
        => InspectRequested?.Invoke(nodeId, displayName);

    /// <summary>
    /// Ask to open the history dialog for a node.
    /// </summary>
    public void RequestHistory(NodeId nodeId, string displayName)
        => HistoryRequested?.Invoke(nodeId, displayName);

    /// <summary>
    /// Show a transient notification toast.
    /// </summary>
    public void Notify(string message, bool isError = false)
        => Notified?.Invoke(message, isError);
}
