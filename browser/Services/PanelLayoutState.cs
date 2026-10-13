namespace DruOpc.Services;

using Microsoft.JSInterop;

/// <summary>
/// Per-circuit collapsed/expanded state of the side panels, persisted to
/// localStorage so it survives page reloads.
/// </summary>
public sealed class PanelLayoutState(IJSRuntime js)
{
    private readonly HashSet<string> _collapsed = [];

    public event Action? Changed;

    public bool IsCollapsed(string panelKey) => _collapsed.Contains(panelKey);

    /// <summary>
    /// Load the saved layout from localStorage in a single JS call.
    /// Call once, after the first interactive render.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            var saved = await js.InvokeAsync<string[]>("druopc.getCollapsedPanels").ConfigureAwait(false);
            _collapsed.Clear();
            _collapsed.UnionWith(saved);
            Changed?.Invoke();
        }
        catch (Exception)
        {
            // localStorage unavailable; not critical.
        }
    }

    public async Task ToggleAsync(string panelKey)
    {
        var collapsed = !_collapsed.Remove(panelKey);
        if (collapsed)
        {
            _collapsed.Add(panelKey);
        }

        Changed?.Invoke();

        try
        {
            await js.InvokeVoidAsync("druopc.setPanelCollapsed", panelKey, collapsed).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // localStorage unavailable; not critical.
        }
    }

    /// <summary>
    /// Expand every panel and forget the saved layout, without reloading the page
    /// (a reload would drop the OPC UA session).
    /// </summary>
    public async Task ResetAsync()
    {
        _collapsed.Clear();
        Changed?.Invoke();

        try
        {
            await js.InvokeVoidAsync("druopc.resetPanelLayout").ConfigureAwait(false);
        }
        catch (Exception)
        {
            // localStorage unavailable; not critical.
        }
    }
}
