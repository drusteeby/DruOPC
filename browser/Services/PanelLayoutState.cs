namespace DruOpc.Services;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

/// <summary>
/// Per-circuit collapsed/expanded state of the side panels.
/// </summary>
/// <remarks>
/// The state is saved in a cookie rather than localStorage so the server can
/// read it while prerendering; the page then arrives with collapsed panels
/// already collapsed instead of flashing open until the circuit connects.
/// The prerendered state is handed to the interactive circuit through
/// <see cref="PersistentComponentState"/>.
/// </remarks>
public sealed class PanelLayoutState(
    IJSRuntime js,
    IHttpContextAccessor httpContextAccessor,
    PersistentComponentState persistentState) : IDisposable
{
    public const string CookieName = "druopc.panelsCollapsed";

    private const string PersistKey = nameof(PanelLayoutState);

    private readonly HashSet<string> _collapsed = [];
    private PersistingComponentStateSubscription? _persisting;

    public event Action? Changed;

    public bool IsCollapsed(string panelKey) => _collapsed.Contains(panelKey);

    /// <summary>
    /// Load the saved layout. Call from the page's OnInitialized, before the
    /// panels render: during prerender it reads the cookie, and in the
    /// interactive circuit it takes the state the prerender persisted.
    /// </summary>
    public void Initialize()
    {
        if (_persisting is not null)
        {
            return;
        }

        _persisting = persistentState.RegisterOnPersisting(() =>
        {
            persistentState.PersistAsJson(PersistKey, _collapsed.ToArray());
            return Task.CompletedTask;
        }, RenderMode.InteractiveServer);

        var saved = persistentState.TryTakeFromJson<string[]>(PersistKey, out var restored)
            ? restored
            : httpContextAccessor.HttpContext?.Request.Cookies[CookieName]?.Split('|');

        _collapsed.UnionWith((saved ?? []).Where(k => k.Length > 0));
    }

    public async Task ToggleAsync(string panelKey)
    {
        if (!_collapsed.Remove(panelKey))
        {
            _collapsed.Add(panelKey);
        }

        Changed?.Invoke();
        await SaveAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Expand every panel and forget the saved layout, without reloading the page
    /// (a reload would drop the OPC UA session).
    /// </summary>
    public async Task ResetAsync()
    {
        _collapsed.Clear();
        Changed?.Invoke();
        await SaveAsync().ConfigureAwait(false);
    }

    private async Task SaveAsync()
    {
        try
        {
            // Cast so the array is passed as one argument, not spread into the params list.
            await js.InvokeVoidAsync("druopc.setCollapsedPanels", (object)_collapsed.ToArray()).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Cookies unavailable; not critical.
        }
    }

    public void Dispose()
    {
        _persisting?.Dispose();
    }
}
