namespace OpcPlc.PluginNodes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using OpcPlc.Configuration;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.Timers;

/// <summary>
/// Nodes with slow changing values.
/// </summary>
public class SlowPluginNodes : PluginNodeBase, IPluginNodes
{
    private readonly SlowNodesConfiguration _config;

    private uint NodeCount => _config.NodeCount;
    private uint NodeRate => _config.NodeRate * 1000; // Convert seconds to ms
    private NodeType NodeType { get; set; }
    private string NodeMinValue => _config.NodeMinValue;
    private string NodeMaxValue => _config.NodeMaxValue;
    private bool NodeRandomization => _config.NodeRandomization;
    private string NodeStepSize => _config.NodeStepSize;
    private uint NodeSamplingInterval => _config.NodeSamplingInterval;

    private PlcNodeManager _plcNodeManager;
    private SlowFastCommon _slowFastCommon;
    protected BaseDataVariableState[] _nodes;
    protected BaseDataVariableState[] _badNodes;
    private ITimer _nodeGenerator;
    private bool _updateNodes = true;

    public SlowPluginNodes(TimeService timeService, ILogger<SlowPluginNodes> logger, IOptions<OpcPlcConfiguration> options)
        : base(timeService, logger)
    {
        _config = options.Value.SlowNodes;
        NodeType = SlowFastCommon.ParseNodeType(_config.NodeType);
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;
        _slowFastCommon = new SlowFastCommon(_plcNodeManager, _timeService, _logger);

        FolderState folder = _plcNodeManager.CreateFolder(
            telemetryFolder,
            path: "Slow",
            name: "Slow",
            NamespaceType.OpcPlcApplications);

        // Used for methods to limit the number of updates to a fixed count.
        FolderState simulatorFolder = _plcNodeManager.CreateFolder(
            telemetryFolder.Parent, // Root.
            path: "SimulatorConfiguration",
            name: "SimulatorConfiguration",
            NamespaceType.OpcPlcApplications);

        AddNodes(folder, simulatorFolder);
        AddMethods(methodsFolder);
    }

    private void AddMethods(FolderState methodsFolder)
    {
        MethodState stopUpdateMethod = _plcNodeManager.CreateMethod(
            methodsFolder,
            path: "StopUpdateSlowNodes",
            name: "StopUpdateSlowNodes",
            "Stop the increase of value of slow nodes",
            NamespaceType.OpcPlcApplications);

        SetStopUpdateSlowNodesProperties(ref stopUpdateMethod);

        MethodState startUpdateMethod = _plcNodeManager.CreateMethod(
            methodsFolder,
            path: "StartUpdateSlowNodes",
            name: "StartUpdateSlowNodes",
            "Start the increase of value of slow nodes",
            NamespaceType.OpcPlcApplications);

        SetStartUpdateSlowNodesProperties(ref startUpdateMethod);
    }

    public void StartSimulation()
    {
        _nodeGenerator = _timeService.NewTimer(UpdateNodes, intervalInMilliseconds: NodeRate);
    }

    public void StopSimulation()
    {
        if (_nodeGenerator != null)
        {
            _nodeGenerator.Enabled = false;
        }
    }

    private void AddNodes(FolderState folder, FolderState simulatorFolder)
    {
        (_nodes, _badNodes) = _slowFastCommon.CreateNodes(NodeType, "Slow", NodeCount, folder, simulatorFolder, NodeRandomization, NodeStepSize, NodeMinValue, NodeMaxValue, NodeRate);

        ExposeNodesWithIntervals();
    }

    /// <summary>
    /// Expose node information for dumping pn.json.
    /// </summary>
    private void ExposeNodesWithIntervals()
    {
        var nodes = new List<NodeWithIntervals>();

        foreach (var node in _nodes)
        {
            nodes.Add(new NodeWithIntervals
            {
                NodeId = node.NodeId.Identifier.ToString(),
                Namespace = OpcPlc.Namespaces.OpcPlcApplications,
                PublishingInterval = NodeRate,
                SamplingInterval = NodeSamplingInterval,
            });
        }

        foreach (var node in _badNodes)
        {
            nodes.Add(new NodeWithIntervals
            {
                NodeId = node.NodeId.Identifier.ToString(),
                Namespace = OpcPlc.Namespaces.OpcPlcApplications,
                PublishingInterval = NodeRate,
                SamplingInterval = NodeSamplingInterval,
            });
        }

        Nodes = nodes;
    }

    private void SetStopUpdateSlowNodesProperties(ref MethodState method)
    {
        method.OnCallMethod += OnStopUpdateSlowNodes;
    }

    private void SetStartUpdateSlowNodesProperties(ref MethodState method)
    {
        method.OnCallMethod += OnStartUpdateSlowNodes;
    }

    /// <summary>
    /// Method to stop updating the slow nodes.
    /// </summary>
    private ServiceResult OnStopUpdateSlowNodes(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
    {
        _updateNodes = false;
        _logger.LogDebug("StopUpdateSlowNodes method called");
        return ServiceResult.Good;
    }

    /// <summary>
    /// Method to start updating the slow nodes.
    /// </summary>
    private ServiceResult OnStartUpdateSlowNodes(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
    {
        _updateNodes = true;
        _logger.LogDebug("StartUpdateSlowNodes method called");
        return ServiceResult.Good;
    }

    private void UpdateNodes(object state, ElapsedEventArgs elapsedEventArgs)
    {
        _slowFastCommon.UpdateNodes(_nodes, _badNodes, NodeType, _updateNodes);
    }
}
