namespace OpcPlc;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using OpcPlc.Configuration;
using OpcPlc.PluginNodes.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public class PlcSimulation
{
    private readonly ILogger _logger;
    private readonly SimulationConfiguration _config;

    public int SimulationCycleCount { get; set; }
    public int SimulationCycleLength { get; set; }
    public uint EventInstanceCount { get; set; }
    public uint EventInstanceRate { get; set; }

    public bool AddAlarmSimulation { get; set; }
    public bool AddSimpleEventsSimulation { get; set; }
    public bool AddReferenceTestSimulation { get; set; } = true;
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
        DeterministicAlarmSimulationFile = _config.DeterministicAlarmSimulationFile;
    }

    /// <summary>
    /// Start the simulation.
    /// </summary>
    public void Start(PlcServer plcServer)
    {
        _logger.LogInformation("Starting simulation with {PluginCount} plugins", PluginNodes.Count);
        
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
        
        foreach (var plugin in PluginNodes)
        {
            plugin.StopSimulation();
        }
    }
}
