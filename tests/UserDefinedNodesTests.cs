namespace OpcPlc.Tests;

using FluentAssertions;
using NUnit.Framework;
using Opc.Ua;
using System.Threading.Tasks;

/// <summary>
/// Tests the nodes configured via nodesfile.json
/// (station-data hierarchy: DemoLine-> Station10/Station20 -> S2_*/S3_* nodes).
/// </summary>
[TestFixture]
public class UserDefinedNodesTests : SubscriptionTestsBase
{
    // Set any config overrides needed for the plc server explicitly
    public UserDefinedNodesTests() : base(["OpcPlc:NodesFile=nodesfile.json"])
    {
    }

    [Test]
    public async Task TestUserDefinedNodes()
    {
        var aceNode = await FindNodeAsync(ObjectsFolder, OpcPlc.Namespaces.OpcPlcApplications, "OpcPlc", "DemoLine").ConfigureAwait(false);
        aceNode.Should().NotBeNull();

        var op45Node = await FindNodeAsync(aceNode, OpcPlc.Namespaces.OpcPlcApplications, "Station10").ConfigureAwait(false);
        op45Node.Should().NotBeNull();

        var op50Node = await FindNodeAsync(aceNode, OpcPlc.Namespaces.OpcPlcApplications, "Station20").ConfigureAwait(false);
        op50Node.Should().NotBeNull();

        (await FindNodeAsync(op45Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Data&.status&.ReadComplete").ConfigureAwait(false))
            .Should().NotBeNull();

        (await FindNodeAsync(op45Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Data&.status&.WriteComplete").ConfigureAwait(false))
            .Should().NotBeNull();

        (await FindNodeAsync(op45Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Data&.Header&.UnitId&.Data").ConfigureAwait(false))
            .Should().NotBeNull();

        (await FindNodeAsync(op50Node, OpcPlc.Namespaces.OpcPlcApplications, "St20_Data&.status&.ReadComplete").ConfigureAwait(false))
            .Should().NotBeNull();
    }

    [Test]
    public async Task TestUserDefinedNodesAreWritable()
    {
        var aceNode = await FindNodeAsync(ObjectsFolder, OpcPlc.Namespaces.OpcPlcApplications, "OpcPlc", "DemoLine").ConfigureAwait(false);
        var op45Node = await FindNodeAsync(aceNode, OpcPlc.Namespaces.OpcPlcApplications, "Station10").ConfigureAwait(false);
        var palletNumberNode = await FindNodeAsync(op45Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Data&.Header&.PalletNumber").ConfigureAwait(false);

        StatusCode status = await WriteValueAsync(palletNumberNode, 1234).ConfigureAwait(false);
        StatusCode.IsGood(status).Should().BeTrue();

        (await ReadValueAsync<int>(palletNumberNode).ConfigureAwait(false)).Should().Be(1234);
    }
}
