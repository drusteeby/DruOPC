# Configuration Examples

This document provides example `appsettings.json` configurations for common scenarios.

> **?? IMPORTANT**: JSON does not support comments. All examples below are valid JSON without comments. If you need to document your configuration, use a separate README file or use property names that are self-documenting.

## Example 1: Basic Development Setup

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning"
    }
  },
  "OpcPlc": {
    "LogLevelCli": "debug",
    "OpcUa": {
      "ServerPort": 50000,
      "Hostname": "localhost",
      "EnableUnsecureTransport": true,
      "AutoAcceptCerts": true
    },
    "ShowPublisherConfigJsonIp": true,
    "WebServerPort": 8080
  }
}
```

## Example 2: Production with Security

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "OpcPlc": {
    "LogLevelCli": "info",
    "DisableAnonymousAuth": false,
    "DisableUsernamePasswordAuth": false,
    "AdminUser": "admin",
    "AdminPassword": "${ADMIN_PASSWORD}",
    "OpcUa": {
      "ServerPort": 50000,
      "Hostname": "opc-plc.company.com",
      "EnableUnsecureTransport": false,
      "AutoAcceptCerts": false,
      "DontRejectUnknownRevocationStatus": false,
      "TrustMyself": false,
      "OpcOwnCertStoreType": "Directory",
      "OpcOwnCertStorePath": "/app/pki/own",
      "OpcTrustedCertStorePath": "/app/pki/trusted",
      "OpcRejectedCertStorePath": "/app/pki/rejected",
      "OpcIssuerCertStorePath": "/app/pki/issuer"
    }
  }
}
```

## Example 3: High-Performance Simulation

```json
{
  "OpcPlc": {
    "Simulation": {
      "SimulationCycleCount": 100,
      "SimulationCycleLength": 50,
      "AddAlarmSimulation": true,
      "AddSimpleEventsSimulation": true
    },
    "FastNodes": {
      "NodeCount": 100,
      "VeryFastRate": 100,
      "NodeType": "Double"
    },
    "SlowNodes": {
      "NodeCount": 50,
      "NodeRate": 5,
      "NodeType": "UInt"
    },
    "DataGeneration": {
      "NoDataValues": false,
      "NoDips": false,
      "NoSpikes": false,
      "NoPosTrend": false,
      "NoNegTrend": false
    }
  }
}
```

## Example 4: Minimal Data Generation (Testing)

```json
{
  "OpcPlc": {
    "DataGeneration": {
      "NoDataValues": true,
      "NoDips": true,
      "NoSpikes": true,
      "NoPosTrend": true,
      "NoNegTrend": true
    },
    "SlowNodes": {
      "NodeCount": 0
    },
    "FastNodes": {
      "NodeCount": 0
    },
    "VeryFastByteStringNodes": {
      "NodeCount": 0
    }
  }
}
```

## Example 5: With OpenTelemetry

```json
{
  "OpcPlc": {
    "OtlpEndpointUri": "http://otel-collector:4317",
    "OtlpExportInterval": "00:00:30",
    "OtlpExportProtocol": "grpc",
    "OtlpPublishMetrics": "enable",
    "OpcUa": {
      "ServerPort": 50000
    }
  }
}
```

## Example 6: Multiple Fast and Slow Nodes with Customization

```json
{
  "OpcPlc": {
    "SlowNodes": {
      "NodeCount": 10,
      "NodeRate": 15,
      "NodeType": "Double",
      "NodeMinValue": "0",
      "NodeMaxValue": "1000",
      "NodeRandomization": true,
      "NodeStepSize": "0.5",
      "NodeSamplingInterval": 1000
    },
    "FastNodes": {
      "NodeCount": 20,
      "VeryFastRate": 500,
      "NodeType": "UInt",
      "NodeMinValue": "0",
      "NodeMaxValue": "100",
      "NodeRandomization": false,
      "NodeStepSize": "1",
      "NodeSamplingInterval": 100
    }
  }
}
```

## Example 7: Chaos Mode for Resilience Testing

```json
{
  "OpcPlc": {
    "RunInChaosMode": true,
    "OpcUa": {
      "ServerPort": 50000,
      "MaxSessionCount": 50,
      "MaxSubscriptionCount": 100
    },
    "Simulation": {
      "SimulationCycleCount": 50,
      "SimulationCycleLength": 100
    }
  }
}
```

## Example 8: Custom Boiler Configuration

```json
{
  "OpcPlc": {
    "Boiler2": {
      "TemperatureSpeed": 2,
      "BaseTemperature": 20,
      "TargetTemperature": 100,
      "MaintenanceInterval": 600,
      "OverheatInterval": 240
    }
  }
}
```

## Example 9: Large ByteString Nodes

```json
{
  "OpcPlc": {
    "VeryFastByteStringNodes": {
      "NodeCount": 5,
      "NodeSize": 10240,
      "NodeRate": 2000
    }
  }
}
```

## Example 10: Docker Compose Configuration

```yaml
version: '3.8'
services:
  opcplc:
    image: opcplc:latest
    ports:
      - "50000:50000"
      - "8080:8080"
    environment:
      - OpcPlc__OpcUa__ServerPort=50000
      - OpcPlc__OpcUa__Hostname=opcplc
      - OpcPlc__LogLevelCli=info
      - OpcPlc__Simulation__AddAlarmSimulation=true
      - OpcPlc__FastNodes__NodeCount=50
      - OpcPlc__SlowNodes__NodeCount=25
    volumes:
      - ./pki:/app/pki
      - ./logs:/app/logs
```

## Using Environment Variables

You can override any configuration using environment variables with the double underscore (`__`) separator:

```bash
# Linux/Mac
export OpcPlc__OpcUa__ServerPort=62541
export OpcPlc__Simulation__AddAlarmSimulation=true
export OpcPlc__FastNodes__NodeCount=100

# Windows PowerShell
$env:OpcPlc__OpcUa__ServerPort=62541
$env:OpcPlc__Simulation__AddAlarmSimulation="true"
$env:OpcPlc__FastNodes__NodeCount=100

# Windows CMD
set OpcPlc__OpcUa__ServerPort=62541
set OpcPlc__Simulation__AddAlarmSimulation=true
set OpcPlc__FastNodes__NodeCount=100
```

## Kubernetes ConfigMap Example

```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: opcplc-config
data:
  appsettings.json: |
    {
      "OpcPlc": {
        "OpcUa": {
          "ServerPort": 50000,
          "Hostname": "opcplc-service"
        },
        "Simulation": {
          "SimulationCycleCount": 100,
          "AddAlarmSimulation": true
        },
        "FastNodes": {
          "NodeCount": 50
        }
      }
    }
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: opcplc
spec:
  replicas: 1
  selector:
    matchLabels:
      app: opcplc
  template:
    metadata:
      labels:
        app: opcplc
    spec:
      containers:
      - name: opcplc
        image: opcplc:latest
        ports:
        - containerPort: 50000
        - containerPort: 8080
        volumeMounts:
        - name: config
          mountPath: /app/appsettings.json
          subPath: appsettings.json
      volumes:
      - name: config
        configMap:
          name: opcplc-config
```

## Notes

- TimeSpan values should be in format: `"HH:MM:SS"` or `"days.HH:MM:SS"`
- Boolean values: `true` or `false` (lowercase, no quotes in JSON)
- Numeric values: No quotes in JSON
- String values: Use quotes in JSON
- Array values: Use JSON array syntax `[]`
- Null values: Use `null` (no quotes)
