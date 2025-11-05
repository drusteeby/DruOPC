namespace OpcPlc.PluginNodes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using OpcPlc.Configuration;
using OpcPlc.Helpers;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;

/// <summary>
/// Nodes with deterministic GUIDs as ID.
/// </summary>
public class DeterministicGuidPluginNodes : PluginNodeBase, IPluginNodes
{
    private readonly DeterministicGuid _deterministicGuid = new ();
    private readonly uint _nodeCount;
    private PlcNodeManager _plcNodeManager;
    private SimulatedVariableNode<uint>[] _nodes;

    private uint NodeRate { get; set; } = 1000; // ms.
    private NodeType NodeType { get; set; } = NodeType.UInt;

    public DeterministicGuidPluginNodes(TimeService timeService, ILogger<DeterministicGuidPluginNodes> logger, IOptions<OpcPlcConfiguration> options)
        : base(timeService, logger)
    {
        _nodeCount = options.Value.GuidNodes.NodeCount;
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;

        FolderState folder = _plcNodeManager.CreateFolder(
            telemetryFolder,
            path: "Deterministic GUID",
            name: "Deterministic GUID",
            NamespaceType.OpcPlcApplications);

        AddNodes(folder);
    }

    public void StartSimulation()
    {
        foreach (var node in _nodes)
        {
            node.Start(value => value + 1, periodMs: 1000);
        }
    }

    public void StopSimulation()
    {
        foreach (var node in _nodes)
        {
            node.Stop();
        }
    }

    private void AddNodes(FolderState folder)
    {
        _nodes = new SimulatedVariableNode<uint>[_nodeCount];
        var nodes = new List<NodeWithIntervals>((int)_nodeCount);

        if (_nodeCount > 0)
        {
            _logger.LogInformation($"Creating {_nodeCount} GUID node(s) of type: {NodeType}");
            _logger.LogInformation($"Node values will change every {NodeRate} ms");
        }

        for (int i = 0; i < _nodeCount; i++)
        {
            Guid id = _deterministicGuid.NewGuid();

            BaseDataVariableState variable = _plcNodeManager.CreateBaseVariable(
                folder,
                path: id,
                name: id.ToString(),
                new NodeId((uint)BuiltInType.UInt32),
                ValueRanks.Scalar,
                AccessLevels.CurrentReadOrWrite,
                "Constantly increasing value",
                NamespaceType.OpcPlcApplications,
                defaultValue: (uint)0);

            _nodes[i] = _plcNodeManager.CreateVariableNode<uint>(variable);

            // Add to node list for creation of pn.json.
            nodes.Add(PluginNodesHelper.GetNodeWithIntervals(variable.NodeId, _plcNodeManager));
        }

        Nodes = nodes;
    }
}
