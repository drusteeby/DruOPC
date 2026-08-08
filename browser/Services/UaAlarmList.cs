namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// One live alarm/condition row, keyed by condition and branch.
/// </summary>
public sealed class UaAlarmRow
{
    public required string Key { get; init; }

    public NodeId? ConditionId { get; init; }

    public byte[] EventId { get; set; } = [];

    public string ConditionName { get; set; } = "";

    public string SourceName { get; set; } = "";

    public string EventType { get; set; } = "";

    public string Message { get; set; } = "";

    public string Severity { get; set; } = "";

    public string Time { get; set; } = "";

    public string Comment { get; set; } = "";

    public bool Active { get; set; }

    public bool Acknowledged { get; set; }

    public bool Enabled { get; set; } = true;

    public bool Retain { get; set; } = true;

    public DateTime LastUpdateUtc { get; set; }
}

/// <summary>
/// Live alarms &amp; conditions view: subscribes to condition events on the
/// Server object, tracks retained conditions, and supports ConditionRefresh
/// and Acknowledge.
/// </summary>
public sealed class UaAlarmList : IAsyncDisposable
{
    private const string SubscriptionName = "UaScope alarms";

    private readonly UaConnection _connection;
    private readonly ILogger<UaAlarmList> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, UaAlarmRow> _rows = [];

    private volatile UaAlarmRow[] _rowsSnapshot = [];
    private Subscription? _subscription;
    private MonitoredItem? _monitoredItem;

    // Select clause layout; indexes must match BuildFilter().
    private const int FieldEventId = 0;
    private const int FieldEventType = 1;
    private const int FieldSourceName = 2;
    private const int FieldTime = 3;
    private const int FieldMessage = 4;
    private const int FieldSeverity = 5;
    private const int FieldConditionId = 6;
    private const int FieldConditionName = 7;
    private const int FieldBranchId = 8;
    private const int FieldRetain = 9;
    private const int FieldAckedState = 10;
    private const int FieldActiveState = 11;
    private const int FieldEnabledState = 12;
    private const int FieldComment = 13;

    public UaAlarmList(UaConnection connection, ILogger<UaAlarmList> logger)
    {
        _connection = connection;
        _logger = logger;

        _connection.SessionReplaced += OnSessionReplaced;
    }

    public bool IsActive => _monitoredItem is not null;

    /// <summary>Thread-safe snapshot of the current alarm rows.</summary>
    public IReadOnlyList<UaAlarmRow> Rows => _rowsSnapshot;

    /// <summary>Raised when alarms change. May fire on a background thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// Subscribe to condition events on the Server object and ask the server
    /// to replay currently retained conditions.
    /// </summary>
    public async Task StartAsync()
    {
        var session = _connection.Session
            ?? throw new InvalidOperationException("Not connected to a server.");

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync().ConfigureAwait(false);

            _subscription = new Subscription(session.DefaultSubscription)
            {
                DisplayName = SubscriptionName,
                PublishingInterval = 500,
                KeepAliveCount = 10,
                LifetimeCount = 1000,
                MaxNotificationsPerPublish = 10_000,
                PublishingEnabled = true,
            };

            session.AddSubscription(_subscription);
            await _subscription.CreateAsync().ConfigureAwait(false);

            _monitoredItem = new MonitoredItem(_subscription.DefaultItem)
            {
                StartNodeId = ObjectIds.Server,
                NodeClass = NodeClass.Object,
                AttributeId = Attributes.EventNotifier,
                DisplayName = "Conditions",
                SamplingInterval = 0,
                QueueSize = 1000,
                DiscardOldest = true,
                Filter = BuildFilter(),
            };

            _monitoredItem.Notification += OnNotification;
            _subscription.AddItem(_monitoredItem);
            await _subscription.ApplyChangesAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        await RefreshAsync().ConfigureAwait(false);
        Changed?.Invoke();
    }

    /// <summary>
    /// Stop the alarm subscription and clear the list.
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

    /// <summary>
    /// Ask the server to replay all retained conditions (ConditionRefresh).
    /// </summary>
    public async Task RefreshAsync()
    {
        var session = _connection.Session;
        var subscription = _subscription;
        if (session is null || subscription is null)
        {
            return;
        }

        try
        {
            await session.CallAsync(
                ObjectTypeIds.ConditionType,
                MethodIds.ConditionType_ConditionRefresh,
                default,
                subscription.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ConditionRefresh failed");
            throw;
        }
    }

    /// <summary>
    /// Acknowledge a condition.
    /// </summary>
    public async Task AcknowledgeAsync(UaAlarmRow row, string comment)
    {
        var session = _connection.Session
            ?? throw new InvalidOperationException("Not connected to a server.");

        if (row.ConditionId is null || row.EventId.Length == 0)
        {
            throw new InvalidOperationException("This condition does not support acknowledgement.");
        }

        await session.CallAsync(
            row.ConditionId,
            MethodIds.AcknowledgeableConditionType_Acknowledge,
            default,
            row.EventId,
            new LocalizedText(comment)).ConfigureAwait(false);
    }

    public void Clear()
    {
        lock (_rows)
        {
            _rows.Clear();
            _rowsSnapshot = [];
        }

        Changed?.Invoke();
    }

    private static EventFilter BuildFilter()
    {
        var filter = new EventFilter();

        void Select(NodeId typeDefinitionId, uint attributeId, params string[] browsePath)
        {
            var operand = new SimpleAttributeOperand
            {
                TypeDefinitionId = typeDefinitionId,
                AttributeId = attributeId,
            };

            foreach (var part in browsePath)
            {
                operand.BrowsePath.Add(new QualifiedName(part));
            }

            filter.SelectClauses.Add(operand);
        }

        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.EventId);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.EventType);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.SourceName);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.Time);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.Message);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.Severity);
        Select(ObjectTypeIds.ConditionType, Attributes.NodeId); // ConditionId
        Select(ObjectTypeIds.ConditionType, Attributes.Value, BrowseNames.ConditionName);
        Select(ObjectTypeIds.ConditionType, Attributes.Value, BrowseNames.BranchId);
        Select(ObjectTypeIds.ConditionType, Attributes.Value, BrowseNames.Retain);
        Select(ObjectTypeIds.AcknowledgeableConditionType, Attributes.Value, BrowseNames.AckedState, BrowseNames.Id);
        Select(ObjectTypeIds.AlarmConditionType, Attributes.Value, BrowseNames.ActiveState, BrowseNames.Id);
        Select(ObjectTypeIds.ConditionType, Attributes.Value, BrowseNames.EnabledState, BrowseNames.Id);
        Select(ObjectTypeIds.ConditionType, Attributes.Value, BrowseNames.Comment);

        // Only condition-derived events belong in the alarm list.
        filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType);

        return filter;
    }

    private async Task StopCoreAsync()
    {
        _monitoredItem = null;

        if (_subscription is not null)
        {
            try
            {
                await _subscription.DeleteAsync(silent: true).ConfigureAwait(false);
                _subscription.Session?.RemoveSubscription(_subscription);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error deleting alarm subscription");
            }

            _subscription = null;
        }

        lock (_rows)
        {
            _rows.Clear();
            _rowsSnapshot = [];
        }
    }

    private void OnSessionReplaced(Session newSession)
    {
        _lock.Wait();
        try
        {
            var clone = newSession.Subscriptions.FirstOrDefault(s => s.DisplayName == SubscriptionName);
            if (clone is not null)
            {
                _subscription = clone;
                _monitoredItem = clone.MonitoredItems.FirstOrDefault();
                _logger.LogInformation("Alarm subscription re-bound to recreated session");
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private void OnNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (e.NotificationValue is not EventFieldList eventFields
            || eventFields.EventFields.Count <= FieldComment)
        {
            return;
        }

        object? Field(int index) => eventFields.EventFields[index].Value;

        var eventTypeId = Field(FieldEventType) as NodeId;

        // Refresh markers frame a ConditionRefresh replay; nothing to show.
        if (eventTypeId == ObjectTypeIds.RefreshStartEventType
            || eventTypeId == ObjectTypeIds.RefreshEndEventType
            || eventTypeId == ObjectTypeIds.RefreshRequiredEventType)
        {
            return;
        }

        var conditionId = Field(FieldConditionId) as NodeId;
        var branchId = Field(FieldBranchId) as NodeId;
        string key = $"{conditionId}|{branchId}";

        bool retain = Field(FieldRetain) is bool r && r;

        lock (_rows)
        {
            if (!_rows.TryGetValue(key, out var row))
            {
                if (!retain)
                {
                    return; // Condition already left the retained set.
                }

                row = new UaAlarmRow { Key = key, ConditionId = conditionId };
                _rows[key] = row;
            }

            row.EventId = Field(FieldEventId) as byte[] ?? row.EventId;
            row.ConditionName = UaFormat.FormatValue(Field(FieldConditionName));
            row.SourceName = UaFormat.FormatValue(Field(FieldSourceName));
            row.EventType = _connection.Session is { } session && eventTypeId is not null
                ? session.NodeCache.GetDisplayText(eventTypeId)
                : eventTypeId?.ToString() ?? "";
            row.Message = UaFormat.FormatValue(Field(FieldMessage));
            row.Severity = UaFormat.FormatValue(Field(FieldSeverity));
            row.Time = UaFormat.FormatValue(Field(FieldTime));
            row.Comment = UaFormat.FormatValue(Field(FieldComment));
            row.Acknowledged = Field(FieldAckedState) is bool acked && acked;
            row.Active = Field(FieldActiveState) is bool active && active;
            row.Enabled = Field(FieldEnabledState) is not bool enabled || enabled;
            row.Retain = retain;
            row.LastUpdateUtc = DateTime.UtcNow;

            if (!retain)
            {
                _rows.Remove(key);
            }

            _rowsSnapshot = _rows.Values
                .OrderByDescending(a => a.Active)
                .ThenByDescending(a => a.LastUpdateUtc)
                .ToArray();
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
