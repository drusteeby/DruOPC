namespace UaScope.Services;

/// <summary>
/// Per-circuit UI state shared between components.
/// </summary>
public sealed class BrowserState
{
    public UaTreeNode? SelectedNode { get; private set; }

    public event Action? SelectionChanged;

    public event Action<string, bool>? Notified;

    public void Select(UaTreeNode? node)
    {
        SelectedNode = node;
        SelectionChanged?.Invoke();
    }

    /// <summary>
    /// Show a transient notification toast.
    /// </summary>
    public void Notify(string message, bool isError = false)
        => Notified?.Invoke(message, isError);
}
