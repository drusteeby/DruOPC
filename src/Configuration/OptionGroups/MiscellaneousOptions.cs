namespace OpcPlc.Configuration.OptionGroups;

using Mono.Options;
using System;

/// <summary>
/// Options for miscellaneous configurations (web server, publisher config, chaos mode, etc.).
/// </summary>
public class MiscellaneousOptions : IOptionGroup
{
    private readonly OpcPlcConfiguration _config;

    public MiscellaneousOptions(OpcPlcConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void RegisterOptions(OptionSet options)
    {
        options.Add(
            "sp|showpnjson",
            $"show OPC Publisher configuration file using IP address as EndpointUrl.\nDefault: {_config.ShowPublisherConfigJsonIp}",
            (s) => _config.ShowPublisherConfigJsonIp = s != null);

        options.Add(
            "sph|showpnjsonph",
            $"show OPC Publisher configuration file using plchostname as EndpointUrl.\nDefault: {_config.ShowPublisherConfigJsonPh}",
            (s) => _config.ShowPublisherConfigJsonPh = s != null);

        options.Add(
            "spf|showpnfname=",
            $"filename of the OPC Publisher configuration file to write when using options sp/sph.\nDefault: {_config.PnJson}",
            (s) => _config.PnJson = s);

        options.Add(
            "wp|webport=",
            $"web server port for hosting OPC Publisher configuration file.\nDefault: {_config.WebServerPort}",
            (uint i) => _config.WebServerPort = i);

        options.Add(
            "chaos",
            $"run the server in Chaos mode. Randomly injects errors, closes sessions and subscriptions etc.\nDefault: {_config.RunInChaosMode}",
            (s) => _config.RunInChaosMode = s != null);

        options.Add(
            "h|help",
            "show this message and exit",
            (s) => _config.ShowHelp = s != null);
    }
}
