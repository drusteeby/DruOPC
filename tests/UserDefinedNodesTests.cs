namespace OpcPlc.Tests;

using FluentAssertions;
using NUnit.Framework;
using Opc.Ua;
using System.Threading.Tasks;

/// <summary>
/// Tests the nodes configured via nodesfile.json
/// (demo line hierarchy: DemoLine -> Station10..Station40 -> St*_ nodes).
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
        var lineNode = await FindNodeAsync(ObjectsFolder, OpcPlc.Namespaces.OpcPlcApplications, "OpcPlc", "DemoLine").ConfigureAwait(false);
        lineNode.Should().NotBeNull();

        var station10Node = await FindNodeAsync(lineNode, OpcPlc.Namespaces.OpcPlcApplications, "Station10").ConfigureAwait(false);
        station10Node.Should().NotBeNull();

        var station20Node = await FindNodeAsync(lineNode, OpcPlc.Namespaces.OpcPlcApplications, "Station20").ConfigureAwait(false);
        station20Node.Should().NotBeNull();

        (await FindNodeAsync(station10Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Handshake&.ReadComplete").ConfigureAwait(false))
            .Should().NotBeNull();

        (await FindNodeAsync(station10Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Handshake&.WriteComplete").ConfigureAwait(false))
            .Should().NotBeNull();

        (await FindNodeAsync(station10Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Recipe&.UnitId").ConfigureAwait(false))
            .Should().NotBeNull();

        (await FindNodeAsync(station20Node, OpcPlc.Namespaces.OpcPlcApplications, "St20_Part&.SerialNumber").ConfigureAwait(false))
            .Should().NotBeNull();
    }

    [Test]
    public async Task TestUserDefinedNodesAreWritable()
    {
        var lineNode = await FindNodeAsync(ObjectsFolder, OpcPlc.Namespaces.OpcPlcApplications, "OpcPlc", "DemoLine").ConfigureAwait(false);
        var station10Node = await FindNodeAsync(lineNode, OpcPlc.Namespaces.OpcPlcApplications, "Station10").ConfigureAwait(false);
        var palletNumberNode = await FindNodeAsync(station10Node, OpcPlc.Namespaces.OpcPlcApplications, "St10_Recipe&.PalletNumber").ConfigureAwait(false);

        StatusCode status = await WriteValueAsync(palletNumberNode, 1234).ConfigureAwait(false);
        StatusCode.IsGood(status).Should().BeTrue();

        (await ReadValueAsync<int>(palletNumberNode).ConfigureAwait(false)).Should().Be(1234);
    }
}
