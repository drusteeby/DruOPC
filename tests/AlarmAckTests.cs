namespace OpcPlc.Tests;

using FluentAssertions;
using NUnit.Framework;
using Opc.Ua;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Tests the alarms &amp; conditions client workflow used by UaScope:
/// condition subscription, ConditionRefresh and Acknowledge.
/// </summary>
[TestFixture]
public class AlarmAckTests : SubscriptionTestsBase
{
    private readonly ConcurrentQueue<(NodeId ConditionId, byte[] EventId, bool Acked, string Source)> _conditions = new();

    public AlarmAckTests() : base(["OpcPlc:Simulation:AddAlarmSimulation=true"])
    {
    }

    [SetUp]
    public async Task CreateMonitoredItem()
    {
        SetUpMonitoredItem(Server, NodeClass.Object, Attributes.EventNotifier);

        var filter = new EventFilter();

        void Select(NodeId typeId, uint attributeId, params string[] path)
        {
            var operand = new SimpleAttributeOperand { TypeDefinitionId = typeId, AttributeId = attributeId };
            foreach (var part in path)
            {
                operand.BrowsePath.Add(new QualifiedName(part));
            }

            filter.SelectClauses.Add(operand);
        }

        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.EventId);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.EventType);
        Select(ObjectTypeIds.BaseEventType, Attributes.Value, BrowseNames.SourceName);
        Select(ObjectTypeIds.ConditionType, Attributes.NodeId);
        Select(ObjectTypeIds.AcknowledgeableConditionType, Attributes.Value, BrowseNames.AckedState, BrowseNames.Id);
        filter.WhereClause.Push(FilterOperator.OfType, ObjectTypeIds.ConditionType);

        MonitoredItem.Filter = filter;
        MonitoredItem.QueueSize = 1000;

        MonitoredItem.Notification += (item, e) =>
        {
            if (e.NotificationValue is not EventFieldList fields || fields.EventFields.Count < 5)
            {
                return;
            }

            var eventType = fields.EventFields[1].Value as NodeId;
            if (eventType == ObjectTypeIds.RefreshStartEventType || eventType == ObjectTypeIds.RefreshEndEventType)
            {
                return;
            }

            if (fields.EventFields[3].Value is NodeId conditionId
                && fields.EventFields[0].Value is byte[] eventId
                && fields.EventFields[4].Value is bool acked)
            {
                _conditions.Enqueue((conditionId, eventId, acked, fields.EventFields[2].Value?.ToString() ?? ""));
            }
        };

        await AddMonitoredItemAsync().ConfigureAwait(false);
    }

    [Test]
    public async Task ConditionRefresh_ReplaysRetainedConditions()
    {
        while (_conditions.TryDequeue(out _))
        {
        }

        await Session.CallAsync(
            ObjectTypeIds.ConditionType,
            MethodIds.ConditionType_ConditionRefresh,
            default,
            SubscriptionId).ConfigureAwait(false);

        var sw = Stopwatch.StartNew();
        while (_conditions.IsEmpty && sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            Thread.Sleep(100);
        }

        _conditions.Should().NotBeEmpty("ConditionRefresh should replay retained conditions");
    }

    [Test]
    public async Task Acknowledge_ReturnsGoodAndSetsAckedState()
    {
        // Wait for a live, unacknowledged condition event.
        (NodeId ConditionId, byte[] EventId, bool Acked, string Source) candidate = default;
        var sw = Stopwatch.StartNew();
        while (candidate.ConditionId is null && sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            while (_conditions.TryDequeue(out var received))
            {
                if (!received.Acked && received.EventId is { Length: > 0 })
                {
                    candidate = received;
                }
            }

            if (candidate.ConditionId is null)
            {
                Thread.Sleep(200);
            }
        }

        candidate.ConditionId.Should().NotBeNull("the alarm simulation should produce unacknowledged conditions");

        // Throws on a bad result; passing means the server accepted the acknowledgement.
        await Session.CallAsync(
            candidate.ConditionId,
            MethodIds.AcknowledgeableConditionType_Acknowledge,
            default,
            candidate.EventId,
            new LocalizedText("Acknowledged by AlarmAckTests")).ConfigureAwait(false);
    }
}
