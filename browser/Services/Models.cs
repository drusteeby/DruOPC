namespace UaScope.Services;

using Opc.Ua;

/// <summary>
/// A node in the address-space tree.
/// </summary>
public sealed class UaTreeNode
{
    public required NodeId NodeId { get; init; }

    public required string DisplayName { get; init; }

    public required string BrowseName { get; init; }

    public required NodeClass NodeClass { get; init; }

    public NodeId? TypeDefinition { get; init; }

    public bool HasChildren { get; set; } = true;

    public bool IsExpanded { get; set; }

    public bool IsLoading { get; set; }

    public List<UaTreeNode> Children { get; } = [];

    public UaTreeNode? Parent { get; set; }
}

/// <summary>
/// One row in the attribute panel.
/// </summary>
public sealed record UaAttributeRow(string Name, string Value, string Detail, bool IsBad);

/// <summary>
/// One row in the references panel.
/// </summary>
public sealed record UaReferenceRow(
    string ReferenceType,
    bool IsForward,
    string Target,
    string TargetNodeClass,
    NodeId? TargetNodeId,
    string TypeDefinition);

/// <summary>
/// A discovered endpoint shown in the connect dialog.
/// </summary>
public sealed record UaEndpointInfo(
    string EndpointUrl,
    string SecurityPolicy,
    MessageSecurityMode SecurityMode,
    byte SecurityLevel,
    string[] UserTokenTypes,
    EndpointDescription Description)
{
    public string SecurityPolicyShort => SecurityPolicy[(SecurityPolicy.LastIndexOf('#') + 1)..];

    public bool IsSecure => SecurityMode != MessageSecurityMode.None;
}

/// <summary>
/// A live-updating entry of the watch list (data access view).
/// </summary>
public sealed class UaWatchItem
{
    public required NodeId NodeId { get; init; }

    public required string DisplayName { get; init; }

    public string NodeIdText => NodeId.ToString();

    public string Value { get; set; } = "";

    public string DataType { get; set; } = "";

    public string SourceTimestamp { get; set; } = "";

    public string ServerTimestamp { get; set; } = "";

    public string StatusCode { get; set; } = "";

    public bool IsBad { get; set; }

    public bool IsWritable { get; set; }

    public NodeId? DataTypeId { get; set; }

    public int ValueRank { get; set; } = ValueRanks.Scalar;

    public DateTime LastUpdateUtc { get; set; }

    public long UpdateCount { get; set; }

    /// <summary>
    /// Rolling window of recent numeric values for the sparkline
    /// (null for non-numeric values). Guarded by its own lock.
    /// </summary>
    public Queue<double> History { get; } = new();

    public const int MaxHistory = 60;

    /// <summary>
    /// Take a thread-safe snapshot of the numeric history.
    /// </summary>
    public double[] HistorySnapshot()
    {
        lock (History)
        {
            return History.ToArray();
        }
    }

    public void PushHistory(object? value)
    {
        double? numeric = value switch
        {
            sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
                => Convert.ToDouble(value),
            bool b => b ? 1 : 0,
            _ => null,
        };

        if (numeric is null || double.IsNaN(numeric.Value) || double.IsInfinity(numeric.Value))
        {
            return;
        }

        lock (History)
        {
            History.Enqueue(numeric.Value);
            while (History.Count > MaxHistory)
            {
                History.Dequeue();
            }
        }
    }
}

/// <summary>
/// A received OPC UA event.
/// </summary>
public sealed record UaEventRow(
    DateTime ReceivedUtc,
    string Time,
    string Severity,
    string SourceName,
    string EventType,
    string Message);

/// <summary>
/// Information about the current session for the session panel.
/// </summary>
public sealed record UaSessionInfo(
    string EndpointUrl,
    string SecurityPolicy,
    string SecurityMode,
    string Identity,
    string SessionId,
    string ServerCertificateSubject,
    string ServerCertificateThumbprint,
    string[] Namespaces);

/// <summary>
/// An input or output argument of an OPC UA method.
/// </summary>
public sealed class UaMethodArgument
{
    public required string Name { get; init; }

    public required string DataTypeName { get; init; }

    public required NodeId DataTypeId { get; init; }

    public int ValueRank { get; init; }

    public string Description { get; init; } = "";

    /// <summary>User-entered value (for input arguments) or result display (for output).</summary>
    public string Value { get; set; } = "";
}

/// <summary>
/// Connection lifecycle states.
/// </summary>
public enum UaConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
}
