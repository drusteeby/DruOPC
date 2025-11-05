namespace OpcPlc.Configuration;

using OpenTelemetry.Exporter;
using System;
using System.Collections.Generic;

public class OpcPlcConfiguration
{
    public const string SectionName = "OpcPlc";

    /// <summary>
    /// Name of the application.
    /// </summary>
    public string ProgramName { get; set; } = "OpcPlc";

    public bool DisableAnonymousAuth { get; set; }

    public bool DisableUsernamePasswordAuth { get; set; }

    public bool DisableCertAuth { get; set; }

    /// <summary>
    /// Admin user.
    /// </summary>
    public string AdminUser { get; set; } = "sysadmin";

    /// <summary>
    /// Admin user password.
    /// </summary>
    public string AdminPassword { get; set; } = "demo";

    /// <summary>
    /// Default user.
    /// </summary>
    public string DefaultUser { get; set; } = "user1";

    /// <summary>
    /// Default user password.
    /// </summary>
    public string DefaultPassword { get; set; } = "password";

    /// <summary>
    /// Gets or sets OTLP reporting endpoint URI.
    /// </summary>
    public string OtlpEndpointUri { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Gets or sets the OTLP export interval in seconds.
    /// </summary>
    public TimeSpan OtlpExportInterval { get; set; } = TimeSpan.FromSeconds(60);

    // <summary>
    /// Gets or sets the OTLP export protocol: grpc, protobuf.
    /// </summary>
    public string OtlpExportProtocol { get; set; } = "grpc";

    /// <summary>
    /// Gets or sets how to handle metrics for publish requests.
    /// Allowed values:
    /// disable=Always disabled,
    /// enable=Always enabled,
    /// auto=Auto-disable when sessions > 40 or monitored items > 500.
    /// </summary>
    public string OtlpPublishMetrics { get; set; } = "auto";

    /// <summary>
    /// Show OPC Publisher configuration file using IP address as EndpointUrl.
    /// </summary>
    public bool ShowPublisherConfigJsonIp { get; set; }

    /// <summary>
    /// Show OPC Publisher configuration file using plchostname as EndpointUrl.
    /// </summary>
    public bool ShowPublisherConfigJsonPh { get; set; }

    /// <summary>
    /// Web server port for hosting OPC Publisher file.
    /// </summary>
    public uint WebServerPort { get; set; } = 8080;

    /// <summary>
    /// Show usage help.
    /// </summary>
    public bool ShowHelp { get; set; }

    public string PnJson { get; set; } = "pn.json";

    /// <summary>
    /// Logging configuration.
    /// </summary>
    public string LogFileName { get; set; } = $"hostname-port-plc.log"; // Set in InitLogging().

    public string LogLevelCli { get; set; } = "info";

    public TimeSpan LogFileFlushTimeSpanSec { get; set; } = TimeSpan.FromSeconds(30);

    public OpcApplicationConfiguration OpcUa { get; set; } = new OpcApplicationConfiguration();

    /// <summary>
    /// Configure chaos mode
    /// </summary>
    public bool RunInChaosMode { get; set; }

    /// <summary>
    /// Simulation configuration.
    /// </summary>
    public SimulationConfiguration Simulation { get; set; } = new SimulationConfiguration();

    /// <summary>
    /// Data generation flags.
    /// </summary>
    public DataGenerationConfiguration DataGeneration { get; set; } = new DataGenerationConfiguration();

    /// <summary>
    /// Slow nodes configuration.
    /// </summary>
    public SlowNodesConfiguration SlowNodes { get; set; } = new SlowNodesConfiguration();

    /// <summary>
    /// Fast nodes configuration.
    /// </summary>
    public FastNodesConfiguration FastNodes { get; set; } = new FastNodesConfiguration();

    /// <summary>
    /// Very fast ByteString nodes configuration.
    /// </summary>
    public VeryFastByteStringNodesConfiguration VeryFastByteStringNodes { get; set; } = new VeryFastByteStringNodesConfiguration();

    /// <summary>
    /// GUID nodes configuration.
    /// </summary>
    public GuidNodesConfiguration GuidNodes { get; set; } = new GuidNodesConfiguration();

    /// <summary>
    /// Boiler #2 configuration.
    /// </summary>
    public Boiler2Configuration Boiler2 { get; set; } = new Boiler2Configuration();

    /// <summary>
    /// File-based node configuration.
    /// </summary>
    public string NodesFile { get; set; }
    public List<string> UaNodesFiles { get; set; } = new();
    public List<string> NodeSet2Files { get; set; } = new();
}

public class SimulationConfiguration
{
    public int SimulationCycleCount { get; set; } = 50;
    public int SimulationCycleLength { get; set; } = 100;
    public uint EventInstanceCount { get; set; } = 0;
    public uint EventInstanceRate { get; set; } = 1000;
    public bool AddAlarmSimulation { get; set; }
    public bool AddSimpleEventsSimulation { get; set; }
    public string DeterministicAlarmSimulationFile { get; set; }
}

public class DataGenerationConfiguration
{
    public bool NoDataValues { get; set; }
    public bool NoDips { get; set; }
    public bool NoSpikes { get; set; }
    public bool NoPosTrend { get; set; }
    public bool NoNegTrend { get; set; }
}

public class SlowNodesConfiguration
{
    public uint NodeCount { get; set; } = 1;
    public uint NodeRate { get; set; } = 10;
    public string NodeType { get; set; } = "UInt";
    public string NodeMinValue { get; set; }
    public string NodeMaxValue { get; set; }
    public bool NodeRandomization { get; set; }
    public string NodeStepSize { get; set; } = "1";
    public uint NodeSamplingInterval { get; set; }
}

public class FastNodesConfiguration
{
    public uint NodeCount { get; set; } = 1;
    public uint NodeRate { get; set; } = 1;
    public uint VeryFastRate { get; set; } = 1000;
    public string NodeType { get; set; } = "UInt";
    public string NodeMinValue { get; set; }
    public string NodeMaxValue { get; set; }
    public bool NodeRandomization { get; set; }
    public string NodeStepSize { get; set; } = "1";
    public uint NodeSamplingInterval { get; set; }
}

public class VeryFastByteStringNodesConfiguration
{
    public uint NodeCount { get; set; } = 1;
    public uint NodeSize { get; set; } = 1024;
    public uint NodeRate { get; set; } = 1000;
}

public class GuidNodesConfiguration
{
    public uint NodeCount { get; set; } = 1;
}

public class Boiler2Configuration
{
    public int TemperatureSpeed { get; set; } = 1;
    public int BaseTemperature { get; set; } = 10;
    public int TargetTemperature { get; set; } = 80;
    public int MaintenanceInterval { get; set; } = 300;
    public int OverheatInterval { get; set; } = 120;
}
