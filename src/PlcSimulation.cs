namespace OpcPlc;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using OpcPlc.Configuration;
using OpcPlc.PluginNodes.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Timers;

public class PlcSimulation
{
    private readonly ILogger _logger;
    private readonly SimulationConfiguration _config;

    private PlcServer _plcServer;
    private ITimer _eventInstanceGenerator;
    private uint _eventInstanceCycle;

    public int SimulationCycleCount { get; set; }
    public int SimulationCycleLength { get; set; }
    public uint EventInstanceCount { get; set; }
    public uint EventInstanceRate { get; set; }

    public bool AddAlarmSimulation { get; set; }
    public bool AddSimpleEventsSimulation { get; set; }
    public bool AddReferenceTestSimulation { get; set; }
    public string DeterministicAlarmSimulationFile { get; set; }

    public ImmutableList<IPluginNodes> PluginNodes { get; }

    public PlcSimulation(IEnumerable<IPluginNodes> pluginNodes, IOptions<OpcPlcConfiguration> options, ILogger<PlcSimulation> logger)
    {
        _logger = logger;
        _config = options.Value.Simulation;

        PluginNodes = pluginNodes.ToImmutableList();

        // Initialize from configuration
        SimulationCycleCount = _config.SimulationCycleCount;
        SimulationCycleLength = _config.SimulationCycleLength;
        EventInstanceCount = _config.EventInstanceCount;
        EventInstanceRate = _config.EventInstanceRate;
        AddAlarmSimulation = _config.AddAlarmSimulation;
        AddSimpleEventsSimulation = _config.AddSimpleEventsSimulation;
        AddReferenceTestSimulation = _config.AddReferenceTestSimulation;
        DeterministicAlarmSimulationFile = _config.DeterministicAlarmSimulationFile;
    }

    /// <summary>
    /// Start the simulation.
    /// </summary>
    public void Start(PlcServer plcServer)
    {
        _logger.LogInformation("Starting simulation with {PluginCount} plugins", PluginNodes.Count);

        _plcServer = plcServer;

        if (EventInstanceCount > 0)
        {
            _eventInstanceGenerator = EventInstanceRate >= 50 || !Stopwatch.IsHighResolution
                ? _plcServer.TimeService.NewTimer(UpdateEventInstances, intervalInMilliseconds: EventInstanceRate)
                : _plcServer.TimeService.NewFastTimer(UpdateVeryFastEventInstances, intervalInMilliseconds: EventInstanceRate);
        }

        foreach (var plugin in PluginNodes)
        {
            plugin.StartSimulation();
        }
    }

    /// <summary>
    /// Stop the simulation.
    /// </summary>
    public void Stop()
    {
        _logger.LogInformation("Stopping simulation");

        if (_eventInstanceGenerator != null)
        {
            _eventInstanceGenerator.Enabled = false;
        }

        foreach (var plugin in PluginNodes)
        {
            plugin.StopSimulation();
        }
    }

    private void UpdateEventInstances(object state, ElapsedEventArgs elapsedEventArgs)
        => UpdateEventInstances();

    private void UpdateVeryFastEventInstances(object state, FastTimerElapsedEventArgs elapsedEventArgs)
        => UpdateEventInstances();

    private void UpdateEventInstances()
    {
        uint eventInstanceCycle = _eventInstanceCycle++;

        for (uint i = 0; i < EventInstanceCount; i++)
        {
            var e = new BaseEventState(null);
            var info = new TranslationInfo(
                "EventInstanceCycleEventKey",
                locale: string.Empty, // Invariant.
                "Event with index '{0}' and event cycle '{1}'",
                i, eventInstanceCycle);

            e.Initialize(
                _plcServer.PlcNodeManager.SystemContext,
                source: null,
                EventSeverity.Medium,
                new LocalizedText(info));

            e.SetChildValue(_plcServer.PlcNodeManager.SystemContext, BrowseNames.SourceName, "System", false);
            e.SetChildValue(_plcServer.PlcNodeManager.SystemContext, BrowseNames.SourceNode, ObjectIds.Server, false);

            _plcServer.PlcNodeManager.Server.ReportEvent(e);
        }
    }
}
