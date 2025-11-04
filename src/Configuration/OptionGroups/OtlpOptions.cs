namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using System;

/// <summary>
/// Options for OpenTelemetry Protocol (OTLP) exporter configuration.
/// </summary>
public class OtlpOptions : IOptionGroup
{
    private readonly OpcPlcConfiguration _config;

    public OtlpOptions(OpcPlcConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RegisterOptions(OptionSet options)
    {
        options.Add(
            "otlpee|otlpendpoint=",
            $"the endpoint URI to which the OTLP exporter is going to send information.\nDefault: '{_config.OtlpEndpointUri}'",
            (s) => _config.OtlpEndpointUri = s);

        options.Add(
            "otlpei|otlpexportinterval=",
            $"the interval for exporting OTLP information in seconds.\nDefault: {_config.OtlpExportInterval.TotalSeconds}",
            (uint i) => _config.OtlpExportInterval = TimeSpan.FromSeconds(i));

        options.Add(
            "otlpep|otlpexportprotocol=",
            $"the protocol for exporting OTLP information.\n(allowed values: grpc, protobuf).\nDefault: {_config.OtlpExportProtocol}",
            (string s) => _config.OtlpExportProtocol = s);

        options.Add(
            "otlpub|otlpublishmetrics=",
            $"how to handle metrics for publish requests.\n(allowed values: disable=Always disabled, enable=Always enabled, auto=Auto-disable when sessions > 40 or monitored items > 500).\nDefault: {_config.OtlpPublishMetrics}",
            (string s) => _config.OtlpPublishMetrics = s);
    }
}
