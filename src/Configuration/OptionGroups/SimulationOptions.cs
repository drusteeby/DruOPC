namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using System;

/// <summary>
/// Options for simulation configuration.
/// </summary>
public class SimulationOptions : IOptionGroup
{
    private readonly PlcSimulation _plcSimulation;

    public SimulationOptions(PlcSimulation plcSimulation)
    {
        _plcSimulation = plcSimulation ?? throw new ArgumentNullException(nameof(plcSimulation));
    }

    public void RegisterOptions(OptionSet options)
    {
        options.Add(
            "sc|simulationcyclecount=",
            $"count of cycles in one simulation phase.\nDefault: {_plcSimulation.SimulationCycleCount} cycles",
            (int i) => _plcSimulation.SimulationCycleCount = i);

        options.Add(
            "ct|cycletime=",
            $"length of one cycle time in milliseconds.\nDefault: {_plcSimulation.SimulationCycleLength} msec",
            (int i) => _plcSimulation.SimulationCycleLength = i);

        options.Add(
            "ei|eventinstances=",
            $"number of event instances.\nDefault: {_plcSimulation.EventInstanceCount}",
            (uint i) => _plcSimulation.EventInstanceCount = i);

        options.Add(
            "er|eventrate=",
            $"rate in milliseconds to send events.\nDefault: {_plcSimulation.EventInstanceRate}",
            (uint i) => _plcSimulation.EventInstanceRate = i);

        options.Add(
            "alm|alarms",
            $"add alarm simulation to address space.\nDefault: {_plcSimulation.AddAlarmSimulation}",
            (s) => _plcSimulation.AddAlarmSimulation = s != null);

        options.Add(
            "ses|simpleevents",
            $"add simple events simulation to address space.\nDefault: {_plcSimulation.AddSimpleEventsSimulation}",
            (s) => _plcSimulation.AddSimpleEventsSimulation = s != null);

        options.Add(
            "dalm|deterministicalarms=",
            "add deterministic alarm simulation to address space.\nProvide a script file for controlling deterministic alarms.",
            (s) => _plcSimulation.DeterministicAlarmSimulationFile = s);
    }
}
