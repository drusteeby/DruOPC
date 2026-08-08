namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// Subscribes to OPC UA events of a notifier node (default: the Server object)
/// and keeps a rolling log of received events.
/// </summary>
public sealed class UaEventLog : IAsyncDisposable
{
    private const int MaxEvents = 500;

    private readonly UaConnection _connection;
    private readonly ILogger<UaEventLog> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly LinkedList<UaEventRow> _events = [];

    private Subscription? _subscription;
    private MonitoredItem? _monitoredItem;
    private volatile UaEventRow[] _eventsSnapshot = [];

    public UaEventLog(UaConnection connection, ILogger<UaEventLog> logger)
    {
        _connection = connection;
        _logger = logger;

        _connection.SessionReplaced += OnSessionReplaced;
    }

    public bool IsActive => _monitoredItem is not null;

    public NodeId? NotifierNodeId { get; private set; }

    public string NotifierName { get; private set; } = "";

    /// <summary>Thread-safe snapshot of the received events, newest first.</summary>
    public IReadOnlyList<UaEventRow> Events => _eventsSnapshot;

    /// <summary>Raised when events arrive. May fire on a background thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// Start listening for events emitted by the given notifier node.
    /// </summary>
    public async Task StartAsync(NodeId notifierNodeId, string notifierName)
    {
        var session = _connection.Session
            ?? throw new InvalidOperationException("Not connected to a server.");

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);

            _subscription = new Subscription(session.DefaultSubscription)
            {
                DisplayName = "UaScope events",
                PublishingInterval = 500,
                KeepAliveCount = 10,
                LifetimeCount = 1000,
                MaxNotificationsPerPublish = 10_000,
                PublishingEnabled = true,
            };

            session.AddSubscription(_subscription);
            await _subscription.CreateAsync().ConfigureAwait(false);

            // The MonitoredItem default filter for EventNotifier items selects the
            // standard base event fields (EventId, EventType, SourceName, Time,
            // Message, Severity, ...).
            _monitoredItem = new MonitoredItem(_subscription.DefaultItem)
            {
                StartNodeId = notifierNodeId,
                NodeClass = NodeClass.Object,
                AttributeId = Attributes.EventNotifier,
                DisplayName = notifierName,
                SamplingInterval = 0,
                QueueSize = 1000,
                DiscardOldest = true,
            };

            _monitoredItem.Notification += OnNotification;
            _subscription.AddItem(_monitoredItem);
            await _subscription.ApplyChangesAsync().ConfigureAwait(false);

            NotifierNodeId = notifierNodeId;
            NotifierName = notifierName;
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Stop listening for events.
    /// </summary>
    public async Task StopAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (_events)
        {
            _events.Clear();
            _eventsSnapshot = [];
        }

        Changed?.Invoke();
    }

    private void OnSessionReplaced(Opc.Ua.Client.Session newSession)
    {
        // The SDK cloned our subscription onto the recreated session; re-bind.
        var clone = newSession.Subscriptions.FirstOrDefault(s => s.DisplayName == "UaScope events");
        if (clone is not null)
        {
            _subscription = clone;
            _monitoredItem = clone.MonitoredItems.FirstOrDefault();
            _logger.LogInformation("Event subscription re-bound to recreated session");
        }
    }

    private async Task StopCoreAsync()
    {
        _monitoredItem = null;
        NotifierNodeId = null;
        NotifierName = "";

        if (_subscription is not null)
        {
            try
            {
                await _subscription.DeleteAsync(silent: true).ConfigureAwait(false);
                _subscription.Session?.RemoveSubscription(_subscription);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error deleting event subscription");
            }

            _subscription = null;
        }
    }

    private void OnNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not EventFieldList eventFields)
        {
            return;
        }

        if (monitoredItem.Filter is not EventFilter filter)
        {
            return;
        }

        string GetField(string name)
        {
            for (int i = 0; i < filter.SelectClauses.Count; i++)
            {
                var clause = filter.SelectClauses[i];
                if (clause.BrowsePath.Count == 1
                    && clause.BrowsePath[0].Name == name
                    && i < eventFields.EventFields.Count)
                {
                    return UaFormat.FormatValue(eventFields.EventFields[i].Value);
                }
            }

            return "";
        }

        string eventTypeText = "";
        for (int i = 0; i < filter.SelectClauses.Count; i++)
        {
            var clause = filter.SelectClauses[i];
            if (clause.BrowsePath.Count == 1
                && clause.BrowsePath[0].Name == BrowseNames.EventType
                && i < eventFields.EventFields.Count
                && eventFields.EventFields[i].Value is NodeId eventTypeId)
            {
                eventTypeText = _connection.Session is { } session
                    ? session.NodeCache.GetDisplayText(eventTypeId)
                    : eventTypeId.ToString();
            }
        }

        var row = new UaEventRow(
            DateTime.UtcNow,
            GetField(BrowseNames.Time),
            GetField(BrowseNames.Severity),
            GetField(BrowseNames.SourceName),
            eventTypeText,
            GetField(BrowseNames.Message));

        lock (_events)
        {
            _events.AddFirst(row);
            while (_events.Count > MaxEvents)
            {
                _events.RemoveLast();
            }

            _eventsSnapshot = _events.ToArray();
        }

        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _connection.SessionReplaced -= OnSessionReplaced;

        try
        {
            await StopAsync().ConfigureAwait(false);
        }
        catch
        {
            // Circuit teardown must not throw.
        }
    }
}
