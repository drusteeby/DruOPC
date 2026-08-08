namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// The live watch list (data access view): one subscription with monitored
/// items whose values update in real time.
/// </summary>
public sealed class UaWatchList : IAsyncDisposable
{
    private readonly UaConnection _connection;
    private readonly UaBrowserService _browser;
    private readonly ILogger<UaWatchList> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<UaWatchItem> _items = [];

    private Subscription? _subscription;
    private uint _nextHandle = 1;

    public UaWatchList(UaConnection connection, UaBrowserService browser, ILogger<UaWatchList> logger)
    {
        _connection = connection;
        _browser = browser;
        _logger = logger;
    }

    /// <summary>Snapshot of the current items (do not mutate).</summary>
    public IReadOnlyList<UaWatchItem> Items => _items;

    public int PublishingIntervalMs { get; private set; } = 500;

    public int SamplingIntervalMs { get; private set; } = 250;

    /// <summary>Raised when any item value changes. May fire on a background thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// Add a variable node to the watch list and start monitoring it.
    /// </summary>
    public async Task AddAsync(NodeId nodeId, string displayName)
    {
        var session = _connection.Session
            ?? throw new InvalidOperationException("Not connected to a server.");

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_items.Any(i => i.NodeId == nodeId))
            {
                return; // already watched
            }

            await EnsureSubscriptionAsync(session).ConfigureAwait(false);

            var item = new UaWatchItem
            {
                ClientHandle = _nextHandle++,
                NodeId = nodeId,
                DisplayName = displayName,
            };

            // Read type info once to enable writes and type display.
            try
            {
                (NodeId dataTypeId, int valueRank, byte accessLevel) =
                    await _browser.ReadVariableMetadataAsync(nodeId).ConfigureAwait(false);
                item.DataTypeId = dataTypeId;
                item.ValueRank = valueRank;
                item.DataType = await _browser.GetDisplayNameAsync(dataTypeId).ConfigureAwait(false);
                if (valueRank != ValueRanks.Scalar)
                {
                    item.DataType += "[]";
                }

                item.IsWritable = (accessLevel & AccessLevels.CurrentWrite) != 0;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read variable metadata for {NodeId}", nodeId);
            }

            var monitoredItem = new MonitoredItem(_subscription!.DefaultItem)
            {
                StartNodeId = nodeId,
                AttributeId = Attributes.Value,
                DisplayName = displayName,
                SamplingInterval = SamplingIntervalMs,
                QueueSize = 10,
                DiscardOldest = true,
                Handle = item,
            };

            monitoredItem.Notification += OnNotification;

            _subscription.AddItem(monitoredItem);
            await _subscription.ApplyChangesAsync().ConfigureAwait(false);

            _items.Add(item);
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Remove an item from the watch list.
    /// </summary>
    public async Task RemoveAsync(UaWatchItem item)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _items.Remove(item);

            if (_subscription is not null)
            {
                var monitoredItem = _subscription.MonitoredItems
                    .FirstOrDefault(m => ReferenceEquals(m.Handle, item));
                if (monitoredItem is not null)
                {
                    _subscription.RemoveItem(monitoredItem);
                    await _subscription.ApplyChangesAsync().ConfigureAwait(false);
                }

                if (_items.Count == 0)
                {
                    await TearDownSubscriptionAsync().ConfigureAwait(false);
                }
            }
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Remove all items.
    /// </summary>
    public async Task ClearAsync()
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _items.Clear();
            await TearDownSubscriptionAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Change publishing/sampling intervals for the whole watch list.
    /// </summary>
    public async Task SetIntervalsAsync(int publishingIntervalMs, int samplingIntervalMs)
    {
        PublishingIntervalMs = Math.Clamp(publishingIntervalMs, 50, 60_000);
        SamplingIntervalMs = Math.Clamp(samplingIntervalMs, 0, 60_000);

        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_subscription is null)
            {
                return;
            }

            _subscription.PublishingInterval = PublishingIntervalMs;
            foreach (var monitoredItem in _subscription.MonitoredItems)
            {
                monitoredItem.SamplingInterval = SamplingIntervalMs;
            }

            await _subscription.ModifyAsync().ConfigureAwait(false);
            await _subscription.ApplyChangesAsync().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    private async Task EnsureSubscriptionAsync(Session session)
    {
        if (_subscription is not null && ReferenceEquals(_subscription.Session, session))
        {
            return;
        }

        await TearDownSubscriptionAsync().ConfigureAwait(false);

        _subscription = new Subscription(session.DefaultSubscription)
        {
            DisplayName = "UaScope watch list",
            PublishingInterval = PublishingIntervalMs,
            KeepAliveCount = 10,
            LifetimeCount = 1000,
            MaxNotificationsPerPublish = 10_000,
            PublishingEnabled = true,
        };

        session.AddSubscription(_subscription);
        await _subscription.CreateAsync().ConfigureAwait(false);
    }

    private async Task TearDownSubscriptionAsync()
    {
        if (_subscription is null)
        {
            return;
        }

        try
        {
            await _subscription.DeleteAsync(silent: true).ConfigureAwait(false);
            _subscription.Session?.RemoveSubscription(_subscription);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error deleting subscription");
        }

        _subscription = null;
    }

    private void OnNotification(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        if (monitoredItem.Handle is not UaWatchItem item)
        {
            return;
        }

        foreach (var value in monitoredItem.DequeueValues())
        {
            item.Value = UaFormat.FormatValue(value.Value);
            item.StatusCode = UaFormat.FormatStatusCode(value.StatusCode);
            item.IsBad = StatusCode.IsBad(value.StatusCode);
            item.SourceTimestamp = value.SourceTimestamp.ToLocalTime().ToString("HH:mm:ss.fff");
            item.ServerTimestamp = value.ServerTimestamp.ToLocalTime().ToString("HH:mm:ss.fff");
            item.LastUpdateUtc = DateTime.UtcNow;
            item.UpdateCount++;
        }

        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await ClearAsync().ConfigureAwait(false);
        }
        catch
        {
            // Circuit teardown must not throw.
        }
    }
}
