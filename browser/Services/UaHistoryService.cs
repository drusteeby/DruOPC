namespace UaScope.Services;

using Opc.Ua;
using Opc.Ua.Client;

/// <summary>
/// One sampled value from a node's history.
/// </summary>
public sealed record UaHistoryPoint(DateTime SourceTimestamp, object? Value, string ValueText, string StatusText, bool IsBad);

/// <summary>
/// Reads raw history of variable nodes.
/// </summary>
public sealed class UaHistoryService
{
    private readonly UaConnection _connection;

    public UaHistoryService(UaConnection connection)
    {
        _connection = connection;
    }

    private Session Session => _connection.Session
        ?? throw new InvalidOperationException("Not connected to a server.");

    /// <summary>
    /// Read raw history for a node in the given time range
    /// (continuation points followed up to <paramref name="maxPoints"/>).
    /// </summary>
    public async Task<List<UaHistoryPoint>> ReadRawAsync(
        NodeId nodeId,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        int maxPoints = 10_000,
        CancellationToken ct = default)
    {
        var points = new List<UaHistoryPoint>();

        var details = new ReadRawModifiedDetails
        {
            StartTime = startTimeUtc,
            EndTime = endTimeUtc,
            NumValuesPerNode = (uint)Math.Min(maxPoints, 2_000),
            IsReadModified = false,
            ReturnBounds = false,
        };

        var nodeToRead = new HistoryReadValueId { NodeId = nodeId };

        while (points.Count < maxPoints)
        {
            ct.ThrowIfCancellationRequested();

            var response = await Session.HistoryReadAsync(
                requestHeader: null,
                new ExtensionObject(details),
                TimestampsToReturn.Both,
                releaseContinuationPoints: false,
                new HistoryReadValueIdCollection { nodeToRead },
                ct).ConfigureAwait(false);

            var result = response.Results[0];
            if (StatusCode.IsBad(result.StatusCode))
            {
                throw new ServiceResultException(result.StatusCode);
            }

            if (ExtensionObject.ToEncodeable(result.HistoryData) is HistoryData historyData)
            {
                foreach (var dataValue in historyData.DataValues)
                {
                    points.Add(new UaHistoryPoint(
                        dataValue.SourceTimestamp,
                        dataValue.Value,
                        UaFormat.FormatValue(dataValue.Value),
                        UaFormat.FormatStatusCode(dataValue.StatusCode),
                        StatusCode.IsBad(dataValue.StatusCode)));
                }
            }

            if (result.ContinuationPoint is not { Length: > 0 })
            {
                break;
            }

            if (points.Count >= maxPoints)
            {
                // Release the outstanding continuation point.
                nodeToRead.ContinuationPoint = result.ContinuationPoint;
                try
                {
                    await Session.HistoryReadAsync(
                        null,
                        new ExtensionObject(details),
                        TimestampsToReturn.Both,
                        releaseContinuationPoints: true,
                        new HistoryReadValueIdCollection { nodeToRead },
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Best effort only.
                }

                break;
            }

            nodeToRead.ContinuationPoint = result.ContinuationPoint;
        }

        return points;
    }
}
