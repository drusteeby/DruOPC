# Migration Guide: Command-Line Arguments to appsettings.json

## Overview

This guide documents the migration from command-line argument parsing to configuration-based settings using `appsettings.json`. All command-line options have been moved to the configuration file for easier management and deployment.

> **?? CRITICAL**: JSON does not support comments (`//` or `/* */`). Your `appsettings.json` file must be valid JSON without any comments, or configuration binding will fail silently. Use property names that are self-documenting or maintain a separate documentation file.

## Summary of Changes

### **Removed Components**
- `src/Configuration/CliOptions.cs` - CLI orchestration class
- `src/Configuration/OptionGroups/` - All option group classes:
  - `IOptionGroup.cs`
  - `LoggingOptions.cs`
  - `SimulationOptions.cs`
  - `OpcUaServerOptions.cs`
  - `CertificateStoreOptions.js`
  - `AuthenticationOptions.cs`
  - `OtlpOptions.cs`
  - `MiscellaneousOptions.cs`

### **Modified Components**

#### Core Configuration
- **`src/Configuration/Configuration.cs`**: Expanded with nested configuration classes:
  - `SimulationConfiguration`
  - `DataGenerationConfiguration`
  - `SlowNodesConfiguration`
  - `FastNodesConfiguration`
  - `VeryFastByteStringNodesConfiguration`
  - `GuidNodesConfiguration`
  - `Boiler2Configuration`

- **`src/appsettings.json`**: Now contains all configuration options

#### Application Startup
- **`src/Program.cs`**: Simplified - removed CLI parsing logic
- **`src/OpcPlcServer.cs`**: Updated to inject `PlcSimulation` instead of creating it
- **`src/PlcSimulation.cs`**: Now uses dependency injection for configuration

#### Plugin Nodes
- **`src/PluginNodes/Models/IPluginNodes.cs`**: Removed `AddOptions` method
- All plugin node implementations updated to use constructor injection for configuration:
  - `SlowPluginNodes.cs`
  - `FastPluginNodes.cs`
  - `VeryFastByteStringPluginNodes.cs`
  - `DataPluginNodes.cs`
  - `DipPluginNode.cs`
  - `SpikePluginNode.cs`
  - `PosTrendPluginNode.cs`
  - `NegTrendPluginNode.cs`

## Configuration Mapping

### Command-Line to appsettings.json Mapping

| Old CLI Argument | New appsettings.json Path | Example Value |
|-----------------|---------------------------|---------------|
| `--lf`, `--logfile` | `OpcPlc:LogFileName` | `"hostname-port-plc.log"` |
| `--lt`, `--logflushtimespan` | `OpcPlc:LogFileFlushTimeSpanSec` | `"00:00:30"` |
| `--ll`, `--loglevel` | `OpcPlc:LogLevelCli` | `"info"` |
| `--sc`, `--simulationcyclecount` | `OpcPlc:Simulation:SimulationCycleCount` | `50` |
| `--ct`, `--cycletime` | `OpcPlc:Simulation:SimulationCycleLength` | `100` |
| `--ei`, `--eventinstances` | `OpcPlc:Simulation:EventInstanceCount` | `0` |
| `--er`, `--eventrate` | `OpcPlc:Simulation:EventInstanceRate` | `1000` |
| `--alm`, `--alarms` | `OpcPlc:Simulation:AddAlarmSimulation` | `false` |
| `--ses`, `--simpleevents` | `OpcPlc:Simulation:AddSimpleEventsSimulation` | `false` |
| `--dalm`, `--deterministicalarms` | `OpcPlc:Simulation:DeterministicAlarmSimulationFile` | `null` |
| `--pn`, `--portnum` | `OpcPlc:OpcUa:ServerPort` | `50000` |
| `--op`, `--path` | `OpcPlc:OpcUa:ServerPath` | `""` |
| `--ph`, `--plchostname` | `OpcPlc:OpcUa:Hostname` | `"localhost"` |
| `--ol`, `--opcmaxstringlen` | `OpcPlc:OpcUa:OpcMaxStringLength` | `1048576` |
| `--lr`, `--ldsreginterval` | `OpcPlc:OpcUa:LdsRegistrationInterval` | `0` |
| `--aa`, `--autoaccept` | `OpcPlc:OpcUa:AutoAcceptCerts` | `false` |
| `--drurs`, `--dontrejectunknownrevocationstatus` | `OpcPlc:OpcUa:DontRejectUnknownRevocationStatus` | `false` |
| `--ut`, `--unsecuretransport` | `OpcPlc:OpcUa:EnableUnsecureTransport` | `false` |
| `--to`, `--trustowncert` | `OpcPlc:OpcUa:TrustMyself` | `false` |
| `--msec`, `--maxsessioncount` | `OpcPlc:OpcUa:MaxSessionCount` | `100` |
| `--mset`, `--maxsessiontimeout` | `OpcPlc:OpcUa:MaxSessionTimeout` | `3600000` |
| `--msuc`, `--maxsubscriptioncount` | `OpcPlc:OpcUa:MaxSubscriptionCount` | `100` |
| `--mqrc`, `--maxqueuedrequestcount` | `OpcPlc:OpcUa:MaxQueuedRequestCount` | `2000` |
| `--at`, `--appcertstoretype` | `OpcPlc:OpcUa:OpcOwnCertStoreType` | `"Directory"` |
| `--ap`, `--appcertstorepath` | `OpcPlc:OpcUa:OpcOwnCertStorePath` | `"pki/own"` |
| `--tp`, `--trustedcertstorepath` | `OpcPlc:OpcUa:OpcTrustedCertStorePath` | `"pki/trusted"` |
| `--rp`, `--rejectedcertstorepath` | `OpcPlc:OpcUa:OpcRejectedCertStorePath` | `"pki/rejected"` |
| `--ip`, `--issuercertstorepath` | `OpcPlc:OpcUa:OpcIssuerCertStorePath` | `"pki/issuer"` |
| `--csr` | `OpcPlc:OpcUa:ShowCreateSigningRequestInfo` | `false` |
| `--daa`, `--disableanonymousauth` | `OpcPlc:DisableAnonymousAuth` | `false` |
| `--dua`, `--disableusernamepasswordauth` | `OpcPlc:DisableUsernamePasswordAuth` | `false` |
| `--dca`, `--disablecertauth` | `OpcPlc:DisableCertAuth` | `false` |
| `--au`, `--adminuser` | `OpcPlc:AdminUser` | `"sysadmin"` |
| `--ac`, `--adminpassword` | `OpcPlc:AdminPassword` | `"demo"` |
| `--du`, `--defaultuser` | `OpcPlc:DefaultUser` | `"user1"` |
| `--dc`, `--defaultpassword` | `OpcPlc:DefaultPassword` | `"password"` |
| `--otlpee`, `--otlpendpoint` | `OpcPlc:OtlpEndpointUri` | `null` |
| `--otlpei`, `--otlpexportinterval` | `OpcPlc:OtlpExportInterval` | `"00:01:00"` |
| `--otlpep`, `--otlpexportprotocol` | `OpcPlc:OtlpExportProtocol` | `"grpc"` |
| `--otlpub`, `--otlpublishmetrics` | `OpcPlc:OtlpPublishMetrics` | `"auto"` |
| `--sp`, `--showpnjson` | `OpcPlc:ShowPublisherConfigJsonIp` | `true` |
| `--sph`, `--showpnjsonph` | `OpcPlc:ShowPublisherConfigJsonPh` | `false` |
| `--spf`, `--showpnfname` | `OpcPlc:PnJson` | `"pn.json"` |
| `--wp`, `--webport` | `OpcPlc:WebServerPort` | `8080` |
| `--chaos` | `OpcPlc:RunInChaosMode` | `false` |
| `--nv`, `--nodatavalues` | `OpcPlc:DataGeneration:NoDataValues` | `false` |
| `--nd`, `--nodips` | `OpcPlc:DataGeneration:NoDips` | `false` |
| `--ns`, `--nospikes` | `OpcPlc:DataGeneration:NoSpikes` | `false` |
| `--np`, `--nopostrend` | `OpcPlc:DataGeneration:NoPosTrend` | `false` |
| `--nn`, `--nonegtrend` | `OpcPlc:DataGeneration:NoNegTrend` | `false` |
| `--sn`, `--slownodes` | `OpcPlc:SlowNodes:NodeCount` | `1` |
| `--sr`, `--slowrate` | `OpcPlc:SlowNodes:NodeRate` | `10` |
| `--st`, `--slowtype` | `OpcPlc:SlowNodes:NodeType` | `"UInt"` |
| `--stl`, `--slowtypelowerbound` | `OpcPlc:SlowNodes:NodeMinValue` | `null` |
| `--stu`, `--slowtypeupperbound` | `OpcPlc:SlowNodes:NodeMaxValue` | `null` |
| `--str`, `--slowtyperandomization` | `OpcPlc:SlowNodes:NodeRandomization` | `false` |
| `--sts`, `--slowtypestepsize` | `OpcPlc:SlowNodes:NodeStepSize` | `"1"` |
| `--ssi`, `--slownodesamplinginterval` | `OpcPlc:SlowNodes:NodeSamplingInterval` | `0` |
| `--fn`, `--fastnodes` | `OpcPlc:FastNodes:NodeCount` | `1` |
| `--fr`, `--fastrate` | `OpcPlc:FastNodes:NodeRate` | `1` |
| `--vfr`, `--veryfastrate` | `OpcPlc:FastNodes:VeryFastRate` | `1000` |
| `--ft`, `--fasttype` | `OpcPlc:FastNodes:NodeType` | `"UInt"` |
| `--ftl`, `--fasttypelowerbound` | `OpcPlc:FastNodes:NodeMinValue` | `null` |
| `--ftu`, `--fasttypeupperbound` | `OpcPlc:FastNodes:NodeMaxValue` | `null` |
| `--ftr`, `--fasttyperandomization` | `OpcPlc:FastNodes:NodeRandomization` | `false` |
| `--fts`, `--fasttypestepsize` | `OpcPlc:FastNodes:NodeStepSize` | `"1"` |
| `--fsi`, `--fastnodesamplinginterval` | `OpcPlc:FastNodes:NodeSamplingInterval` | `0` |
| `--vfbs`, `--veryfastbsnodes` | `OpcPlc:VeryFastByteStringNodes:NodeCount` | `1` |
| `--vfbss`, `--veryfastbssize` | `OpcPlc:VeryFastByteStringNodes:NodeSize` | `1024` |
| `--vfbsr`, `--veryfastbsrate` | `OpcPlc:VeryFastByteStringNodes:NodeRate` | `1000` |
| `--gn`, `--guidnodes` | `OpcPlc:GuidNodes:NodeCount` | `1` |

## Migration Steps

### 1. Update Your appsettings.json

Instead of passing command-line arguments, update your `appsettings.json` file with the desired configuration.

**Example:**
```json
{
  "OpcPlc": {
    "OpcUa": {
      "ServerPort": 62541,
      "Hostname": "my-plc-server"
    },
    "Simulation": {
      "SimulationCycleCount": 100,
      "AddAlarmSimulation": true
    },
    "SlowNodes": {
      "NodeCount": 5,
      "NodeRate": 5
    }
  }
}
```

### 2. Environment-Specific Configuration

You can create environment-specific configuration files:
- `appsettings.Development.json`
- `appsettings.Production.json`
- `appsettings.{Environment}.json`

### 3. Environment Variables

Configuration can be overridden using environment variables with the `__` (double underscore) separator:

```bash
OpcPlc__OpcUa__ServerPort=50001
OpcPlc__Simulation__AddAlarmSimulation=true
```

### 4. Docker Configuration

When running in Docker, you can:

**Option 1: Mount a custom appsettings.json**
```bash
docker run -v ./my-appsettings.json:/app/appsettings.json opcplc
```

**Option 2: Use environment variables**
```bash
docker run -e OpcPlc__OpcUa__ServerPort=50001 opcplc
```

**Option 3: Use docker-compose**
```yaml
services:
  opcplc:
    image: opcplc
    environment:
      - OpcPlc__OpcUa__ServerPort=50001
      - OpcPlc__Simulation__AddAlarmSimulation=true
    volumes:
      - ./custom-appsettings.json:/app/appsettings.json
```

## Benefits of the New Approach

1. **Easier Configuration Management**: All settings in one place
2. **Environment-Specific Settings**: Easy to maintain different configurations
3. **Better DevOps Integration**: Works seamlessly with orchestration tools
4. **Type Safety**: Configuration is validated at startup
5. **Hierarchical Structure**: Logically organized settings
6. **Hot Reload**: Some settings can be changed without restart (if configured)

## Backwards Compatibility

**Breaking Change**: Command-line arguments are no longer supported. All configuration must be provided through `appsettings.json` or environment variables.

## Need Help?

For questions or issues related to this migration, please refer to:
- The `appsettings.json` template in the source repository
- The configuration class definitions in `src/Configuration/Configuration.cs`
- Microsoft's official documentation on configuration in .NET applications
