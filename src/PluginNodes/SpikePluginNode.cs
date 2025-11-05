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
/// Node with a sine wave value with a spike anomaly.
/// </summary>
public class SpikePluginNode : PluginNodeBase, IPluginNodes
{
    private readonly bool _isEnabled;
    private PlcNodeManager _plcNodeManager;
    private SimulatedVariableNode<double> _node;
    private readonly Random _random = new();
    private int _spikeCycleInPhase;
    private int _spikeAnomalyCycle;
    private const double SimulationMaxAmplitude = 100.0;

    public SpikePluginNode(TimeService timeService, ILogger<SpikePluginNode> logger, IOptions<OpcPlcConfiguration> options)
        : base(timeService, logger)
    {
        _isEnabled = !options.Value.DataGeneration.NoSpikes;
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;

        if (_isEnabled)
        {
            FolderState folder = _plcNodeManager.CreateFolder(
                telemetryFolder,
                path: "Anomaly",
                name: "Anomaly",
                NamespaceType.OpcPlcApplications);

            AddNodes(folder);
        }
    }

    public void StartSimulation()
    {
        if (_isEnabled)
        {
            _spikeCycleInPhase = _plcNodeManager.PlcSimulationInstance.SimulationCycleCount;
            _spikeAnomalyCycle = _random.Next(_plcNodeManager.PlcSimulationInstance.SimulationCycleCount);
            _logger.LogTrace($"First spike anomaly cycle: {_spikeAnomalyCycle}");

            _node.Start(SpikeGenerator, _plcNodeManager.PlcSimulationInstance.SimulationCycleLength);
        }
    }

    public void StopSimulation()
    {
        if (_isEnabled)
        {
            _node.Stop();
        }
    }

    private void AddNodes(FolderState folder)
    {
        BaseDataVariableState variable = _plcNodeManager.CreateBaseVariable(
            folder,
            path: "SpikeData",
            name: "SpikeData",
            new NodeId((uint)BuiltInType.Double),
            ValueRanks.Scalar,
            AccessLevels.CurrentRead,
            "Value with random spikes",
            NamespaceType.OpcPlcApplications);

        _node = _plcNodeManager.CreateVariableNode<double>(variable);

        // Add to node list for creation of pn.json.
        Nodes = new List<NodeWithIntervals>
        {
            PluginNodesHelper.GetNodeWithIntervals(variable.NodeId, _plcNodeManager),
        };
    }

    /// <summary>
    /// Generates a sine wave with spikes at a random cycle in the phase.
    /// Called each SimulationCycleLength msec.
    /// </summary>
    private double SpikeGenerator(double value)
    {
        // calculate next value
        double nextValue;
        if (_isEnabled && _spikeCycleInPhase == _spikeAnomalyCycle)
        {
            // TODO: calculate
            nextValue = SimulationMaxAmplitude * 10;
            _logger.LogTrace("Generate spike anomaly");
        }
        else
        {
            nextValue = SimulationMaxAmplitude * Math.Sin(((2 * Math.PI) / _plcNodeManager.PlcSimulationInstance.SimulationCycleCount) * _spikeCycleInPhase);
        }
        _logger.LogTrace($"Spike cycle: {_spikeCycleInPhase} data: {nextValue}");

        // end of cycle: reset cycle count and calc next anomaly cycle
        if (--_spikeCycleInPhase == 0)
        {
            _spikeCycleInPhase = _plcNodeManager.PlcSimulationInstance.SimulationCycleCount;
            _spikeAnomalyCycle = _random.Next(_plcNodeManager.PlcSimulationInstance.SimulationCycleCount);
            _logger.LogTrace($"Next spike anomaly cycle: {_spikeAnomalyCycle}");
        }

        return nextValue;
    }
}
