namespace OpcPlc;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using OpcPlc.Configuration;
using OpcPlc.Extensions;
using OpcPlc.Helpers;
using OpcPlc.PluginNodes.Models;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

public class OpcPlcServer : BackgroundService
{
    private const int DefaultMinThreads = 20;
    private const int DefaultCompletionPortThreads = 20;

    private readonly string[] _args;
    private readonly OpcPlcConfiguration _config;
    private readonly ILogger<OpcPlcServer> _logger;
    private readonly TimeService _timeService;
    private readonly ImmutableList<IPluginNodes> _pluginNodes;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IpAddressProvider _ipAddressProvider;

    /// <summary>
    /// The LoggerFactory used to create logging objects.
    /// </summary>
    public ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// OPC UA server object.
    /// </summary>
    public PlcServer PlcServer { get; private set; }

    /// <summary>
    /// Simulation object.
    /// </summary>
    public PlcSimulation PlcSimulationInstance { get; private set; }

    /// <summary>
    /// Service returning <see cref="DateTime"/> values and <see cref="Timer"/> instances. Mocked in tests.
    /// </summary>
    public TimeService TimeService => _timeService;

    /// <summary>
    /// A flag indicating when the server is up and ready to accept connections.
    /// </summary>
    public bool Ready { get; private set; }

    public OpcPlcServer(
        string[] args,
        IOptions<OpcPlcConfiguration> options,
        IEnumerable<IPluginNodes> pluginNodes,
        ILoggerFactory loggerFactory,
        ILogger<OpcPlcServer> logger,
        TimeService timeService,
        IHostApplicationLifetime lifetime,
        IpAddressProvider ipAddressProvider)
    {
        _args = args;
        _config = options.Value;
        _pluginNodes = pluginNodes.ToImmutableList();
        LoggerFactory = loggerFactory;
        _logger = logger;
        _timeService = timeService;
        _lifetime = lifetime;
        _ipAddressProvider = ipAddressProvider;
    }

    /// <summary>
    /// Execute the background service.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        PlcSimulationInstance = new PlcSimulation(_pluginNodes);
        var extraArgs = CliOptions.InitConfiguration(_args, PlcSimulationInstance, _config, _pluginNodes);

        // Show usage if requested
        if (_config.ShowHelp)
        {
            _logger.LogInformation(CliOptions.GetUsageHelp(_config.ProgramName));
            _lifetime.StopApplication();
            return;
        }

        // Validate and parse extra arguments.
        if (extraArgs.Count > 0)
        {
            _logger.LogWarning("Found one or more invalid command line arguments: {InvalidArgs}", string.Join(" ", extraArgs));
            _logger.LogInformation(CliOptions.GetUsageHelp(_config.ProgramName));
        }

        LogLogo();

        ThreadPool.SetMinThreads(DefaultMinThreads, DefaultCompletionPortThreads);
        ThreadPool.GetMinThreads(out int minWorkerThreads, out int minCompletionPortThreads);
        _logger.LogInformation(
            "Min worker threads: {MinWorkerThreads}, min completion port threads: {MinCompletionPortThreads}",
            minWorkerThreads,
            minCompletionPortThreads);

        _logger.LogInformation("Current directory: {CurrentDirectory}", Directory.GetCurrentDirectory());
        _logger.LogInformation("Log file: {LogFileName}", Path.GetFullPath(_config.LogFileName));
        _logger.LogInformation("Log level: {LogLevel}", _config.LogLevelCli);

        // Show OPC PLC version.
        var fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
        _logger.LogInformation("{ProgramName} v{Version} from {Date} starting up ...",
            _config.ProgramName,
            $"{fileVersion.ProductMajorPart}.{fileVersion.ProductMinorPart}.{fileVersion.ProductBuildPart}",
            File.GetLastWriteTimeUtc(Assembly.GetExecutingAssembly().Location));
        _logger.LogDebug("{ProgramName} informational version: v{Version}",
            _config.ProgramName,
            (Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute)?.InformationalVersion);

        // Show OPC UA SDK version.
        _logger.LogInformation(
            "OPC UA SDK {Version} from {Date}",
            Utils.GetAssemblyBuildNumber(),
            Utils.GetAssemblyTimestamp());
        _logger.LogDebug(
            "OPC UA SDK informational version: {Version}",
            Utils.GetAssemblySoftwareVersion());

        if (_config.OtlpEndpointUri is not null)
        {
            OtelHelper.ConfigureOpenTelemetry(_config.ProgramName, _config.OtlpEndpointUri, _config.OtlpExportProtocol, _config.OtlpExportInterval);
        }

        try
        {
            await StartPlcServerAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "OPC UA server failed unexpectedly");
            _lifetime.StopApplication();
            throw;
        }

        _logger.LogInformation("OPC UA server exiting ...");
    }

    /// <summary>
    /// Cleanup when the service is stopping.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping PLC server and simulation ...");
        
        PlcSimulationInstance?.Stop();
        PlcServer?.Stop();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Restart the PLC server and simulation.
    /// </summary>
    public async Task RestartAsync()
    {
        _logger.LogInformation("Stopping PLC server and simulation ...");
        PlcServer.Stop();
        PlcSimulationInstance.Stop();

        _logger.LogInformation("Restarting PLC server and simulation ...");
        LogLogo();

        await StartPlcServerAndSimulationAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Log web server information.
    /// </summary>
    private void LogWebServerInfo()
    {
        try
        {
            if (_config.ShowPublisherConfigJsonIp)
            {
                _logger.LogInformation("Web server started: {PnJsonUri}", 
                    $"http://{_ipAddressProvider.GetIpAddress()}:{_config.WebServerPort}/{_config.PnJson}");
            }
            else if (_config.ShowPublisherConfigJsonPh)
            {
                _logger.LogInformation("Web server started: {PnJsonUri}", 
                    $"http://{_config.OpcUa.Hostname}:{_config.WebServerPort}/{_config.PnJson}");
            }
            else
            {
                _logger.LogInformation("Web server started on port {WebServerPort}", _config.WebServerPort);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not log web server information: {Message}", e.Message);
        }
    }

    /// <summary>
    /// Start the server.
    /// </summary>
    private async Task StartPlcServerAsync(CancellationToken cancellationToken)
    {
        await StartPlcServerAndSimulationAsync().ConfigureAwait(false);

        if (_config.ShowPublisherConfigJsonIp)
        {
            await PnJsonHelper.PrintPublisherConfigJsonAsync(
                _config.PnJson,
                $"{_ipAddressProvider.GetIpAddress()}:{_config.OpcUa.ServerPort}{_config.OpcUa.ServerPath}",
                !_config.OpcUa.EnableUnsecureTransport,
                _pluginNodes,
                _logger).ConfigureAwait(false);
        }
        else if (_config.ShowPublisherConfigJsonPh)
        {
            await PnJsonHelper.PrintPublisherConfigJsonAsync(
                _config.PnJson,
                $"{_config.OpcUa.Hostname}:{_config.OpcUa.ServerPort}{_config.OpcUa.ServerPath}",
                !_config.OpcUa.EnableUnsecureTransport,
                _pluginNodes,
                _logger).ConfigureAwait(false);
        }

        Ready = true;
        _logger.LogInformation("PLC simulation started, press Ctrl+C to exit ...");

        // Wait for cancellation.
        await cancellationToken.WhenCanceled().ConfigureAwait(false);
    }

    private async Task StartPlcServerAndSimulationAsync()
    {
        // init OPC configuration and tracing
        var opcUaAppConfigFactory = new OpcUaAppConfigFactory(_config, _logger, LoggerFactory);
        ApplicationConfiguration plcApplicationConfiguration = await opcUaAppConfigFactory.ConfigureAsync().ConfigureAwait(false);

        // start the server.
        _logger.LogInformation("Starting server on endpoint {Endpoint} ...", plcApplicationConfiguration.ServerConfiguration.BaseAddresses[0]);
        _logger.LogInformation("Simulation settings are:");
        _logger.LogInformation("One simulation phase consists of {SimulationCycleCount} cycles", PlcSimulationInstance.SimulationCycleCount);
        _logger.LogInformation("One cycle takes {SimulationCycleLength} ms", PlcSimulationInstance.SimulationCycleLength);
        _logger.LogInformation("Reference test simulation: {AddReferenceTestSimulation}",
            PlcSimulationInstance.AddReferenceTestSimulation ? "Enabled" : "Disabled");
        _logger.LogInformation("Simple events: {AddSimpleEventsSimulation}",
            PlcSimulationInstance.AddSimpleEventsSimulation ? "Enabled" : "Disabled");
        _logger.LogInformation("Alarms: {AddAlarmSimulation}", PlcSimulationInstance.AddAlarmSimulation ? "Enabled" : "Disabled");
        _logger.LogInformation("Deterministic alarms: {DeterministicAlarmSimulation}",
            PlcSimulationInstance.DeterministicAlarmSimulationFile != null ? "Enabled" : "Disabled");

        _logger.LogInformation("Anonymous authentication: {AnonymousAuth}", _config.DisableAnonymousAuth ? "Disabled" : "Enabled");
        _logger.LogInformation("Reject chain validation with CA certs with unknown revocation status: {RejectValidationUnknownRevocStatus}", _config.OpcUa.DontRejectUnknownRevocationStatus ? "Disabled" : "Enabled");
        _logger.LogInformation("Username/Password authentication: {UsernamePasswordAuth}", _config.DisableUsernamePasswordAuth ? "Disabled" : "Enabled");
        _logger.LogInformation("Certificate authentication: {CertAuth}", _config.DisableCertAuth ? "Disabled" : "Enabled");

        // Add simple events, alarms, reference test simulation and deterministic alarms.
        PlcServer = new PlcServer(_config, PlcSimulationInstance, _timeService, _pluginNodes, _logger);
        PlcServer.Start(plcApplicationConfiguration);
        _logger.LogInformation("OPC UA Server started");

        // Add remaining base simulations.
        PlcSimulationInstance.Start(PlcServer);
    }

    private void LogLogo()
    {
        _logger.LogInformation(
            @"
 ██████╗ ██████╗  ██████╗    ██████╗ ██╗      ██████╗
██╔═══██╗██╔══██╗██╔════╝    ██╔══██╗██║     ██╔════╝
██║   ██║██████╔╝██║         ██████╔╝██║     ██║
██║   ██║██╔═══╝ ██║         ██╔═══╝ ██║     ██║
╚██████╔╝██║     ╚██████╗    ██║     ███████╗╚██████╗
 ╚═════╝ ╚═╝      ╚═════╝    ╚═╝     ╚══════╝ ╚═════╝
");
    }
}
