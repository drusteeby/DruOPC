namespace OpcPlc.PluginNodes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.DI;
using OpcPlc.Configuration;
using OpcPlc.Helpers;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Timers;

/// <summary>
/// Boiler that inherits from DI companion spec.
/// </summary>
public class Boiler2PluginNodes : PluginNodeBase, IPluginNodes
{
    private readonly Boiler2Configuration _config;

    private PlcNodeManager _plcNodeManager;
    private BaseDataVariableState _tempSpeedDegreesPerSecNode;
    private BaseDataVariableState _baseTempDegreesNode;
    private BaseDataVariableState _targetTempDegreesNode;
    private BaseDataVariableState _overheatThresholdDegreesNode;
    private BaseDataVariableState _maintenanceIntervalInSecondsNode;
    private BaseDataVariableState _overheatIntervalInSecondsNode;
    private BaseDataVariableState _currentTempDegreesNode;
    private BaseDataVariableState _overheatedNode;
    private BaseDataVariableState _heaterStateNode;
    private BaseDataVariableState _deviceHealth;
    private DeviceHealthDiagnosticAlarmTypeState _failureEv;
    private DeviceHealthDiagnosticAlarmTypeState _checkFunctionEv;
    private DeviceHealthDiagnosticAlarmTypeState _offSpecEv;
    private DeviceHealthDiagnosticAlarmTypeState _maintenanceRequiredEv;
    private OpcPlc.ITimer _nodeGenerator;
    private OpcPlc.ITimer _maintenanceGenerator;
    private OpcPlc.ITimer _overheatGenerator;
    private BaseObjectState _boiler2Object;

    private float _tempSpeedDegreesPerSec => _config.TemperatureSpeed;
    private float _baseTempDegrees => _config.BaseTemperature;
    private float _targetTempDegrees => _config.TargetTemperature;
    private TimeSpan _maintenanceInterval => TimeSpan.FromSeconds(_config.MaintenanceInterval);
    private TimeSpan _overheatInterval => TimeSpan.FromSeconds(_config.OverheatInterval);

    private bool _isOverheated;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Boiler2PluginNodes(TimeService timeService, ILogger<Boiler2PluginNodes> logger, IOptions<OpcPlcConfiguration> options)
        : base(timeService, logger)
    {
        _config = options.Value.Boiler2;
    }

    public void AddToAddressSpace(FolderState telemetryFolder, FolderState methodsFolder, PlcNodeManager plcNodeManager)
    {
        _plcNodeManager = plcNodeManager;

        AddNodes();

        // Register Boiler #2 as a root notifier so it can be independently subscribed for events.
        if (_boiler2Object != null)
        {
            _plcNodeManager.RegisterRootNotifier(_boiler2Object);
        }
    }

    public void StartSimulation()
    {
        _nodeGenerator = _timeService.NewTimer(UpdateBoiler2, intervalInMilliseconds: 1000);
        StartTimers();
    }

    public void StopSimulation()
    {
        if (_nodeGenerator is not null)
        {
            _nodeGenerator.Enabled = false;
        }

        if (_maintenanceGenerator is not null)
        {
            _maintenanceGenerator.Enabled = false;
        }

        if (_overheatGenerator is not null)
        {
            _overheatGenerator.Enabled = false;
        }
    }

    private void AddNodes()
    {
        // Load complex types from binary uanodes file.
        _plcNodeManager.LoadPredefinedNodes(LoadPredefinedNodes);

        // Locate Boiler #2 object itself.
        _boiler2Object = (BaseObjectState)_plcNodeManager.FindPredefinedNode(new NodeId(5017, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseObjectState));
        if (_boiler2Object != null)
        {
            _boiler2Object.EventNotifier = EventNotifiers.SubscribeToEvents;
            // Set the event handler that routes events through the subscription system.
            _boiler2Object.OnReportEvent = OnReportBoilerEvent;
        }

        // Find the Boiler2 configuration nodes.
        _tempSpeedDegreesPerSecNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_TemperatureChangeSpeed, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _baseTempDegreesNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_BaseTemperature, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _targetTempDegreesNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_TargetTemperature, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _maintenanceIntervalInSecondsNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_MaintenanceInterval, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _overheatIntervalInSecondsNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_OverheatInterval, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _overheatThresholdDegreesNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_OverheatedThresholdTemperature, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));

        AllowReadAndWrite(_overheatIntervalInSecondsNode);
        AllowReadAndWrite(_overheatThresholdDegreesNode);
        AllowReadAndWrite(_maintenanceIntervalInSecondsNode);

        SetValue(_tempSpeedDegreesPerSecNode, _tempSpeedDegreesPerSec);
        SetValue(_baseTempDegreesNode, _baseTempDegrees);
        SetValue(_targetTempDegreesNode, _targetTempDegrees);
        SetValue(_maintenanceIntervalInSecondsNode, (uint)_maintenanceInterval.TotalSeconds);
        SetValue(_overheatIntervalInSecondsNode, (uint)_overheatInterval.TotalSeconds);
        SetValue(_overheatThresholdDegreesNode, _targetTempDegrees + 10.0f);

        _maintenanceIntervalInSecondsNode.OnSimpleWriteValue = OnWriteMaintenanceIntervalInSeconds;
        _overheatIntervalInSecondsNode.OnSimpleWriteValue = OnWriteOverheatIntervalInSeconds;

        // Find the Boiler2 data nodes.
        _currentTempDegreesNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_CurrentTemperature, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _overheatedNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_Overheated, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        _heaterStateNode = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_ParameterSet_HeaterState, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));

        SetValue(_currentTempDegreesNode, _baseTempDegrees);
        SetValue(_overheatedNode, false);
        SetValue(_heaterStateNode, true);

        // Find the Boiler2 deviceHealth nodes.
        _deviceHealth = (BaseDataVariableState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Variables.Boilers_Boiler__2_DeviceHealth, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(BaseDataVariableState));
        SetValue(_deviceHealth, DeviceHealthEnumeration.NORMAL);

        AddMethods();
        InitEvents();

        // Add to node list for creation of pn.json.
        Nodes = new List<NodeWithIntervals>
        {
            PluginNodesHelper.GetNodeWithIntervals(_currentTempDegreesNode.NodeId, _plcNodeManager),
        };
    }

    /// <summary>
    /// Loads a node set from a file or resource and adds them to the set of predefined nodes.
    /// </summary>
    private static NodeStateCollection LoadPredefinedNodes(ISystemContext context)
    {
        var uanodesPath = "Boilers/Boiler2/BoilerModel2.PredefinedNodes.uanodes";
        var snapLocation = Environment.GetEnvironmentVariable("SNAP");
        if (!string.IsNullOrWhiteSpace(snapLocation))
        {
            // Application running as a snap
            uanodesPath = Path.Join(snapLocation, uanodesPath);
        }

        var predefinedNodes = new NodeStateCollection();

        predefinedNodes.LoadFromBinaryResource(context,
            uanodesPath, // CopyToOutputDirectory -> PreserveNewest.
            typeof(PlcNodeManager).GetTypeInfo().Assembly,
            updateTables: true);

        return predefinedNodes;
    }

    private void SetValue<T>(BaseVariableState variable, T value)
    {
        variable.Value = value;
        variable.Timestamp = _timeService.Now();
        variable.ClearChangeMasks(_plcNodeManager.SystemContext, includeChildren: false);
    }

    private void AllowReadAndWrite(BaseVariableState variable)
    {
        variable.Timestamp = _timeService.Now();
        variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
        variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
        variable.ClearChangeMasks(_plcNodeManager.SystemContext, includeChildren: false);
    }

    private ServiceResult OnWriteMaintenanceIntervalInSeconds(ISystemContext context, NodeState node, ref object value)
    {
        var newInterval = TimeSpan.FromSeconds((uint)value);
        _maintenanceGenerator?.Dispose();
        _maintenanceGenerator = _timeService.NewTimer(UpdateMaintenance, intervalInMilliseconds: (uint)newInterval.TotalMilliseconds);
        return ServiceResult.Good;
    }

    private ServiceResult OnWriteOverheatIntervalInSeconds(ISystemContext context, NodeState node, ref object value)
    {
        var newInterval = TimeSpan.FromSeconds((uint)value);
        _overheatGenerator?.Dispose();
        _overheatGenerator = _timeService.NewTimer(UpdateOverheat, intervalInMilliseconds: (uint)newInterval.TotalMilliseconds);
        return ServiceResult.Good;
    }

    public void UpdateBoiler2(object state, ElapsedEventArgs elapsedEventArgs)
    {
        _lock.Wait();

        float currentTemperatureDegrees = (float)_currentTempDegreesNode.Value;
        float newTemperature;
        float tempSpeedDegreesPerSec = (float)_tempSpeedDegreesPerSecNode.Value;
        float baseTempDegrees = (float)_baseTempDegreesNode.Value;
        float targetTempDegrees = (float)_targetTempDegreesNode.Value;
        float overheatThresholdDegrees = (float)_overheatThresholdDegreesNode.Value;

        if ((bool)_heaterStateNode.Value)
        {
            // Heater on, increase by specified speed, but the step should not be bigger than targetTemp.
            newTemperature = currentTemperatureDegrees + Math.Min(tempSpeedDegreesPerSec, Math.Abs(targetTempDegrees - currentTemperatureDegrees));

            // Target temp reached, turn off heater.
            if (newTemperature >= targetTempDegrees)
            {
                SetValue(_heaterStateNode, false);
            }
        }
        else
        {
            // Heater off, decrease by specified speed, but the step should not be bigger than baseTemp.
            newTemperature = currentTemperatureDegrees - Math.Min(tempSpeedDegreesPerSec, Math.Abs(currentTemperatureDegrees - baseTempDegrees));

            // Base temp reached, turn on heater.
            if (newTemperature <= baseTempDegrees)
            {
                SetValue(_heaterStateNode, true);
            }
        }

        // Change other values.
        SetValue(_currentTempDegreesNode, newTemperature);
        SetValue(_overheatedNode, newTemperature > overheatThresholdDegrees);

        // Update DeviceHealth status.
        SetDeviceHealth(newTemperature, baseTempDegrees, targetTempDegrees, overheatThresholdDegrees);

        EmitEvents();

        _lock.Release();
    }

    private void AddMethods()
    {
        MethodState switchMethodNode = (MethodState)_plcNodeManager.FindPredefinedNode(new NodeId(BoilerModel2.Methods.Boilers_Boiler__2_MethodSet_Switch, _plcNodeManager.NamespaceIndexes[(int)NamespaceType.Boiler]), typeof(MethodState));

        switchMethodNode.OnCallMethod += SwitchOnCall;
    }

    /// <summary>
    /// Set the heater on/off. Executes synchronously.
    /// </summary>
    private ServiceResult SwitchOnCall(ISystemContext context, MethodState method, IList<object> inputArguments, IList<object> outputArguments)
    {
        SetValue(_heaterStateNode, inputArguments.First());
        _logger.LogDebug($"SwitchOnCall method called with argument: {inputArguments.First()}");

        return ServiceResult.Good;
    }

    private void InitEvents()
    {
        // Construct the events.
        _failureEv = new DeviceHealthDiagnosticAlarmTypeState(parent: null);
        _checkFunctionEv = new DeviceHealthDiagnosticAlarmTypeState(parent: null);
        _offSpecEv = new DeviceHealthDiagnosticAlarmTypeState(parent: null);
        _maintenanceRequiredEv = new DeviceHealthDiagnosticAlarmTypeState(parent: null);

        // Use the boiler object as the event source so subscriptions on the boiler NodeId receive the events.
        NodeState sourceNodeForEvents = _boiler2Object != null
            ? (NodeState)_boiler2Object
            : _currentTempDegreesNode;

        // Init the events.
        _failureEv.Initialize(_plcNodeManager.SystemContext,
            source: sourceNodeForEvents,
            EventSeverity.Max,
            new LocalizedText($"Temperature is above or equal to the overheat threshold!"));

        _checkFunctionEv.Initialize(_plcNodeManager.SystemContext,
            source: sourceNodeForEvents,
            EventSeverity.Low,
            new LocalizedText($"Temperature is above target!"));

        _offSpecEv.Initialize(_plcNodeManager.SystemContext,
            source: sourceNodeForEvents,
            EventSeverity.MediumLow,
            new LocalizedText($"Temperature is off spec!"));

        _maintenanceRequiredEv.Initialize(_plcNodeManager.SystemContext,
                source: sourceNodeForEvents,
                EventSeverity.Medium,
                new LocalizedText($"Maintenance required!"));

        _maintenanceRequiredEv.SetChildValue(_plcNodeManager.SystemContext, Opc.Ua.BrowseNames.SourceName, value: "Maintenance", copy: false);
    }

    private void SetDeviceHealth(float currentTemp, float baseTemp, float targetTemp, float overheatedTemp)
    {
        DeviceHealthEnumeration deviceHealth = currentTemp switch {
            _ when currentTemp >= baseTemp && currentTemp <= targetTemp => DeviceHealthEnumeration.NORMAL,
            _ when currentTemp > targetTemp && currentTemp < overheatedTemp => DeviceHealthEnumeration.CHECK_FUNCTION,
            _ when currentTemp >= overheatedTemp => DeviceHealthEnumeration.FAILURE,
            _ when currentTemp < baseTemp || currentTemp > overheatedTemp + 5 => DeviceHealthEnumeration.OFF_SPEC,
            _ => throw new ArgumentOutOfRangeException(nameof(currentTemp))
        };

        SetValue(_deviceHealth, deviceHealth);
    }

    private void StartTimers()
    {
        _maintenanceGenerator = _timeService.NewTimer(UpdateMaintenance, intervalInMilliseconds: (uint)_maintenanceInterval.TotalMilliseconds);
        _overheatGenerator = _timeService.NewTimer(UpdateOverheat, intervalInMilliseconds: (uint)_overheatInterval.TotalMilliseconds);
    }

    private void UpdateMaintenance(object state, ElapsedEventArgs elapsedEventArgs)
    {
        _lock.Wait();

        SetValue(_deviceHealth, DeviceHealthEnumeration.MAINTENANCE_REQUIRED);

        _maintenanceRequiredEv.SetChildValue(_plcNodeManager.SystemContext, Opc.Ua.BrowseNames.Time, value: DateTime.Now, copy: false);

        // Report event through the boiler object.
        _boiler2Object?.ReportEvent(_plcNodeManager.SystemContext, _maintenanceRequiredEv);

        _lock.Release();
    }

    private void UpdateOverheat(object state, ElapsedEventArgs elapsedEventArgs)
    {
        _lock.Wait();

        SetValue(_currentTempDegreesNode, (float)_overheatThresholdDegreesNode.Value + 10.0f);
        SetValue(_heaterStateNode, false);
        SetValue(_deviceHealth, DeviceHealthEnumeration.OFF_SPEC);

        _offSpecEv.SetChildValue(_plcNodeManager.SystemContext, Opc.Ua.BrowseNames.Time, value: DateTime.Now, copy: false);

        // Report event through the boiler object.
        _boiler2Object?.ReportEvent(_plcNodeManager.SystemContext, _offSpecEv);

        _isOverheated = true;

        _lock.Release();
    }

    private void EmitEvents()
    {
        if (_isOverheated)
        {
            switch ((DeviceHealthEnumeration)_deviceHealth.Value)
            {
                case DeviceHealthEnumeration.NORMAL:
                    _isOverheated = false;
                    break;
                case DeviceHealthEnumeration.CHECK_FUNCTION:
                    _checkFunctionEv.SetChildValue(_plcNodeManager.SystemContext, Opc.Ua.BrowseNames.Time, value: DateTime.Now, copy: false);

                    // Report event through the boiler object.
                    _boiler2Object?.ReportEvent(_plcNodeManager.SystemContext, _checkFunctionEv);
                    break;
                case DeviceHealthEnumeration.FAILURE:
                    _failureEv.SetChildValue(_plcNodeManager.SystemContext, Opc.Ua.BrowseNames.Time, value: DateTime.Now, copy: false);

                    // Report event through the boiler object.
                    _boiler2Object?.ReportEvent(_plcNodeManager.SystemContext, _failureEv);
                    break;
            }
        }

        if ((DeviceHealthEnumeration)_deviceHealth.Value == DeviceHealthEnumeration.OFF_SPEC)
        {
            _offSpecEv.SetChildValue(_plcNodeManager.SystemContext, Opc.Ua.BrowseNames.Time, value: DateTime.Now, copy: false);

            // Report event through the boiler object.
            _boiler2Object?.ReportEvent(_plcNodeManager.SystemContext, _offSpecEv);
        }
    }

    private void OnReportBoilerEvent(ISystemContext context, NodeState node, IFilterTarget @event)
    {
        _plcNodeManager.Server.ReportEvent(context, @event);
    }
}
